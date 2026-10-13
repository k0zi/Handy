using Avalonia.Interactivity;
using Handy.ViewModels;
using KD.Avalonia.Rice.Controls;

namespace Handy.Views;

public partial class MainWindow : RiceWindow
{
    public MainWindow() => InitializeComponent();

    private async void OnSettingsRequested(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
            await new SettingsWindow(viewModel.CreateSettings()).ShowDialog(this);
    }
}
