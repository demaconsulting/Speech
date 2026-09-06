## SpeechDemo RecognitionPanelSubsystem Design

![RecognitionPanelSubsystem Structure](RecognitionPanelSubsystemView.svg)

### Overview

The RecognitionPanelSubsystem provides the demo's speech-to-text panel. It contains the
following units:

- **IRecognizerSessionFactory** / **RecognizerSessionFactory**: the demo-owned recognizer
  composition seam and its real implementation over the library's `SpeechModelStore` and
  `SpeechRecognizerFactory`
- **RecognitionPanelViewModel**: the panel's presentation state — the installed recognition
  models, the Start/Stop streaming lifecycle, and the progressive partial-then-final transcript

### Interfaces

The subsystem exposes one presentation surface, `RecognitionPanelViewModel`, consumed by
`RecognitionPanelView.axaml`; no other subsystem depends on it. It consumes:

- `IModelCatalogService` and `IAudioDeviceService` — the shell-provided demo services for
  listing installed models and creating capture devices
- the shared `DeviceSelectionViewModel` — the capture-device picker's presentation state
- `IRecognizerSessionFactory` (below) — the demo-owned recognizer composition seam

The library composes a recognizer through the static
`SpeechRecognizerFactory.Create(IRecognitionModel, string, IAudioCaptureDevice, ISpeechDiagnostics?)`
method, which requires an `IRecognitionModel` — an interface whose members are partly `internal`
to the library, so only the library's own assemblies can implement it. A demo-owned seam
therefore accepts the common `ISpeechModel` contract instead and performs the narrowing itself:

| Member | Returns | Behavior |
| --- | --- | --- |
| `Create(model, captureDevice)` | `ISpeechRecognizer` | Never throws |

This is what lets a demo test substitute a plain, publicly implementable fake model for every
scenario — including "wrong role" — with no `InternalsVisibleTo` grant from the library, and
works around the static factory method itself not being substitutable in a ViewModel unit test.

### Design

`RecognizerSessionFactory` resolves the model's installed-files directory from the shared
`SpeechModelStore`, narrows the model to `IRecognitionModel`, and forwards to
`SpeechRecognizerFactory.Create`, inheriting that factory's "nothing throws at composition"
contract. A model that declares a role other than recognition — and is therefore not an
`IRecognitionModel` — returns the library's own `UnavailableSpeechRecognizer.Instance`, exactly
like a model that is not installed, rather than throwing: a host that lets a user choose an
installed model with the wrong role must still get a working, if unavailable, recognizer back.

#### RecognitionPanelViewModel

| Member | Type | Purpose |
| --- | --- | --- |
| `NoModelsMessage` etc. | `const string` | Explanation per honest outcome (no models, no selection, no device, error) |
| `AvailableModels` | `ObservableCollection<ISpeechModel>` | The installed recognition models |
| `SelectedModel` | `ISpeechModel?` | The chosen model |
| `State` | `RecognitionStreamingState` | The current lifecycle state: `Idle`, `Listening`, `Error` |
| `StatusMessage` | `string?` | The current or most recently failed attempt's explanation |
| `Partial` | `string` | The trailing provisional (not-yet-final) transcript text |
| `Finals` | `ObservableCollection<string>` | The ordered committed final transcript lines |
| `HasModels` / `CanStart` / `CanStop` | `bool` | Derived enablement values |
| `CanChangeModel` | `bool` | `true` only when `State != Listening` |
| `RefreshCommand` | generated command | Re-reads the catalog |
| `StartCommand` | generated command | Begins a streaming session |
| `StopCommand` | generated command | Ends the in-flight session |

Implements `IDisposable`: disposing releases an active recognizer and unsubscribes its events,
and unsubscribes from `IModelCatalogService.ModelInstalled` (subscribed in the constructor - see
below), all idempotently, so a shell shutting down never leaks unmanaged inference resources, a
live capture device, or a stale event subscription.

**Model filtering and refresh.** `Refresh()` lists only descriptors whose role is `Recognition`
and whose state is `Downloaded`, mirroring the synthesis panel's own filtering and refresh
algorithm, for the same reason: a model of the wrong role or one not yet downloaded cannot
honestly transcribe audio.

**Auto-refresh on install.** The constructor subscribes to
`IModelCatalogService.ModelInstalled`. The handler ignores any event whose
`ModelInstalledEventArgs.Role` is not `SpeechModelRole.Recognition`, and otherwise calls
`Refresh()` marshaled onto the UI thread through a `SynchronizationContext` captured at
construction (falling back to calling `Refresh()` synchronously when none was captured) - the
same pattern already used for `ResultReceived` below, since the event's raising thread is not
otherwise guaranteed. This is what lets a newly downloaded recognition model completed from the
Model Catalog panel appear in `AvailableModels` automatically, without a manual Refresh click or
app restart. `Dispose()` unsubscribes this handler.

**Start algorithm.** `Start()`:

1. Reports `NoModelSelectedMessage` and enters `Error` when no model is selected
2. Creates the capture device through `IAudioDeviceService`; reports `NoCaptureDeviceMessage` and
   enters `Error` when it is unavailable
3. Composes a recognizer through `IRecognizerSessionFactory`; disposes it and reports
   `RecognizerUnavailableMessage` and enters `Error` when it honestly reports itself unavailable
4. Otherwise clears `Finals`, `Partial`, and `StatusMessage`; captures the current
   `SynchronizationContext`; subscribes to `ResultReceived`; and calls `Start()` on the recognizer
5. On success, enters `Listening`; on `SpeechRecognizerUnavailableException` (the recognizer
   reported itself available but its capture device failed to start), unsubscribes, disposes the
   recognizer, reports the exception's message, and enters `Error`

**Transcript sequencing.** `ResultReceived` is documented to raise from the recognizer's own
background decoding thread, so `OnResultReceived` posts each event through the UI thread's
captured `SynchronizationContext` (falling back to applying it synchronously when none was
captured) before `ApplyResult` touches any observable property — mirroring the library's own
`ConfigureAwait(true)` marshaling used elsewhere in this demo. `ApplyResult` commits a final
result to `Finals` and clears `Partial`, or replaces `Partial` with a provisional one: exactly the
sequencing a live captioning display depends on, so the trailing guess is replaced rather than
accumulated as noise, and a finished utterance becomes a stable committed line the instant the
recognizer considers it final. `BuildTranscriptText()` renders every committed line followed by
any in-progress partial.

**Stop algorithm.** `Stop()` is a safe no-op when nothing is listening. Otherwise it stops the
recognizer, unsubscribes, disposes it, clears the captured context, enters `Idle`, and reports
`StoppedMessage`.

**Model-switch guard.** `CanChangeModel` is `true` only when `State != RecognitionStreamingState.Listening`,
computed via `[NotifyPropertyChangedFor(nameof(CanChangeModel))]` on the generated `State`
partial property (the same pattern `CanStart`/`CanStop` already use), so it recomputes on every
state transition with no manual notification code. `RecognitionPanelView.axaml` binds the
model-selection `ComboBox`'s `IsEnabled` to it, so a user cannot switch the selected recognition
model while a streaming session is active - switching models mid-session would otherwise leave a
running recognizer bound to a model no longer reflected in `SelectedModel`.

#### Testability

`RecognitionPanelViewModel` depends on `IModelCatalogService`, `IAudioDeviceService`, the shared
`DeviceSelectionViewModel`, and `IRecognizerSessionFactory` — never on the library's recognition
concretes directly. This is what allows the whole Start/Stop lifecycle, the partial-then-final
transcript sequencing, every unavailable-state path, and the auto-refresh-on-install behavior to
be verified with no downloaded model, no native runtime, and no real microphone. The view
(`RecognitionPanelView.axaml`) binds a "Refresh" button to `RefreshCommand` alongside Start/Stop -
a mechanical XAML addition requiring no new command logic, since `RefreshCommand` already existed.
