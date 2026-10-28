<p align="center">
  <img src="src/Handy/Assets/handy.png" alt="Handy, a beaver in a hard hat and overalls" width="220">
</p>

# Handy

An open source, modular desktop toolbox built with [Avalonia](https://avaloniaui.net/). The application itself is
only a shell: a window with a side menu, a settings page, themes and languages. Everything you actually use in it is
a **module**, a plugin assembly dropped into a `Modules` folder.

- **Drop-in modules** – every module adds one menu item and one view, plus an optional settings page.
- **Loaded at runtime** – a module copied into a `Modules` folder shows up in the menu without a restart.
- **Installable from sources** – modules can be searched and installed from folders of `.nupkg` files and NuGet
  feeds set in the settings (see [Distributing a module](docs/module-development.md#as-a-nuget-package)).
- **Isolated** – each module is loaded in its own `AssemblyLoadContext` and may bring its own dependencies; a broken
  module cannot take the application down.
- **Localized** – English and Hungarian, switchable at runtime; modules follow the language of the host.
- **Themed** – light and dark themes from [KD.Avalonia.Rice](https://www.nuget.org/packages/KD.Avalonia.Rice).
- **Cross-platform** – runs wherever Avalonia and .NET 10 do (Windows, Linux, macOS).

## Getting started

You need the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```bash
git clone https://github.com/k0zi/Handy.git
cd Handy
dotnet build
dotnet run --project src/Handy --no-build
```

Build the whole solution before running: the host does not reference the modules, so `dotnet run --project src/Handy`
on its own builds the application without them. Building a module copies it into the host's `Modules` folder.

Run the tests with:

```bash
dotnet test
```

## Linux packages

`packaging/build-packages.sh` builds self-contained `.deb` and `.rpm` packages (x64) into `artifacts/packages/`; it
runs the tests first unless you pass `--skip-tests`, and `deb` or `rpm` builds only one format. nfpm is downloaded
into `artifacts/tools/` when it is not installed. The packages contain the host only, without the sample modules.

```bash
packaging/build-packages.sh             # both packages
packaging/install.sh                    # build and install with apt, dnf or zypper
packaging/install.sh --uninstall
```

The application is installed to `/usr/lib/handy` with a `handy` launcher in `/usr/bin` and a menu entry.

Versions come from [Nerdbank.GitVersioning](https://github.com/dotnet/Nerdbank.GitVersioning): `version.json`
holds `<year>.<month>` and the number of commits since it last changed is the patch, e.g. `26.10.3`. The build
script moves `version.json` to the current month when needed; commit that change. Builds outside `main` and `v*`
tags get a `~g<commit>` suffix, which sorts before the release of the same version.

## Repository layout

| Path | Content |
| --- | --- |
| `src/Handy` | The host application: main window, settings page, module loader. |
| `src/Handy.Abstractions` | The contract between the host and the modules (`IHandyModule` and friends). |
| `modules/` | The modules. `Handy.Modules.Notes` and `Handy.Modules.SystemInfo` are samples. |
| `tests/Handy.Tests` | Headless UI tests of the host with the sample modules. |
| `docs/` | Documentation. |
| `packaging/` | Linux packages: nfpm configuration, build and install scripts. |

## Writing a module

A module is a class library with a public class that implements `IHandyModule`:

```csharp
public sealed class HelloModule : IHandyModule
{
    public string Id => "example.hello";
    public string Title => "Hello";
    public object Icon => MaterialIconKind.HandWaveOutline;

    public void Initialize(IModuleContext context) { }

    public Control CreateView() => new TextBlock { Text = "Hello from a module!" };
}
```

The full guide, with views, localization, settings and deployment, is in
[docs/module-development.md](docs/module-development.md).

## Where things are stored

| What | Where |
| --- | --- |
| Modules | `<data>/handy/Modules/`, one folder per module, and `Modules/` next to the Handy executable |
| Application settings (language) | `<config>/handy/settings.json` |
| Theme | `<config>/handy/rice.json` |
| Module settings | `<config>/handy/modules/<module id>.json` |

`<config>` is `~/.config` on Linux, `%APPDATA%` on Windows and `~/Library/Application Support` on macOS.
`<data>` is `~/.local/share` on Linux, `%LOCALAPPDATA%` on Windows and `~/Library/Application Support` on macOS.
With the same module id in both modules folders, the one in `<data>` wins.

## License

Handy is licensed under the [MIT license](LICENSE). Its builds also redistribute third-party components (.NET runtime,
Avalonia, SkiaSharp, the Inter font, Material Design Icons, …) under their own licenses; see
[THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) and the `licenses/` directory. `dotnet publish` copies `LICENSE`,
`THIRD_PARTY_NOTICES.md` and `licenses/` next to the binaries; the Linux packages install them to
`/usr/share/doc/handy/` (deb) and `/usr/share/licenses/handy/` (rpm).

Theme names such as Ubuntu, Fedora, Nord or Dracula only name the palette inspiration; Handy is not affiliated with
or endorsed by those projects.
