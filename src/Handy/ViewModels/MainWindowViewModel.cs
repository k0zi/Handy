using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Handy.Abstractions;
using Handy.Services;

namespace Handy.ViewModels;

internal sealed partial class MainWindowViewModel : ObservableObject
{
    private readonly AppServices _services;
    private ModuleMenuItemViewModel? _moduleBeforeSettings;

    public MainWindowViewModel(AppServices services)
    {
        _services = services;
        foreach (var module in services.Modules.Modules)
            Modules.Add(new ModuleMenuItemViewModel(module));
        SelectedModule = Modules.FirstOrDefault();

        services.Modules.Modules.CollectionChanged += OnModulesChanged;
        services.Localization.CultureChanged += (_, _) =>
        {
            foreach (var item in Modules)
                item.RefreshTitle();
        };
    }

    public ObservableCollection<ModuleMenuItemViewModel> Modules { get; } = [];

    [ObservableProperty]
    public partial ModuleMenuItemViewModel? SelectedModule { get; set; }

    /// <summary>The settings shown in place of a module, <c>null</c> while a module is shown.</summary>
    [ObservableProperty]
    public partial SettingsViewModel? Settings { get; private set; }

    public string ModulesDirectory => _services.Modules.ModulesDirectory;

    public SettingsViewModel CreateSettings() =>
        new(_services.Localization, _services.Modules.Modules, new ModuleManagerViewModel(_services));

    [RelayCommand]
    private void OpenSettings()
    {
        if (Settings is not null)
            return;
        _moduleBeforeSettings = SelectedModule;
        Settings = CreateSettings();
        SelectedModule = null;
    }

    [RelayCommand]
    private void SaveSettings() => CloseSettings(save: true);

    [RelayCommand]
    private void CancelSettings() => CloseSettings(save: false);

    private void CloseSettings(bool save)
    {
        if (Settings is not { } settings)
            return;
        if (save)
            settings.Save();
        // Cleared first, so selecting the module below does not close the settings again.
        Settings = null;
        settings.Dispose();
        SelectedModule ??= _moduleBeforeSettings ?? Modules.FirstOrDefault();
    }

    // Picking a module in the menu while the settings are open leaves them like Cancel does.
    partial void OnSelectedModuleChanged(ModuleMenuItemViewModel? value)
    {
        if (value is not null)
            CloseSettings(save: false);
    }

    // The host only ever inserts modules.
    private void OnModulesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action != NotifyCollectionChangedAction.Add || e.NewItems is null)
            return;
        var index = e.NewStartingIndex;
        foreach (IHandyModule module in e.NewItems)
            Modules.Insert(index++, new ModuleMenuItemViewModel(module));
        if (Settings is null)
            SelectedModule ??= Modules.FirstOrDefault();
    }
}
