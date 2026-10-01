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

Implements `IDisposable`: disposing releases a cached recognizer and unsubscribes its events,
and unsubscribes from `IModelCatalogService.ModelInstalled` and the shared
`DeviceSelectionViewModel`'s property-change notifications (both subscribed in the constructor -
see below), all idempotently, so a shell shutting down never leaks unmanaged inference resources,
a live capture device, or a stale event subscription.

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

**Recognizer reuse.** Per `ISpeechRecognizer`'s own "hot reuse" guidance - composing a recognizer
is the expensive step (it loads the model into native memory), while `Start()`/`Stop()` are cheap
and may be called repeatedly on the same instance - this ViewModel caches at most one recognizer
at a time, bound to the model/capture-device pair it was composed for, and reuses it across many
Start/Stop clicks instead of composing (and reloading the model) on every click. The cache is
invalidated (the recognizer is disposed and the field cleared, so the next Start composes a fresh
one) in exactly three cases, each of which genuinely requires a different recognizer/device pair:
the selected model changes (`OnSelectedModelChanged`), the selected capture device changes
(`OnDeviceSelectionChanged`, subscribed to the shared `DeviceSelectionViewModel`'s
`PropertyChanged`), or a device refresh is pending (the pre-refresh hook below). A recognizer that
reports `SpeechRecognizerUnavailableException` from `Start()` is also invalidated immediately,
rather than retried, since that failure means the specific cached instance is now known-broken.

Both `OnSelectedModelChanged` and `OnDeviceSelectionChanged` call `Stop()` first when `CanStop` is
`true`, before invalidating - defensively, not merely for tidiness: `SelectedModel` and the shared
`DeviceSelectionViewModel.SelectedCaptureDevice`/`CaptureSelection` both have public setters and
are only *disabled in the view* while listening (`CanChangeModel` for the model picker; the
capture picker is never disabled at all), so either change can genuinely arrive while a session is
active - not just programmatically, but from the capture picker, which has no `Listening` guard.
Disposing the recognizer without stopping it first would leave `State` stuck at `Listening`
forever, since a later `Stop()` would see no cached recognizer and no-op while the native
recognizer kept running, unreachable and unstoppable from the UI.

**Stops deterministically before a shared device refresh.** The constructor also registers a
pre-refresh hook with the shared `DeviceSelectionViewModel` via `RegisterPreRefreshHook`,
mirroring the `ModelInstalled` subscription precedent above. The hook calls `Stop()` when
`CanStop` is `true`, then unconditionally invalidates the cached recognizer - its bound capture
device is about to become stale the instant the refresh completes, so it must not be reused - all
wrapped to satisfy the hook's `Func<Task>` contract by returning `Task.CompletedTask`: `Stop()` is
fully synchronous down to the capture device's own closure (it blocks on draining the recognizer
before returning), so no real awaiting is ever needed here. This is what lets the
`DeviceSelectionViewModel.Refresh()` clicked from the "Refresh devices" button stop an actively
listening session deterministically before the shared device table is re-scanned, rather than
relying on the `AudioDeviceInUseException` fallback. `Dispose()` unregisters this hook.

**Start algorithm.** `Start()`:

1. Reports `NoModelSelectedMessage` and enters `Error` when no model is selected
2. When no recognizer is cached: creates the capture device through `IAudioDeviceService`;
   reports `NoCaptureDeviceMessage` and enters `Error` when it is unavailable; composes a
   recognizer through `IRecognizerSessionFactory`; disposes it and reports
   `RecognizerUnavailableMessage` and enters `Error` when it honestly reports itself unavailable;
   otherwise subscribes to `ResultReceived` and caches both the recognizer and its capture device
3. Otherwise reuses the cached recognizer unchanged, skipping composition entirely
4. Clears `Finals`, `Partial`, and `StatusMessage`; captures the current `SynchronizationContext`;
   and calls `Start()` on the (cached or newly composed) recognizer
5. On success, enters `Listening`; on `SpeechRecognizerUnavailableException` (the recognizer
   reported itself available but its capture device failed to start), invalidates the cached
   recognizer (see "Recognizer reuse" above), reports the exception's message, and enters `Error`

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

**Stop algorithm.** `Stop()` is a safe no-op when nothing is cached. Otherwise it stops the
recognizer - without unsubscribing or disposing it, since it remains cached for reuse (see
"Recognizer reuse" above) - clears the captured context, enters `Idle`, and reports
`StoppedMessage`.

**Model-switch guard.** `CanChangeModel` is `true` only when `State != RecognitionStreamingState.Listening`,
computed via `[NotifyPropertyChangedFor(nameof(CanChangeModel))]` on the generated `State`
partial property (the same pattern `CanStart`/`CanStop` already use), so it recomputes on every
state transition with no manual notification code. `RecognitionPanelView.axaml` binds the
model-selection `ComboBox`'s `IsEnabled` to it, so a user cannot switch the selected recognition
model through the view while a streaming session is active. This is a view-level convenience, not
the sole safeguard: `SelectedModel`'s setter remains public, so `OnSelectedModelChanged` (see
"Recognizer reuse" above) still stops an active session defensively before invalidating, rather
than assuming the view's guard makes a mid-session change unreachable.

#### Testability

`RecognitionPanelViewModel` depends on `IModelCatalogService`, `IAudioDeviceService`, the shared
`DeviceSelectionViewModel`, and `IRecognizerSessionFactory` — never on the library's recognition
concretes directly. This is what allows the whole Start/Stop lifecycle, the partial-then-final
transcript sequencing, every unavailable-state path, and the auto-refresh-on-install behavior to
be verified with no downloaded model, no native runtime, and no real microphone. `RefreshCommand`
is not bound to a visible button in `RecognitionPanelView.axaml` - it exists solely for the
auto-refresh-on-install path above and is not a user-facing control, since a manually clickable
refresh beside Start/Stop/the model dropdown proved to be a redundant, confusingly placed control
once auto-refresh-on-install existed.
