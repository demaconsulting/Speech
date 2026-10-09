# SpeechOnnxKokoro System Design

This document provides the system-level design for the SpeechOnnxKokoro library.

![SpeechOnnxKokoro Structure](SpeechOnnxKokoroView.svg)

## Architecture

SpeechOnnxKokoro is a .NET library, published as the `DemaConsulting.Speech.Onnx.Kokoro` NuGet
package, that supplies a concrete, ONNX-Runtime-backed Kokoro-82M v1.0 text-to-speech model and
inference engine implementing the Speech library's `ISynthesisModel`/`ISynthesisBackend`
contracts - directly analogous in role to SpeechSherpa, but for the Kokoro v1.0 model family
specifically, and built on top of the sibling `DemaConsulting.Speech.Onnx` package's shared
execution-provider-selection helper rather than sherpa-onnx. It is a sibling system to Speech
rather than a subsystem of it, for the same reasons SpeechSherpa is: it is a separately built,
separately packaged software item with its own dependencies, and the Speech library must never
depend on it.

Unlike SpeechSherpa, this package owns its entire text-to-speech pipeline itself rather than
delegating phonemization to a bundled native library: the real `onnx-community/Kokoro-82M-v1.0-ONNX`
ONNX export performs only the final waveform-generation forward pass, so this package supplies its
own embedded, verified word-to-phoneme lexicon and its own phoneme-to-token-id vocabulary, in
addition to the ONNX Runtime inference engine itself. The package's `.csproj` confirms two project
references - `DemaConsulting.Speech` (for the `ISynthesisModel`/`ISynthesisBackend`/
`SpeechModelCatalog` contracts) and `DemaConsulting.Speech.Onnx` (for
`OnnxExecutionProviderSelector`) - so this system depends on both sibling systems.

The library consists of two subsystems:

- **ModelManagementSubsystem**: the `AddKokoroModels()` catalog extension method, the
  `OnnxKokoroEnglishSynthesisModel` concrete `ISynthesisModel` implementation, the embedded-lexicon
  `KokoroLexiconPhonemizer`, and the embedded-vocabulary `KokoroPhonemeVocabulary` - see
  _SpeechOnnxKokoro ModelManagementSubsystem Design_
- **SynthesisSubsystem**: `OnnxKokoroSynthesisEngine`, the real implementation of Speech's public
  `ISynthesisBackend` seam over ONNX Runtime's managed inference API - see _SpeechOnnxKokoro
  SynthesisSubsystem Design_

## External Interfaces

SpeechOnnxKokoro exposes a small public API - the catalog extension method and the one model
class - and implements Speech's public `ISynthesisModel` model seam and its equally public
`ISynthesisBackend` backend seam. Both seams are genuinely public, by design, so a third-party
package can implement either level without any special assembly access; this assembly is not
granted `InternalsVisibleTo` by the Speech library and behaves as an ordinary external consumer of
Speech's public API, exactly as a genuine third-party model package would (see _Speech
ModelManagementSubsystem Design_).

| Interface | Direction | Format | Constraints |
| --- | --- | --- | --- |
| `SpeechModelCatalogKokoroExtensions.AddKokoroModels()` | Inbound | Extension method | Throws only for a null catalog |
| `OnnxKokoroEnglishSynthesisModel` | Inbound | Public class | Stateless model declaration |
| `ISynthesisModel.CreateBackend(...)` | Inbound/Outbound | Public method call | Throws when unusable |
| `ISynthesisBackend` | Inbound | Public interface | Not thread-safe; one caller at a time |
| `SpeechModelCatalog.AddModels(...)` | Outbound | Method call/return | Consumed; builder-phase only |
| `OnnxExecutionProviderSelector.Create(...)` | Outbound | Static method call | Consumed building the session |
| Microsoft.ML.OnnxRuntime managed API | Outbound | Method call/return | Requires a runtime for non-CPU execution |

## Dependencies

SpeechOnnxKokoro has two project dependencies - the Speech library and the sibling SpeechOnnx
library - and the following NuGet dependency:

- **Microsoft.ML.OnnxRuntime** supplies the managed ONNX Runtime API this package's synthesis
  engine runs its loaded session against. Per the sibling SpeechOnnx package's own convention,
  this package never references an accelerated execution-provider package itself; a consuming
  application that wants GPU acceleration adds the matching native runtime package itself and
  passes the matching provider name through `AddKokoroModels`'s
  `preferredExecutionProviderNames` parameter

See _OTS Integration Design_, _Microsoft.ML.OnnxRuntime Design_, and _SpeechOnnx Design_ for
details.

## Risk Control Measures

N/A - SpeechOnnxKokoro provides no safety-critical functionality requiring risk control measures
(IEC 62304 §5.3.3). It supplies a speech-synthesis model and inference backend with no clinical or
safety role.

## Data Flow

**Model registration path:**

1. **Input**: A host constructs a Speech `SpeechModelCatalog` and calls `AddKokoroModels()` on it,
   optionally supplying an ordered list of preferred execution provider names
2. **Registration**: The extension method passes the one `OnnxKokoroEnglishSynthesisModel`
   instance to `SpeechModelCatalog.AddModels(...)`
3. **Output**: The same catalog instance is returned and now enumerates the model alongside its
   install state

**Model install path:**

1. **Input**: Speech's `SpeechModelDownloader` fetches and checksum-verifies every file this
   model's `DownloadDescriptor` declares - the ONNX model graph and every voice's style-vector
   file - directly into the staged directory, each at its own declared relative install path
2. **Output**: Because every declared file is already directly usable with no extraction step,
   this model needs no `InstallAsync` override (unlike SpeechSherpa's archive-based models); the
   staged directory is promoted to the installed location unchanged

**Backend construction path:**

1. **Input**: Speech's `SpeechSynthesizerFactory` loads an engine for the installed model through
   its model-driven default backend factory
2. **Session construction**: `OnnxKokoroEnglishSynthesisModel.CreateBackend` calls
   `OnnxExecutionProviderSelector.Create` with the installed ONNX model graph path and this
   instance's own preferred execution provider names
3. **Voice loading**: every declared voice's style-vector `.bin` file is read into memory and
   keyed by its resolved integer speaker id
4. **Construction**: the model constructs `OnnxKokoroSynthesisEngine` from the loaded session, a
   fresh `KokoroPhonemeVocabulary`, a fresh `KokoroLexiconPhonemizer`, and the loaded voice styles
5. **Output**: the engine is returned to Speech as an engine-neutral backend; a load failure is
   thrown back to Speech's factory, which degrades it to an honest unavailable synthesizer

**Synthesis path (text to audio)**:

1. **Input**: Speech's `SynthesisSession` passes one rendered text chunk, speed, and the
   model-resolved speaker id to `OnnxKokoroSynthesisEngine.Generate`
2. **Phonemization**: the engine's `KokoroLexiconPhonemizer` converts the text into Kokoro's own
   IPA phoneme string by looking up each word in the embedded lexicon, passing through supported
   punctuation, and collapsing whitespace; unrecognized words are dropped
3. **Tokenization**: `KokoroPhonemeVocabulary` converts the phoneme string into the model's
   integer token ids (dropping any unmapped character), and the engine pads the sequence with the
   vocabulary's pad token at both ends
4. **Style selection**: the engine selects the speaker's style-vector row indexed by the
   utterance's own phoneme-token count, mirroring the proven Python reference pipeline
5. **Inference**: the engine runs a single ONNX Runtime forward pass over `input_ids`, `style`,
   and `speed` tensors
6. **Output**: the raw waveform tensor is returned to Speech as `EngineAudio` at the model's fixed
   24000 Hz sample rate, which Speech resamples to the playback device's format when needed

## Design Constraints

- **Speech must never depend on SpeechOnnxKokoro**: the dependency direction is one-way. Speech
  carries no `ProjectReference` and no ONNX Runtime package, and names no type from this system,
  so it remains independently publishable and consumable with other model providers
- **This system owns its entire pipeline**: unlike SpeechSherpa's sherpa-onnx-backed models, this
  system's ONNX export performs only the waveform-generation forward pass; phonemization and
  tokenization are this system's own responsibility, confined to `KokoroLexiconPhonemizer` and
  `KokoroPhonemeVocabulary`
- **No accelerated execution-provider package reference**: this library references only the base
  `Microsoft.ML.OnnxRuntime` package, matching the sibling SpeechOnnx package's convention; a
  consuming application supplies its own accelerated native runtime package for its own target RID
- **Metadata and download descriptors only**: no model weights are bundled or redistributed; a
  consumer fetches the ONNX graph and every voice style vector through Speech's download
  machinery on their own explicit action, and the model visibly declares its own license
- **Honest, documented phonemization limitations**: an out-of-vocabulary word is silently dropped
  rather than guessed at, and homograph pronunciation is resolved with no sentence context - both
  deliberate, documented simplifications rather than hidden defects (see _SpeechOnnxKokoro
  ModelManagementSubsystem Design_)
- **Compliance**: all functionality must be traceable to requirements
- **Quality**: zero warnings, complete documentation; automated unit test coverage does not yet
  exist for this package - see _SpeechOnnxKokoro System Verification_ for the honestly-documented
  gap

### Platform Support

The library targets the same frameworks as the Speech library:

| Target Framework | Runtime / Environment |
| --- | --- |
| `net8.0` | .NET 8 LTS |
| `net9.0` | .NET 9 |
| `net10.0` | .NET 10 |

The library runs wherever the Speech library and the base `Microsoft.ML.OnnxRuntime` package's CPU
provider are available - effectively anywhere the targeted .NET runtime itself runs. An
accelerated execution provider requires the consuming application to additionally supply the
matching native runtime for its own target RID; its absence degrades silently to the CPU fallback.

### Integration Patterns

- **NuGet Packaging**: published as a separate package that depends on both the Speech package and
  the sibling SpeechOnnx package
- **Extension-method registration**: a host opts in with
  `new SpeechModelCatalog().AddKokoroModels()`
- **Model-owned backend construction**: the model constructs its own backend, so the Speech
  library's default backend factory needs no knowledge of this system
