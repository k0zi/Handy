using System.Globalization;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Handy.Services;
using Handy.ViewModels;
using Handy.Views;
using KD.Avalonia.Rice.Controls;
using Xunit;

namespace Handy.Tests;

public class ModuleTests
{
    private const string Notes = "Handy.Modules.Notes";
    private const string SystemInfo = "Handy.Modules.SystemInfo";

    private static readonly CultureInfo Hungarian = CultureInfo.GetCultureInfo("hu");

    [AvaloniaFact]
    public void Modules_in_the_folder_are_loaded_in_menu_order()
    {
        using var host = new TestHost(SystemInfo, Notes);

        host.Services.Modules.LoadNewModules();
        host.Services.Modules.LoadNewModules();

        Assert.Equal(["handy.notes", "handy.system-info"], host.Services.Modules.Modules.Select(m => m.Id));
    }

    [AvaloniaFact]
    public void A_broken_assembly_does_not_stop_the_other_modules()
    {
        using var host = new TestHost(Notes);
        File.WriteAllText(Path.Combine(host.ModulesDirectory, "Broken.dll"), "not an assembly");

        host.Services.Modules.LoadNewModules();

        Assert.Equal(["handy.notes"], host.Services.Modules.Modules.Select(m => m.Id));
    }

    [AvaloniaFact]
    public void Module_titles_follow_the_language_and_the_language_is_saved()
    {
        using var host = new TestHost(Notes, SystemInfo);
        host.Services.Modules.LoadNewModules();
        Assert.Equal(["Notes", "System info"], host.Services.Modules.Modules.Select(m => m.Title));

        host.Services.Localization.CurrentCulture = Hungarian;

        Assert.Equal(["Jegyzetek", "Rendszerinformáció"], host.Services.Modules.Modules.Select(m => m.Title));
        Assert.Equal(Hungarian, host.CreateServices().Localization.CurrentCulture);
    }

    [AvaloniaFact]
    public void Main_window_shows_the_modules_and_switches_language_live()
    {
        using var host = new TestHost(Notes, SystemInfo);
        host.Services.Modules.LoadNewModules();
        var window = new MainWindow { DataContext = new MainWindowViewModel(host.Services) };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        var menu = window.FindControl<RiceSideMenu>("SideMenu")!;

        Assert.Equal(["Notes", "System info"], MenuTexts(menu));
        Assert.Contains(window.GetVisualDescendants(), c => c.GetType().Name == "NotesView");
        Assert.False(window.FindControl<StackPanel>("EmptyState")!.IsVisible);

        menu.SelectedIndex = 1;
        host.Services.Localization.CurrentCulture = Hungarian;
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(["Jegyzetek", "Rendszerinformáció"], MenuTexts(menu));
        var view = window.GetVisualDescendants().OfType<UserControl>().Single(c => c.GetType().Name == "SystemInfoView");
        Assert.Equal("Rendszerinformáció", view.FindControl<TextBlock>("Heading")!.Text);
        window.Close();
    }

    [AvaloniaFact]
    public void Main_window_shows_the_empty_state_without_modules()
    {
        using var host = new TestHost();
        host.Services.Modules.LoadNewModules();
        var window = new MainWindow { DataContext = new MainWindowViewModel(host.Services) };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.True(window.FindControl<StackPanel>("EmptyState")!.IsVisible);
        window.Close();
    }

    [AvaloniaFact]
    public async Task A_module_copied_into_the_folder_at_runtime_appears_in_the_menu()
    {
        using var host = new TestHost();
        host.Services.Modules.LoadNewModules();
        host.Services.Modules.StartWatching();
        var viewModel = new MainWindowViewModel(host.Services);

        host.Install(Notes);
        for (var i = 0; i < 100 && viewModel.Modules.Count == 0; i++)
        {
            await Task.Delay(100, TestContext.Current.CancellationToken);
            Dispatcher.UIThread.RunJobs();
        }

        Assert.Equal(["Notes"], viewModel.Modules.Select(m => m.Title));
        Assert.Same(viewModel.Modules[0], viewModel.SelectedModule);
    }

    [AvaloniaFact]
    public void Settings_window_has_a_tab_for_modules_with_a_settings_page_only()
    {
        using var host = new TestHost(Notes, SystemInfo);
        host.Services.Modules.LoadNewModules();
        var settings = new MainWindowViewModel(host.Services).CreateSettings();
        var window = new SettingsWindow(settings);
        window.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(["General", "Notes"], TabTexts(window));

        settings.SelectedCulture = Hungarian;
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(["Általános", "Jegyzetek"], TabTexts(window));
        Assert.Equal("Beállítások", window.Title);
        window.Close();
    }

    [AvaloniaFact]
    public void Module_settings_are_saved_on_ok_only()
    {
        using var host = new TestHost(Notes);
        host.Services.Modules.LoadNewModules();
        var notesFile = Path.Combine(host.ConfigDirectory, "modules", "handy.notes.json");

        var cancelled = new MainWindowViewModel(host.Services).CreateSettings();
        SetFontSize(cancelled, 22);
        Assert.False(File.Exists(notesFile));

        var saved = new MainWindowViewModel(host.Services).CreateSettings();
        SetFontSize(saved, 22);
        saved.Save();
        Assert.Contains("\"FontSize\": 22", File.ReadAllText(notesFile));
    }

    private static void SetFontSize(SettingsViewModel settings, double fontSize)
    {
        // The page type lives in the module's own load context, so it is only reachable by reflection.
        var page = settings.Tabs[1].Page!;
        page.GetType().GetProperty("FontSize")!.SetValue(page, fontSize);
    }

    private static IEnumerable<object?> MenuTexts(RiceSideMenu menu) =>
        menu.GetVisualDescendants().OfType<RiceSideMenuItem>().Select(item => item.Content);

    private static IEnumerable<string?> TabTexts(SettingsWindow window) =>
        window.FindControl<TabControl>("Tabs")!.GetVisualDescendants().OfType<TabItem>()
            .Select(tab => tab.GetVisualDescendants().OfType<TextBlock>().First().Text);
}
