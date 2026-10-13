using Avalonia.Controls;
using Handy.Abstractions;
using Material.Icons;

namespace Handy.Modules.SystemInfo;

/// <summary>Sample module without a settings page.</summary>
public sealed class SystemInfoModule : IHandyModule
{
    private SystemInfoViewModel? _viewModel;

    public string Id => "handy.system-info";

    public string Title => L.Instance["Title"];

    public object Icon => MaterialIconKind.InformationOutline;

    public int Order => 20;

    public void Initialize(IModuleContext context) => _viewModel = new SystemInfoViewModel(context.Localization);

    public Control CreateView() => new SystemInfoView { DataContext = _viewModel };
}
