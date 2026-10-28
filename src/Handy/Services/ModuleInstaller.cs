using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json;

namespace Handy.Services;

/// <summary>A module installed from a package, with the change waiting for the next start, if any.</summary>
internal sealed record InstalledModulePackage(string Id, string Version, string Source, string? PendingVersion, bool PendingRemoval)
{
    public bool IsRestartRequired => PendingVersion is not null || PendingRemoval;
}

internal enum ModuleInstallResult
{
    /// <summary>Copied into the modules folder; the module host loads it right away.</summary>
    Installed,

    /// <summary>The module was already there and may be loaded; the new version is applied on the next start.</summary>
    RestartRequired,
}

/// <summary>
/// Installs module packages into the user's modules folder: <c>lib/net&lt;x&gt;.0/</c> of the package becomes
/// <c>Modules/&lt;id&gt;/</c>, so the package must contain <c>&lt;id&gt;.dll</c>. Loaded modules are never unloaded, so
/// updating or removing a module that is already there is queued in <c>Modules/.pending</c> and done by
/// <see cref="ApplyPendingChanges"/> on the next start, before the modules are loaded.
/// </summary>
internal sealed class ModuleInstaller(string modulesDirectory)
{
    private const string MarkerFileName = ".handy-package.json";
    private const string RemovalSuffix = ".remove";

    private string PendingDirectory => Path.Combine(modulesDirectory, ".pending");

    /// <summary>The modules installed from packages (not the ones copied in by hand).</summary>
    public IReadOnlyList<InstalledModulePackage> GetInstalled()
    {
        if (!Directory.Exists(modulesDirectory))
            return [];
        var installed = new List<InstalledModulePackage>();
        foreach (var directory in Directory.EnumerateDirectories(modulesDirectory))
        {
            var id = Path.GetFileName(directory);
            if (id.StartsWith('.') || ReadMarker(directory) is not { } marker)
                continue;
            var pending = ReadMarker(Path.Combine(PendingDirectory, id));
            installed.Add(new InstalledModulePackage(marker.Id, marker.Version, marker.Source, pending?.Version,
                File.Exists(RemovalMarker(id))));
        }
        // Installed while the module was already there: only the queued version exists so far.
        if (Directory.Exists(PendingDirectory))
        {
            foreach (var directory in Directory.EnumerateDirectories(PendingDirectory))
            {
                if (!installed.Any(m => m.Id.Equals(Path.GetFileName(directory), StringComparison.OrdinalIgnoreCase)) &&
                    ReadMarker(directory) is { } pending)
                    installed.Add(new InstalledModulePackage(pending.Id, "", pending.Source, pending.Version, false));
            }
        }
        return installed.OrderBy(m => m.Id, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <param name="package">The <c>.nupkg</c>; read to the end, not disposed.</param>
    /// <param name="source">Where it came from, shown in the list of installed modules.</param>
    public async Task<ModuleInstallResult> InstallAsync(Stream package, string source, CancellationToken cancellationToken = default)
    {
        var seekable = package;
        if (!package.CanSeek)
        {
            seekable = new MemoryStream();
            await package.CopyToAsync(seekable, cancellationToken);
            seekable.Position = 0;
        }

        Directory.CreateDirectory(modulesDirectory);
        // Starts with a dot, so the module host does not take it for a module while it is filled.
        var staging = Path.Combine(modulesDirectory, ".staging-" + Guid.NewGuid().ToString("N"));
        try
        {
            string id;
            using (var archive = new ZipArchive(seekable, ZipArchiveMode.Read, leaveOpen: true))
            {
                var manifest = PackageManifest.Read(archive);
                id = manifest.Id;
                if (!manifest.IsModule)
                    throw new InvalidDataException($"'{id}' is not a Handy module package (package type {PackageManifest.ModulePackageType}).");
                if (id.StartsWith('.') || id.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                    throw new InvalidDataException($"'{id}' cannot be used as a folder name.");
                Extract(archive, FindLibFolder(archive, id), staging);
                WriteMarker(staging, new PackageMarker(id, manifest.Version, source));
            }

            var target = Path.Combine(modulesDirectory, id);
            if (!Directory.Exists(target))
            {
                DeleteFile(RemovalMarker(id));
                DeleteDirectory(Path.Combine(PendingDirectory, id));
                Directory.Move(staging, target);
                return ModuleInstallResult.Installed;
            }

            Directory.CreateDirectory(PendingDirectory);
            DeleteFile(RemovalMarker(id));
            DeleteDirectory(Path.Combine(PendingDirectory, id));
            Directory.Move(staging, Path.Combine(PendingDirectory, id));
            return ModuleInstallResult.RestartRequired;
        }
        finally
        {
            DeleteDirectory(staging);
        }
    }

    /// <summary>Removes the module on the next start (or right away, when it is only queued for installation).</summary>
    public void Uninstall(string id)
    {
        DeleteDirectory(Path.Combine(PendingDirectory, id));
        if (!Directory.Exists(Path.Combine(modulesDirectory, id)))
            return;
        Directory.CreateDirectory(PendingDirectory);
        File.WriteAllText(RemovalMarker(id), "");
    }

    /// <summary>Does the queued updates and removals. Call on start, before the modules are loaded.</summary>
    public void ApplyPendingChanges()
    {
        if (!Directory.Exists(PendingDirectory))
            return;
        foreach (var marker in Directory.EnumerateFiles(PendingDirectory, "*" + RemovalSuffix))
        {
            var id = Path.GetFileName(marker)[..^RemovalSuffix.Length];
            TryApply(id, () =>
            {
                DeleteDirectory(Path.Combine(modulesDirectory, id));
                File.Delete(marker);
            });
        }
        foreach (var update in Directory.EnumerateDirectories(PendingDirectory))
        {
            var id = Path.GetFileName(update);
            TryApply(id, () =>
            {
                var target = Path.Combine(modulesDirectory, id);
                DeleteDirectory(target);
                Directory.Move(update, target);
            });
        }
        if (!Directory.EnumerateFileSystemEntries(PendingDirectory).Any())
            Directory.Delete(PendingDirectory);
    }

    /// <summary>The highest <c>lib/net&lt;x&gt;.&lt;y&gt;</c> the running .NET can load that contains <c>&lt;id&gt;.dll</c>.</summary>
    private static string FindLibFolder(ZipArchive archive, string id)
    {
        var runtime = new Version(Environment.Version.Major, Environment.Version.Minor);
        var folder = archive.Entries
            .Select(e => e.FullName.Split('/'))
            .Where(parts => parts.Length == 3 && parts[0] == "lib" &&
                            parts[2].Equals(id + ".dll", StringComparison.OrdinalIgnoreCase))
            .Select(parts => (Folder: parts[1], Version: parts[1].StartsWith("net") && Version.TryParse(parts[1][3..], out var v) ? v : null))
            .Where(f => f.Version is not null && f.Version <= runtime)
            .MaxBy(f => f.Version);
        return folder.Folder is { } name
            ? $"lib/{name}/"
            : throw new InvalidDataException($"The package has no lib/net{runtime}/{id}.dll (or an older .NET version of it).");
    }

    private static void Extract(ZipArchive archive, string libFolder, string destination)
    {
        var root = Path.GetFullPath(destination) + Path.DirectorySeparatorChar;
        foreach (var entry in archive.Entries)
        {
            if (!entry.FullName.StartsWith(libFolder, StringComparison.Ordinal) || entry.FullName.EndsWith('/'))
                continue;
            var target = Path.GetFullPath(Path.Combine(root, entry.FullName[libFolder.Length..]));
            // "Zip slip": an entry like "../../x" must not end up outside the module's folder.
            if (!target.StartsWith(root, StringComparison.Ordinal))
                throw new InvalidDataException($"The package entry '{entry.FullName}' points outside the module folder.");
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            entry.ExtractToFile(target);
        }
    }

    private string RemovalMarker(string id) => Path.Combine(PendingDirectory, id + RemovalSuffix);

    private static void TryApply(string id, Action apply)
    {
        try
        {
            apply();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Trace.TraceError($"Could not apply the pending change of module '{id}': {e.Message}");
        }
    }

    // File.Delete throws when the folder is missing.
    private static void DeleteFile(string path)
    {
        if (File.Exists(path))
            File.Delete(path);
    }

    private static void DeleteDirectory(string path)
    {
        if (Directory.Exists(path))
            Directory.Delete(path, recursive: true);
    }

    private static PackageMarker? ReadMarker(string moduleDirectory)
    {
        var path = Path.Combine(moduleDirectory, MarkerFileName);
        try
        {
            return File.Exists(path) ? JsonSerializer.Deserialize<PackageMarker>(File.ReadAllText(path)) : null;
        }
        catch (Exception e) when (e is IOException or JsonException)
        {
            return null;
        }
    }

    private static void WriteMarker(string moduleDirectory, PackageMarker marker) =>
        File.WriteAllText(Path.Combine(moduleDirectory, MarkerFileName), JsonSerializer.Serialize(marker));

    /// <summary>Kept in the module's folder: which package and version it was installed from.</summary>
    private sealed record PackageMarker(string Id, string Version, string Source);
}
