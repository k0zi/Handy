using System.Globalization;

namespace Handy.Abstractions;

/// <summary>Host services handed to a module in <see cref="IHandyModule.Initialize"/>.</summary>
public interface IModuleContext
{
    ILocalizationService Localization { get; }

    /// <summary>Key/value store private to the module, saved by the host.</summary>
    IModuleSettings Settings { get; }
}

public interface ILocalizationService
{
    CultureInfo CurrentCulture { get; set; }

    IReadOnlyList<CultureInfo> SupportedCultures { get; }

    event EventHandler? CultureChanged;
}

public interface IModuleSettings
{
    /// <summary>Returns <paramref name="fallback"/> when the key is missing or holds a value of another type.</summary>
    T? Get<T>(string key, T? fallback = default);

    /// <summary>Stores the value and saves the file.</summary>
    void Set<T>(string key, T value);
}
