using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using Handy.Abstractions;

namespace Handy.Modules.Notes;

/// <summary>Edits a copy of the notepad's look; the notepad only changes when the settings are saved.</summary>
public sealed partial class NotesSettingsPage(NotesViewModel notes) : ObservableObject, IModuleSettingsPage
{
    public string Title => L.Instance["Title"];

    [ObservableProperty]
    public partial double FontSize { get; set; } = notes.FontSize;

    [ObservableProperty]
    public partial bool WrapText { get; set; } = notes.WrapText;

    public Control CreateView() => new NotesSettingsView { DataContext = this };

    public void Save()
    {
        notes.FontSize = FontSize;
        notes.WrapText = WrapText;
    }
}
