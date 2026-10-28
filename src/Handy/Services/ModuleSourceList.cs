using Handy.Abstractions;

namespace Handy.Services;

/// <summary>The module sources (folders and NuGet feed URLs) the user added, saved in the application settings.</summary>
internal sealed class ModuleSourceList(IModuleSettings settings)
{
    private const string SourcesKey = "moduleSources";

    public IReadOnlyList<string> Items => settings.Get<List<string>>(SourcesKey) ?? [];

    /// <summary><c>false</c> when <paramref name="location"/> is not a feed URL or a full folder path, or is already listed.</summary>
    public bool Add(string location)
    {
        location = location.Trim();
        if (!ModulePackageSource.IsValidLocation(location) ||
            Items.Contains(location, StringComparer.OrdinalIgnoreCase))
            return false;
        settings.Set(SourcesKey, Items.Append(location).ToList());
        return true;
    }

    public void Remove(string location) => settings.Set(SourcesKey, Items.Where(s => s != location).ToList());
}
