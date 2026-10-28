namespace Handy.Services;

internal static class AppPaths
{
    /// <summary>Also the Rice app id, so the theme choice (<c>rice.json</c>) lives in the same folder.</summary>
    public const string AppId = "handy";

    public static string ConfigDirectory { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppId);

    /// <summary>The user's own modules (<c>~/.local/share/handy/Modules</c> on Linux), writable without admin rights.</summary>
    public static string UserModulesDirectory { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppId, "Modules");

    /// <summary>The modules shipped with the application, next to the executable (read-only when installed).</summary>
    public static string BundledModulesDirectory { get; } = Path.Combine(AppContext.BaseDirectory, "Modules");
}
