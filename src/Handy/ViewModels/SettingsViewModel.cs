using System.Diagnostics;
using System.Globalization;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using Handy.Abstractions;
using Handy.Views;

namespace Handy.ViewModels;

/// <summary>One tab of the settings window.</summary>
internal sealed class SettingsTabViewModel(Func<string> title, Control view, IModuleSettingsPage? page = null)
    : ObservableObject
{
    public string Title => title();

    public Control View => view;

    /// <summary><c>null</c> for the host's own tab.</summary>
    public IModuleSettingsPage? Page => page;

    public void RefreshTitle() => OnPropertyChanged(nameof(Title));
}

/// <summary>The "General" tab followed by one tab for every module that has a settings page.</summary>
internal sealed class SettingsViewModel : ObservableObject, IDisposable
{
    private readonly ILocalizationService _localization;

    public SettingsViewModel(ILocalizationService localization, IEnumerable<IHandyModule> modules)
    {
        _localization = localization;
        Tabs = [new SettingsTabViewModel(() => L.Instance["General"], new GeneralSettingsView { DataContext = this })];
        foreach (var module in modules)
        {
            if (TryCreateTab(module) is { } tab)
                Tabs.Add(tab);
        }
        SelectedTab = Tabs[0];
        localization.CultureChanged += OnCultureChanged;
    }

    public List<SettingsTabViewModel> Tabs { get; }

    public SettingsTabViewModel SelectedTab
    {
        get;
        set => SetProperty(ref field, value);
    }

    public IReadOnlyList<CultureInfo> Cultures => _localization.SupportedCultures;

    /// <summary>Applied and saved at once, like the theme.</summary>
    public CultureInfo SelectedCulture
    {
        get => _localization.CurrentCulture;
        set
        {
            // A ComboBox writes null while its items are replaced.
            if (value is null)
                return;
            _localization.CurrentCulture = value;
            OnPropertyChanged();
        }
    }

    public void Save()
    {
        foreach (var tab in Tabs)
        {
            try
            {
                tab.Page?.Save();
            }
            catch (Exception e)
            {
                Trace.TraceError($"Could not save the settings page '{tab.Title}': {e}");
            }
        }
    }

    public void Dispose() => _localization.CultureChanged -= OnCultureChanged;

    private void OnCultureChanged(object? sender, EventArgs e)
    {
        foreach (var tab in Tabs)
            tab.RefreshTitle();
    }

    // Modules are third party code: a broken settings page only loses its own tab.
    private static SettingsTabViewModel? TryCreateTab(IHandyModule module)
    {
        try
        {
            return module.CreateSettingsPage() is { } page
                ? new SettingsTabViewModel(() => page.Title, page.CreateView(), page)
                : null;
        }
        catch (Exception e)
        {
            Trace.TraceError($"Could not create the settings page of module '{module.Id}': {e}");
            return null;
        }
    }
}
