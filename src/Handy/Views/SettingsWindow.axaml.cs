using Avalonia.Interactivity;
using Handy.ViewModels;
using KD.Avalonia.Rice.Controls;

namespace Handy.Views;

public partial class SettingsWindow : RiceWindow
{
    public SettingsWindow() => InitializeComponent();

    internal SettingsWindow(SettingsViewModel viewModel) : this()
    {
        DataContext = viewModel;
        Closed += (_, _) => viewModel.Dispose();
    }

    private void OnOkClicked(object? sender, RoutedEventArgs e)
    {
        (DataContext as SettingsViewModel)?.Save();
        Close();
    }

    private void OnCancelClicked(object? sender, RoutedEventArgs e) => Close();
}
