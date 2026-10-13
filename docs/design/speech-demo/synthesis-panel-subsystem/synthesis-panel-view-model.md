### SynthesisPanelViewModel

**Purpose**: Present the text-to-speech panel's installed synthesis models, the embedded
`ModelSettingsViewModel`, the text input, the audio-tag hints, and the async Play/Stop lifecycle,
caching and reusing an `ISpeechSynthesizerEngine` and an `ISynthesisSession` across many Play
calls instead of recomposing them on every call.

**Data Model**:

| Member | Type | Purpose |
| --- | --- | --- |
| `NoModelsMessage` and the other `*Message` constants | `const string` | One explanation per honest outcome |
| `ExampleTagHints` | `static IReadOnlyList<string>` | Example tags drawn from `AudioTagCatalog` |
| `AvailableModels` | `ObservableCollection<ISpeechModel>` | The installed synthesis models |
| `SelectedModel` | `ISpeechModel?` | The chosen model |
| `Text` | `string` | The text to synthesize, which may contain inline Natural Language Audio Tags |
| `State` | `SynthesisPlaybackState` | The current lifecycle state: `Idle`, `Synthesizing`, `Playing`, `Error` |
| `StatusMessage` | `string?` | The current or most recently failed attempt's explanation |
| `HasStatusMessage` | `bool` | `true` when `StatusMessage` has content to show |
| `VoiceNotice` | `string?` | Consent note and pack attributions for the Chatterbox Turbo model, else `null` |
| `HasVoiceNotice` | `bool` | `true` when `VoiceNotice` is not `null` |
| `Settings` | `ModelSettingsViewModel` | The embedded settings panel for the selected model |
| `HasModels` / `CanPlay` | `bool` | Derived enablement values |
| `CanChangeModel` | `bool` | `true` only when `State` is `Idle` or `Error` |
| `RefreshCommand` | generated command | Re-reads the catalog |
| `PlayCommand` | generated async command | Synthesizes and speaks `Text` |
| `StopCommand` | generated async command | Stops an in-flight Play |
| `DisposeAsync()` | `ValueTask` | Releases subscriptions, the pre-refresh hook, the session and the engine |

**Key Methods**:

- **Refresh()**: Lists only descriptors whose role is `Synthesis` and whose state is
  `Downloaded`, because a model of the wrong role or one not yet downloaded cannot honestly
  speak text. Preserves the current selection by id when it is still offered, matching the
  model catalog panel's own refresh algorithm.
- **PlayAsync(cancellationToken)**:
  1. Reports `NoModelSelectedMessage` and enters `Error` when no model is selected
  2. Builds the current parameter value bag via `Settings.BuildValueBag()` and creates the
     playback device through `IAudioDeviceService` before paying for the comparatively expensive
     async engine load; reports `NoPlaybackDeviceMessage` and enters `Error` when it is
     unavailable
  3. When no engine is cached, or the selected model or built parameter values differ from the
     ones the cached engine was loaded with: invalidates the stale engine (and its session), then
     loads a fresh one through `ISynthesizerSessionFactory.LoadAsync`; disposes it and reports
     `SynthesizerUnavailableMessage` and enters `Error` when it honestly reports itself
     unavailable
  4. Otherwise reuses the cached engine unchanged, skipping the load entirely
  5. When no session is cached, or the selected playback device differs from the one the cached
     session was created for: releases the stale session, then creates a fresh one through
     `engine.CreateSessionAsync`; on `SynthesisEngineBusyException` or an unavailable session,
     reports the appropriate message and enters `Error`; otherwise subscribes `StateChanged`
  6. Otherwise reuses the cached session unchanged — the central reuse path that avoids
     recreating the session on every Play
  7. Clears the status message and awaits `session.SpeakAsync(Text, cancellationToken)`
  8. On success, `StateChanged` has already driven `State` back to `Idle`; on
     `OperationCanceledException` (from `StopCommand`), reports `StoppedMessage`; on
     `SpeechSynthesizerUnavailableException`, enters `Error` with the exception's message; on
     `SynthesisSessionFaultedException` (terminal for a session), releases the session so the
     next Play creates a fresh one against the still-cached engine, and reports the exception's
     message
- **StopAsync()**: Calls `StopAsync()` on the active session, if any, and cancels `PlayCommand`'s
  token as defense-in-depth in case the session itself cannot be stopped. A safe no-op when
  nothing is playing.
- **DisposeAsync()**: Unsubscribes from `IModelCatalogService.ModelInstalled` and the shared
  `DeviceSelectionViewModel`'s pre-refresh hook; stops and awaits an in-flight Play, if any; then
  releases the cached session and disposes the cached engine. All actions are safe to repeat, so
  `DisposeAsync()` is idempotent.

**Auto-refresh on install.** The constructor subscribes to
`IModelCatalogService.ModelInstalled`. The handler ignores any event whose
`ModelInstalledEventArgs.Role` is not `SpeechModelRole.Synthesis`, and otherwise calls `Refresh()`
marshaled onto the UI thread through a `SynchronizationContext` captured at construction (falling
back to calling `Refresh()` synchronously when none was captured), mirroring
`RecognitionPanelViewModel`'s identical pattern - since the event's raising thread is not
otherwise guaranteed.

**Engine/session reuse.** Per `ISpeechSynthesizerEngine`'s and
`ISynthesisSession`'s own reuse guidance, this ViewModel loads an engine at most once per selected
model/parameter-value combination, and creates a session at most once per engine/playback-device
combination, reusing both across many Play calls instead of reloading the model and recreating
the session on every click, because loading a model is the expensive step. PlayAsync detects a reason
to reload lazily, on its next call, rather than
proactively on a property change: the cached engine is reloaded only when the selected model or
`Settings.BuildValueBag()`'s built parameter values (compared by key/value, since a freshly built
dictionary is never the same instance twice) differ from the ones it was last loaded with; the
cached session is independently recreated whenever the engine was just reloaded or the selected
playback device differs from the one it was last bound to. `ISynthesisSession.StateChanged`
drives `State` (via `MapSessionState`, below) rather than explicit `State = ...` assignments
around the `await SpeakAsync` call.

**Reference voice refresh and notice.** The constructor also subscribes to
`IModelCatalogService.ReferenceVoicesChanged` (unsubscribed in `DisposeAsync`). The handler posts to
the UI context and re-reads the catalog while keeping the user's settings: it captures
`Settings.BuildValueBag()`, calls `Refresh()`, then `Settings.RestoreValues(...)`, so a rebuilt
Chatterbox Turbo model offers the new voice list without resetting the chosen voice. `VoiceNotice`
(with `HasVoiceNotice`) is non-null only when the selected model is the Chatterbox Turbo model; it
carries the voice-cloning consent note and, for each installed pack, "Voices from {name}:
{attribution} ({license})."; the view shows it under the voice dropdown.

**Stops deterministically before a shared device refresh.** The constructor also registers a
pre-refresh hook with the shared `DeviceSelectionViewModel` via `RegisterPreRefreshHook`,
mirroring the `ModelInstalled` subscription precedent above. Unlike
`RecognitionPanelViewModel`'s equivalent hook, calling `StopAsync()` alone here does not guarantee
the playback device is actually closed by the time the hook returns: the session's own
`StopAsync()` only requests cancellation of the in-flight `SpeakAsync` - the real playback
device's own stop happens as part of the already-in-flight `PlayAsync` task settling. So the hook
first checks `PlayCommand.IsRunning`; when `true`, it calls `StopAsync()` and then awaits
`PlayCommand.ExecutionTask` (from `IAsyncRelayCommand`), swallowing the expected
`OperationCanceledException` that cancellation causes, before releasing the cached session (never
the cached engine). This is what lets the `DeviceSelectionViewModel.Refresh()` clicked from the
"Refresh devices" button stop an in-flight Play and wait for the playback device to genuinely
close before the shared device table is re-scanned, rather than relying on the
`AudioDeviceInUseException` fallback. `DisposeAsync()` unregisters this hook.

**State derivation.** `SynthesisPlaybackState` is never assigned ad hoc at each call site;
`MapSessionState` is the single, pure mapping from `SynthesisSessionState` (`Starting` maps to
`Synthesizing`; `Running`/`Stopping` map to `Playing`; `Faulted` maps to `Error`; anything else
maps to `Idle`), applied every time `ISynthesisSession.StateChanged` raises, marshaled onto the
UI thread captured when the session was created (falling back to applying it synchronously when
none was captured); a session reporting `Faulted` additionally sets `StatusMessage` to
`SessionFaultedMessage`.

**Embedded settings.** Assigning `SelectedModel` also assigns the embedded `Settings.Model`, so
the settings panel always presents the currently selected voice's declared parameters, reusing
the ModelSettingsSubsystem instead of duplicating its rendering logic. It does not proactively
invalidate the cached engine/session: `PlayAsync` detects the model change lazily on its next
call (see "Engine/session reuse" above).

**Audio tag hints.** `ExampleTagHints` projects each entry in the library's `AudioTagCatalog.Tags`
to its first alias, bracketed (for example, `[whispers]`). Drawing the hints from the library's
own closed, published tag vocabulary - rather than a hand-maintained demo list - keeps the hints
from drifting out of sync with what the library actually recognizes.

**Model-switch guard.** `CanChangeModel` is `true` only when `State` is `Idle` or `Error` -
mirroring `CanPlay`'s own condition - computed via
`[NotifyPropertyChangedFor(nameof(CanChangeModel))]` on the generated `State` partial property,
so it recomputes on every state transition with no manual notification code.
`SynthesisPanelView.axaml` binds the model-selection `ComboBox`'s `IsEnabled` to it, and wraps
the embedded `ModelSettingsView` (the rendered voice/speaker parameter controls) in a `Border`
whose `IsEnabled` is also bound to it - a `Border` rather than binding `IsEnabled` directly on
`ModelSettingsView`, because that element's own `DataContext` is separately bound to `Settings`,
and a same-element `DataContext` override would otherwise resolve `CanChangeModel` against
`Settings` (the wrong object) instead of the parent `SynthesisPanelViewModel`; Avalonia's
`IsEnabled` cascades through the visual tree regardless of each descendant's own `DataContext`,
so the wrapping `Border` correctly disables the settings view. This prevents a user from
switching the selected model or its voice/speaker parameters while synthesis is in flight or
audio is playing.

**Threading**: Not thread-safe; the panel is used from the UI thread. Catalog events and session
state changes that may be raised on another thread are marshaled onto the UI context captured at
construction (or session creation) before they touch presentation state.

**Error Handling**: The expected failure modes resolve to `State`/`StatusMessage` rather than
escaping: no model selected, no playback device, an unavailable engine or session, a busy engine
reported by `SynthesisEngineBusyException` while creating the session, a
`SpeechSynthesizerUnavailableException` or `SynthesisSessionFaultedException` raised while
speaking, and a session reporting `Faulted` through `StateChanged` (which sets `StatusMessage` to
`SessionFaultedMessage`). Cancellation of `SpeakAsync` is distinguished from a genuine failure via
`OperationCanceledException`, and a terminal session fault releases the session (keeping the
engine) rather than leaving a known-dead session cached for the next Play.

_Known limitations_ (the current behavior; not yet handled by the panel):

- Any other exception from a seam call is not caught and surfaces through the asynchronous
  command rather than through `State`/`StatusMessage`. This includes the `ArgumentException` the
  library's engine composition throws from `LoadAsync` for an invalid recognized parameter value,
  any exception other than `SynthesisEngineBusyException` from `CreateSessionAsync`, and any
  exception other than the two named above from `SpeakAsync`. `State` is left at whatever the
  last transition set.
- `OperationCanceledException` is caught only around `SpeakAsync`. A cancellation that surfaces
  from `LoadAsync` or `CreateSessionAsync` is not caught, leaves `StatusMessage` unchanged and
  does not report `StoppedMessage`.
- Stop requested while the engine is still loading (before any session exists) has no session
  to stop; it only cancels the command's token. The load observes that token only if the seam
  honors it, and a Stop that arrives before `SpeakAsync` starts takes effect when `SpeakAsync`
  observes the already-canceled token.
- `SynthesisSessionFaultedException` and `SpeechSynthesizerUnavailableException` differ in
  handling: a fault releases the session, while an unavailable-synthesizer exception sets
  `Error` but keeps the session cached.
- A catalog refresh (`Refresh()`, including the one a voice-pack change or install triggers) is
  not deferred while a Play is in flight: it rebuilds `AvailableModels` and re-applies the selection
  and, for a voice change, the settings values, but leaves the running session and cached engine
  alone. The in-flight Play keeps using the model and values it started with; a change to the
  selection or values is picked up by the next Play through the cache comparison. It is verified by
  `SynthesisPanelViewModel_Refresh_WhilePlaying_DoesNotDisturbPlayAndSettlesIdle`.

**Dependencies**: `IModelCatalogService` (installed-model list, `ModelInstalled`,
`ReferenceVoicesChanged`, reference voice packs and their state), `IAudioDeviceService`, the
shared `DeviceSelectionViewModel`, `ISynthesizerSessionFactory`, and the embedded
`ModelSettingsViewModel`. The library synthesis and model types are reached directly for two
reasons. For presentation: `AudioTagCatalog` (the tag hints), and
`OnnxChatterboxTurboEnglishSynthesisModel` with `ReferenceVoiceLibrary.ConsentNotice` (to decide
whether to show `VoiceNotice`, where the installed packs and their state come from
`IModelCatalogService`). For the lifecycle contracts: `ISpeechSynthesizerEngine`,
`ISynthesisSession`, `SynthesisSessionState`, and the `SynthesisEngineBusyException`,
`SynthesisSessionFaultedException` and `SpeechSynthesizerUnavailableException` types it handles.
The panel therefore does reference library concretes for presentation, but nothing that composes
a synthesizer: that goes through `ISynthesizerSessionFactory`. This is what allows the whole
Play/Stop lifecycle, including state derivation from `ISynthesisSession.StateChanged`, every
unavailable-state path, the engine/session reuse behavior, and the auto-refresh-on-install
behavior, to be verified with no downloaded model, no native runtime, and no real speakers.

**Callers**: `SynthesisPanelView.axaml` (binds Play/Stop and the model/settings controls to this
presentation state). `RefreshCommand` is not bound to a visible button - it exists solely for the
auto-refresh-on-install path above, because installs refresh the panel automatically and a
manual refresh beside Play/Stop/the model dropdown would be redundant.
