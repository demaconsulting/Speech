### ISpeechSynthesizerEngine

**Purpose**: Define the public Layer 3 contract for a loaded, reusable text-to-speech model, so
hosts and tests can depend on synthesis behavior without depending on a specific inference engine
or native audio type.

**Data Model**: `IsAvailable` indicates whether this engine is backed by a loaded native model.
The adjacent `SynthesizedSpeech` record carries one segment's normalized `Samples`, the
`SampleRate` they were produced at, and the real `PreSilence`/`PostSilence` durations to play
alongside them, so a caller playing or saving segments needs nothing from any other segment to
use this one correctly.

**Key Methods**:

- **CreateSessionAsync(device, cancellationToken)**: Binds this engine to one playback device
  and returns a reusable `ISynthesisSession`. Throws `SynthesisEngineBusyException` if another
  leased session is still undisposed, and `ArgumentNullException` for a null `device`.
- **SpeakAsync(device, text, cancellationToken)**: One-shot convenience - internally creates a
  session, speaks once, and disposes the session before returning, for the common case of a
  single isolated utterance.
- **SynthesizeAsync(text, cancellationToken)**: One-shot convenience - internally creates a
  session with no device needed, returns the full-fidelity `IReadOnlyList<SynthesizedSpeech>`
  segment list (including pure-silence pause segments), and disposes the session.
- **DisposeAsync()**: Releases this engine's native resources. Disposes any still-active leased
  session first (session disposal also releases the lease), then the owned backend. Idempotent.

An engine supports many independent sessions over its life, one at a time - obtaining an engine
from `SpeechSynthesizerFactory` is the expensive step, so a host doing repeated, low-latency
synthesis (for example many turns of a voice conversation) should load one engine once and reuse
it, creating and disposing a session per device binding it needs, rather than reloading per turn.

**Error Handling**: Real implementations and the unavailable fallback both use
`SpeechSynthesizerUnavailableException` when an operational call is invalid because no usable
engine is available. Calling an operational member after disposal throws
`ObjectDisposedException`.

**Dependencies**: `SynthesizedSpeech`, `ISynthesisSession`, `SynthesisEngineBusyException`,
`SpeechSynthesizerUnavailableException`.

**Callers**: `SpeechSynthesizerFactory.LoadAsync(...)` and hosts that speak synthesized text.
