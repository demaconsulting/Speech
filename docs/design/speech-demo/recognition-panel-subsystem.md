## SpeechDemo RecognitionPanelSubsystem Design

![RecognitionPanelSubsystem Structure](RecognitionPanelSubsystemView.svg)

### Overview

The RecognitionPanelSubsystem provides the demo's speech-to-text panel. It contains the
following units:

- **IRecognizerSessionFactory** / **RecognizerSessionFactory**: the demo-owned recognizer-engine
  composition seam and its real implementation over the library's `SpeechModelStore` and
  `SpeechRecognizerFactory`
- **RecognitionPanelViewModel**: the panel's presentation state — the installed recognition
  models, the async Start/Stop streaming lifecycle over an `ISpeechRecognizerEngine` and an
  `IRecognitionSession`, and the progressive partial-then-final transcript

### Interfaces

The subsystem exposes one presentation surface, `RecognitionPanelViewModel`, consumed by
`RecognitionPanelView.axaml`; no other subsystem depends on it. It consumes:

- `IModelCatalogService` and `IAudioDeviceService` — the shell-provided demo services for
  listing installed models and creating capture devices
- the shared `DeviceSelectionViewModel` — the capture-device picker's presentation state
- `IRecognizerSessionFactory` (below) — the demo-owned recognizer-engine composition seam

The library composes a recognizer engine through the static
`SpeechRecognizerFactory.LoadAsync(IRecognitionModel, SpeechModelStore, ISpeechDiagnostics?, IReadOnlyDictionary<string,object>?, CancellationToken)`
method, which requires an `IRecognitionModel` — an interface whose members are partly `internal`
to the library, so only the library's own assemblies can implement it. A demo-owned seam
therefore accepts the common `ISpeechModel` contract instead and performs the narrowing itself:

| Member | Returns | Behavior |
| --- | --- | --- |
| `LoadAsync(model, cancellationToken)` | `Task<ISpeechRecognizerEngine>` | Never throws |

This is what lets a demo test substitute a plain, publicly implementable fake model for every
scenario — including "wrong role" — with no `InternalsVisibleTo` grant from the library, and
works around the static factory method itself not being substitutable in a ViewModel unit test.
Only the engine is composed through this seam; a capture device is bound later, per run, through
`ISpeechRecognizerEngine.CreateSessionAsync` directly against the returned engine.

### Design

`RecognizerSessionFactory` resolves the model's installed-files directory from the shared
`SpeechModelStore`, narrows the model to `IRecognitionModel`, and forwards to
`SpeechRecognizerFactory.LoadAsync`, inheriting that factory's "nothing throws at composition"
contract. A model that declares a role other than recognition — and is therefore not an
`IRecognitionModel` — returns the library's own `UnavailableSpeechRecognizerEngine.Instance`,
exactly like a model that is not installed, rather than throwing: a host that lets a user choose
an installed model with the wrong role must still get a working, if unavailable, engine back.

#### RecognitionPanelViewModel

| Member | Type | Purpose |
| --- | --- | --- |
| `NoModelsMessage` etc. | `const string` | Explanation per honest outcome (no models, no selection, no device, unavailable engine, session faulted) |
| `AvailableModels` | `ObservableCollection<ISpeechModel>` | The installed recognition models |
| `SelectedModel` | `ISpeechModel?` | The chosen model |
| `State` | `RecognitionStreamingState` | The current lifecycle state: `Idle`, `Listening`, `Error` |
| `StatusMessage` | `string?` | The current or most recently failed attempt's explanation |
| `Partial` | `string` | The trailing provisional (not-yet-final) transcript text |
| `Finals` | `ObservableCollection<string>` | The ordered committed final transcript lines |
| `HasModels` / `CanStart` / `CanStop` | `bool` | Derived enablement values |
| `CanChangeModel` | `bool` | `true` only when `State != Listening` |
| `RefreshCommand` | generated command | Re-reads the catalog |
| `StartCommand` | generated async command | Begins a streaming session |
| `StopCommand` | generated async command (`AllowConcurrentExecutions`) | Ends the in-flight session |

Implements `IAsyncDisposable`: disposing stops and releases an active session and the cached
engine, and unsubscribes from `IModelCatalogService.ModelInstalled` and the shared
`DeviceSelectionViewModel`'s property-change notifications and pre-refresh hook (both registered
in the constructor - see below), all idempotently, so a shell shutting down never leaks unmanaged
inference resources, a live capture device, or a stale event subscription.

**Model filtering and refresh.** `Refresh()` lists only descriptors whose role is `Recognition`
and whose state is `Downloaded`, mirroring the synthesis panel's own filtering and refresh
algorithm, for the same reason: a model of the wrong role or one not yet downloaded cannot
honestly transcribe audio.

**Auto-refresh on install.** The constructor subscribes to
`IModelCatalogService.ModelInstalled`. The handler ignores any event whose
`ModelInstalledEventArgs.Role` is not `SpeechModelRole.Recognition`, and otherwise calls
`Refresh()` marshaled onto the UI thread through a `SynchronizationContext` captured at
construction (falling back to calling `Refresh()` synchronously when none was captured). This is
what lets a newly downloaded recognition model completed from the Model Catalog panel appear in
`AvailableModels` automatically, without a manual Refresh click or app restart. `DisposeAsync()`
unsubscribes this handler.

**Engine/session reuse.** Per `ISpeechRecognizerEngine`'s own reuse guidance, this ViewModel
loads an engine at most once per selected model and reuses it across many Start/Stop cycles,
instead of reloading its model on every click. Because an `IRecognitionSession` is single-use, a
fresh session is created from the cached engine for every Start and released (via
`InvalidateSessionAsync`) before the next one is created. The cached engine is invalidated (via
`InvalidateEngineAsync` - which first releases the session, then disposes and clears the engine)
only when the selected model changes; the cached session alone is invalidated (released, keeping
the engine) whenever the selected capture device changes or the shared device-selection panel
forces a device refresh - a session, not an engine, is bound to a capture device.

Both `OnSelectedModelChanged` and `OnDeviceSelectionChanged` call `StopAsync()` first when
`CanStop` is `true`, before invalidating - defensively, not merely for tidiness: `SelectedModel`
and the shared `DeviceSelectionViewModel.SelectedCaptureDevice`/`CaptureSelection` both have
public setters and are only *disabled in the view* while listening (`CanChangeModel` for the
model picker; the capture picker is never disabled at all), so either change can genuinely arrive
while a session is active - not just programmatically, but from the capture picker, which has no
`Listening` guard. Disposing a session without stopping it first would leave `State` stuck at
`Listening` forever.

**Stops deterministically before a shared device refresh.** The constructor also registers a
pre-refresh hook (`StopBeforeDeviceRefreshAsync`) with the shared `DeviceSelectionViewModel` via
`RegisterPreRefreshHook`, mirroring the `ModelInstalled` subscription precedent above. The hook
delegates to the same stop-then-invalidate-session path used for a capture-device change, so an
actively listening session is stopped and released before the shared device table is re-scanned
- its bound capture device would otherwise become stale the instant the refresh completes. This
is what lets the `DeviceSelectionViewModel.Refresh()` clicked from the "Refresh devices" button
stop an actively listening session deterministically, rather than relying on the
`AudioDeviceInUseException` fallback. `DisposeAsync()` unregisters this hook.

**State derivation.** `RecognitionStreamingState` is never assigned ad hoc at each call site;
`MapSessionState` is the single, pure mapping from `RecognitionSessionState` (`Starting`,
`Running`, `Stopping` map to `Listening`; `Faulted` maps to `Error`; anything else maps to
`Idle`), applied every time `IRecognitionSession.StateChanged` raises. Because `StateChanged` and
the results pumped from `GetResultsAsync` are documented to raise/resume from the session's own
background thread, `StartAsync` captures the UI thread's `SynchronizationContext` before the
session starts, and `OnSessionStateChanged` posts every transition through it (falling back to
applying it synchronously when none was captured) before `ApplySessionStateChanged` touches
`State`; a session reporting `Faulted` additionally sets `StatusMessage` to
`SessionFaultedMessage`.

**Start algorithm.** `StartAsync()`:

1. Reports `NoModelSelectedMessage` and enters `Error` when no model is selected
2. Creates the capture device through `IAudioDeviceService` before paying for the comparatively
   expensive async engine load, so an honest "no device" outcome does not depend on what the
   engine-loading seam happens to return for this combination; reports `NoCaptureDeviceMessage`
   and enters `Error` when it is unavailable
3. When no engine is cached for the selected model: invalidates any stale engine, then loads one
   through `IRecognizerSessionFactory.LoadAsync`; disposes it and reports
   `RecognizerUnavailableMessage` and enters `Error` when it honestly reports itself unavailable
4. Otherwise reuses the cached engine unchanged, skipping the load entirely
5. Releases any previous (single-use) session via `InvalidateSessionAsync`, then creates a fresh
   session through `engine.CreateSessionAsync(captureDevice, ...)`; on
   `RecognitionEngineBusyException`, reports the exception's message and enters `Error`
6. Clears `Finals`, `Partial`, and `StatusMessage`; captures the current
   `SynchronizationContext`; subscribes `StateChanged`; and calls `session.StartAsync()`
7. On `SpeechRecognizerUnavailableException` from `StartAsync()`, unsubscribes, disposes the
   session, reports the exception's message, and enters `Error`
8. On success, starts a background pump task (`PumpResultsAsync`) over
   `session.GetResultsAsync`, stored for later awaiting/canceling on Stop, invalidation, or
   disposal

**Transcript sequencing.** `PumpResultsAsync` runs an `await foreach` over
`session.GetResultsAsync(cancellationToken)`, relying on each continuation naturally resuming on
the UI thread context captured by `StartAsync` rather than explicit marshaling, and applies each
result via `ApplyResult`: a final result is committed to `Finals` and `Partial` is cleared; a
provisional result replaces `Partial`. This is exactly the sequencing a live captioning display
depends on, so the trailing guess is replaced rather than accumulated as noise, and a finished
utterance becomes a stable committed line the instant the recognizer considers it final. The
pump's own `cancellationToken` ends only the enumeration (not the session) when the session is
released while still producing results; `OperationCanceledException` from that is swallowed as
expected invalidation, while `RecognitionSessionFaultedException`/
`SpeechRecognizerUnavailableException` are reported through `StatusMessage`.
`BuildTranscriptText()` renders every committed line followed by any in-progress partial.

**Stop algorithm.** `StopAsync()` (`AllowConcurrentExecutions = true`, so a concurrent second
call completes without racing to double-dispose) is a safe no-op when nothing is cached.
Otherwise it calls `session.StopAsync()`, awaits the pump task to fully drain, finalizes any
diagnostic capture recording, clears the captured UI context, and - if not already in `Error` -
reports `StoppedMessage`; the session itself remains cached (released only by the next
invalidation), so a subsequent Start reuses it only implicitly through the still-cached engine
(a fresh session is always created on the next Start, since a session is single-use).

**Model-switch guard.** `CanChangeModel` is `true` only when `State != RecognitionStreamingState.Listening`,
computed via `[NotifyPropertyChangedFor(nameof(CanChangeModel))]` on the generated `State`
partial property (the same pattern `CanStart`/`CanStop` already use), so it recomputes on every
state transition with no manual notification code. `RecognitionPanelView.axaml` binds the
model-selection `ComboBox`'s `IsEnabled` to it, so a user cannot switch the selected recognition
model through the view while a streaming session is active. This is a view-level convenience, not
the sole safeguard: `SelectedModel`'s setter remains public, so `OnSelectedModelChanged` (see
"Engine/session reuse" above) still stops an active session defensively before invalidating,
rather than assuming the view's guard makes a mid-session change unreachable.

#### Testability

`RecognitionPanelViewModel` depends on `IModelCatalogService`, `IAudioDeviceService`, the shared
`DeviceSelectionViewModel`, and `IRecognizerSessionFactory` — never on the library's recognition
concretes directly. This is what allows the whole Start/Stop lifecycle, the partial-then-final
transcript sequencing, state derivation from `IRecognitionSession.StateChanged`, every
unavailable-state path, and the auto-refresh-on-install behavior to be verified with no
downloaded model, no native runtime, and no real microphone. `RefreshCommand` is not bound to a
visible button in `RecognitionPanelView.axaml` - it exists solely for the auto-refresh-on-install
path above and is not a user-facing control, since a manually clickable refresh beside
Start/Stop/the model dropdown proved to be a redundant, confusingly placed control once
auto-refresh-on-install existed.
