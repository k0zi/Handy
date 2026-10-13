using Handy.Abstractions;

namespace Handy.Modules.SystemInfo;

/// <summary>The module's texts (<c>Resources/Strings*.resx</c>).</summary>
internal static class L
{
    public static Localizer Instance { get; } = new("Handy.Modules.SystemInfo.Resources.Strings", typeof(L).Assembly);
}
