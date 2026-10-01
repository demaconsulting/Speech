### SherpaOnnxSpeechSynthesizerEngine

**Purpose**: Implement the real `ISpeechSynthesizerEngine`, owning one loaded `SynthesisBackend`
and enforcing single-session exclusivity with a fail-fast lease.

**Data Model**: Holds the owned `ISynthesisBackend`, the `ISynthesisModel` supplying
`NormalizeText`/`CapabilityProfile`/`ResolveSpeakerId`, an optional `parameterValues` bag (the
engine's default selected voice/tunable-parameter values, forwarded unchanged from
`SpeechSynthesizerFactory.LoadAsync` to every session it creates), a diagnostics sink, a binary
`SemaphoreSlim(1, 1)` exclusivity lease, and a disposed flag. `IsAvailable` is always `true`,
because this type is only ever created after the backend loaded successfully - every unavailable
case is represented by `UnavailableSpeechSynthesizerEngine` instead.

**Key Methods**:

- **CreateSessionAsync(device, cancellationToken)**: Attempts to acquire the lease with
  `SemaphoreSlim.WaitAsync(0, cancellationToken)` (zero timeout - fails immediately rather than
  queueing). On success, constructs and returns a new `SherpaOnnxSynthesisSession` bound to
  `device`, with a release callback that releases the lease exactly once when that session is
  disposed. On failure (lease already held), throws `SynthesisEngineBusyException` immediately.
  Throws `ArgumentNullException` for a null `device`.
- **DisposeAsync()**: If a session is currently leased, disposes it first (which releases the
  lease as a side effect of that session's own `DisposeAsync`), then disposes the owned backend.
  Idempotent: a second call disposes the backend exactly once.

The fail-fast design (zero-timeout `WaitAsync` rather than an unbounded or timed wait) means a
caller's `CreateSessionAsync` latency never depends on how long an unrelated, already-leased
session takes to be disposed; a caller that genuinely needs concurrent sessions must load a
second engine instance instead.

**Error Handling**: `SynthesisEngineBusyException` when the lease is already held.
`ArgumentNullException` for a null `device`. Operational members throw
`ObjectDisposedException` after disposal.

**Dependencies**: `ISynthesisBackend`, `SherpaOnnxSynthesisSession`, `SynthesisEngineBusyException`,
`IAudioPlaybackDevice` from the AudioSubsystem, `ISynthesisModel` from the
ModelManagementSubsystem, `ISpeechDiagnostics` from the Diagnostics subsystem.

**Callers**: `SpeechSynthesizerFactory.LoadAsync(...)`.
