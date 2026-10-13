using Handy.Abstractions;

namespace Handy.Modules.Notes;

/// <summary>The module's texts (<c>Resources/Strings*.resx</c>).</summary>
internal static class L
{
    public static Localizer Instance { get; } = new("Handy.Modules.Notes.Resources.Strings", typeof(L).Assembly);
}
