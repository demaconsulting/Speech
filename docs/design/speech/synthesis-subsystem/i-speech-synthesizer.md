### ISpeechSynthesizer

**Purpose**: Define the contract for chunked, streaming text-to-speech synthesis and playback, so
hosts and tests can depend on synthesis behavior without depending on a specific inference engine
or native audio type.

**Data Model**: `IsAvailable` indicates whether the synthesizer is backed by a loaded engine and
a usable playback device. The adjacent `SynthesizedSpeech` record carries one segment's
normalized `Samples`, the `SampleRate` they were produced at, and the real `PreSilence`/
`PostSilence` durations to play alongside them, so a caller playing segments as they arrive needs
nothing from any other segment to play this one correctly.

**Key Methods**:

- **SynthesizeStreamAsync(text, cancellationToken)**: Normalizes, tag-parses, Layer 2 renders,
  and chunks the input text, then synthesizes and yields one `SynthesizedSpeech` per chunk as it
  becomes ready, so an early chunk is available before a later chunk finishes synthesizing.
- **PlayStreamAsync(stream, cancellationToken)**: Starts the playback device once, plays each
  yielded segment's pre-silence, resampled/upmixed audio, and post-silence in order, and stops
  the device once the stream ends or faults, guaranteeing the device is released either way.
- **SpeakAsync(text, cancellationToken)**: Composes the two above into one convenience call for
  the common case of synthesizing and playing a whole utterance.
- **Stop()**: Cancels an in-flight `SpeakAsync` session deterministically. A safe no-op when no
  session is in flight.
- **Dispose()**: Releases the engine resources a synthesizer holds. Implies `Stop()` and is
  idempotent.

A synthesizer supports many independent synthesize/play sessions on the same instance without
reconstruction - obtaining a synthesizer from `SpeechSynthesizerFactory` is the expensive step, so
a host doing repeated, low-latency synthesis (for example, many turns of a voice conversation)
should construct one synthesizer once and reuse it across sessions rather than disposing and
recreating it per turn.

**Error Handling**: Real implementations and the unavailable fallback both use
`SpeechSynthesizerUnavailableException` when an operational call is invalid because no usable
synthesizer is available or the playback device fails at first use. Calling an operational
member after disposal throws `ObjectDisposedException`.

**Dependencies**: `SynthesizedSpeech`, `SpeechSynthesizerUnavailableException`.

**Callers**: `SpeechSynthesizerFactory.Create(...)` and hosts that speak synthesized text.
