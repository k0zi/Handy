<p align="center">
  <img src="src/Handy/Assets/handy.png" alt="Handy, a beaver in a hard hat and overalls" width="220">
</p>

# Handy

An open source, modular desktop toolbox built with [Avalonia](https://avaloniaui.net/). The application itself is
only a shell: a window with a side menu, a settings page, themes and languages. Everything you actually use in it is
a **module**, a plugin assembly dropped into the `Modules` folder.

- **Drop-in modules** – every module adds one menu item and one view, plus an optional settings page.
- **Loaded at runtime** – a module copied into the `Modules` folder shows up in the menu without a restart.
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

## Repository layout

| Path | Content |
| --- | --- |
| `src/Handy` | The host application: main window, settings page, module loader. |
| `src/Handy.Abstractions` | The contract between the host and the modules (`IHandyModule` and friends). |
| `modules/` | The modules. `Handy.Modules.Notes` and `Handy.Modules.SystemInfo` are samples. |
| `tests/Handy.Tests` | Headless UI tests of the host with the sample modules. |
| `docs/` | Documentation. |

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
| Modules | `Modules/` next to the Handy executable, one folder per module |
| Application settings (language) | `<config>/handy/settings.json` |
| Theme | `<config>/handy/rice.json` |
| Module settings | `<config>/handy/modules/<module id>.json` |

`<config>` is `~/.config` on Linux, `%APPDATA%` on Windows and `~/Library/Application Support` on macOS.

## License

Handy is licensed under the [MIT license](LICENSE). Its builds also redistribute third-party components (.NET runtime,
Avalonia, SkiaSharp, the Inter font, Material Design Icons, …) under their own licenses; see
[THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) and the `licenses/` directory. `dotnet publish` copies `LICENSE`,
`THIRD_PARTY_NOTICES.md` and `licenses/` next to the binaries.

Theme names such as Ubuntu, Fedora, Nord or Dracula only name the palette inspiration; Handy is not affiliated with
or endorsed by those projects.
