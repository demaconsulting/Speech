## Diagnostics Subsystem Design

![Diagnostics Structure](DiagnosticsView.svg)

### Overview

The Diagnostics subsystem gives a host application a single, stable way to observe structural
events raised by the library (for example, "a device was selected" or "a fallback was used")
without ever exposing raw user audio samples or dictated/synthesized text. Its boundary is the
`ISpeechDiagnostics` interface: every other subsystem accepts an `ISpeechDiagnostics` instance
(directly or via a factory) and reports through it, but never depends on a concrete
implementation. It contains two units:

- **ISpeechDiagnostics**: the reporting contract implemented by hosts or by the library's default
- **NullSpeechDiagnostics**: the library's default no-op implementation used when a host does not
  supply its own sink

### Interfaces

The subsystem exposes `ISpeechDiagnostics` and `SpeechDiagnosticLevel` (declared alongside it) to
the rest of the library and to hosts. It consumes nothing from other subsystems; it is a
leaf dependency that every other subsystem (AudioSubsystem, ModelManagementSubsystem,
RecognitionSubsystem, and SynthesisSubsystem) depends on.

### Design

`NullSpeechDiagnostics` is the only implementation the library ships. It is
exposed as a process-wide singleton (`NullSpeechDiagnostics.Instance`) so that any factory
needing a default `ISpeechDiagnostics` can obtain one without allocating and without ever
failing to do so, per this library's "nothing throws at composition" decision. There is no
collaboration between the two units beyond `NullSpeechDiagnostics` implementing
`ISpeechDiagnostics`. Each unit is documented in its own file under `diagnostics/`:
_ISpeechDiagnostics_ and _NullSpeechDiagnostics_.
