## CommunityToolkit.Mvvm

### Purpose

CommunityToolkit.Mvvm is used as the SpeechDemo application's MVVM support library. It was chosen
so the demo's view models could be written as plain, testable classes without hand-writing change
notification and command boilerplate for every bound value — boilerplate that would add untested
code to an application whose purpose is to exercise the Speech library.

### Features Used

- `ObservableObject` as the view-model base type
- The `[ObservableProperty]` source generator, including `[NotifyPropertyChangedFor]` for derived
  values
- The `[RelayCommand]` source generator, for both synchronous commands and cancellable
  asynchronous commands

### Integration Pattern

The demo references `CommunityToolkit.Mvvm` directly from `DemaConsulting.Speech.Demo.csproj`.
Every view model derives from `ObservableObject` and declares its observable state as private
generator-annotated fields; views bind to the generated public properties and commands.

The generated members are treated as ordinary public API in tests: the demo's tests assign
generated properties, assert the resulting change notifications, and execute the generated
commands, so a source-generator regression surfaces as a test failure rather than as a silently
unbound UI.

Nothing in the Speech library references this package; it is confined to the demo application.

The package version is pinned in `DemaConsulting.Speech.Demo.csproj`. The `[ObservableProperty]`
and `[RelayCommand]` generators ship in the package as Roslyn source generators and need no extra
configuration, but they require that every annotated class is declared `partial` (and, for
observable properties, derives from `ObservableObject`) and that the project's C# language version
supports the generated code (the demo uses `LangVersion` latest). Annotated members must be
instance methods or fields; a missing `partial` modifier or an unsupported language version is a
build-time error or analyzer diagnostic, not a runtime failure.

Because the generators are compiled into the demo assembly, upgrading the pinned package version
changes the generated code; the OTS tests are re-run on every upgrade to re-verify the generated
members.
