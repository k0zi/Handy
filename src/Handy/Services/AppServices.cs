namespace Handy.Services;

internal sealed record AppServices(LocalizationService Localization, ModuleHost Modules)
{
    public static AppServices Create(string configDirectory, string modulesDirectory)
    {
        var localization = new LocalizationService(new JsonSettingsStore(Path.Combine(configDirectory, "settings.json")));
        var modules = new ModuleHost(modulesDirectory, module => new ModuleContext(
            localization,
            new JsonSettingsStore(Path.Combine(configDirectory, "modules", ToFileName(module.Id) + ".json"))));
        return new AppServices(localization, modules);
    }

    private static string ToFileName(string moduleId) =>
        string.Join('_', moduleId.Split(Path.GetInvalidFileNameChars()));
}
