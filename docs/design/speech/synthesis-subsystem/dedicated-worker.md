### DedicatedWorker

**Purpose**: Run one native synthesis call on a dedicated, long-running background thread,
applying a cooperative-cancel-then-abandon policy so a non-cooperative native call cannot hang a
caller's awaited task indefinitely. It follows the same cancel-then-abandon policy as
`RecognitionSubsystem`'s internal worker, but is a separate, static implementation (`Run`/`Start`
returning a `DedicatedWorkerRun<T>`) rather than the recognition subsystem's instance class.

**Data Model**: A stateless static class. `Run(delegate, cancellationToken, diagnostics,
diagnosticsCategory, abandonTimeout = null)` and `Start(...)` (same parameters) take no instance
state; `abandonTimeout` defaults to `DefaultAbandonTimeout` (2 seconds) when omitted/`null`, and
is injectable for deterministic test coverage of the abandon path. `DedicatedWorkerRun<T>` is the
small result type returned by `Start`: an abandon-aware `Task` (the same task `Run` returns) plus
a `Completion` task that completes only when the dedicated thread's delegate has genuinely
returned, even if the abandon-aware task was already completed as canceled.

**Key Methods**:

- **Run(delegate, cancellationToken, diagnostics, diagnosticsCategory, abandonTimeout)**: Starts
  `delegate` on a `TaskCreationOptions.LongRunning` task. On normal completion, returns its
  result. On cancellation, awaits the task for up to `abandonTimeout`; if it completes
  cooperatively within that window, the cancellation propagates normally. If it does not, reports
  a `Warning` diagnostic, detaches the still-running task (attaching a continuation that observes,
  rather than propagates, whatever it eventually produces, so it can never become an
  unobserved-exception crash), and throws `OperationCanceledException` to its own caller anyway -
  the caller must not be held open indefinitely by a native call that refuses to stop.
- **Start(delegate, cancellationToken, diagnostics, diagnosticsCategory, abandonTimeout)**: The
  internal variant used by `SynthesisSession` when a caller must not reuse or dispose a resource
  that the delegate shares with the session (the backend, or the playback device's start call)
  until the delegate has really finished. It applies the same policy as `Run` but returns a
  `DedicatedWorkerRun<T>`, so the session can await `Completion` (the raw completion) in
  `StopAsync`/`DisposeAsync` before reporting the operation stopped or releasing the engine's
  exclusivity lease, even when the abandon-aware `Task` has already been completed as canceled.
  `Run` and `Start` share one private abandon-handling implementation.

**Error Handling**: A genuine (non-cancellation) fault from `delegate` propagates normally from
the awaited task. The abandon path above is the sole case where this method returns/throws before
`delegate` has actually finished running; the raw `Completion` task exposed by `Start` is the only
signal that the delegate has truly returned in that case. A null `delegate` throws
`ArgumentNullException`.

**Dependencies**: `ISpeechDiagnostics` from the Diagnostics subsystem.

**Callers**: `SynthesisSession.GenerateSegmentAsync(...)` for every native
`ISynthesisBackend.Generate(...)` call, `SynthesisSession`'s playback-device start (via `Start`),
and `SpeechSynthesizerFactory.LoadAsync(...)` for the blocking native model load.
