### DedicatedWorker

**Purpose**: Run one blocking delegate (a model load, or a session's pump loop) on its own
dedicated, long-running thread rather than a pooled thread, and bound how long a caller ever
waits for it to react to cancellation (see the subsystem-level design doc's "Cancellation and
abandon policy").

**Data Model**: An optional `abandonTimeout` (default `DefaultAbandonTimeout`, 2 seconds), an
`ISpeechDiagnostics` sink, and a diagnostics category string, all supplied at construction.

**Key Methods**:

- **RunAsync(Func&lt;CancellationToken, Task&gt; or Func&lt;CancellationToken, T&gt; work,
  CancellationToken)**: Starts `work` on a dedicated thread created with
  `TaskCreationOptions.LongRunning`, forwards `CancellationToken` to it, and returns a `Task`/
  `Task<T>` representing the work. If the caller cancels, `work`'s own token is signaled first
  (cooperative cancellation); if `work` has not completed within `abandonTimeout` of that signal,
  the returned task completes faulted with `OperationCanceledException` without waiting further,
  and the abandonment is reported through the diagnostics sink at `Warning` level, including which
  diagnostics category the abandoned work belonged to.

**Error Handling**: An abandoned delegate is never forcibly terminated (the CLR provides no safe
way to do so) - it is left to run to completion or forever on its own thread, and only the
*caller's view* of it (the returned `Task`) is abandoned. A delegate that throws is surfaced
through the returned task exactly as `Task.Run` would.

**Dependencies**: `ISpeechDiagnostics`.

**Callers**: `SpeechRecognizerFactory.LoadAsync(...)` for model loading;
`RecognitionSession` for its pump loop (one new worker constructed per session by
`SpeechRecognizerEngine.CreateSessionAsync`).
