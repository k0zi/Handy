using Avalonia.Controls;

namespace Handy.Abstractions;

/// <summary>A module's tab page in the settings window.</summary>
public interface IModuleSettingsPage
{
    /// <summary>Tab header in the current language; read again whenever the language changes.</summary>
    string Title { get; }

    Control CreateView();

    /// <summary>Called when the settings window is closed with OK; not called on cancel.</summary>
    void Save();
}
