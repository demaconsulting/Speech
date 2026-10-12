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

The subsystem exposes `ISynthesizerSessionFactory`, `SynthesizerSessionFactory`, and
`SynthesisPanelViewModel` to the shell composition root. It consumes `IModelCatalogService` from
the ModelCatalogSubsystem (for the installed-model list, auto-refresh-on-install through
`ModelInstalled`, and the reference-voice packs and `ReferenceVoicesChanged` event),
`IAudioDeviceService` and `DeviceSelectionViewModel` from the DeviceSelectionSubsystem (including
its pre-refresh hook registration), and `ModelSettingsViewModel` from the ModelSettingsSubsystem
for the embedded parameter panel. It consumes the library's `SpeechModelStore`,
`SpeechSynthesizerFactory`, `ISpeechModel`, and `ISynthesisModel` only through
`SynthesizerSessionFactory`; `SynthesisPanelViewModel` itself touches only presentation-level
library types (`AudioTagCatalog`, and the Chatterbox Turbo model type and `ReferenceVoiceLibrary`
consent note for the voice notice). `SynthesisPanelViewModel` implements `IAsyncDisposable` so
the composition root can release its subscriptions, cached session and cached engine at shutdown.

### Design

`SynthesisPanelViewModel` depends on the seam interface, the shared device/settings view
models, and the library's public `ISpeechModel` contract; it never composes or runs a
synthesizer itself. This is what allows the whole Play/Stop lifecycle, including state derivation
from `ISynthesisSession.StateChanged`, every unavailable-state path, the engine/session reuse
across Play calls, and the auto-refresh-on-install behavior, to be verified with no downloaded
model, no native runtime, and no real speakers. `SynthesizerSessionFactory` is the sole unit that
narrows a public model to `ISynthesisModel` and calls into the library's synthesizer-engine
composition.

The panel also handles four concerns inside `SynthesisPanelViewModel`:

- **Voice refresh**: when `ReferenceVoicesChanged` is raised (a voice pack was installed or
  removed, or the mirror changed), the panel captures the settings value bag, re-reads the
  catalog, and restores the bag so the chosen voice survives the catalog rebuild
- **Consent and attribution notice**: when the selected model is the Chatterbox Turbo model, the
  panel exposes `VoiceNotice` with the consent note and each installed pack's attribution
- **Pre-refresh hook**: the panel registers with the shared `DeviceSelectionViewModel` so an
  in-flight Play is stopped, and the playback device closed, before a device refresh
- **Model-switch guard**: `CanChangeModel` disables model and parameter changes while synthesis
  or playback is in flight

See each unit's own design document for its data model, algorithms, and error handling.
