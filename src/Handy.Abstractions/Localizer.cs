using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Resources;

namespace Handy.Abstractions;

/// <summary>
/// Texts of one assembly's <c>.resx</c> resources in the application's current language.
/// Bind to the indexer to get texts that follow a language change at runtime:
/// <code>Text="{Binding [Greeting], Source={x:Static local:L.Instance}}"</code>
/// </summary>
public sealed class Localizer : INotifyPropertyChanged
{
    private static CultureInfo _culture = CultureInfo.GetCultureInfo("en");
    private static event Action? CultureChanged;

    private readonly ResourceManager _resources;

    public Localizer(ResourceManager resources)
    {
        _resources = resources;
        // An empty property name tells bindings that every property, the indexer included, has changed.
        CultureChanged += () => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
    }

    /// <param name="baseName">Root name of the resources, e.g. <c>MyModule.Resources.Strings</c>.</param>
    /// <param name="assembly">Assembly that contains the neutral (English) resources.</param>
    public Localizer(string baseName, Assembly assembly) : this(new ResourceManager(baseName, assembly))
    {
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Language of every <see cref="Localizer"/>. Owned by the host; modules change the language through
    /// <see cref="ILocalizationService.CurrentCulture"/>.
    /// </summary>
    public static CultureInfo Culture
    {
        get => _culture;
        set
        {
            _culture = value;
            CultureChanged?.Invoke();
        }
    }

    /// <summary>The text for <paramref name="key"/>, or the key itself when there is no such resource.</summary>
    public string this[string key] => _resources.GetString(key, _culture) ?? key;

    public string Format(string key, params object?[] args) => string.Format(_culture, this[key], args);
}
