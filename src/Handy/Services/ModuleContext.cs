using Handy.Abstractions;

namespace Handy.Services;

internal sealed record ModuleContext(ILocalizationService Localization, IModuleSettings Settings) : IModuleContext;
