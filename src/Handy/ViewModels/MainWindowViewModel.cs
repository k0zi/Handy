using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;
using Handy.Abstractions;
using Handy.Services;

namespace Handy.ViewModels;

internal sealed partial class MainWindowViewModel : ObservableObject
{
    private readonly AppServices _services;

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

    public string ModulesDirectory => _services.Modules.ModulesDirectory;

    public SettingsViewModel CreateSettings() =>
        new(_services.Localization, _services.Modules.Modules);

    // The host only ever inserts modules.
    private void OnModulesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action != NotifyCollectionChangedAction.Add || e.NewItems is null)
            return;
        var index = e.NewStartingIndex;
        foreach (IHandyModule module in e.NewItems)
            Modules.Insert(index++, new ModuleMenuItemViewModel(module));
        SelectedModule ??= Modules.FirstOrDefault();
    }
}
