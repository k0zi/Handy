namespace Handy.Services;

internal static class AppPaths
{
    /// <summary>Also the Rice app id, so the theme choice (<c>rice.json</c>) lives in the same folder.</summary>
    public const string AppId = "handy";

    public static string ConfigDirectory { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppId);

    public static string ModulesDirectory { get; } = Path.Combine(AppContext.BaseDirectory, "Modules");
}
