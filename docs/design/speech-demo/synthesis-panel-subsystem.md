## SpeechDemo SynthesisPanelSubsystem Design

![SynthesisPanelSubsystem Structure](SynthesisPanelSubsystemView.svg)

### Overview

The SynthesisPanelSubsystem provides the demo's text-to-speech panel. It contains the following
units, each documented in its own file:

- **SynthesizerSessionFactory** (`ISynthesizerSessionFactory` / `SynthesizerSessionFactory`): the
  demo-owned synthesizer-engine composition seam and its real implementation over the library's
  `SpeechModelStore` and `SpeechSynthesizerFactory` — see _SynthesizerSessionFactory Design_
- **SynthesisPanelViewModel**: the panel's presentation state — the installed synthesis models,
  the embedded `ModelSettingsViewModel`, the text input, the audio-tag hints, and the async
  Play/Stop lifecycle over a cached `ISpeechSynthesizerEngine` and `ISynthesisSession` reused
  across many Play calls — see _SynthesisPanelViewModel Design_

### Interfaces

The subsystem exposes the following to the shell composition root:

- `ISynthesizerSessionFactory` / `SynthesizerSessionFactory`: the engine composition seam and its
  implementation
- `SynthesisPanelViewModel`: the panel presentation state; it is `IAsyncDisposable`, so the root
  can release its subscriptions, cached session and cached engine at shutdown

It consumes:

- `IModelCatalogService` (ModelCatalogSubsystem): the installed-model list, `ModelInstalled`, the
  reference-voice packs and `ReferenceVoicesChanged`
- `IAudioDeviceService` and `DeviceSelectionViewModel` (DeviceSelectionSubsystem): the playback
  device and pre-refresh hook registration
- `ModelSettingsViewModel` (ModelSettingsSubsystem): the embedded parameter panel
- `SpeechModelStore`, `SpeechSynthesizerFactory`, `ISpeechModel` and `ISynthesisModel` (library):
  only through `SynthesizerSessionFactory`
- `AudioTagCatalog`, the Chatterbox Turbo model type and the `ReferenceVoiceLibrary` consent note
  (library): presentation in `SynthesisPanelViewModel`
- `ISpeechSynthesizerEngine`, `ISynthesisSession`, `SynthesisSessionState` and the synthesis
  exception types (library): the contracts it drives and handles

### Design

`SynthesisPanelViewModel` depends on the seam interface, the shared device/settings view
models, and the library's public `ISpeechModel` contract; it never composes or runs a
synthesizer itself. This is what allows the whole Play/Stop lifecycle, including state derivation
from `ISynthesisSession.StateChanged`, every unavailable-state path, the engine/session reuse
across Play calls, and the auto-refresh-on-install behavior, to be verified with no downloaded
model, no native runtime, and no real speakers. `SynthesizerSessionFactory` is the sole unit that
narrows a public model to `ISynthesisModel` and calls into the library's synthesizer-engine
composition.

**Observable responsibilities and the mechanisms behind them.** The subsystem requirements state
only what a user or host observes; this section records how each is achieved. All mechanisms
live inside `SynthesisPanelViewModel` unless noted:

- **Speaking the entered text** (`PlaybackThroughSeams`, `ModelSettings`): `PlayAsync` passes the
  text, unchanged, to an `ISynthesisSession` created on the selected playback device from an
  `ISpeechSynthesizerEngine` loaded through `ISynthesizerSessionFactory` for the selected model
  and the settings value bag. The embedded `ModelSettingsViewModel` is re-pointed at each newly
  selected model, so the editable parameters always describe the model that will speak.
- **Status** (`LifecycleThroughSeams`, `LifecycleStatusCleared`, `StopPlayback`): the displayed
  state is derived from `ISynthesisSession.StateChanged`; the status message is cleared when
  speaking starts and stays clear on a successful finish; Stop cancels the session and the
  in-flight command.
- **Voice preparation and reuse** (`EngineSessionReuse`, `ChangedVoiceRespected`,
  `DeviceChangeRespected`): the loaded engine and the created session are cached across Play
  calls. The next Play compares the selected model instance, the value bag (key/value) and the
  playback device with those the cache was built for, and reloads the engine or recreates only the
  session as needed. While the model, value bag and playback device are unchanged, repeated Play
  calls prepare the voice and open the playback device only once; a playback-device change
  recreates the session through the newly selected device without preparing the voice again.
- **Refresh** (`ExternalChangeHandling`, `ReferenceVoiceChangeHandling`,
  `ReferenceVoiceChoiceKept`): `ModelInstalled` (synthesis role only) and `ReferenceVoicesChanged`
  trigger a catalog re-read on the UI context. For a voice change the settings value bag is
  captured before the re-read and restored after it, so the chosen voice survives the catalog
  rebuild. A refresh never disturbs an in-flight Play.
- **Consent and attribution notice** (`VoiceNotice`, `VoiceAttribution`,
  `VoiceNoticeOtherModels`): `VoiceNotice` is built only when the selected model is the Chatterbox
  Turbo model: the consent note plus each installed pack's attribution; otherwise it is `null`.
- **Shared device refresh** (`DeviceRefreshCoordination`): the panel registers a pre-refresh hook
  with the shared `DeviceSelectionViewModel` that stops an in-flight Play and awaits its
  completion, so the playback device is closed before the device list is re-scanned.
- **Model-switch guard** (`ModelChangeGuard`): `CanChangeModel` is derived from the lifecycle
  state and disables model and parameter changes while synthesis or playback is in flight.
- **Shutdown** (`ShutdownReleasesPlayback`, `ShutdownStopsCatalogReaction`): `DisposeAsync`
  unsubscribes from the catalog events, unregisters the pre-refresh hook, stops and awaits an
  in-flight Play, then releases the cached session and engine; every step is safe to repeat.
- **Input validation** (`DependencyValidation`, `ModelValidation`): the view model rejects a missing
  dependency at construction, and the session factory rejects a missing store or model, each with an
  argument-null error.
- **Synthesizer faults** (`SetupFaultReporting`): faults while preparing the voice or opening
  playback are converted to an error state with the fault message; this includes the
  invalid-argument error the library raises for an invalid parameter value, which
  `SynthesizerSessionFactory` deliberately does not catch.

See each unit's own design document for its data model, algorithms, and error handling.
