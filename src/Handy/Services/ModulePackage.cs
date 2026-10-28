using System.IO.Compression;
using System.Xml.Linq;

namespace Handy.Services;

/// <summary>The newest version of a module package in a source.</summary>
/// <param name="DownloadLocation">The <c>.nupkg</c>: a file path for a folder source, a URL for a NuGet feed.</param>
internal sealed record ModulePackage(
    string Id, string Version, string? Title, string? Description, string? Authors,
    IModulePackageSource Source, string DownloadLocation)
{
    public string DisplayTitle => string.IsNullOrWhiteSpace(Title) ? Id : Title;
}

/// <summary>What the <c>.nuspec</c> of a package says.</summary>
internal sealed record PackageManifest(
    string Id, string Version, string? Title, string? Description, string? Authors, bool IsModule)
{
    /// <summary>NuGet package type of Handy modules: <c>&lt;PackageType&gt;HandyModule&lt;/PackageType&gt;</c>.</summary>
    public const string ModulePackageType = "HandyModule";

    public static PackageManifest Read(ZipArchive package)
    {
        var entry = package.Entries.FirstOrDefault(e =>
                        !e.FullName.Contains('/') && e.FullName.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase))
                    ?? throw new InvalidDataException("The package has no .nuspec file.");
        XElement? metadata;
        using (var stream = entry.Open())
            metadata = XDocument.Load(stream).Root?.Elements().FirstOrDefault(e => e.Name.LocalName == "metadata");
        if (metadata is null)
            throw new InvalidDataException("The .nuspec file has no metadata.");

        // The nuspec namespace changes with the schema version, so elements are matched by local name.
        string? Value(string name) =>
            metadata.Elements().FirstOrDefault(e => e.Name.LocalName == name)?.Value.Trim() is { Length: > 0 } value
                ? value
                : null;
        var isModule = metadata.Descendants().Any(e =>
            e.Name.LocalName == "packageType" &&
            string.Equals((string?)e.Attribute("name"), ModulePackageType, StringComparison.OrdinalIgnoreCase));
        return new PackageManifest(
            Value("id") ?? throw new InvalidDataException("The .nuspec file has no id."),
            Value("version") ?? throw new InvalidDataException("The .nuspec file has no version."),
            Value("title"), Value("description"), Value("authors"), isModule);
    }
}

/// <summary>Orders NuGet (SemVer 2) versions: <c>1.2.0-beta &lt; 1.2.0 &lt; 1.10.0</c>; build metadata is ignored.</summary>
internal sealed class PackageVersionComparer : IComparer<string>
{
    public static PackageVersionComparer Instance { get; } = new();

    public int Compare(string? x, string? y)
    {
        var (releaseX, prereleaseX) = Split(x ?? "");
        var (releaseY, prereleaseY) = Split(y ?? "");
        for (var i = 0; i < Math.Max(releaseX.Length, releaseY.Length); i++)
        {
            var result = Number(releaseX, i).CompareTo(Number(releaseY, i));
            if (result != 0)
                return result;
        }
        // A release sorts after its prereleases.
        if (prereleaseX is null || prereleaseY is null)
            return (prereleaseX is null).CompareTo(prereleaseY is null);
        return ComparePrerelease(prereleaseX, prereleaseY);
    }

    private static (string[] Release, string? Prerelease) Split(string version)
    {
        var withoutMetadata = version.Split('+')[0];
        var dash = withoutMetadata.IndexOf('-');
        return dash < 0
            ? (withoutMetadata.Split('.'), null)
            : (withoutMetadata[..dash].Split('.'), withoutMetadata[(dash + 1)..]);
    }

    private static long Number(string[] parts, int index) =>
        index < parts.Length && long.TryParse(parts[index], out var number) ? number : 0;

    private static int ComparePrerelease(string x, string y)
    {
        var partsX = x.Split('.');
        var partsY = y.Split('.');
        for (var i = 0; i < Math.Min(partsX.Length, partsY.Length); i++)
        {
            var result = long.TryParse(partsX[i], out var numberX) && long.TryParse(partsY[i], out var numberY)
                ? numberX.CompareTo(numberY)
                : string.Compare(partsX[i], partsY[i], StringComparison.OrdinalIgnoreCase);
            if (result != 0)
                return result;
        }
        return partsX.Length.CompareTo(partsY.Length);
    }
}
