## SynthesisSubsystem Design

![SynthesisSubsystem Structure](SynthesisSubsystemView.svg)

### Overview

The SynthesisSubsystem provides text-to-speech capability. Sub-phase 4a shipped its Layer 1
concern: the closed, fixed Natural Language Audio Tag vocabulary and the model-independent
parser that recognizes inline bracket syntax against it. Sub-phase 4b, described below, adds
Layer 2 per-model rendering of a parsed span sequence into a `SpeechPlan`, a chunked/streaming
pipeline that synthesizes and plays speech, a real sherpa-onnx synthesis engine, and the public
`ISpeechSynthesizer` contract with its honest unavailable fallback. Phase 10 threads an optional,
session-level `parameterValues` bag (for example a selected voice) through
`SpeechSynthesizerFactory.Create` into `SherpaOnnxSpeechSynthesizer`, which resolves it to a real
sherpa-onnx speaker id once per synthesized segment via the ModelManagementSubsystem's new
`ISynthesisModel.ResolveSpeakerId` hook, replacing a previously hard-coded `speakerId: 0` -
without disturbing the independent, per-segment `ParameterOverrides` speed/volume mechanism
already described below. It contains the following direct units:

- **AudioTagParser**: the pure, model-independent scanner, together with the **AudioTagCatalog**
  alias/kind lookup table it consumes and the **NaturalLanguageAudioTag** /
  **NaturalLanguageAudioTagKind** / **TaggedTextSpan** / **TaggedTextSpanKind** value types it
  operates over (Sub-phase 4a, unchanged)
- **IModelCapabilityProfile** and **DefaultModelCapabilityProfile**: the Layer 2 rendering
  strategy that turns a parsed span sequence into an ordered **SpeechPlan** of **SpeechSegment**s,
  deciding per tag whether to pass it through, approximate it, or strip it, driven purely by the
  selected model's own declared audio-tag support and parameters
- **SentenceChunker**: splits synthesis-ready plain text into sentence/clause-sized chunks so
  chunked, pipelined synthesis has natural-sounding boundaries
- **ISpeechSynthesizer**: the public chunked/streaming synthesis-and-playback contract, together
  with the **SynthesizedSpeech** value type it yields
- **SpeechSynthesizerFactory**: composition root that returns either a real synthesizer or the
  honest unavailable fallback, and never throws for an ordinary machine state
- **SherpaOnnxSpeechSynthesizer**: the real chunked/pipelined implementation, together with the
  internal **ISynthesisEngine**/**ISynthesisEngineFactory** seam, its real
  **SherpaOnnxSynthesisEngine**/**SherpaOnnxSynthesisEngineFactory** implementations, and the
  **PlaybackAudioResampler** that converts synthesized audio into the format a playback device
  requires
- **UnavailableSpeechSynthesizer** and **SpeechSynthesizerUnavailableException**: honest fallback
  behavior when no model, no engine, or no playback device is available

### Interfaces

The subsystem exposes `NaturalLanguageAudioTag`, `NaturalLanguageAudioTagKind`,
`AudioTagDescriptor`, `AudioTagCatalog`, `TaggedTextSpanKind`, `TaggedTextSpan`, `AudioTagParser`
(Sub-phase 4a, unchanged), `ISpeechSynthesizer`, `SynthesizedSpeech`, `SpeechSynthesizerFactory`,
`UnavailableSpeechSynthesizer`, and `SpeechSynthesizerUnavailableException` as its public API. It
consumes `IAudioPlaybackDevice` from the AudioSubsystem for output audio, `ISynthesisModel` from
the ModelManagementSubsystem for the engine configuration, preferred playback-format hint, and
Layer 2 rendering strategy, and `ISpeechDiagnostics` from the Diagnostics subsystem to report
structural composition, lifecycle, and fault facts without ever exposing synthesized text.

Both cross-subsystem dependencies were extended in this phase, additively, mirroring Phase 3's
identical recognition-direction additions: `IAudioPlaybackDevice` gained
`ChannelCount`/`SampleRate` so the resolved playback format can be discovered (see
_IAudioPlaybackDevice Design_), and `ISynthesisModel` gained a public best-effort
`PreferredAudioFormat` plus internal `CreateEngineConfig` and default-hook `CapabilityProfile` so
each model owns both its playback hint and its engine configuration/Layer 2 rendering strategy
(see _SpeechModelContract Design_).

No member of the subsystem's public API names a sherpa-onnx type, per architecture.md's
"engine backend stays swappable at the public API surface" decision. The sherpa-onnx
configuration type appears only on `ISynthesisModel`'s internal members and inside the
subsystem's internal engine seam.

### Design

`AudioTagCatalog` is the single source-of-truth alias table: a static, immutable dictionary
mapping every documented bracket-text alias (already normalized) to its canonical
`NaturalLanguageAudioTag` and `NaturalLanguageAudioTagKind`, built once from the same descriptor
list it also exposes for enumeration (`AudioTagCatalog.Tags`), so the lookup table and the
enumerable report can never drift out of sync with each other. `Normalize` trims, collapses
internal whitespace, and lower-cases bracket-interior text so a caller's exact spacing and casing
never affects resolution.

`AudioTagParser.Parse` is a single-pass scanner with no dependency on the catalog's internal
shape - it looks for `[`...`]` pairs, hands the interior text to
`AudioTagCatalog.TryResolve`, and either emits a `TaggedTextSpanKind.Tag` span (on a match) or
folds the bracket text into the running literal-text buffer (on no match). Per architecture.md's
"never worse than plain narration" guarantee, nothing this parser encounters ever throws or is
silently dropped: an unknown word, an empty bracket, an unclosed bracket, or a stray closing
bracket all become ordinary `TaggedTextSpanKind.PlainText` content, brackets included, exactly as
they appeared in the input. Recognized tag spans are always flushed as their own span, so
adjacent tags and text at either edge of the input never merge with neighboring plain text.

The parser is pure and allocation-light: it holds no state beyond one `StringBuilder` for the
in-progress plain-text run and returns a single ordered `IReadOnlyList<TaggedTextSpan>`. This
makes it trivially unit-testable with plain strings and no model, engine, or device double
required - the entire subsystem's public surface at this stage of the design has no threading,
disposal, or fault-containment concerns for the same reason.

`IModelCapabilityProfile.Render(IReadOnlyList<TaggedTextSpan> spans, ISpeechModel model)` is the
Layer 2 rendering strategy: it consumes the model-independent span sequence Layer 1 produced and
turns it into an ordered `SpeechPlan` of `SpeechSegment`s, deciding per tag - based purely on
`model.AudioTagSupport` and `model.Parameters` - whether to pass the tag through as its canonical
bracket text (native support), approximate it as a timed pause or a numeric parameter override on
the surrounding segment (parameter-mapped support), or strip it to plain narration (no support or
no matching convention). This signature deliberately differs from the parameter-dictionary shape
sketched in the Phase 4 plan: `DefaultModelCapabilityProfile.Instance` is a stateless singleton
shared by every model, and the public `ISpeechSynthesizer` contract has no caller-supplied
parameter bag for it to consume, so the strategy instead reads everything it needs directly from
the model it is rendering for. This keeps the contract trivially satisfiable by a model that
wants the default behavior (the `CapabilityProfile` hook on `ISynthesisModel` simply returns
`DefaultModelCapabilityProfile.Instance`) while still letting a model override it with bespoke,
non-generic rendering when its native tag support needs something the default cannot express.

`SpeechParameterConventions`, a small internal helper, is where the one, deliberately narrow
mapping from tag to numeric parameter lives: only `Fast`/`VeryFast`/`Slow`/`VerySlow` map to a
speed override (a fraction of the parameter's declared range) and only `Loud`/`Soft`/`Whispers`
map to a volume override; every other tag (every Emotion, every Non-verbal cue, `Breathy`,
`Emphasis`) has no built-in numeric meaning and silently strips under parameter-mapped support.
This is architecture.md's Risk #1 mitigation deliberately kept conservative: a wrong guess at
what "louder" or "more emphatic" numerically means for an arbitrary model would be worse than
narrating the plain text, so the default profile only maps the handful of tags with an
unambiguous numeric direction and lets a model override the profile entirely if it wants richer
behavior. Pause tags (`ShortPause`/`LongPause`) always render as real silence regardless of a
model's declared support, per architecture.md's explicit "pauses require no model cooperation"
decision - no model text is spoken for a pause, so there is nothing for it to get wrong. Ordinary
narration text between tags is split into `SpeechSegment`s by `SentenceChunker` so the resulting
`SpeechPlan` already has chunk-sized boundaries lined up with natural speech units before the
pipeline ever synthesizes anything.

`SentenceChunker.Chunk(text, maxLength)` splits on the coarsest natural boundary that still fits
the budget: it tries primary sentence-ending punctuation first, falls back to secondary clause
punctuation (commas, semicolons, colons) only for a sentence still over budget, and falls back
further to a plain whitespace budget only for a clause with no punctuation at all. A single word
that alone exceeds `maxLength` is still returned whole, never split mid-word, since a partial
word cannot be synthesized intelligibly. Splitting on the coarsest boundary that fits keeps each
chunk as large - and therefore as natural-sounding when read aloud - as the budget allows.

The primary sentence-ending pass treats a maximal run of consecutive sentence-ending characters
(any combination of `.`, `!`, `?` - e.g. an ellipsis `...`, or mixed terminators such as `?!` or
`!!`) as a single boundary, splitting only once after the last character of the run rather than
once per character. Without this, an ellipsis or repeated terminal punctuation would produce
degenerate chunks that are just a single punctuation character with no other content, and
synthesizing such a near-empty chunk produces audible glitches/artifacts regardless of which
model is doing the synthesis. The whole run of punctuation stays attached to the sentence that
precedes it, matching how a human naturally pauses once at an ellipsis rather than stopping
three separate times. This merged-run handling applies only to the primary sentence-ending pass;
the secondary clause-boundary pass still splits on every individual occurrence of `,`/`;`/`:`.

`SherpaOnnxSpeechSynthesizer`'s chunked, pipelined design mirrors
`SherpaOnnxSpeechRecognizer`'s threading pattern but runs it in the synthesis direction. A
producer task walks the `SpeechPlan` chunk by chunk, calling the engine to synthesize each
`SpeechSegment` in turn and writing the resulting `SynthesizedSpeech` into a bounded
`Channel<SynthesizedSpeech>` of capacity 5; the caller's own task drains that channel and plays
each segment as it arrives, so synthesis of a later chunk runs concurrently with playback of an
earlier one. Unlike the recognition-direction channel, which uses `DropOldest` because live
capture audio can tolerate drops, this channel uses `BoundedChannelFullMode.Wait`: synthesized
audio has already cost real inference time, so it must never be silently discarded, and instead
the producer simply waits for the consumer to catch up. A pause segment (empty text) skips the
engine entirely and produces pure silence directly, since there is nothing for the engine to
synthesize. `PlaybackAudioResampler` performs the playback-direction format conversion -
resample from the engine's actual rate to the device's resolved rate, using the same small
windowed-sinc FIR anti-aliasing step before downsampling decimation that the recognition-side
resampler now uses, then upmix mono to the device's channel count - mirroring
`AudioFrameResampler`'s identical recognition-direction role. The internal
`ISynthesisEngine`/`ISynthesisEngineFactory` seam confines every sherpa-onnx call to
`SherpaOnnxSynthesisEngine`/`SherpaOnnxSynthesisEngineFactory`, mirroring the
`IRecognitionEngine`/`IRecognitionEngineFactory` seam so the whole pipeline - chunking, Layer 2
rendering, pipelined synthesize-while-play ordering, cancellation, and fault containment - is
verifiable in CI with pure managed fakes.

`SpeakAsync` composes `SynthesizeStreamAsync` and `PlayStreamAsync` under one cancellable session:
calling `Stop()` cancels that session's linked `CancellationTokenSource` so an in-flight
utterance stops promptly and deterministically (for example in response to a user's barge-in
action), while a `finally` block around playback guarantees the playback device is stopped even
when the session is cancelled or a segment faults - a dropped playback device or a model failure
must fail the caller's awaited task honestly, never hang or crash the process.

#### ISpeechSynthesizer

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

**Error Handling**: Real implementations and the unavailable fallback both use
`SpeechSynthesizerUnavailableException` when an operational call is invalid because no usable
synthesizer is available or the playback device fails at first use. Calling an operational
member after disposal throws `ObjectDisposedException`.

**Dependencies**: `SynthesizedSpeech`, `SpeechSynthesizerUnavailableException`.

**Callers**: `SpeechSynthesizerFactory.Create(...)` and hosts that speak synthesized text.

#### SpeechSynthesizerFactory

**Purpose**: Provide the single composition entry point for obtaining an `ISpeechSynthesizer`, so
all "can this machine speak right now?" logic lives in one reviewable place, mirroring
`SpeechRecognizerFactory` exactly. The recommended caller pattern is to compose
`playbackDevice` first via `AudioDeviceFactory.CreatePlaybackDevice(selection,
model.PreferredAudioFormat)` and then pass that device into `SpeechSynthesizerFactory.Create(...)`;
when the backend honors the hint and the loaded engine later reports the same rate,
`PlaybackAudioResampler` stays on its existing equal-rate no-op fast path. Because
`PreferredAudioFormat` is only a best-effort hint, the resampler remains the guaranteed fallback
when the loaded engine's real `SampleRate` differs.

**Data Model**: A static class with no state. The public `Create(...)` overload composes against
the real sherpa-onnx engine factory; an internal overload accepts an injected
`ISynthesisEngineFactory` so composition can be verified without model files or a native runtime.

**Key Methods**:

- **Create(ISynthesisModel model, string installedModelDirectory, IAudioPlaybackDevice
  playbackDevice, ISpeechDiagnostics? diagnostics, IReadOnlyDictionary&lt;string, object&gt;?
  parameterValues)**: Returns a real `SherpaOnnxSpeechSynthesizer`
  when the model's installed directory exists, the model declares `SpeechModelRole.Synthesis`,
  the playback device reports `IsAvailable`, and the engine loads. Otherwise returns
  `UnavailableSpeechSynthesizer.Instance`. Preconditions: `model` and `playbackDevice` are
  non-null. Postcondition: the returned synthesizer is never null, and either owns a loaded
  engine or is the shared unavailable instance. Loading the engine allocates native resources, so
  the returned synthesizer must be disposed. The new, optional `parameterValues` argument
  (default `null`) is forwarded unchanged to the constructed synthesizer, which re-resolves it to
  a speaker id via `ISynthesisModel.ResolveSpeakerId` once per synthesized segment; a `null` bag
  (the pre-Phase-10 call shape) resolves to every model's own default voice, so this addition is
  fully backward compatible.

The checks run in the same deliberate order as the recognition-direction factory - installed,
then role, then device, then engine load - so the cheapest and most common cause of
unavailability (a model not downloaded yet) is reported first and no native memory is allocated
for a synthesizer that could never run.

**Error Handling**: Every ordinary machine state is represented as the honest unavailable
synthesizer plus a structural diagnostic, never as an exception, per architecture.md's "nothing
throws at composition" decision. An engine load failure is caught and degraded identically to a
missing model. Only a null `model`, `playbackDevice`, or engine factory throws
`ArgumentNullException`, since a null argument is a programming error rather than a machine
state.

**Dependencies**: `ISynthesisModel` and `SpeechModelRole` from the ModelManagementSubsystem,
`IAudioPlaybackDevice` from the AudioSubsystem, `ISpeechDiagnostics`/`NullSpeechDiagnostics` from
the Diagnostics subsystem, and the subsystem's own `ISynthesisEngineFactory`,
`SherpaOnnxSynthesisEngineFactory`, `SherpaOnnxSpeechSynthesizer`, and
`UnavailableSpeechSynthesizer`.

**Callers**: Host applications composing speech synthesis at start-up, and the system-level
integration tests.

#### UnavailableSpeechSynthesizer

**Purpose**: Provide a safe, always-obtainable `ISpeechSynthesizer` fallback for use when
synthesis is not possible on the current machine.

**Data Model**: No instance state. Exposes a single static `Instance` singleton; the constructor
is private, since the type carries no state and multiple instances would provide no value.
`IsAvailable` always returns `false`.

**Key Methods**:

- **SynthesizeStreamAsync(...)** / **PlayStreamAsync(...)** / **SpeakAsync(...)** / **Stop()**:
  Always throw `SpeechSynthesizerUnavailableException`.
- **Dispose()**: A no-op that never throws and never invalidates `Instance`, so a host that wraps
  its synthesizer in a disposal scope runs unchanged on a machine without synthesis.

**Error Handling**: Obtaining and holding the instance never throws. Only the operational members
throw, and only when actually invoked - a caller that checks `IsAvailable` first never triggers
them. This mirrors `UnavailableSpeechRecognizer` exactly, so both subsystems degrade the same
recognizable way.

**Dependencies**: `SpeechSynthesizerUnavailableException`; implements `ISpeechSynthesizer`.

**Callers**: `SpeechSynthesizerFactory.Create(...)` when the model is not installed, the model's
role is not synthesis, the playback device is unavailable, or the engine cannot be loaded.

#### SpeechSynthesizerUnavailableException

**Purpose**: Signal that an operational member of an unavailable synthesizer was invoked, or that
a synthesizer that claimed to be available failed on first use.

**Data Model**: No additional fields beyond the standard `Exception` base members.

**Key Methods**: Standard three-constructor exception pattern.

**Error Handling**: This type is itself the error-handling mechanism.

**Dependencies**: `Exception`.

**Callers**: `UnavailableSpeechSynthesizer` for every operational member, and
`SherpaOnnxSpeechSynthesizer` when its playback device fails to start.

#### Design Constraints

**Volume is applied as post-hoc amplitude scaling, not a native engine parameter.** sherpa-onnx's
offline TTS API has no volume/gain input, so a `Loud`/`Soft`/`Whispers` tag's numeric override is
applied by scaling the generated segment's samples and clamping to `[-1.0, 1.0]`, rather than
being passed into `Generate(...)`. This is a pragmatic, model-independent way to honor a
parameter-mapped volume override without depending on any specific engine exposing gain control.

**Speed is applied as a native engine parameter.** Unlike volume, sherpa-onnx's `Generate(text,
speed, speakerId)` already accepts a speed multiplier, so a `Fast`/`VeryFast`/`Slow`/`VerySlow`
tag's override is passed straight through rather than post-processed, giving better quality than
resampling the output audio would.

**Voice/speaker selection is a session-level concern, not a per-segment override.** The
`speakerId` argument to `Generate(...)` is resolved once per segment by calling
`ISynthesisModel.ResolveSpeakerId(parameterValues)` against the constructor-supplied
`parameterValues` bag - deliberately _not_ reused via the per-segment `ParameterOverrides`
mechanism above, which is Natural Language Audio Tag-scoped and transient (a `[fast]`/`[loud]`
tag applies only to the segment it annotates). A selected voice, by contrast, applies to the
whole session, so it is threaded through the constructor instead and re-resolved fresh per
segment (cheap and pure) rather than cached once for the whole session - keeping it correctly
independent of, and non-regressing for, the existing speed/volume tag mechanism.

**Synthesized text is never reported through diagnostics.** Every diagnostic this subsystem emits
is a structural fact - composed, started, stopped, or a named fault - and never includes
synthesized text, honoring the same `ISpeechDiagnostics` contract the recognition direction
honors for recognized text.

**Voice selection is now solved for every synthesis model this subsystem registers.** Phase 4
shipped this subsystem against zero real synthesis models, proven only against a test model.
Phase 7b added the library's first real `ISynthesisModel` (VITS/Piper), which exposed only its
default speaker as a documented, accepted, out-of-scope limitation. Phase 10 closes that
limitation for models with real per-voice knowledge: the new `ISynthesisModel.ResolveSpeakerId`
hook lets a model such as `SherpaOnnxKokoroEnglishSynthesisModel` (11 genuinely distinct voices)
resolve a selected voice to sherpa-onnx's real speaker id, proven end-to-end in this project's
development sandbox by synthesizing the same sentence with two different voice selections and
observing genuinely different, non-silent output - not merely that the configuration field is
accepted. A later pass closes the VITS model's own remaining limitation too, by declaring a
plain numeric speaker parameter over its full 904-speaker range (LibriTTS-R's speaker embeddings
have no published name mapping, so a numeric index rather than a named choice is the honest
declaration) and overriding `ResolveSpeakerId` for it, proven the same way against two different
numeric speaker selections.
