using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Handy.Services;
using Handy.ViewModels;
using Handy.Views;

namespace Handy;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var services = AppServices.Create(AppPaths.ConfigDirectory,
                [AppPaths.UserModulesDirectory, AppPaths.BundledModulesDirectory]);
            services.Installer.ApplyPendingChanges();
            services.Modules.LoadNewModules();
            services.Modules.StartWatching();
            desktop.MainWindow = new MainWindow { DataContext = new MainWindowViewModel(services) };
            desktop.Exit += (_, _) => services.Modules.Dispose();
        }
        base.OnFrameworkInitializationCompleted();
    }
}
