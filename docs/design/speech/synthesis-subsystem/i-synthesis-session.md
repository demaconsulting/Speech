### ISynthesisSession

**Purpose**: Define the public Layer 5 contract for one text-to-speech session, bound to exactly
one playback device for its entire life, cheap to obtain from
`ISpeechSynthesizerEngine.CreateSessionAsync` and safely reusable across many
`SpeakAsync`/`SynthesizeAsync` calls without reconstruction.

**Data Model**: `IsAvailable` reflects disposal/fault status (`false` once disposed or `Faulted`).
`State` exposes the current `SynthesisSessionState` (`Created`, `Starting`, `Running`,
`Stopping`, `Stopped`, `Disposing`, `Disposed`, `Faulted`); `StateChanged` reports every
transition via a `SessionStateChangedEventArgs` carrying the previous and current state. Unlike
recognition's continuous capture-window states, `Starting`/`Running`/`Stopping` denote one
discrete in-flight operation, not a continuous stream - a session returns to `Stopped` after each
operation and is immediately ready for another call. See the `synthesis-subsystem.md` design
document for the full state diagram.

**Key Methods**:

- **SpeakAsync(text, cancellationToken)**: Synthesizes the text and plays it through the bound
  playback device. Throws `InvalidOperationException` if called while another
  `SpeakAsync`/`SynthesizeAsync` call on this same session is already in flight (the two methods
  never overlap on one session), `SynthesisSessionFaultedException` once `Faulted`,
  `SpeechSynthesizerUnavailableException` when unavailable, and `ArgumentNullException` for null
  `text`.
- **SynthesizeAsync(text, cancellationToken)**: Synthesizes the text and returns the full-fidelity
  `IReadOnlyList<SynthesizedSpeech>` segment list (including pure-silence pause segments) without
  playing it - no playback device interaction at all. Same overlap/fault/availability error
  behavior as `SpeakAsync`.
- **StopAsync(cancellationToken)**: Requests cooperative cancellation of whichever operation is
  currently in flight, by cancelling that operation's own linked cancellation source. A safe
  no-op when no operation is in flight. The cancelled operation unwinds to `Stopped`, not
  `Faulted` - a caller-requested stop is an ordinary, successful outcome.
- **DisposeAsync()**: Cancels any in-flight operation, transitions through `Disposing` to
  `Disposed`, and releases the owning engine's exclusivity lease exactly once. Idempotent.

**Error Handling**: `SpeechSynthesizerUnavailableException` when unavailable;
`SynthesisSessionFaultedException` (wrapping the original fault) once `Faulted` - a faulted
session is terminal and must be disposed and replaced, never resumed;
`InvalidOperationException` on overlap; `ObjectDisposedException` after disposal.

**Dependencies**: `SynthesizedSpeech`, `SynthesisSessionState`, `SessionStateChangedEventArgs`,
`SynthesisSessionFaultedException`, `SpeechSynthesizerUnavailableException`.

**Callers**: `ISpeechSynthesizerEngine.CreateSessionAsync(...)` callers and
`ISpeechSynthesizerEngine`'s own one-shot `SpeakAsync`/`SynthesizeAsync` convenience overloads.
