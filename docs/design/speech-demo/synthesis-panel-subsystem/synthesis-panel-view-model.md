### SynthesisPanelViewModel

**Purpose**: Present the text-to-speech panel's installed synthesis models, the embedded
`ModelSettingsViewModel`, the text input, the audio-tag hints, and the Play/Stop lifecycle,
composing a synthesizer for each Play through `ISynthesizerSessionFactory`.

**Data Model**:

| Member | Type | Purpose |
| --- | --- | --- |
| `NoModelsMessage` etc. | `const string` | Explanation per honest outcome (no models, no selection, no device, error) |
| `ExampleTagHints` | `static IReadOnlyList<string>` | Example tags drawn from `AudioTagCatalog` |
| `AvailableModels` | `ObservableCollection<ISpeechModel>` | The installed synthesis models |
| `SelectedModel` | `ISpeechModel?` | The chosen model |
| `Text` | `string` | The text to synthesize, which may contain inline Natural Language Audio Tags |
| `State` | `SynthesisPlaybackState` | The current lifecycle state: `Idle`, `Synthesizing`, `Playing`, `Error` |
| `StatusMessage` | `string?` | The current or most recently failed attempt's explanation |
| `Settings` | `ModelSettingsViewModel` | The embedded settings panel for the selected model |
| `HasModels` / `CanPlay` | `bool` | Derived enablement values |
| `CanChangeModel` | `bool` | `true` only when `State` is `Idle` or `Error` |
| `RefreshCommand` | generated command | Re-reads the catalog |
| `PlayCommand` | generated async command | Synthesizes and speaks `Text` |
| `StopCommand` | generated command | Stops an in-flight Play |

**Key Methods**:

- **Refresh()**: Lists only descriptors whose role is `Synthesis` and whose state is
  `Downloaded`, because a model of the wrong role or one not yet downloaded cannot honestly
  speak text. Preserves the current selection by id when it is still offered, matching the
  model catalog panel's own refresh algorithm.
- **PlayAsync(cancellationToken)**:
  1. Reports `NoModelSelectedMessage` and enters `Error` when no model is selected
  2. Creates the playback device through `IAudioDeviceService`; reports
     `NoPlaybackDeviceMessage` and enters `Error` when it is unavailable
  3. Composes a synthesizer through `ISynthesizerSessionFactory`, forwarding
     `Settings.BuildValueBag()` (the embedded settings panel's currently selected parameter
     values, for example a selected voice) as the `parameterValues` argument; disposes it and
     reports `SynthesizerUnavailableMessage` and enters `Error` when it honestly reports itself
     unavailable
  4. Otherwise clears the status message, transitions through `Synthesizing` then `Playing`
     (both reported before awaiting `SpeakAsync`, since the current library API has no separate
     synthesizing-vs-playing callback granularity), and awaits `SpeakAsync`
  5. On success, settles on `Idle`; on `OperationCanceledException` (from `StopCommand`), settles
     on `Idle` with `StoppedMessage`; on `SpeechSynthesizerUnavailableException`, enters `Error`
     with the exception's message
  6. Always disposes the synthesizer in a `finally` block, whichever path was taken
- **Stop()**: Calls `Stop()` on the active synthesizer, if any, and cancels `PlayCommand`'s
  token, which is what drives `PlayAsync`'s `OperationCanceledException` path. A safe no-op when
  nothing is playing.
- **Dispose()**: Unsubscribes from `IModelCatalogService.ModelInstalled` and, defensively,
  disposes an active synthesizer if one is still held — ordinarily released by `PlayAsync`'s own
  `finally` block, but no longer guaranteed once this ViewModel can be disposed independently of
  any in-flight Play. Both actions are safe to repeat, so `Dispose()` is idempotent.

**Auto-refresh on install.** The constructor subscribes to
`IModelCatalogService.ModelInstalled`. The handler ignores any event whose
`ModelInstalledEventArgs.Role` is not `SpeechModelRole.Synthesis`, and otherwise calls `Refresh()`
marshaled onto the UI thread through a `SynchronizationContext` captured at construction (falling
back to calling `Refresh()` synchronously when none was captured), mirroring
`RecognitionPanelViewModel`'s identical pattern - since the event's raising thread is not
otherwise guaranteed.

**Embedded settings.** Assigning `SelectedModel` also assigns the embedded `Settings.Model`, so
the settings panel always presents the currently selected voice's declared parameters, reusing
the ModelSettingsSubsystem instead of duplicating its rendering logic.

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

**Error Handling**: Never lets a seam fault, a device fault, or an unavailable-synthesizer state
escape as an unhandled exception; every path resolves to `State`/`StatusMessage`. Cancellation is
distinguished from a genuine failure via `OperationCanceledException`.

**Dependencies**: `IModelCatalogService`, `IAudioDeviceService`, the shared
`DeviceSelectionViewModel`, `ISynthesizerSessionFactory`, and the embedded
`ModelSettingsViewModel` — never the library's synthesis concretes directly. This is what allows
the whole Play/Stop lifecycle, including every unavailable-state path and the
auto-refresh-on-install behavior, to be verified with no downloaded model, no native runtime, and
no real speakers.

**Callers**: `SynthesisPanelView.axaml` (binds "Refresh"/Play/Stop and the model/settings
controls to this presentation state).
