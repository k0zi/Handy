using Avalonia;
using Avalonia.Headless;
using Handy.Services;
using Handy.Tests;
using KD.Avalonia.Rice;

[assembly: AvaloniaTestApplication(typeof(TestApp))]
// Rice's ThemeManager is a singleton that can only be attached to one Application per process.
[assembly: AvaloniaTestIsolation(AvaloniaTestIsolationLevel.PerAssembly)]

namespace Handy.Tests;

public static class TestApp
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions())
            .UseRice(Program.ConfigureRice);
}

/// <summary>Host services working on a temporary config and modules folder.</summary>
internal sealed class TestHost : IDisposable
{
    public static readonly string BuiltModules = Path.Combine(AppContext.BaseDirectory, "TestModules");

    private readonly string _root = Directory.CreateTempSubdirectory("handy-tests-").FullName;

    public TestHost(params string[] modules)
    {
        Directory.CreateDirectory(ModulesDirectory);
        foreach (var module in modules)
            Install(module);
        Services = CreateServices();
    }

    public AppServices Services { get; }

    public string ConfigDirectory => Path.Combine(_root, "config");

    public string ModulesDirectory => Path.Combine(_root, "Modules");

    /// <summary>For module packages (<c>.nupkg</c>), e.g. a folder source; created by the test.</summary>
    public string PackagesDirectory => Path.Combine(_root, "Packages");

    /// <summary>Like the modules next to an installed executable; does not exist until a module is installed there.</summary>
    public string BundledModulesDirectory => Path.Combine(_root, "Bundled");

    public AppServices CreateServices(HttpClient? http = null) =>
        AppServices.Create(ConfigDirectory, [ModulesDirectory, BundledModulesDirectory], http);

    /// <summary>Copies a built sample module into the modules folder (or into <paramref name="modulesDirectory"/>).</summary>
    public void Install(string module, string? modulesDirectory = null)
    {
        var source = Path.Combine(BuiltModules, module);
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(modulesDirectory ?? ModulesDirectory, module, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target);
        }
    }

    public void Dispose()
    {
        Services.Modules.Dispose();
        Directory.Delete(_root, recursive: true);
    }
}
