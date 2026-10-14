## Avalonia

### Purpose

Avalonia is used as the SpeechDemo application's cross-platform desktop UI framework. It was
chosen because the demo has to run on the same desktop platforms the Speech library supports —
Windows, Linux, and macOS — and a Windows-only UI framework would have limited the demo's
evidence value to a single platform.

### Features Used

- The Avalonia application host and classic desktop lifetime (`Avalonia`, `Avalonia.Desktop`)
- XAML views with compiled bindings, `DataTemplate`-based panel hosting, and `TabControl`
  navigation
- The Fluent theme (`Avalonia.Themes.Fluent`) and the bundled Inter font
  (`Avalonia.Fonts.Inter`), so the demo looks the same on every platform without relying on
  system-installed fonts
- The developer-tools overlay (`Avalonia.Diagnostics`), referenced only in `Debug` builds

### Integration Pattern

The demo references the Avalonia packages directly from `DemaConsulting.Speech.Demo.csproj`.
`Program.BuildAvaloniaApp()` configures the application host, and `App` supplies the theme and
the composition root. Views are XAML files bound to view models; no application logic lives in
code-behind, so nothing the demo verifies depends on Avalonia being started.

`Avalonia.Diagnostics` is conditioned on `'$(Configuration)' == 'Debug'` because the
developer-tools overlay is a development aid that must never ship in a `Release` build.

The exact Avalonia package versions are pinned in `DemaConsulting.Speech.Demo.csproj` and recorded
in the SBOM. `Avalonia.Diagnostics` is intentionally kept on its final standalone release, because
the newer major line replaced it with a separately distributed developer-tools offering. Since it
is a Debug-only development aid that never ships, the version skew carries no runtime risk.

Only the application host is covered by an OTS requirement. The XAML views, Fluent theme, and Inter
font are used too, but they are verified by compiled bindings at build time and by manual visual
inspection rather than by automated OTS tests.

The Speech library itself never references Avalonia. Its project file asserts this: the build
fails if the library gains an `Avalonia*` package reference, which keeps the demo's UI framework
out of the shipped NuGet package.
