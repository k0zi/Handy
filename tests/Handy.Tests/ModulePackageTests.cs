using System.IO.Compression;
using System.Net;
using System.Text;
using Avalonia.Headless.XUnit;
using Handy.Services;
using Handy.ViewModels;
using Xunit;

namespace Handy.Tests;

public class ModulePackageTests
{
    private const string Notes = "Handy.Modules.Notes";

    [AvaloniaFact]
    public async Task Folder_source_lists_the_newest_module_package_and_skips_other_packages()
    {
        using var host = new TestHost();
        CreatePackage(host.PackagesDirectory, Notes, "1.0.0");
        CreatePackage(host.PackagesDirectory, Notes, "1.10.0");
        CreatePackage(host.PackagesDirectory, Notes, "1.9.0");
        CreatePackage(host.PackagesDirectory, "Some.Library", "2.0.0", isModule: false);
        var source = host.Services.OpenSource(host.PackagesDirectory);

        var packages = await source.SearchAsync("");

        var package = Assert.Single(packages);
        Assert.Equal((Notes, "1.10.0"), (package.Id, package.Version));
        Assert.Single(await source.SearchAsync("notes"));
        Assert.Empty(await source.SearchAsync("nothing like this"));
    }

    [AvaloniaFact]
    public async Task An_installed_package_is_loaded_right_away()
    {
        using var host = new TestHost();
        var path = CreatePackage(host.PackagesDirectory, Notes, "1.0.0");

        await using (var package = File.OpenRead(path))
            Assert.Equal(ModuleInstallResult.Installed, await host.Services.Installer.InstallAsync(package, "test source"));
        host.Services.Modules.LoadNewModules();

        Assert.Equal(["handy.notes"], host.Services.Modules.Modules.Select(m => m.Id));
        var installed = Assert.Single(host.Services.Installer.GetInstalled());
        Assert.Equal((Notes, "1.0.0", "test source", false), (installed.Id, installed.Version, installed.Source, installed.IsRestartRequired));
    }

    [AvaloniaFact]
    public async Task Updating_and_uninstalling_wait_for_the_next_start()
    {
        using var host = new TestHost();
        var installer = host.Services.Installer;
        await Install(installer, CreatePackage(host.PackagesDirectory, Notes, "1.0.0"));

        Assert.Equal(ModuleInstallResult.RestartRequired,
            await Install(installer, CreatePackage(host.PackagesDirectory, Notes, "1.1.0")));
        Assert.Equal(("1.0.0", "1.1.0"), (installer.GetInstalled()[0].Version, installer.GetInstalled()[0].PendingVersion));

        installer.ApplyPendingChanges();
        Assert.Equal(("1.1.0", null), (installer.GetInstalled()[0].Version, installer.GetInstalled()[0].PendingVersion));

        installer.Uninstall(Notes);
        Assert.True(installer.GetInstalled()[0].PendingRemoval);
        Assert.True(Directory.Exists(Path.Combine(host.ModulesDirectory, Notes)));

        installer.ApplyPendingChanges();
        Assert.Empty(installer.GetInstalled());
        Assert.Empty(Directory.EnumerateFileSystemEntries(host.ModulesDirectory));
    }

    [AvaloniaFact]
    public async Task Packages_that_are_not_modules_or_point_outside_the_module_folder_are_rejected()
    {
        using var host = new TestHost();
        var installer = host.Services.Installer;
        var library = CreatePackage(host.PackagesDirectory, Notes, "1.0.0", isModule: false);
        var zipSlip = CreatePackage(host.PackagesDirectory, Notes, "2.0.0", extraEntry: "lib/net10.0/../../../escaped.txt");

        await Assert.ThrowsAsync<InvalidDataException>(() => Install(installer, library));
        await Assert.ThrowsAsync<InvalidDataException>(() => Install(installer, zipSlip));

        Assert.Empty(Directory.EnumerateFileSystemEntries(host.ModulesDirectory));
        Assert.False(File.Exists(Path.Combine(host.ModulesDirectory, "..", "escaped.txt")));
    }

    [AvaloniaFact]
    public async Task NuGet_feed_is_searched_for_module_packages_and_downloaded_from_the_flat_container()
    {
        var nupkg = await File.ReadAllBytesAsync(CreatePackage(Directory.CreateTempSubdirectory().FullName, Notes, "1.2.0"));
        var feed = new FakeFeed(uri => uri.AbsolutePath switch
        {
            "/v3/index.json" => Json("""
                {"version": "3.0.0", "resources": [
                  {"@id": "https://feed.test/query", "@type": "SearchQueryService/3.5.0"},
                  {"@id": "https://feed.test/flat", "@type": "PackageBaseAddress/3.0.0"}]}
                """),
            "/query" => Json("""
                {"data": [
                  {"id": "Handy.Modules.Notes", "version": "1.2.0", "title": "Notes", "authors": ["k0zi"],
                   "packageTypes": [{"name": "HandyModule"}]},
                  {"id": "Some.Library", "version": "1.0.0", "packageTypes": [{"name": "Dependency"}]}]}
                """),
            "/flat/handy.modules.notes/1.2.0/handy.modules.notes.1.2.0.nupkg" => new HttpResponseMessage { Content = new ByteArrayContent(nupkg) },
            _ => null,
        });
        using var host = new TestHost();
        var services = host.CreateServices(new HttpClient(feed));
        var source = services.OpenSource("https://feed.test/v3/index.json");

        var package = Assert.Single(await source.SearchAsync("notes"));
        await using (var stream = await source.OpenPackageAsync(package))
            await services.Installer.InstallAsync(stream, source.Location);

        Assert.Equal(("Notes", "k0zi"), (package.DisplayTitle, package.Authors));
        Assert.Contains("packageType=HandyModule", feed.Requests.Single(u => u.AbsolutePath == "/query").Query);
        Assert.Equal("1.2.0", Assert.Single(services.Installer.GetInstalled()).Version);
    }

    [AvaloniaFact]
    public async Task Module_manager_saves_the_sources_searches_and_installs()
    {
        using var host = new TestHost();
        CreatePackage(host.PackagesDirectory, Notes, "1.0.0");
        using var manager = new ModuleManagerViewModel(host.Services);

        manager.NewSource = "relative/folder";
        manager.AddSourceCommand.Execute(null);
        Assert.Empty(manager.Sources);
        Assert.NotNull(manager.Status);

        manager.NewSource = host.PackagesDirectory;
        manager.AddSourceCommand.Execute(null);
        await manager.SearchCommand.ExecuteAsync(null);
        var item = Assert.Single(manager.Packages);
        Assert.True(item.CanInstall);

        await item.InstallCommand.ExecuteAsync(null);
        host.Services.Modules.LoadNewModules();

        Assert.True(item.IsUpToDate);
        Assert.Equal(Notes, Assert.Single(manager.Installed).Module.Id);
        Assert.Equal(["handy.notes"], host.Services.Modules.Modules.Select(m => m.Id));
        Assert.Equal([host.PackagesDirectory], host.CreateServices().Sources.Items);
    }

    private static async Task<ModuleInstallResult> Install(ModuleInstaller installer, string path)
    {
        await using var package = File.OpenRead(path);
        return await installer.InstallAsync(package, "test source");
    }

    /// <summary>Packs a built sample module the way <c>dotnet pack</c> would: <c>lib/net10.0/</c> plus a <c>.nuspec</c>.</summary>
    private static string CreatePackage(string folder, string id, string version, bool isModule = true, string? extraEntry = null)
    {
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, $"{id}.{version}.nupkg");
        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        var packageTypes = isModule ? """<packageTypes><packageType name="HandyModule" /></packageTypes>""" : "";
        using (var writer = new StreamWriter(archive.CreateEntry(id + ".nuspec").Open()))
            writer.Write($"""
                <?xml version="1.0" encoding="utf-8"?>
                <package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd">
                  <metadata><id>{id}</id><version>{version}</version><authors>k0zi</authors>
                    <description>Test package</description>{packageTypes}</metadata>
                </package>
                """);
        var built = Path.Combine(TestHost.BuiltModules, Notes);
        foreach (var file in Directory.EnumerateFiles(built, "*", SearchOption.AllDirectories))
            archive.CreateEntryFromFile(file, "lib/net10.0/" + Path.GetRelativePath(built, file).Replace('\\', '/'));
        if (extraEntry is not null)
            using (var writer = new StreamWriter(archive.CreateEntry(extraEntry).Open()))
                writer.Write("outside");
        return path;
    }

    private static HttpResponseMessage Json(string json) =>
        new() { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private sealed class FakeFeed(Func<Uri, HttpResponseMessage?> respond) : HttpMessageHandler
    {
        public List<Uri> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri!);
            return Task.FromResult(respond(request.RequestUri!) ?? new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }
}
