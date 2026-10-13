using Avalonia.Controls;
using Handy.Abstractions;
using Material.Icons;

namespace Handy.Modules.Notes;

/// <summary>Sample module with a settings page: a notepad whose text and look are saved by the host.</summary>
public sealed class NotesModule : IHandyModule
{
    private NotesViewModel? _viewModel;

    public string Id => "handy.notes";

    public string Title => L.Instance["Title"];

    public object Icon => MaterialIconKind.NoteTextOutline;

    public int Order => 10;

    private NotesViewModel ViewModel =>
        _viewModel ?? throw new InvalidOperationException("The module is not initialized.");

    public void Initialize(IModuleContext context) => _viewModel = new NotesViewModel(context.Settings);

    public Control CreateView() => new NotesView { DataContext = ViewModel };

    public IModuleSettingsPage CreateSettingsPage() => new NotesSettingsPage(ViewModel);
}
