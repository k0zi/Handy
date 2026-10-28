using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Handy.Services;

namespace Handy.ViewModels;

internal sealed class ModuleSourceItemViewModel
{
    public ModuleSourceItemViewModel(string location, Action<ModuleSourceItemViewModel> remove)
    {
        Location = location;
        RemoveCommand = new RelayCommand(() => remove(this));
    }

    public string Location { get; }

    public IRelayCommand RemoveCommand { get; }
}

/// <summary>A search result, with the state of the same module in the modules folder.</summary>
internal sealed class ModulePackageItemViewModel : ObservableObject
{
    private InstalledModulePackage? _installed;

    public ModulePackageItemViewModel(ModulePackage package, Func<ModulePackageItemViewModel, Task> install)
    {
        Package = package;
        InstallCommand = new AsyncRelayCommand(() => install(this));
    }

    public ModulePackage Package { get; }

    public IAsyncRelayCommand InstallCommand { get; }

    public bool CanInstall => _installed is null;

    public bool CanUpdate => _installed is { IsRestartRequired: false } installed &&
                             PackageVersionComparer.Instance.Compare(Package.Version, installed.Version) > 0;

    public bool IsUpToDate => _installed is { IsRestartRequired: false } && !CanUpdate;

    public bool IsRestartRequired => _installed is { IsRestartRequired: true };

    public void SetInstalled(InstalledModulePackage? installed)
    {
        _installed = installed;
        OnPropertyChanged(string.Empty);
    }
}

internal sealed class InstalledModuleItemViewModel
{
    public InstalledModuleItemViewModel(InstalledModulePackage module, Action<InstalledModuleItemViewModel> uninstall)
    {
        Module = module;
        UninstallCommand = new RelayCommand(() => uninstall(this), () => !module.PendingRemoval);
    }

    public InstalledModulePackage Module { get; }

    /// <summary>The queued version, if an update waits for the restart.</summary>
    public string Version => Module.PendingVersion ?? Module.Version;

    public IRelayCommand UninstallCommand { get; }
}

/// <summary>
/// The "Modules" tab of the settings: the module sources, searching them and installing, updating and removing
/// modules. Sources are saved at once, like the language; installing does not wait for OK either.
/// </summary>
internal sealed partial class ModuleManagerViewModel : ObservableObject, IDisposable
{
    private readonly AppServices _services;
    private (string Key, object[] Args)? _status;

    public ModuleManagerViewModel(AppServices services)
    {
        _services = services;
        RefreshSources();
        RefreshInstalled();
        L.Instance.PropertyChanged += OnLanguageChanged;
    }

    public ObservableCollection<ModuleSourceItemViewModel> Sources { get; } = [];

    public ObservableCollection<ModulePackageItemViewModel> Packages { get; } = [];

    public ObservableCollection<InstalledModuleItemViewModel> Installed { get; } = [];

    [ObservableProperty]
    public partial string NewSource { get; set; } = "";

    [ObservableProperty]
    public partial string Query { get; set; } = "";

    [ObservableProperty]
    public partial bool IsBusy { get; private set; }

    /// <summary>The outcome of the last action, in the current language.</summary>
    public string? Status => _status is var (key, args) ? string.Format(L.Instance[key], args) : null;

    public void Dispose() => L.Instance.PropertyChanged -= OnLanguageChanged;

    [RelayCommand]
    private void AddSource()
    {
        if (!_services.Sources.Add(NewSource))
        {
            SetStatus("InvalidModuleSource");
            return;
        }
        NewSource = "";
        SetStatus(null);
        RefreshSources();
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        Packages.Clear();
        if (_services.Sources.Items.Count == 0)
        {
            SetStatus("NoModuleSources");
            return;
        }

        IsBusy = true;
        SetStatus(null);
        try
        {
            var results = await Task.WhenAll(_services.Sources.Items.Select(async location =>
            {
                try
                {
                    return (Location: location, Packages: await _services.OpenSource(location).SearchAsync(Query), Error: (Exception?)null);
                }
                // A source that is down or wrong must not hide the results of the others.
                catch (Exception e)
                {
                    return (Location: location, Packages: (IReadOnlyList<ModulePackage>)[], Error: e);
                }
            }));

            var newest = results.SelectMany(r => r.Packages)
                .GroupBy(p => p.Id, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.MaxBy(p => p.Version, PackageVersionComparer.Instance)!)
                .OrderBy(p => p.DisplayTitle, StringComparer.CurrentCultureIgnoreCase);
            foreach (var package in newest)
                Packages.Add(new ModulePackageItemViewModel(package, InstallAsync));
            RefreshInstalled();

            if (results.FirstOrDefault(r => r.Error is not null) is { Error: { } error } failed)
                SetStatus("ModuleSourceError", failed.Location, error.Message);
            else if (Packages.Count == 0)
                SetStatus("NoModulesFound");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task InstallAsync(ModulePackageItemViewModel item)
    {
        var package = item.Package;
        IsBusy = true;
        try
        {
            await using var stream = await package.Source.OpenPackageAsync(package);
            var result = await _services.Installer.InstallAsync(stream, package.Source.Location);
            SetStatus(result == ModuleInstallResult.Installed ? "ModuleInstalled" : "ModuleInstalledAfterRestart",
                package.DisplayTitle, package.Version);
        }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException
                                      or HttpRequestException or TaskCanceledException or System.Xml.XmlException)
        {
            SetStatus("ModuleInstallFailed", package.DisplayTitle, e.Message);
        }
        finally
        {
            IsBusy = false;
            RefreshInstalled();
        }
    }

    private void Uninstall(InstalledModuleItemViewModel item)
    {
        try
        {
            _services.Installer.Uninstall(item.Module.Id);
            SetStatus("ModuleRemovedAfterRestart", item.Module.Id);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            SetStatus("ModuleInstallFailed", item.Module.Id, e.Message);
        }
        RefreshInstalled();
    }

    private void RemoveSource(ModuleSourceItemViewModel item)
    {
        _services.Sources.Remove(item.Location);
        RefreshSources();
    }

    private void RefreshSources()
    {
        Sources.Clear();
        foreach (var location in _services.Sources.Items)
            Sources.Add(new ModuleSourceItemViewModel(location, RemoveSource));
    }

    private void RefreshInstalled()
    {
        var installed = _services.Installer.GetInstalled();
        Installed.Clear();
        foreach (var module in installed)
            Installed.Add(new InstalledModuleItemViewModel(module, Uninstall));
        foreach (var package in Packages)
            package.SetInstalled(installed.FirstOrDefault(m => m.Id.Equals(package.Package.Id, StringComparison.OrdinalIgnoreCase)));
    }

    private void SetStatus(string? key, params object[] args)
    {
        _status = key is null ? null : (key, args);
        OnPropertyChanged(nameof(Status));
    }

    private void OnLanguageChanged(object? sender, PropertyChangedEventArgs e) => OnPropertyChanged(nameof(Status));
}
