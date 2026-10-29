namespace Handy.Services;

internal sealed record AppServices(
    LocalizationService Localization,
    ModuleHost Modules,
    ModuleInstaller Installer,
    ModuleSourceList Sources,
    Func<string, IModulePackageSource> OpenSource)
{
    private static readonly Lazy<HttpClient> DefaultHttp = new(() =>
    {
        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("Handy");
        return http;
    });

    /// <param name="modulesDirectories">The user's modules folder first; modules are installed there.</param>
    /// <param name="http">For the NuGet feeds; a shared client when omitted.</param>
    public static AppServices Create(string configDirectory, IReadOnlyList<string> modulesDirectories, HttpClient? http = null)
    {
        // One store for the file, so the language and the module sources do not overwrite each other.
        var settings = new JsonSettingsStore(Path.Combine(configDirectory, "settings.json"));
        var localization = new LocalizationService(settings);
        var modules = new ModuleHost(modulesDirectories, module => new ModuleContext(
            localization,
            new JsonSettingsStore(Path.Combine(configDirectory, "modules", ToFileName(module.Id) + ".json"))));
        return new AppServices(localization, modules, new ModuleInstaller(modulesDirectories[0]),
            new ModuleSourceList(settings), location => ModulePackageSource.Create(location, http ?? DefaultHttp.Value));
    }

    private static string ToFileName(string moduleId) =>
        string.Join('_', moduleId.Split(Path.GetInvalidFileNameChars()));
}
