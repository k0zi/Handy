using System.Globalization;
using Handy.Abstractions;
using KD.Avalonia.Rice;

namespace Handy.Services;

/// <summary>Holds the application language, saves it and pushes it to every <see cref="Localizer"/>.</summary>
internal sealed class LocalizationService : ILocalizationService
{
    private const string LanguageKey = "language";

    private readonly IModuleSettings _settings;
    private CultureInfo _currentCulture;

    public LocalizationService(IModuleSettings settings)
    {
        _settings = settings;
        var saved = settings.Get<string>(LanguageKey);
        _currentCulture = SupportedCultures.FirstOrDefault(c => c.Name == saved) ?? SupportedCultures[0];
        Apply();
    }

    /// <summary>The first one is the default. A new language needs a <c>Strings.&lt;code&gt;.resx</c> and an entry here.</summary>
    public IReadOnlyList<CultureInfo> SupportedCultures { get; } =
    [
        CultureInfo.GetCultureInfo("en"),
        CultureInfo.GetCultureInfo("hu"),
    ];

    public event EventHandler? CultureChanged;

    public CultureInfo CurrentCulture
    {
        get => _currentCulture;
        set
        {
            if (Equals(value, _currentCulture) || !SupportedCultures.Contains(value))
                return;
            _currentCulture = value;
            _settings.Set(LanguageKey, value.Name);
            Apply();
            CultureChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void Apply()
    {
        CultureInfo.DefaultThreadCurrentUICulture = _currentCulture;
        CultureInfo.CurrentUICulture = _currentCulture;
        Localizer.Culture = _currentCulture;

        var l = L.Instance;
        RiceStrings.ToggleTheme = l["Rice_ToggleTheme"];
        RiceStrings.Settings = l["Settings"];
        RiceStrings.About = l["Rice_About"];
        RiceStrings.Minimize = l["Rice_Minimize"];
        RiceStrings.Maximize = l["Rice_Maximize"];
        RiceStrings.Restore = l["Rice_Restore"];
        RiceStrings.Close = l["Rice_Close"];
        RiceStrings.Author = l["Rice_Author"];
        RiceStrings.Version = l["Rice_Version"];
        RiceStrings.Ok = l["Ok"];
        RiceStrings.LightVariant = l["Rice_LightVariant"];
        RiceStrings.DarkVariant = l["Rice_DarkVariant"];
        RiceStrings.CollapseMenu = l["Rice_CollapseMenu"];
        RiceStrings.ExpandMenu = l["Rice_ExpandMenu"];
    }
}
