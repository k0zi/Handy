using Handy.Abstractions;

namespace Handy;

/// <summary>The host's own texts (<c>Resources/Strings*.resx</c>).</summary>
internal static class L
{
    public static Localizer Instance { get; } = new("Handy.Resources.Strings", typeof(L).Assembly);
}
