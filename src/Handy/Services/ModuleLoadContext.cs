using System.Reflection;
using System.Runtime.Loader;

namespace Handy.Services;

/// <summary>
/// Loads one module assembly with its private dependencies from the module's own folder.
/// Everything the host ships (Handy.Abstractions, Avalonia, Rice, …) comes from the host, otherwise the module's
/// <c>IHandyModule</c> would be a different type than the host's.
/// </summary>
internal sealed class ModuleLoadContext(string moduleAssemblyPath)
    : AssemblyLoadContext(Path.GetFileNameWithoutExtension(moduleAssemblyPath))
{
    private static readonly HashSet<string> HostAssemblyNames = GetHostAssemblyNames();

    private readonly AssemblyDependencyResolver _resolver = new(moduleAssemblyPath);

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        if (assemblyName.Name is { } name && IsHostAssembly(name))
            return null;
        var path = _resolver.ResolveAssemblyToPath(assemblyName);
        return path is null ? null : LoadFromAssemblyPath(path);
    }

    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        var path = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        return path is null ? IntPtr.Zero : LoadUnmanagedDllFromPath(path);
    }

    private static bool IsHostAssembly(string name) =>
        HostAssemblyNames.Contains(name) || Default.Assemblies.Any(a => a.GetName().Name == name);

    private static HashSet<string> GetHostAssemblyNames()
    {
        var paths = (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string ?? "")
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
        return paths.Select(Path.GetFileNameWithoutExtension).ToHashSet(StringComparer.OrdinalIgnoreCase)!;
    }
}
