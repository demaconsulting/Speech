## SynthesisSubsystem Design

![SynthesisSubsystem Structure](ChatterboxTurboSynthesisSubsystemView.svg)

### Overview

The SpeechOnnxChatterboxTurbo SynthesisSubsystem is the real, raw-ONNX-backed implementation of the
Speech library's public `ISynthesisBackend` seam for Chatterbox Turbo. It contains the following
units:

- **OnnxChatterboxTurboSynthesisEngine**: prompt assembly and the bounded generation loop
- **OnnxChatterboxTurboNetworks**: the four ONNX Runtime sessions behind an internal seam
- **ChatterboxTurboTokenSelector**: repetition-penalized greedy token selection
- **ChatterboxTurboCapabilityProfile**: the model's audio-tag rendering policy

### Interfaces

The engine implements `ISynthesisBackend` (`SampleRate`, `Generate`, `Dispose`). The engine talks to
the networks only through the internal `IChatterboxTurboNetworks` interface
(`Encode`, `Embed`, `Step`, `Decode`, `Dispose`) so the generation algorithm is testable with a
fake. The capability profile is the synthesis model's public capability description, consumed by the
Speech library's renderer.

### Design

Speech's session renders text and tags through the capability profile, then calls `Generate` one
segment at a time. The engine encodes each voice once (speech encoder over the reference clip) and
caches the conditioning, then per call tokenizes and embeds the text, prefixes the voice
conditioning, runs the language model step by step through the token selector until the stop token
or the bound, and passes the prompt tokens, speech tokens and trailing silence tokens to the
decoder. The engine is not thread-safe; one caller at a time.

The capability profile maps tag kinds to treatments: laugh, throat-clear and gasp become native
bracketed model tokens; sighs and pauses become silence segments; every other tag is omitted so the
model never speaks bracket text. A native tag that would end a sentence is moved before the final
word by default (or suffixed, or stripped, per policy). Sentences are split on terminal punctuation
(ignoring digit-flanked punctuation), ellipsis endings add a short pause, and sentences over a
length budget are split at clause punctuation and then by word budget.

### Error Handling

Hitting the generation bound never throws; the result is flagged truncated. Invalid arguments throw
argument exceptions; use after dispose throws `ObjectDisposedException`. Native failures propagate as
`OnnxRuntimeException` to Speech's fault-containment layer.

### Dependencies

`Microsoft.ML.OnnxRuntime` managed API and the Speech library's public contracts.
