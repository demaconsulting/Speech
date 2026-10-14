## SynthesisSubsystem Design

![SynthesisSubsystem Structure](ChatterboxTurboSynthesisSubsystemView.svg)

### Overview

The SpeechOnnxChatterboxTurbo SynthesisSubsystem is the real, raw-ONNX-backed implementation of the
Speech library's public `ISynthesisBackend` seam for Chatterbox Turbo. It contains the following
units:

- **OnnxChatterboxTurboSynthesisEngine**: prompt assembly and the bounded generation loop
- **OnnxChatterboxTurboNetworks**: the four ONNX Runtime sessions behind an internal seam
- **ChatterboxTurboTokenSelector**: repetition-penalized greedy token selection with blocked-token support
- **ChatterboxTurboConditioning**: the reference-clip conditioning bundle (conditioning and
  speaker embeddings plus prompt tokens) the engine computes once per voice and mood and reuses
- **ChatterboxTurboCapabilityProfile**: the model's audio-tag and mood rendering policy

### Interfaces

The engine implements `ISynthesisBackend` (`SampleRate`, `Generate`, `Dispose`). The engine talks to
the networks only through the internal `IChatterboxTurboNetworks` interface (`Encode`, `Embed`,
`BeginGeneration`, `Decode`, `Dispose`); `BeginGeneration` returns an
`IChatterboxTurboLanguageModelState` whose `Step` and `Dispose` own one generation's key/value cache.
This keeps the generation algorithm testable with a fake. The capability profile is the synthesis
model's public capability description, consumed by the Speech library's renderer.

### Design

Speech's session renders text and tags through the capability profile, then calls `Generate` one
segment at a time. The engine encodes each voice once (speech encoder over the reference clip) and
caches the conditioning, then per call tokenizes and embeds the text, prefixes the voice
conditioning, runs the language model step by step through the token selector until the stop token
or the bound, and passes the prompt tokens, speech tokens and trailing silence tokens to the
decoder. Mood tags choose which reference clip of the voice conditions each segment; the model has no
emotion tokens. Calls are serialized by an internal lock, so concurrent callers run one at a time.

The engine also applies run-length capping (a token repeated more than the configured number of times is
blocked for the next step; the stop token never is), runs on the CPU provider only, and produces 24 kHz
mono audio. An out-of-range speaker id falls back to the first voice, and a voice without a clip for the
requested mood uses its base clip. The stop token ends generation immediately, so a run of the stop
token cannot occur; the cap blocks only the last generated token and never the stop token, so a capped
run can still end speech.

Defaults and limits are owned by `ChatterboxTurboModelOptions` and are constructor-configurable by the
host: at most 1024 speech tokens per segment, 25 speech tokens per text token plus a fixed slack of 32
tokens (the stricter bound wins), at most 12 identical consecutive speech tokens (0 turns the cap off),
a 150 ms join silence at mood changes (0 to 1000 ms), and the `EndOfSentenceTagPolicy` option
(`MoveBeforeFinalWord` by default). Segmentation keeps each sentence within 200 characters of source
text, counting each audio tag as one character, so a unit may exceed 200 characters once native tags
expand. The bounds and the run-length cap apply to each mood segment separately.

Disposal while a generation is running is safe: `Dispose` first sets the disposed flag, which the
running generation checks before every step, so it stops by throwing `ObjectDisposedException`;
`Dispose` then takes the internal lock, waiting for that generation to leave it, and only then
disposes the networks, so native sessions are never released under a running inference. A new
`Generate` after dispose throws `ObjectDisposedException`.

The capability profile maps tag kinds to treatments: laugh, throat-clear and gasp become native
bracketed model tokens; sighs and pauses become silence segments; every other tag is omitted so the
model never speaks bracket text. A native tag that would end a sentence is moved before the final
word by default (or suffixed, or stripped, per policy; a tag without a suffixed form is dropped under
the suffix policy). A tag between two sentences leads the following sentence. A chunk holding only a
trailing tag joins the previous unit, and a tag with no neighboring speech at all is dropped.
Sentences are split on terminal punctuation
(ignoring digit-flanked punctuation), ellipsis endings add a short pause, and sentences over a
length budget are split at clause punctuation and then by word budget. A mood tag makes the profile
prefix the following text with a private-use mood marker and add a short join silence wherever the mood
changes; the engine reads the marker to choose the reference clip and strips it before tokenization.
Join silence interplay: a join silence of 0 adds nothing; a pause that already exists at the mood
change is not doubled; and an ellipsis pause longer than the join silence wins instead of stacking.

The token selector treats NaN logits as negative infinity, so a NaN is never chosen over a real score;
positive infinity stays the strongest score even for a token generated before; ties resolve to the
lowest id.

### Error Handling

Hitting the generation bound never throws; the result is flagged truncated. Invalid arguments throw
argument exceptions; use after dispose throws `ObjectDisposedException`. Native failures propagate as
`OnnxRuntimeException` to Speech's fault-containment layer. Empty text, and text that holds only
mood-marker characters, yield empty audio without error. The `speed` argument is ignored because the
model has no speed control. Disposal during generation, non-finite logits, use after dispose and
invalid arguments are verified at unit level; the subsystem verification document maps each subsystem
requirement to its scenario.

### Dependencies

`Microsoft.ML.OnnxRuntime` managed API and the Speech library's public contracts.
