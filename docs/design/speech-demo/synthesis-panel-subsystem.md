## SpeechDemo SynthesisPanelSubsystem Design

![SynthesisPanelSubsystem Structure](SynthesisPanelSubsystemView.svg)

### Overview

The SynthesisPanelSubsystem provides the demo's text-to-speech panel. It contains the following
units, each documented in its own file:

- **SynthesizerSessionFactory** (`ISynthesizerSessionFactory` / `SynthesizerSessionFactory`): the
  demo-owned synthesizer composition seam and its real implementation over the library's
  `SpeechModelStore` and `SpeechSynthesizerFactory` — see _SynthesizerSessionFactory Design_
- **SynthesisPanelViewModel**: the panel's presentation state — the installed synthesis models,
  the embedded `ModelSettingsViewModel`, the text input, the audio-tag hints, and the Play/Stop
  lifecycle — see _SynthesisPanelViewModel Design_

### Interfaces

The subsystem exposes `ISynthesizerSessionFactory`, `SynthesizerSessionFactory`, and
`SynthesisPanelViewModel` to the shell composition root. It consumes `IModelCatalogService` from
the ModelCatalogSubsystem (for the installed-model list and auto-refresh-on-install),
`IAudioDeviceService` and `DeviceSelectionViewModel` from the DeviceSelectionSubsystem, and
`ModelSettingsViewModel` from the ModelSettingsSubsystem for the embedded parameter panel. It
consumes the library's `SpeechModelStore`, `SpeechSynthesizerFactory`, `ISpeechModel`, and
`ISynthesisModel` only through `SynthesizerSessionFactory`, never directly from
`SynthesisPanelViewModel`.

### Design

`SynthesisPanelViewModel` depends only on the seam interface, the shared device/settings view
models, and the library's public `ISpeechModel` contract — never on the library's synthesis
concretes directly. This is what allows the whole Play/Stop lifecycle, including every
unavailable-state path and the auto-refresh-on-install behavior, to be verified with no
downloaded model, no native runtime, and no real speakers. `SynthesizerSessionFactory` is the
sole unit that narrows a public model to `ISynthesisModel` and calls into the library's
synthesizer composition. See each unit's own design document for its data model, algorithms, and
error handling.
