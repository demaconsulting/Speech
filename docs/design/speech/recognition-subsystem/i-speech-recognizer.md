### ISpeechRecognizer

**Purpose**: Define the contract for streaming speech-to-text, so hosts and tests can depend on
recognition behavior without depending on a specific inference engine.

**Data Model**: `IsAvailable` indicates whether the recognizer is backed by a loaded engine and a
usable capture device. The adjacent `SpeechRecognitionResult` record carries the full recognized
`Text` of the current utterance plus an `IsFinal` flag distinguishing a provisional hypothesis
from a committed one; `Text` is never a delta, so a host can render it directly. The
`SpeechRecognitionEvent` record wraps one result as the event payload, mirroring the
AudioSubsystem's `AudioCaptureFrameEventArgs` pattern.

**Key Methods**:

- **Start()** / **Stop()**: Begin/end streaming recognition. Starting an already-running
  recognizer and stopping one that is not running are both safe no-ops. `Stop()` drains
  already-captured audio, so every result derived from audio accepted before the call is
  delivered before it returns. A recognizer supports many independent Start/Stop cycles on the
  same instance without reconstruction - obtaining a recognizer from `SpeechRecognizerFactory` is
  the expensive step, so a host doing repeated, low-latency recognition (for example, many turns
  of a voice conversation) should construct one recognizer once and reuse it across cycles rather
  than disposing and recreating it per turn.
- **Dispose()**: Releases the engine resources a running recognizer holds. Implies `Stop()` and
  is idempotent.
- **ResultReceived**: Raised for each provisional or final result. Raised from the recognizer's
  own background decoding thread, never from the audio callback thread, so a handler may do
  moderate work. Handler exceptions are caught and reported, never propagated.

**Error Handling**: Real implementations and the unavailable fallback both use
`SpeechRecognizerUnavailableException` when an operational call is invalid because no usable
recognizer is available or the capture device fails at first use. Starting a disposed recognizer
throws `ObjectDisposedException`.

**Dependencies**: `SpeechRecognitionResult`, `SpeechRecognitionEvent`,
`SpeechRecognizerUnavailableException`.

**Callers**: `SpeechRecognizerFactory.Create(...)` and hosts that render a live transcript.
