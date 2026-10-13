using Avalonia.Controls;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using Handy.Abstractions;

namespace Handy.ViewModels;

/// <summary>A module as shown in the side menu.</summary>
internal sealed class ModuleMenuItemViewModel(IHandyModule module) : ObservableObject
{
    private Control? _view;

    public IHandyModule Module => module;

    public string Title => module.Title;

    public object? Icon => module.Icon;

    /// <summary>The module's view, created on first use.</summary>
    public Control View => _view ??= CreateView();

    public void RefreshTitle() => OnPropertyChanged(nameof(Title));

    private Control CreateView()
    {
        try
        {
            return module.CreateView();
        }
        catch (Exception e)
        {
            return new TextBlock
            {
                Margin = new Avalonia.Thickness(24),
                TextWrapping = TextWrapping.Wrap,
                Text = L.Instance.Format("ModuleViewError", e.Message),
            };
        }
    }
}
