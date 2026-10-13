using System.Collections.ObjectModel;
using System.Diagnostics;
using Avalonia.Threading;
using Handy.Abstractions;

namespace Handy.Services;

/// <summary>
/// Loads the modules found in the modules folder and keeps watching it, so a module copied there while the
/// application runs shows up as well. Modules are never unloaded: removing or replacing one needs a restart.
/// </summary>
internal sealed class ModuleHost(string modulesDirectory, Func<IHandyModule, IModuleContext> createContext) : IDisposable
{
    private static readonly TimeSpan WatchDelay = TimeSpan.FromMilliseconds(500);

    private readonly HashSet<string> _loadedAssemblies = [];
    private FileSystemWatcher? _watcher;
    private Timer? _watchTimer;

    /// <summary>Loaded modules in menu order. Changes on the UI thread only.</summary>
    public ObservableCollection<IHandyModule> Modules { get; } = [];

    public string ModulesDirectory => modulesDirectory;

    /// <summary>Loads every module assembly that is not loaded yet. Call on the UI thread.</summary>
    public void LoadNewModules()
    {
        Directory.CreateDirectory(modulesDirectory);
        foreach (var path in FindModuleAssemblies())
        {
            // A failed assembly (e.g. still being copied) is tried again on the next change of the folder.
            if (!_loadedAssemblies.Contains(path) && TryLoadAssembly(path))
                _loadedAssemblies.Add(path);
        }
    }

    public void StartWatching()
    {
        Directory.CreateDirectory(modulesDirectory);
        _watchTimer = new Timer(_ => Dispatcher.UIThread.Post(LoadNewModules));
        // No file filter: a module folder moved into place only raises an event for the folder itself.
        _watcher = new FileSystemWatcher(modulesDirectory) { IncludeSubdirectories = true };
        _watcher.Created += OnModulesDirectoryChanged;
        _watcher.Changed += OnModulesDirectoryChanged;
        _watcher.Renamed += OnModulesDirectoryChanged;
        _watcher.EnableRaisingEvents = true;
    }

    public void Dispose()
    {
        _watcher?.Dispose();
        _watchTimer?.Dispose();
    }

    // Copying a module raises many events; wait until they stop before loading.
    private void OnModulesDirectoryChanged(object sender, FileSystemEventArgs e) =>
        _watchTimer?.Change(WatchDelay, Timeout.InfiniteTimeSpan);

    /// <summary><c>Modules/&lt;Name&gt;/&lt;Name&gt;.dll</c> and the assemblies placed directly in the folder.</summary>
    private IEnumerable<string> FindModuleAssemblies()
    {
        var inFolders = Directory.EnumerateDirectories(modulesDirectory)
            .Select(directory => Path.Combine(directory, Path.GetFileName(directory) + ".dll"))
            .Where(File.Exists);
        return Directory.EnumerateFiles(modulesDirectory, "*.dll").Concat(inFolders).Order();
    }

    private bool TryLoadAssembly(string path)
    {
        Type[] moduleTypes;
        try
        {
            var assembly = new ModuleLoadContext(path).LoadFromAssemblyPath(path);
            moduleTypes = assembly.GetExportedTypes()
                .Where(type => type is { IsClass: true, IsAbstract: false } && type.IsAssignableTo(typeof(IHandyModule)))
                .ToArray();
        }
        catch (Exception e)
        {
            Trace.TraceError($"Could not load module assembly '{path}': {e.Message}");
            return false;
        }

        foreach (var type in moduleTypes)
            TryAddModule(type);
        return true;
    }

    // Modules are third party code: whatever one throws must not take the application down.
    private void TryAddModule(Type type)
    {
        try
        {
            var module = (IHandyModule)Activator.CreateInstance(type)!;
            if (Modules.Any(m => m.Id == module.Id))
            {
                Trace.TraceWarning($"Module '{module.Id}' ({type.FullName}) is already loaded, skipped.");
                return;
            }
            module.Initialize(createContext(module));
            var index = Modules.Count(m => (m.Order, m.Id).CompareTo((module.Order, module.Id)) <= 0);
            Modules.Insert(index, module);
        }
        catch (Exception e)
        {
            Trace.TraceError($"Could not initialize module '{type.FullName}': {e}");
        }
    }
}
