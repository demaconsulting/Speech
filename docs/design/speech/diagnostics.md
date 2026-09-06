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
leaf dependency that other subsystems (such as AudioSubsystem) depend on.

### Design

`NullSpeechDiagnostics` is the only implementation the library ships in this phase. It is
exposed as a process-wide singleton (`NullSpeechDiagnostics.Instance`) so that any factory
needing a default `ISpeechDiagnostics` can obtain one without allocating and without ever
failing to do so, per architecture.md's "nothing throws at composition" decision. There is no
collaboration between the two units beyond `NullSpeechDiagnostics` implementing
`ISpeechDiagnostics`; see below for each unit's own design.

#### ISpeechDiagnostics

**Purpose**: Define the single contract through which the library reports structural diagnostic
events to a host, independent of which component raised the event.

**Data Model**: No fields or properties; a pure behavioral contract. The
`SpeechDiagnosticLevel` enum (`Info`, `Warning`, `Error`) declared alongside it classifies event
severity.

**Key Methods**:

- **Report(SpeechDiagnosticLevel level, string category, string message)**: Reports a single
  structural diagnostic event. Preconditions: `category` and `message` are non-null (enforced by
  each implementation, not by the interface itself). Postconditions: the event has been
  delivered to whatever the implementation does with it (logged, discarded, forwarded); the
  method itself never throws for a well-formed call, by convention for all implementations
  shipped by this library.

**Error Handling**: The interface itself defines no error handling; each implementation decides
how to treat invalid input. `NullSpeechDiagnostics`, the library's own implementation, never
throws.

**Dependencies**: None.

**Callers**: `AudioDeviceFactory` (AudioSubsystem) and any future subsystem that needs to report
structural events; hosts that implement their own sink.

#### NullSpeechDiagnostics

**Purpose**: Provide a default, always-available `ISpeechDiagnostics` implementation that
discards every event without throwing, so factories always have a working diagnostics sink even
when a host supplies none.

**Data Model**: No instance fields. Exposes a single static `Instance` property returning the
shared singleton; the constructor is private.

**Key Methods**:

- **Report(SpeechDiagnosticLevel level, string category, string message)**: Discards the event
  and returns immediately. Preconditions: none (accepts any input, including default enum
  values). Postconditions: no observable state change; never throws.

**Error Handling**: None required — the method has no failure modes.

**Dependencies**: `ISpeechDiagnostics` (implements it).

**Callers**: `AudioDeviceFactory` (AudioSubsystem) when no diagnostics sink is supplied; any
other factory that needs a safe default in a later phase.
