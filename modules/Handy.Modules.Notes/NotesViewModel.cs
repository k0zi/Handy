using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using Handy.Abstractions;

namespace Handy.Modules.Notes;

public sealed partial class NotesViewModel : ObservableObject
{
    public const double DefaultFontSize = 14;

    private readonly IModuleSettings _settings;
    private readonly bool _loaded;

    public NotesViewModel(IModuleSettings settings)
    {
        _settings = settings;
        Text = settings.Get(nameof(Text), "")!;
        FontSize = settings.Get(nameof(FontSize), DefaultFontSize);
        WrapText = settings.Get(nameof(WrapText), true);
        _loaded = true;
        // The summary is a formatted text, so it has to follow the language by hand.
        L.Instance.PropertyChanged += (_, _) => OnPropertyChanged(nameof(Summary));
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Summary))]
    public partial string Text { get; set; }

    [ObservableProperty]
    public partial double FontSize { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Wrapping))]
    public partial bool WrapText { get; set; }

    public TextWrapping Wrapping => WrapText ? TextWrapping.Wrap : TextWrapping.NoWrap;

    public string Summary => L.Instance.Format("Characters", Text.Length);

    partial void OnTextChanged(string value) => Save(nameof(Text), value);

    partial void OnFontSizeChanged(double value) => Save(nameof(FontSize), value);

    partial void OnWrapTextChanged(bool value) => Save(nameof(WrapText), value);

    // Reading the settings in the constructor must not write them back.
    private void Save<T>(string key, T value)
    {
        if (_loaded)
            _settings.Set(key, value);
    }
}
