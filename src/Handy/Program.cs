using Avalonia;
using Handy.Services;
using KD.Avalonia.Rice;
using KD.Avalonia.Rice.Options;
using KD.Avalonia.Rice.Theming;

namespace Handy;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args) =>
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace()
            .UseRice(ConfigureRice);

    internal static void ConfigureRice(RiceOptions options)
    {
        options.AppInfo = new RiceAppInfo
        {
            AppId = AppPaths.AppId,
            Name = "Handy",
            Author = "k0zi",
            IconUri = "avares://Handy/Assets/icon.png",
        };
        options.SupportedThemes = BuiltInThemes.All;
        options.DefaultThemeId = BuiltInThemes.Ubuntu.Id;
    }
}
