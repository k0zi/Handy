using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json;

namespace Handy.Services;

/// <summary>A place module packages (<c>.nupkg</c> files of type <c>HandyModule</c>) can be installed from.</summary>
internal interface IModulePackageSource
{
    /// <summary>The folder path or feed URL the user entered.</summary>
    string Location { get; }

    /// <summary>The newest version of every module package whose id, title or description contains <paramref name="query"/>.</summary>
    Task<IReadOnlyList<ModulePackage>> SearchAsync(string query, CancellationToken cancellationToken = default);

    Task<Stream> OpenPackageAsync(ModulePackage package, CancellationToken cancellationToken = default);
}

internal static class ModulePackageSource
{
    /// <summary>An <c>http(s)</c> URL is a NuGet v3 feed, anything else a folder.</summary>
    public static IModulePackageSource Create(string location, HttpClient http) =>
        IsFeedUrl(location, out var uri) ? new NuGetFeedPackageSource(uri, http) : new FolderPackageSource(location);

    /// <summary>A feed URL or the full path of a folder (which may not be reachable right now, e.g. a network share).</summary>
    public static bool IsValidLocation(string location) =>
        IsFeedUrl(location, out _) || (!location.Contains("://") && Path.IsPathFullyQualified(location));

    private static bool IsFeedUrl(string location, out Uri uri) =>
        Uri.TryCreate(location, UriKind.Absolute, out uri!) && uri.Scheme is "http" or "https";

    internal static bool Matches(string query, string id, string? title, string? description) =>
        string.IsNullOrWhiteSpace(query) ||
        id.Contains(query.Trim(), StringComparison.OrdinalIgnoreCase) ||
        (title?.Contains(query.Trim(), StringComparison.OrdinalIgnoreCase) ?? false) ||
        (description?.Contains(query.Trim(), StringComparison.OrdinalIgnoreCase) ?? false);
}

/// <summary>A folder of <c>.nupkg</c> files, flat or in the <c>&lt;id&gt;/&lt;version&gt;/</c> layout of a local NuGet feed.</summary>
internal sealed class FolderPackageSource(string folder) : IModulePackageSource
{
    public string Location => folder;

    public Task<IReadOnlyList<ModulePackage>> SearchAsync(string query, CancellationToken cancellationToken = default) =>
        Task.Run<IReadOnlyList<ModulePackage>>(() =>
        {
            if (!Directory.Exists(folder))
                throw new DirectoryNotFoundException($"The folder '{folder}' does not exist.");
            return Directory.EnumerateFiles(folder, "*.nupkg", SearchOption.AllDirectories)
                .Select(path => (Path: path, Manifest: TryReadManifest(path)))
                .Where(p => p.Manifest is { IsModule: true } m && ModulePackageSource.Matches(query, m.Id, m.Title, m.Description))
                .GroupBy(p => p.Manifest!.Id, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.MaxBy(p => p.Manifest!.Version, PackageVersionComparer.Instance))
                .Select(p => new ModulePackage(p.Manifest!.Id, p.Manifest.Version, p.Manifest.Title,
                    p.Manifest.Description, p.Manifest.Authors, this, p.Path))
                .OrderBy(p => p.DisplayTitle, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }, cancellationToken);

    public Task<Stream> OpenPackageAsync(ModulePackage package, CancellationToken cancellationToken = default) =>
        Task.FromResult<Stream>(File.OpenRead(package.DownloadLocation));

    private static PackageManifest? TryReadManifest(string path)
    {
        try
        {
            using var package = ZipFile.OpenRead(path);
            return PackageManifest.Read(package);
        }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException or System.Xml.XmlException)
        {
            Trace.TraceWarning($"Skipped the package '{path}': {e.Message}");
            return null;
        }
    }
}

/// <summary>
/// A NuGet v3 feed (nuget.org, GitHub Packages, Azure Artifacts, BaGet, …), given by its service index URL. Searches
/// with the <c>SearchQueryService</c> filtered to the <c>HandyModule</c> package type and downloads from the
/// <c>PackageBaseAddress</c> (flat container).
/// </summary>
internal sealed class NuGetFeedPackageSource(Uri serviceIndex, HttpClient http) : IModulePackageSource
{
    private (Uri Search, Uri PackageBase)? _resources;

    public string Location => serviceIndex.ToString();

    public async Task<IReadOnlyList<ModulePackage>> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        var (search, _) = await GetResourcesAsync(cancellationToken);
        var url = new Uri($"{search}?q={Uri.EscapeDataString(query.Trim())}&packageType={PackageManifest.ModulePackageType}" +
                          "&prerelease=false&semVerLevel=2.0.0&take=100");
        using var response = await GetJsonAsync(url, cancellationToken);
        var packages = new List<ModulePackage>();
        foreach (var item in response.RootElement.GetProperty("data").EnumerateArray())
        {
            // Feeds that ignore the packageType filter still list the types of every result.
            if (item.TryGetProperty("packageTypes", out var types) && types.ValueKind == JsonValueKind.Array &&
                !types.EnumerateArray().Any(t => string.Equals(String(t, "name"), PackageManifest.ModulePackageType,
                    StringComparison.OrdinalIgnoreCase)))
                continue;
            var id = String(item, "id");
            var version = String(item, "version");
            if (id is null || version is null)
                continue;
            packages.Add(new ModulePackage(id, version, String(item, "title"), String(item, "description"),
                Authors(item), this, await GetPackageUrlAsync(id, version, cancellationToken)));
        }
        return packages;
    }

    public async Task<Stream> OpenPackageAsync(ModulePackage package, CancellationToken cancellationToken = default)
    {
        var response = await http.GetAsync(package.DownloadLocation, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStreamAsync(cancellationToken);
    }

    private async Task<string> GetPackageUrlAsync(string id, string version, CancellationToken cancellationToken)
    {
        var (_, packageBase) = await GetResourcesAsync(cancellationToken);
        var (lowerId, lowerVersion) = (id.ToLowerInvariant(), version.ToLowerInvariant());
        return new Uri(packageBase, $"{lowerId}/{lowerVersion}/{lowerId}.{lowerVersion}.nupkg").ToString();
    }

    private async Task<(Uri Search, Uri PackageBase)> GetResourcesAsync(CancellationToken cancellationToken)
    {
        if (_resources is { } resources)
            return resources;
        using var index = await GetJsonAsync(serviceIndex, cancellationToken);
        Uri? search = null, packageBase = null;
        foreach (var resource in index.RootElement.GetProperty("resources").EnumerateArray())
        {
            var (id, type) = (String(resource, "@id"), String(resource, "@type"));
            if (id is null || type is null)
                continue;
            if (search is null && type.StartsWith("SearchQueryService", StringComparison.Ordinal))
                search = new Uri(id);
            else if (packageBase is null && type.StartsWith("PackageBaseAddress", StringComparison.Ordinal))
                packageBase = new Uri(id.EndsWith('/') ? id : id + "/");
        }
        if (search is null || packageBase is null)
            throw new InvalidDataException($"'{serviceIndex}' is not a NuGet v3 feed with search and package download.");
        _resources = (search, packageBase);
        return _resources.Value;
    }

    private async Task<JsonDocument> GetJsonAsync(Uri url, CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
    }

    private static string? String(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    // "authors" is a string or an array of strings, depending on the feed.
    private static string? Authors(JsonElement item) =>
        !item.TryGetProperty("authors", out var authors) ? null
        : authors.ValueKind == JsonValueKind.Array ? string.Join(", ", authors.EnumerateArray().Select(a => a.GetString()))
        : authors.ValueKind == JsonValueKind.String ? authors.GetString()
        : null;
}
