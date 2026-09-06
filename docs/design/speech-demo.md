# SpeechDemo System Design

This document provides the system-level design for the SpeechDemo application.

![SpeechDemo Structure](SpeechDemoView.svg)

## Architecture

SpeechDemo is a cross-platform Avalonia desktop application that demonstrates the Speech
library from the position of an ordinary consumer: it references the shipped library project
and uses only its public API. It is a sibling system to Speech rather than a subsystem of it,
because it is a separately built, separately run software item with its own externally visible
behavior and its own users, and because the library must never depend on it.

In this phase the application consists of six subsystems:

- **ShellSubsystem**: the manual composition root and the window shell — it constructs the
  library entry points and the demo's service adapters, injects them into the panel view
  models, and presents one titled, selectable panel per demonstrated capability — see
  _SpeechDemo ShellSubsystem Design_
- **DeviceSelectionSubsystem**: the capture and playback device pickers — a demo-owned
  enumeration seam over the library's `AudioDeviceFactory` probes, plus the presentation state
  that lists devices, tracks the chosen device by name, refreshes on demand, and explains a
  machine with no audio devices honestly — see _SpeechDemo DeviceSelectionSubsystem Design_
- **ModelCatalogSubsystem**: the model catalog and download panel — a demo-owned catalog seam
  over the library's `SpeechModelCatalog`, plus the row presentation, download progress
  reporting, honest failure states, and the explanatory empty-catalog message — see
  _SpeechDemo ModelCatalogSubsystem Design_
- **ModelSettingsSubsystem**: the generic per-model tunable-parameter settings panel — presenters
  for the library's numeric, choice, and boolean parameter kinds chosen by concrete type alone,
  embedded within the synthesis and recognition panels for whichever model is selected there —
  see _SpeechDemo ModelSettingsSubsystem Design_
- **SynthesisPanelSubsystem**: the text-to-speech panel — a demo-owned synthesizer composition
  seam over the library's `SpeechSynthesizerFactory`, a text input with example Natural Language
  Audio Tag hints, and the Play/Stop lifecycle with honest unavailable-state reporting — see
  _SpeechDemo SynthesisPanelSubsystem Design_
- **RecognitionPanelSubsystem**: the speech-to-text panel — a demo-owned recognizer composition
  seam over the library's `SpeechRecognizerFactory`, and the Start/Stop streaming lifecycle with
  progressive partial-then-final transcript rendering and honest unavailable-state reporting —
  see _SpeechDemo RecognitionPanelSubsystem Design_

This phase completes the three capabilities in the demo application's design:
text-to-speech, speech-to-text, and per-model settings all now have working panels
alongside the device and model-catalog panels from the previous phase.

## External Interfaces

SpeechDemo is an application, not a library: it exposes no public API to other software. Its
external interfaces are its user interface and the interfaces it consumes.

| Interface | Direction | Format | Constraints |
| --- | --- | --- | --- |
| Device pickers | Inbound/Outbound | User interface | Lists devices; explains an empty list |
| Model catalog list | Inbound/Outbound | User interface | Lists models and install state |
| Model settings panel | Inbound/Outbound | User interface | Renders generic controls; explains empty state |
| Text-to-speech panel | Inbound/Outbound | User interface | Text input, tag hints, Play/Stop, playback status |
| Speech-to-text panel | Inbound/Outbound | User interface | Start/Stop, live partial/final transcript |
| Refresh commands | Inbound | User interface | Never throw |
| Download command | Inbound/Outbound | User interface | Reports progress and honest failure |
| `AudioDeviceFactory` | Outbound | Constructor/property | Consumed; never throws |
| `IAudioCaptureDeviceProbe.Enumerate()` | Outbound | Method call/return | Consumed; never throws |
| `IAudioPlaybackDeviceProbe.Enumerate()` | Outbound | Method call/return | Consumed; never throws |
| `AudioDeviceSelection` | Outbound | Value construction | Name-only device identity |
| `SpeechModelCatalog.Enumerate()` | Outbound | Method call/return | Consumed; never throws |
| `SpeechModelCatalog.DownloadAsync(...)` | Outbound | Method call/return | Throws for unknown model id |
| `SpeechSynthesizerFactory.Create(...)` | Outbound | Method call/return | Consumed; never throws |
| `SpeechRecognizerFactory.Create(...)` | Outbound | Method call/return | Consumed; never throws |
| `ISpeechSynthesizer.SpeakAsync(...)` | Outbound | Method call/return | Consumed; cancellation stops playback |
| `ISpeechRecognizer.Start()` / `Stop()` / `ResultReceived` | Outbound | Method call/event | Consumed; UI-marshaled |

## Dependencies

SpeechDemo has one project dependency — the Speech library — and the following NuGet
dependencies:

- **Avalonia** (with `Avalonia.Desktop`, `Avalonia.Themes.Fluent`, and `Avalonia.Fonts.Inter`)
  supplies the cross-platform desktop application host, styling, and view layer;
  `Avalonia.Diagnostics` is referenced only in `Debug` builds because the developer-tools
  overlay must never ship in a `Release` build
- **CommunityToolkit.Mvvm** supplies the observable-property and command source generators the
  view models are built on
- **`org.k2fsa.sherpa.onnx.runtime.win-x64`** and **`org.k2fsa.sherpa.onnx.runtime.linux-x64`**
  supply the native speech-inference runtimes. Per the library's decision not to bundle native
  speech runtimes, the consuming application — this demo — declares the runtime package for
  each RID it intends to run on
- No PortAudio runtime package is declared: `PortAudioSharp2`, referenced by the library,
  declares its native runtime packages as its own dependencies, so they restore transitively
  through the project reference

See _OTS Integration Design_, _Avalonia Design_, and _CommunityToolkit.Mvvm Design_ for details.

## Risk Control Measures

N/A - SpeechDemo provides no safety-critical functionality requiring risk control measures
(IEC 62304 §5.3.3). It is a demonstration application with no clinical or safety role.

## Data Flow

**Start-up composition path:**

1. **Input**: The process entry point configures the Avalonia application host
2. **Composition**: On framework initialization the application constructs an
   `AudioDeviceFactory` and a `SpeechModelCatalog`, wraps each in its demo-owned service
   adapter, and injects those adapters into the panel view models and the window view model
3. **Output**: The main window opens on its first panel; the catalog is disposed when the
   desktop lifetime signals shutdown

**Device enumeration path:**

1. **Input**: The device panel is constructed, or its refresh command is invoked
2. **Enumeration**: The demo's audio device seam calls the library's capture and playback
   probes
3. **Selection**: The panel keeps the current selection when the machine still reports it, and
   otherwise falls back to the first remaining device
4. **Output**: Both pickers list the reported devices and expose the chosen device as an
   `AudioDeviceSelection`; a direction with no devices shows an explanatory status and reports
   the library's system-default selection

**Model catalog and download path:**

1. **Input**: The catalog panel is constructed, or its refresh or download command is invoked
2. **Enumeration**: The demo's catalog seam calls `SpeechModelCatalog.Enumerate()` and one row
   is built per reported model
3. **Download**: The selected row enters the downloading state, the library's download progress
   is reflected on that row, and the library's reported outcome decides the row's final state
4. **Output**: Rows show installed, downloading, not-downloaded, or failed with an explanation;
   an entirely empty catalog shows the explanatory message instead of a blank list

**Model settings path:**

1. **Input**: The synthesis or recognition panel assigns a newly selected model to the embedded
   settings panel's `Model` property
2. **Presentation**: One presenter is built per declared parameter, chosen by the parameter's
   concrete type alone
3. **Output**: Generic controls render each presenter's current value with two-way binding; a
   panel with no model or a model with no declared parameters shows an explanatory message

**Text-to-speech path:**

1. **Input**: The synthesis panel is constructed, or its refresh, Play, or Stop command is
   invoked
2. **Composition**: The demo's synthesizer session seam resolves the selected model's installed
   directory and forwards to `SpeechSynthesizerFactory.Create(...)`, narrowing the model to the
   library's synthesis role
3. **Playback**: The composed synthesizer speaks the entered text (which may contain inline
   Natural Language Audio Tags) through the selected playback device, reporting each lifecycle
   transition
4. **Output**: Playback status reflects synthesizing, playing, idle, or an honest error; Stop
   cancels an in-flight session deterministically

**Speech-to-text path:**

1. **Input**: The recognition panel is constructed, or its refresh, Start, or Stop command is
   invoked
2. **Composition**: The demo's recognizer session seam resolves the selected model's installed
   directory and forwards to `SpeechRecognizerFactory.Create(...)`, narrowing the model to the
   library's recognition role
3. **Streaming**: The composed recognizer streams audio from the selected capture device,
   raising progressive partial and final results
4. **Output**: The trailing partial line is replaced by each new provisional result; a final
   result is committed to the ordered transcript and the partial is cleared; Stop ends the
   session deterministically and releases the recognizer

## Design Constraints

- **No new library API**: The demo must be buildable against the library exactly as shipped. It
  adds no public API to the library and uses no internal access, because doing so would
  invalidate the evidence the demo exists to provide
- **Manual composition, no container**: The object graph is constructed explicitly in the
  application class. A dependency-injection container would hide the very wiring a reader of
  the demo is trying to see
- **Demo-owned seams over library concretes**: The library exposes sealed concrete entry points,
  and static factory methods requiring partly-`internal` interfaces
  (`ISynthesisModel`/`IRecognitionModel`), rather than injectable interfaces for device, catalog,
  and session composition. Rather than change the library, the demo owns thin interfaces over
  each, narrowing the public `ISpeechModel` to the library's role-specific interface inside the
  seam implementation. This is also what makes every panel unit testable without audio hardware,
  a downloaded model, or an `InternalsVisibleTo` grant from the library
- **Nothing throws at start-up**: A machine with no audio backend and no installed model must
  produce a working window, matching the library's own composition guarantee
- **Honest empty states**: An empty device list, an empty catalog, no installed synthesis or
  recognition model, and no selected model in the settings panel are all explained in their
  panel. The library ships no downloadable model in this phase, so every "no installed model"
  state is expected, not a defect
- **No UI automation tests**: Verification is at the view-model level. Driving a real window on
  a CI runner with no display would add fragility without adding evidence

### Platform Support

The application targets a single framework:

| Target Framework | Runtime / Environment |
| --- | --- |
| `net8.0` | .NET 8 LTS |

This is deliberately the floor of the library's supported range rather than the library's full
`net8.0;net9.0;net10.0` matrix: the demo is never packaged for consumers, so it has no
compatibility matrix to satisfy, and building it against the oldest supported runtime proves
the library is genuinely consumable there.

The application runs on Windows, Linux, and macOS wherever Avalonia and the library's native
runtimes are available. Native speech-inference runtimes are declared for `win-x64` and
`linux-x64`; other RIDs run the application successfully but report speech features as
unavailable, exactly as the library does.

### Integration Patterns

- **MVVM**: Every panel is driven from a view model; views contain no application logic
- **Compiled bindings**: Bindings are compiled by default, so a mistyped binding is a build
  error rather than a silent runtime blank
- **Seam-and-adapter**: Each library entry point the demo consumes is reached through a
  demo-owned interface with one real delegating implementation
