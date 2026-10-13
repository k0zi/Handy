using System.Runtime.InteropServices;
using CommunityToolkit.Mvvm.ComponentModel;
using Handy.Abstractions;

namespace Handy.Modules.SystemInfo;

public sealed class SystemInfoViewModel : ObservableObject
{
    private readonly ILocalizationService _localization;

    public SystemInfoViewModel(ILocalizationService localization)
    {
        _localization = localization;
        localization.CultureChanged += (_, _) => OnPropertyChanged(nameof(Language));
    }

    public string OperatingSystem => RuntimeInformation.OSDescription;

    public string Architecture => RuntimeInformation.OSArchitecture.ToString();

    public string Runtime => RuntimeInformation.FrameworkDescription;

    public string MachineName => Environment.MachineName;

    public int Processors => Environment.ProcessorCount;

    public string Language => _localization.CurrentCulture.NativeName;
}
