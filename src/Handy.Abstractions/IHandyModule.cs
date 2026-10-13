using Avalonia.Controls;

namespace Handy.Abstractions;

/// <summary>
/// A Handy module: one menu item and one view, plus an optional settings page.
/// The host creates one instance of every public, non-abstract implementation with a parameterless
/// constructor found in the assemblies of its <c>Modules</c> folder.
/// </summary>
public interface IHandyModule
{
    /// <summary>Stable, unique identifier (e.g. <c>handy.notes</c>); also names the module's settings file.</summary>
    string Id { get; }

    /// <summary>Menu text in the current language; read again whenever the language changes.</summary>
    string Title { get; }

    /// <summary>
    /// Menu icon: a <c>MaterialIconKind</c>, an <c>IImage</c>, a string (e.g. an emoji) or any control.
    /// </summary>
    object? Icon { get; }

    /// <summary>Position in the menu; lower values come first.</summary>
    int Order => 0;

    /// <summary>Called once on the UI thread, right after the module is loaded.</summary>
    void Initialize(IModuleContext context);

    /// <summary>Creates the module's view. Called once, the first time the menu item is selected.</summary>
    Control CreateView();

    /// <summary>
    /// Creates the module's page for the settings window, or <c>null</c> when the module has no settings.
    /// Called every time the settings window opens.
    /// </summary>
    IModuleSettingsPage? CreateSettingsPage() => null;
}
