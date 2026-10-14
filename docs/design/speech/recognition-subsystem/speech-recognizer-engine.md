### SpeechRecognizerEngine

**Purpose**: Implement the `ISpeechRecognizerEngine` contract against a loaded
`IRecognitionBackend`, enforcing the subsystem's single-session-at-a-time exclusivity (see the
subsystem-level design doc's "Engine exclusivity and lease behavior").

**Data Model**: Holds the loaded `IRecognitionBackend`, the owning `IRecognitionModel`, a
`SemaphoreSlim(1, 1)` lease, and a diagnostics sink; constructs a new `DedicatedWorker` per
session it creates. `IsAvailable` is always `true`, because this type is only ever created after the
backend loaded successfully - every unavailable case is represented by
`UnavailableSpeechRecognizerEngine` instead.

**Key Methods**:

- **CreateSessionAsync(IAudioCaptureDevice, CancellationToken)**: When the supplied device
  reports `IsAvailable` as `false` (no microphone or no working audio backend - an ordinary
  machine state, exactly like a model not being installed), reports a Warning diagnostic and
  returns `UnavailableRecognitionSession.Instance` without attempting the backend lease. Otherwise
  attempts `_lease.Wait(0, ...)` -
  a zero-timeout, fail-fast acquire with no queueing. On success, constructs and returns a new
  `RecognitionSession` wrapping the shared backend, device, model, resampler
  configuration, diagnostics sink, pump worker, and a release-lease callback that calls
  `_lease.Release()`. On failure (the lease is already held), throws
  `RecognitionEngineBusyException` without blocking.
- **DisposeAsync()**: First disposes any still-active session it created (reporting an Error diagnostic if that
  disposal fails, without preventing the rest of teardown), then disposes the owned backend and the
  lease semaphore.

**Error Handling**: `RecognitionEngineBusyException` is the only condition this type represents as
an exception; every other call either succeeds or is routed through the created session's own
error handling.

**Dependencies**: `IRecognitionBackend`, `RecognitionSession`, `DedicatedWorker`,
`RecognitionEngineBusyException`; implements `ISpeechRecognizerEngine`.

**Callers**: `SpeechRecognizerFactory.LoadAsync(...)` constructs it; hosts call
`CreateSessionAsync` once per capture device.

The exception this unit throws when the lease is held, `RecognitionEngineBusyException`, is
documented in its own design document (_RecognitionEngineBusyException_).
