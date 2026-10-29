using System.Collections.ObjectModel;
using System.Diagnostics;
using Avalonia.Threading;
using Handy.Abstractions;

namespace Handy.Services;

/// <summary>
/// Loads the modules found in the modules folders and keeps watching them, so a module copied there while the
/// application runs shows up as well. Modules are never unloaded: removing or replacing one needs a restart.
/// The first folder is the user's own and is created when missing; the others (e.g. the modules shipped next to the
/// executable, read-only when installed) are only read when they exist. With equal ids, the first folder wins.
/// </summary>
internal sealed class ModuleHost(IReadOnlyList<string> modulesDirectories, Func<IHandyModule, IModuleContext> createContext) : IDisposable
{
    private static readonly TimeSpan WatchDelay = TimeSpan.FromMilliseconds(500);

    private readonly HashSet<string> _loadedAssemblies = [];
    private readonly List<FileSystemWatcher> _watchers = [];
    private Timer? _watchTimer;

    /// <summary>Loaded modules in menu order. Changes on the UI thread only.</summary>
    public ObservableCollection<IHandyModule> Modules { get; } = [];

    /// <summary>Where the user installs modules.</summary>
    public string ModulesDirectory => modulesDirectories[0];

    /// <summary>Loads every module assembly that is not loaded yet. Call on the UI thread.</summary>
    public void LoadNewModules()
    {
        Directory.CreateDirectory(ModulesDirectory);
        foreach (var path in modulesDirectories.Where(Directory.Exists).SelectMany(FindModuleAssemblies))
        {
            // A failed assembly (e.g. still being copied) is tried again on the next change of the folder.
            if (!_loadedAssemblies.Contains(path) && TryLoadAssembly(path))
                _loadedAssemblies.Add(path);
        }
    }

    public void StartWatching()
    {
        Directory.CreateDirectory(ModulesDirectory);
        _watchTimer = new Timer(_ => Dispatcher.UIThread.Post(LoadNewModules));
        foreach (var directory in modulesDirectories.Where(Directory.Exists))
        {
            // No file filter: a module folder moved into place only raises an event for the folder itself.
            var watcher = new FileSystemWatcher(directory) { IncludeSubdirectories = true };
            watcher.Created += OnModulesDirectoryChanged;
            watcher.Changed += OnModulesDirectoryChanged;
            watcher.Renamed += OnModulesDirectoryChanged;
            watcher.EnableRaisingEvents = true;
            _watchers.Add(watcher);
        }
    }

    public void Dispose()
    {
        foreach (var watcher in _watchers)
            watcher.Dispose();
        _watchTimer?.Dispose();
    }

    // Copying a module raises many events; wait until they stop before loading.
    private void OnModulesDirectoryChanged(object sender, FileSystemEventArgs e) =>
        _watchTimer?.Change(WatchDelay, Timeout.InfiniteTimeSpan);

    /// <summary><c>Modules/&lt;Name&gt;/&lt;Name&gt;.dll</c> and the assemblies placed directly in the folder.</summary>
    private static IEnumerable<string> FindModuleAssemblies(string modulesDirectory)
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
