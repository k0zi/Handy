# Developing a module

A Handy module is a .NET class library that adds **one menu item and one view** to the application, and optionally
**one tab to the settings window**. This guide builds a module from scratch; the two sample modules in `modules/` show
the same things in working code:

- [`Handy.Modules.SystemInfo`](../modules/Handy.Modules.SystemInfo) – the minimum: a view and localized texts.
- [`Handy.Modules.Notes`](../modules/Handy.Modules.Notes) – adds saved settings and a settings page.

## Contents

1. [How modules work](#how-modules-work)
2. [Create the project](#1-create-the-project)
3. [Implement the module](#2-implement-the-module)
4. [Build the view](#3-build-the-view)
5. [Localize the texts](#4-localize-the-texts)
6. [Save settings](#5-save-settings)
7. [Add a settings page](#6-add-a-settings-page)
8. [Build, run and debug](#7-build-run-and-debug)
9. [Dependencies](#dependencies)
10. [Testing](#testing)
11. [Distributing a module](#distributing-a-module)
12. [Checklist](#checklist)

## How modules work

The host looks for module assemblies in the `Modules` folder next to its executable:

```
Handy
Modules/
  Handy.Modules.Hello/
    Handy.Modules.Hello.dll        <- must have the same name as its folder
    hu/Handy.Modules.Hello.resources.dll
    SomeDependency.dll
  Standalone.dll                   <- an assembly directly in Modules works too
```

For every assembly it finds, the host

1. loads it in an `AssemblyLoadContext` of its own,
2. creates an instance of every **public, non-abstract class that implements `IHandyModule`**, using the
   **parameterless constructor**,
3. calls `Initialize` on it and inserts it into the side menu, sorted by `Order`, then `Id`.

The folder is watched while the application runs, so a module copied there appears in the menu about half a second
later. Modules are **never unloaded**: replacing or removing one takes effect at the next start.

The lifecycle of a module instance:

| Member | When it is called |
| --- | --- |
| constructor | Once, when the assembly is loaded. |
| `Id`, `Order` | Right after construction, before `Initialize`. |
| `Initialize(context)` | Once, on the UI thread. |
| `Title` | When the menu is built and again after every language change. |
| `Icon` | When the menu item is created. |
| `CreateView()` | Once, the first time the menu item is selected. The view is then kept. |
| `CreateSettingsPage()` | Every time the settings window opens. |

Everything is called on the UI thread. An exception thrown from `Initialize` means the module is not loaded; one thrown
from `CreateView` is shown in place of the view; one from `CreateSettingsPage` only loses the settings tab. In every
case the rest of the application keeps working, and the error goes to the trace output.

## 1. Create the project

Create `modules/Handy.Modules.Hello/Handy.Modules.Hello.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <!-- Provided by the host at runtime, so it must not be copied next to the module. -->
    <ProjectReference Include="..\..\src\Handy.Abstractions\Handy.Abstractions.csproj" Private="false" />
    <PackageReference Include="CommunityToolkit.Mvvm" Version="$(MvvmToolkitVersion)" />
  </ItemGroup>
</Project>
```

and add it to the solution:

```bash
dotnet sln Handy.slnx add modules/Handy.Modules.Hello --solution-folder modules
```

That is the whole project file, because the rest is inherited:

- `Directory.Build.props` in the repository root sets the target framework, nullable reference types, compiled
  bindings and the package versions (`$(AvaloniaVersion)`, `$(MvvmToolkitVersion)`, …).
- `modules/Directory.Build.targets` copies the build output into the host's `Modules/<AssemblyName>/` folder after
  every build.
- `Handy.Abstractions` brings Avalonia, KD.Avalonia.Rice and Material.Icons along, in the versions the host uses.

`Private="false"` matters: `Handy.Abstractions.dll` must come from the host. A module that ships its own copy would
implement a *different* `IHandyModule` type than the one the host looks for. (The host guards against this by always
preferring its own assemblies, but there is no reason to ship the file.)

`CommunityToolkit.Mvvm` is optional; the samples use it for their view models, and the host already ships it.

## 2. Implement the module

`HelloModule.cs`:

```csharp
using Avalonia.Controls;
using Handy.Abstractions;
using Material.Icons;

namespace Handy.Modules.Hello;

public sealed class HelloModule : IHandyModule
{
    private HelloViewModel? _viewModel;

    public string Id => "example.hello";

    public string Title => L.Instance["Title"];

    public object Icon => MaterialIconKind.HandWaveOutline;

    public int Order => 30;

    public void Initialize(IModuleContext context) => _viewModel = new HelloViewModel(context.Settings);

    public Control CreateView() => new HelloView { DataContext = _viewModel };
}
```

| Member | Notes |
| --- | --- |
| `Id` | Stable and unique, e.g. `vendor.module-name`. A second module with the same id is skipped. The id also names the settings file, so changing it later loses the user's settings. |
| `Title` | The menu text. Return it in the current language (see [Localize the texts](#4-localize-the-texts)); the host reads it again when the language changes. |
| `Icon` | A `MaterialIconKind`, an `IImage`, a string (for example an emoji) or any control. May be `null`. |
| `Order` | Position in the menu, lower first. Defaults to `0`; the samples use `10` and `20`. |
| `Initialize` | Keep the `IModuleContext` or the services you need from it. Do not build UI here. |
| `CreateView` | Returns the control shown in the content area. |
| `CreateSettingsPage` | Optional, returns `null` by default. |

The class must be `public` and must have a public parameterless constructor. Everything else in the assembly may be
`internal`.

## 3. Build the view

A view is an ordinary Avalonia `UserControl`. `HelloView.axaml`:

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:local="clr-namespace:Handy.Modules.Hello"
             x:Class="Handy.Modules.Hello.HelloView"
             x:DataType="local:HelloViewModel">
  <StackPanel Margin="24" Spacing="12">
    <TextBlock FontSize="24" FontWeight="SemiBold"
               Text="{Binding [Title], Source={x:Static local:L.Instance}}" />
    <TextBox Text="{Binding Name}"
             PlaceholderText="{Binding [NamePlaceholder], Source={x:Static local:L.Instance}}" />
    <TextBlock Foreground="{DynamicResource RiceForegroundMutedBrush}" Text="{Binding Greeting}" />
  </StackPanel>
</UserControl>
```

`HelloView.axaml.cs`:

```csharp
using Avalonia.Controls;

namespace Handy.Modules.Hello;

public partial class HelloView : UserControl
{
    public HelloView() => InitializeComponent();
}
```

Things to know:

- **Compiled bindings are on** for the whole repository, so every view needs an `x:DataType`.
- **Use the theme's resources** (`{DynamicResource Rice…Brush}`) instead of fixed colors, so the view follows the
  light/dark theme. `DynamicResource`, not `StaticResource`: the theme can change while the view is open.
- The samples use a `24` margin and a `24`pt semi-bold heading; do the same to look at home next to them.
- The view is created once and kept for the lifetime of the application, so it keeps its state when the user switches
  to another module and back.

## 4. Localize the texts

Handy speaks English (the neutral language) and Hungarian, and the user can switch at runtime. A module is expected to
follow.

Put the texts in `.resx` files — they are embedded automatically, no project file change is needed:

```
Resources/Strings.resx       English, the fallback
Resources/Strings.hu.resx    Hungarian
```

```xml
<?xml version="1.0" encoding="utf-8"?>
<root>
  <!-- copy the four <resheader> elements from a sample module's Strings.resx -->
  <data name="Title" xml:space="preserve">
    <value>Hello</value>
  </data>
  <data name="NamePlaceholder" xml:space="preserve">
    <value>Your name</value>
  </data>
  <data name="Greeting" xml:space="preserve">
    <value>Hello, {0}!</value>
  </data>
</root>
```

Expose them through a `Localizer`. `L.cs`:

```csharp
using Handy.Abstractions;

namespace Handy.Modules.Hello;

/// <summary>The module's texts (<c>Resources/Strings*.resx</c>).</summary>
internal static class L
{
    public static Localizer Instance { get; } = new("Handy.Modules.Hello.Resources.Strings", typeof(L).Assembly);
}
```

The first argument is `<root namespace>.<folder>.<file name without extension>`. A typo there does not fail: a
`Localizer` returns the key itself for a text it cannot find, so seeing `Title` in the menu means the base name or the
key is wrong.

Three ways to use it:

| Where | How | Follows a language change |
| --- | --- | --- |
| XAML | `Text="{Binding [Title], Source={x:Static local:L.Instance}}"` | Yes, automatically. |
| `Title` of the module / settings page | `L.Instance["Title"]` | Yes, the host reads it again. |
| Formatted text in a view model | `L.Instance.Format("Greeting", Name)` | Only if you raise the change yourself. |

For the last case, subscribe to the localizer:

```csharp
L.Instance.PropertyChanged += (_, _) => OnPropertyChanged(nameof(Greeting));
```

`Format` formats numbers and dates with the application's language as well.

If you need the language itself rather than a text — to format something by hand, say — use
`context.Localization.CurrentCulture` and its `CultureChanged` event. `SystemInfoViewModel` does this.

> The list of languages belongs to the host (`LocalizationService.SupportedCultures`). A module cannot add one; a
> language the module has no `.resx` for falls back to English.

## 5. Save settings

`context.Settings` is a key/value store that belongs to the module alone. The host keeps it in
`<config>/handy/modules/<module id>.json`.

```csharp
T? Get<T>(string key, T? fallback = default);
void Set<T>(string key, T value);
```

Values are serialized with `System.Text.Json`, so anything it can handle works: primitives, strings, collections,
your own records. `Get` returns the fallback when the key is missing or holds something that is not a `T`, so it never
throws on a hand-edited or outdated file. `Set` writes the file immediately.

`HelloViewModel.cs`:

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using Handy.Abstractions;

namespace Handy.Modules.Hello;

public sealed partial class HelloViewModel : ObservableObject
{
    private readonly IModuleSettings _settings;

    public HelloViewModel(IModuleSettings settings)
    {
        _settings = settings;
        _name = settings.Get(nameof(Name), "")!;
        L.Instance.PropertyChanged += (_, _) => OnPropertyChanged(nameof(Greeting));
    }

    public string Name
    {
        get => _name;
        set
        {
            if (!SetProperty(ref _name, value))
                return;
            _settings.Set(nameof(Name), value);
            OnPropertyChanged(nameof(Greeting));
        }
    }
    private string _name;

    public string Greeting => L.Instance.Format("Greeting", Name);
}
```

Because every `Set` is a file write, take care not to write the values back while you are only loading them.
The example above avoids it by assigning the field; `NotesViewModel` uses `[ObservableProperty]` and a `_loaded` flag
instead. For values that change many times a second (a slider being dragged, a window being resized), save when the
change is finished rather than on every step.

## 6. Add a settings page

A module that has something to configure returns an `IModuleSettingsPage` from `CreateSettingsPage`. The host shows it
as a tab in the settings window, after its own *General* tab.

```csharp
public interface IModuleSettingsPage
{
    string Title { get; }     // tab header, read again when the language changes
    Control CreateView();
    void Save();              // called on OK only
}
```

The settings window has **OK** and **Cancel**, so a page has to work on a copy: it reads the current values when it is
created, the user edits the copy, and `Save` applies it. On cancel the page is simply dropped. `CreateSettingsPage` is
called every time the window opens, which makes this easy — a new page starts from the current values.

Say the view model gets a `Shout` property, loaded and stored exactly like `Name`, that makes the greeting upper
case. Its settings page:

```csharp
public sealed partial class HelloSettingsPage(HelloViewModel hello) : ObservableObject, IModuleSettingsPage
{
    public string Title => L.Instance["Title"];

    [ObservableProperty]
    public partial bool Shout { get; set; } = hello.Shout;

    public Control CreateView() => new HelloSettingsView { DataContext = this };

    public void Save() => hello.Shout = Shout;
}
```

and in the module:

```csharp
public IModuleSettingsPage CreateSettingsPage() => new HelloSettingsPage(_viewModel!);
```

Here `Save` writes to the view model, and the view model stores the value in `IModuleSettings` like any other
property. That way the open view updates at once. See `NotesSettingsPage` and `NotesSettingsView.axaml` for the
complete example.

## 7. Build, run and debug

```bash
dotnet build
dotnet run --project src/Handy --no-build
```

Always build the solution (or at least your module's project): the host has no reference to the modules, so building
`src/Handy` alone does not build or deploy them. After the build your module is in

```
src/Handy/bin/Debug/net10.0/Modules/Handy.Modules.Hello/
```

To debug, start the `Handy` project from your IDE; breakpoints in the module work, since its `.pdb` is deployed too.
Make sure the IDE builds the module before it starts the host — building the whole solution is the simplest way.

You can rebuild a *new* module while Handy is running and watch it appear in the menu. Rebuilding a module that is
already loaded has no effect until the restart (and on Windows the build fails, because the loaded assembly is locked).

If the module does not show up, look at the trace output of the host (the debug output of the IDE). The usual causes:

| Symptom | Cause |
| --- | --- |
| Nothing in the menu, no error | The assembly is not named after its folder, or the module class is not `public`. |
| `Could not load module assembly …` | Not a .NET assembly, or a dependency is missing next to it. |
| `Could not initialize module …` | No parameterless constructor, or the constructor / `Initialize` threw. |
| `Module '…' is already loaded, skipped` | Another module uses the same `Id`. |
| The key is shown instead of the text | Wrong base name in `L.cs`, or the key is missing from `Strings.resx`. |

## Dependencies

Each module has a load context of its own, with one rule: **whatever the host ships comes from the host**, anything
else is loaded from the module's folder.

- *Provided by the host* – the .NET runtime, `Handy.Abstractions`, Avalonia, KD.Avalonia.Rice, Material.Icons,
  CommunityToolkit.Mvvm. Reference them freely, in the versions set in `Directory.Build.props`. A copy next to the
  module is ignored.
- *Private to the module* – any other NuGet package or project. Its assemblies must be deployed next to the module.
  A class library does not copy its NuGet dependencies to the output by default, so turn that on:

  ```xml
  <PropertyGroup>
    <CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies>
  </PropertyGroup>
  ```

  This copies the host-provided assemblies (Avalonia, Rice, …) as well. They are harmless, the host never loads
  them, but you can leave them out of a module you distribute.

  Keep the generated `<module>.deps.json` next to the assembly: the host resolves dependencies, native libraries
  included, from it.

Two modules can use different versions of the same private dependency without getting in each other's way. The other
side of the isolation is that modules do not see each other's types: there is no supported way for one module to call
another.

## Testing

`tests/Handy.Tests` runs the real host headlessly against built modules, with a temporary modules and config folder
per test (`TestHost`). To cover your module there:

1. Add it to `Handy.Tests.csproj` as a build-order-only reference, like the samples:

   ```xml
   <ProjectReference Include="..\..\modules\Handy.Modules.Hello\Handy.Modules.Hello.csproj" ReferenceOutputAssembly="false" />
   ```

2. Install it in a test and assert on what the host shows:

   ```csharp
   [AvaloniaFact]
   public void Hello_module_is_loaded()
   {
       using var host = new TestHost("Handy.Modules.Hello");

       host.Services.Modules.LoadNewModules();

       Assert.Contains(host.Services.Modules.Modules, m => m.Id == "example.hello");
   }
   ```

The module's types live in the module's own load context, so the test project cannot reference them directly: go
through the `IHandyModule` / `IModuleSettingsPage` interfaces, the visual tree, or reflection, as `ModuleTests` does.
Logic that deserves real unit tests is easier to test in a test project of its own that references the module
project normally.

## Distributing a module

A built module is a folder; installing it means copying that folder into `Modules` next to the Handy executable.

```bash
dotnet build modules/Handy.Modules.Hello -c Release
```

The files to ship are in `modules/Handy.Modules.Hello/bin/Release/net10.0/` (the same files the build deploys to
the host's `Modules/Handy.Modules.Hello/`):

- `Handy.Modules.Hello.dll` and `Handy.Modules.Hello.deps.json`
- the language folders (`hu/Handy.Modules.Hello.resources.dll`)
- the private dependencies, if any
- the `.pdb` is optional

The folder must be called `Handy.Modules.Hello`, like the assembly in it.

A module developed outside this repository needs the same three things the repository gives for free: a reference to
`Handy.Abstractions` that is not copied to the output, the same target framework and Avalonia version as the Handy
build it targets, and compiled against that build's `Handy.Abstractions.dll`. `Handy.Abstractions` is not published as
a NuGet package yet, so reference the assembly from a Handy build.

> Modules run with the full rights of the application. Only install modules you trust.

## Checklist

- [ ] The project references `Handy.Abstractions` with `Private="false"`.
- [ ] The module class is `public`, not abstract, and has a parameterless constructor.
- [ ] `Id` is unique and will not change.
- [ ] `Title` and every text in the views come from the `Localizer`, and both `Strings.resx` and `Strings.hu.resx`
      have every key.
- [ ] Formatted texts are refreshed on `L.Instance.PropertyChanged`.
- [ ] Views have an `x:DataType` and use `DynamicResource` theme brushes.
- [ ] Settings are not written back while they are being loaded.
- [ ] The settings page edits a copy and applies it in `Save`.
- [ ] Private dependencies end up in the module's folder.
- [ ] The module is in `Handy.slnx`, so `dotnet build` deploys it.
