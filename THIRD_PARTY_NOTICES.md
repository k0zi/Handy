# Third-party notices

Handy's own source code is licensed under the MIT license in LICENSE.
The self-contained builds of Handy also redistribute the third-party components listed below. These components
keep their own licenses, and the full license texts are in `licenses/`. Listing them here does not relicense
them or imply endorsement.

## Redistributed libraries (self-contained publish, linux-x64)

| Component | Source | License / local copy |
| --- | --- | --- |
| .NET runtime 10.0.13 (self-contained runtime pack) | https://github.com/dotnet/runtime | MIT: licenses/dotnet-runtime-MIT.txt; upstream component notices: licenses/dotnet-runtime-THIRD-PARTY-NOTICES.txt |
| Avalonia 12.1.3 (Avalonia, Avalonia.Desktop, Avalonia.FreeDesktop, Avalonia.FreeDesktop.AtSpi, Avalonia.HarfBuzz, Avalonia.Native, Avalonia.Remote.Protocol, Avalonia.Skia, Avalonia.Themes.Fluent, Avalonia.Win32, Avalonia.X11, Avalonia.Fonts.Inter) | https://github.com/AvaloniaUI/Avalonia | MIT: licenses/Avalonia-MIT.txt; upstream component notices: licenses/Avalonia-NOTICE.txt |
| Inter font, embedded in Avalonia.Fonts.Inter 12.1.3 | https://github.com/rsms/inter | OFL-1.1: licenses/Inter-OFL-1.1.txt (applies to the font, separately from Avalonia's MIT wrapper) |
| SkiaSharp 3.119.4 and HarfBuzzSharp 8.3.1.3, with their Linux native assets (libSkiaSharp.so, libHarfBuzzSharp.so) | https://github.com/mono/SkiaSharp | MIT: licenses/SkiaSharp-MIT.txt; native component notices (Skia, HarfBuzz, libpng, …): licenses/SkiaSharp-THIRD-PARTY-NOTICES.txt |
| CommunityToolkit.Mvvm 8.4.2 | https://github.com/CommunityToolkit/dotnet | MIT: licenses/CommunityToolkit-MIT.txt; upstream component notices: licenses/CommunityToolkit-THIRD-PARTY-NOTICES.txt |
| KD.Avalonia.Rice 26.10.6 | https://github.com/k0zi/KD.Avalonia.Rice | MIT: licenses/KD.Avalonia.Rice-MIT.txt |
| Material.Icons and Material.Icons.Avalonia 3.0.2 | https://github.com/SKProCH/Material.Icons | Wrapper code, MIT: licenses/Material.Icons-MIT.txt; icon paths (Pictogrammers Material Design Icons): licenses/Pictogrammers-LICENSE.txt and licenses/Apache-2.0.txt |
| MicroCom.Runtime 0.11.6 | https://github.com/kekekeks/MicroCom | MIT: licenses/MicroCom-MIT.txt |
| Tmds.DBus.Protocol 0.94.1 | https://github.com/tmds/Tmds.DBus | MIT: licenses/Tmds.DBus-MIT.txt |

Builds for other platforms ship other native assets (for example the ANGLE natives of
Avalonia.Angle.Windows.Natives on Windows, BSD-3-Clause) and need their notices added before they are released.

### Theme palettes (through KD.Avalonia.Rice)

KD.Avalonia.Rice's themes adapt these color schemes. The theme names identify the palette inspiration (and, for
Ubuntu, Fedora, Manjaro and openSUSE, the Linux distribution whose colors inspired the theme). No affiliation with
or endorsement by the named projects is claimed; their names and trademarks belong to their owners.

| Palette | Source | License copy |
| --- | --- | --- |
| Nord, Sven Greb | https://github.com/nordtheme/nord | licenses/Nord-MIT.txt |
| Solarized, Ethan Schoonover | https://github.com/altercation/solarized | licenses/Solarized-MIT.txt |
| Dracula, Dracula Theme | https://github.com/dracula/dracula-theme | licenses/Dracula-MIT.txt |
| Gruvbox, morhetz | https://github.com/morhetz/gruvbox | licenses/Gruvbox-MIT.txt (upstream has no license file; MIT/X11 as declared in its README) |
| Catppuccin | https://github.com/catppuccin/palette | licenses/Catppuccin-MIT.txt |

## Own artwork

The Handy beaver artwork (`src/Handy/Assets/handy.png`, `icon.png`, `icon.ico`) was generated for this project in
October 2026 with OpenAI's image generation (ChatGPT, gpt-image; C2PA content credentials are embedded in the PNG
files), using no reference image. To the extent the project contributors hold rights in it, it is available under
the repository's MIT license. This is not a claim of exclusive copyright in machine-generated material.

## Not redistributed

The following are used only to build or test Handy and are not included in the publish output:
Avalonia.Headless.XUnit, Avalonia.Headless, Avalonia.BuildServices, Microsoft.NET.Test.Sdk, Microsoft.CodeCoverage,
Microsoft.TestPlatform.ObjectModel, Microsoft.TestPlatform.TestHost, Microsoft.Testing.Platform,
Microsoft.Testing.Platform.MSBuild, Microsoft.Testing.Extensions.Telemetry,
Microsoft.Testing.Extensions.TrxReport.Abstractions, Microsoft.ApplicationInsights, Microsoft.Bcl.AsyncInterfaces,
Microsoft.Win32.Registry 5.0.0, xunit.v3 (and its xunit.v3.* packages), xunit.analyzers, xunit.runner.visualstudio.
The macOS, Windows and WebAssembly native asset packages of SkiaSharp and HarfBuzzSharp and
Avalonia.Angle.Windows.Natives are not part of the linux-x64 build.

## Maintenance

When you add copied code or assets, or change the dependencies, record the source, version,
author and license here, and update the license copies in `licenses/`. Before a release, check
the publish output (`dotnet publish src/Handy -c Release -r linux-x64 --self-contained`) for new native libraries.
