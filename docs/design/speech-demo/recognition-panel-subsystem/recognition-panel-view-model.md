### RecognitionPanelViewModel

**Purpose**:

`RecognitionPanelViewModel` is the speech-to-text panel's presentation state. It lists the
installed recognition models, drives the async Start/Stop streaming lifecycle over an
`ISpeechRecognizerEngine` and an `IRecognitionSession`, derives its lifecycle state from the
session, and presents a progressive partial-then-final transcript. It depends on
`IModelCatalogService`, `IAudioDeviceService`, the shared `DeviceSelectionViewModel`, and
`IRecognizerSessionFactory` — never on the library's recognition concretes directly — which is what
allows the whole lifecycle to be verified with no downloaded model, no native runtime, and no real
microphone. `RefreshCommand` is not bound to a visible button in `RecognitionPanelView.axaml`; it
exists for the auto-refresh-on-install path, since installs refresh the panel automatically.

**Data Model**:

| Member | Type | Purpose |
| --- | --- | --- |
| `NoModelsMessage` etc. | `const string` | Honest-outcome text (no models/selection/device, unavailable, faulted) |
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
`DeviceSelectionViewModel`'s property-change notifications and pre-refresh hook (both registered in
the constructor), all idempotently, so a shell shutting down never leaks unmanaged inference
resources, a live capture device, or a stale event subscription.

**Key Methods**:

- **Model filtering and refresh**: `Refresh()` lists only descriptors whose role is `Recognition`
  and whose state is `Downloaded`, mirroring the synthesis panel's own filtering and refresh
  algorithm, because a model of the wrong role or one not yet downloaded cannot honestly
  transcribe audio. A still-installed selection is preserved; otherwise the first offered model is
  selected. When no installed recognition model exists, `Refresh()` sets `StatusMessage` to
  `NoModelsMessage` (an honest empty state, not an error: `State` is unchanged); when a model
  appears on a later refresh and that message is still showing, `Refresh()` clears it.
- **Auto-refresh on install**: the constructor subscribes to `IModelCatalogService.ModelInstalled`.
  The handler ignores any event whose `ModelInstalledEventArgs.Role` is not
  `SpeechModelRole.Recognition`, and otherwise calls `Refresh()` marshaled onto the UI thread
  through a `SynchronizationContext` captured at construction (falling back to calling `Refresh()`
  synchronously when none was captured). `DisposeAsync()` unsubscribes this handler.
- **Engine/session reuse**: per `ISpeechRecognizerEngine`'s own reuse guidance, an engine is
  loaded at most once per selected model and reused across many Start/Stop cycles. Because an
  `IRecognitionSession` is single-use, a fresh session is created from the cached engine for every
  Start and released (via `InvalidateSessionAsync`) before the next one is created. The cached
  engine is invalidated (via `InvalidateEngineAsync`, which first releases the session, then
  disposes and clears the engine) only when the selected model changes; the cached session alone
  is invalidated whenever the selected capture device changes or the shared device-selection panel
  forces a device refresh - a session, not an engine, is bound to a capture device.
- **Serialized lifecycle**: Start, Stop, capture-device change, model change, shared-refresh
  pre-hook and `DisposeAsync()` all run their body while holding a single lifecycle gate (a
  `SemaphoreSlim(1,1)`). The command attribute `AllowConcurrentExecutions` only lets command
  invocations overlap; it does not serialize ownership of the session, pump task, and recorder, so
  the gate is what guarantees two requests never stop, drain, or dispose the same session at the
  same time and that the cached engine/session/pump fields are only mutated by one request.
  The fire-and-forget handlers (`OnDeviceSelectionChanged`, `OnSelectedModelChanged`) acquire the
  same gate. Internal steps that are reached with the gate already held use gate-free variants.
- **Stop before invalidating**: `OnDeviceSelectionChanged` stops an active session
  (`CanStop` true) before invalidating the cached session, because the capture picker is never
  disabled while listening and disposing a session without stopping it first would leave `State`
  stuck at `Listening`. A change of `SelectedModel` while listening is not stopped-and-invalidated
  but refused (see _Model-switch guard_).
- **Stop before a shared device refresh**: the constructor registers a pre-refresh hook
  (`StopBeforeDeviceRefreshAsync`) with the shared `DeviceSelectionViewModel` via
  `RegisterPreRefreshHook`. The hook delegates to the same stop-then-invalidate-session path used
  for a capture-device change, so an actively listening session is stopped and released before the
  shared device table is re-scanned, rather than relying on the `AudioDeviceInUseException`
  fallback. `DisposeAsync()` unregisters this hook.
- **State derivation**: `RecognitionStreamingState` is never assigned ad hoc at each call site;
  `MapSessionState` is the single, pure mapping from `RecognitionSessionState` (`Starting`,
  `Running`, `Stopping` map to `Listening`; `Faulted` maps to `Error`; anything else maps to
  `Idle`), applied every time `IRecognitionSession.StateChanged` raises. Because `StateChanged`
  and the results pumped from `GetResultsAsync` are documented to raise/resume from the session's
  own background thread, `StartAsync` captures the UI thread's `SynchronizationContext` before the
  session starts, and `OnSessionStateChanged` posts every transition through it (falling back to
  applying it synchronously when none was captured) before `ApplySessionStateChanged` touches
  `State`; a session reporting `Faulted` additionally sets `StatusMessage` to
  `SessionFaultedMessage`.
- **Threading**: the library raises `IRecognitionSession.StateChanged` (and resumes the result
  stream) on the session's own background threads, never on the UI thread. The view model owns the
  marshaling: every transition and result is posted to the UI `SynchronizationContext` captured in
  `StartAsync()` before any bound property is changed, so the view only ever observes property
  changes on the UI thread.
- **Start algorithm** (`StartAsync()`):
  1. Reports `NoModelSelectedMessage` and enters `Error` when no model is selected.
  2. Creates the capture device through `IAudioDeviceService` before paying for the comparatively
     expensive async engine load, so an honest "no device" outcome does not depend on what the
     engine-loading seam happens to return; reports `NoCaptureDeviceMessage` and enters `Error`
     when it is unavailable.
  3. When no engine is cached for the selected model: invalidates any stale engine, then loads one
     through `IRecognizerSessionFactory.LoadAsync`; disposes it, reports
     `RecognizerUnavailableMessage` and enters `Error` when it honestly reports itself unavailable.
  4. Otherwise reuses the cached engine unchanged, skipping the load entirely.
  5. Releases any previous (single-use) session via `InvalidateSessionAsync`, then creates a fresh
     session through `engine.CreateSessionAsync(captureDevice, ...)`; on
     `RecognitionEngineBusyException`, reports the exception's message and enters `Error`.
  6. Clears `Finals`, `Partial`, and `StatusMessage`; captures the current
     `SynchronizationContext`; subscribes `StateChanged`; and calls `session.StartAsync()`.
  7. On `SpeechRecognizerUnavailableException` from `StartAsync()`, unsubscribes, disposes the
     session, reports the exception's message, and enters `Error`.
  8. On success, starts a background pump task (`PumpResultsAsync`) over `session.GetResultsAsync`,
     stored for later awaiting/canceling on Stop, invalidation, or disposal.
- **Transcript sequencing**: `PumpResultsAsync` runs an `await foreach` over
  `session.GetResultsAsync(cancellationToken)`, relying on each continuation naturally resuming on
  the UI thread context captured by `StartAsync`, and applies each result via `ApplyResult`: a
  final result is committed to `Finals` and `Partial` is cleared; a provisional result replaces
  `Partial`. The trailing guess is thus replaced rather than accumulated as noise, and a finished
  utterance becomes a stable committed line the instant the recognizer considers it final.
  `BuildTranscriptText()` renders every committed line followed by any in-progress partial.
- **Stop algorithm**: `StopAsync()` (`AllowConcurrentExecutions = true`, so a concurrent second
  call completes, and the lifecycle gate ensures the two never double-dispose) is a safe no-op when nothing is cached.
  Otherwise it calls `session.StopAsync()`, awaits the pump task to fully drain, finalizes any
  diagnostic capture recording, clears the captured UI context, and - if not already in `Error` -
  reports `StoppedMessage`. The stopped session stays cached only until the next invalidation: a
  session is single-use, so the next Start always releases it and creates a fresh session from the
  still-cached engine.
- **Model-switch guard**: `CanChangeModel` is `true` only when
  `State != RecognitionStreamingState.Listening`, computed via
  `[NotifyPropertyChangedFor(nameof(CanChangeModel))]` on the generated `State` partial property,
  so it recomputes on every state transition. `RecognitionPanelView.axaml` binds the
  model-selection `ComboBox`'s `IsEnabled` to it, but the guard is also enforced at the unit
  boundary: while `State` is `Listening`, a write to `SelectedModel` is ignored (the previous
  selection is restored, and the running session and cached engine are left untouched). The model
  can be changed again after Stop or in `Error`.

**Error Handling**:

- A null constructor dependency throws `ArgumentNullException`.
- An empty catalog is reported through `StatusMessage` (`NoModelsMessage`) without entering `Error`;
  the message is cleared once a model becomes available.
- A `SelectedModel` write while listening is ignored rather than throwing.
- No selected model, no capture device, an unavailable engine, a busy engine, and a session whose
  `StartAsync()` throws `SpeechRecognizerUnavailableException` are each reported through
  `StatusMessage` with `State` set to `Error`, never thrown to the caller; any engine or session
  created for the failed attempt is disposed.
- The result pump swallows `OperationCanceledException` as expected invalidation, while
  `RecognitionSessionFaultedException` and `SpeechRecognizerUnavailableException` are reported
  through `StatusMessage`. A session that reports `Faulted` through `StateChanged` sets `Error`
  and `SessionFaultedMessage`.
- `StopAsync()` and `DisposeAsync()` are safe no-ops with nothing active, and neither throws;
  overlapping calls are serialized by the lifecycle gate.

**Dependencies**:

- `IModelCatalogService`, `IAudioDeviceService`, the shared `DeviceSelectionViewModel`, and
  `IRecognizerSessionFactory`
- The library's `ISpeechRecognizerEngine`, `IRecognitionSession` and recognition state/exception
  types
- `CaptureDebugRecorder` for opt-in diagnostic recording (see the subsystem design)

**Callers**:

- `RecognitionPanelView.axaml`, which binds to the view model
- `App.Compose`, which constructs it, and `MainWindowViewModel`, which hosts it
