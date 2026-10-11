# SpeechOnnxNemotronStt System Design

This document provides the system-level design for the SpeechOnnxNemotronStt library.

![SpeechOnnxNemotronStt Structure](SpeechOnnxNemotronSttView.svg)

## Architecture

SpeechOnnxNemotronStt is a .NET library, published as the `DemaConsulting.Speech.Onnx.NemotronStt`
NuGet package, that supplies a concrete, ONNX-Runtime-backed streaming speech-recognition model
and inference engine for NVIDIA Nemotron 3.5 ASR streaming (0.6B parameters, int4-quantized ONNX
export) implementing the Speech library's `IRecognitionModel`/`IRecognitionBackend` contracts. It
is directly analogous in role to SpeechSherpa's Nemotron model, but runs the model's graphs
directly through ONNX Runtime instead of sherpa-onnx, and is built on top of the sibling
`DemaConsulting.Speech.Onnx` package's shared execution-provider-selection helper. It is a
sibling system to Speech rather than a subsystem of it, for the same reasons SpeechSherpa and
SpeechOnnxKokoro are: it is a separately built, separately packaged software item with its own
dependencies, and the Speech library must never depend on it. The package's `.csproj` confirms
two project references - `DemaConsulting.Speech` (for the `IRecognitionModel`/
`IRecognitionBackend`/`SpeechModelCatalog` contracts) and `DemaConsulting.Speech.Onnx` (for
`OnnxExecutionProviderSelector`) - so this system depends on both sibling systems.

The model is the `onnx-community/nemotron-3.5-asr-streaming-0.6b-onnx-int4` Hugging Face export
(about 790 MB, eleven files: `encoder.onnx` plus `encoder.onnx.data`, `decoder.onnx` plus
`decoder.onnx.data`, `joint.onnx` plus `joint.onnx.data`, `vocab.txt`, `tokenizer.json`,
`tokenizer_config.json`, `genai_config.json`, and `audio_processor_config.json`), each file
SHA-256-verified on download. The export contains only the three neural networks of a
cache-aware streaming RNN-T recognizer, so this package supplies the rest of the pipeline itself:
the streaming log-mel feature extractor, the RNN-T greedy decoder, vocabulary handling, the
leading-silence mitigation, and endpointing.

The library consists of two subsystems:

- **ModelManagementSubsystem**: the `AddNemotronSttModels()` catalog extension method, the
  `OnnxNemotronMultilingualRecognitionModel` concrete `IRecognitionModel` implementation, and the
  `NemotronVocabulary` loader - see _SpeechOnnxNemotronStt ModelManagementSubsystem Design_
- **RecognitionSubsystem**: `OnnxNemotronRecognitionEngine`, the real implementation of Speech's
  public `IRecognitionBackend` seam, and the `NemotronFeatureExtractor`,
  `NemotronRnntGreedyDecoder`, and `SilenceRunLimiter` units (with `DitherNoise`) it composes -
  see _SpeechOnnxNemotronStt RecognitionSubsystem Design_

### Design Decisions

- **D1 - one catalog id with a language parameter**: the download mirror layout is
  `<mirror>/<modelId>/<file>`, so a model id must equal the mirror's folder name, and every
  locale shares the same ~790 MB of files. The package therefore registers one model,
  `nemotron-3.5-asr-streaming-0.6b-onnx-int4`, with a `language` `ChoiceParameter` (`en-US`
  default, `en-GB`) instead of one id per locale. The encoder's `lang_id` is derived from the
  vocabulary's locale marker tokens at load time (ordinal among the `<unk>` and `<xx-XX>` tokens,
  with `<unk>` equal to 0, which gives en-GB = 24 and en-US = 25 for the shipped vocabulary).
- **D2 - leading-silence calibration stays internal to the engine**: the limiter's adaptive
  threshold is calibrated from the first 400 ms of audio. This is content-driven and happens on
  the audio stream, whereas the Speech library's `RecognitionSessionState.Starting` means the
  audio device is starting; calibration therefore cannot map to a session state transition
  without a Speech contract change, and no such change is made.
- **D3 - two subsystems**: model management (identity, download, vocabulary, backend
  construction) and recognition (the streaming engine and its signal-processing units) have
  different change drivers and different verification boundaries, mirroring the sibling
  packages.

## External Interfaces

SpeechOnnxNemotronStt exposes a small public API - the catalog extension method and the one model
class - and implements Speech's public `IRecognitionModel` model seam and its equally public
`IRecognitionBackend` backend seam. Both seams are genuinely public, by design, so a third-party
package can implement either level without any special assembly access; this assembly is not
granted `InternalsVisibleTo` by the Speech library and behaves as an ordinary external consumer of
Speech's public API (see _Speech ModelManagementSubsystem Design_).

| Interface | Direction | Format | Constraints |
| --- | --- | --- | --- |
| `SpeechModelCatalogNemotronSttExtensions.AddNemotronSttModels()` | Inbound | Extension method | Null catalog throws |
| `OnnxNemotronMultilingualRecognitionModel` | Inbound | Public class | Stateless model declaration |
| `IRecognitionModel.CreateBackend(...)` | Inbound/Outbound | Public method call | Throws when unusable |
| `IRecognitionBackend` | Inbound | Public interface | Not thread-safe; one caller at a time |
| `SpeechModelCatalog.AddModels(...)` | Outbound | Method call/return | Consumed; builder-phase only |
| `OnnxExecutionProviderSelector.Create(...)` | Outbound | Static method call | Consumed building the encoder session |
| Microsoft.ML.OnnxRuntime managed API | Outbound | Method call/return | Requires a runtime for non-CPU execution |

## Dependencies

SpeechOnnxNemotronStt has two project dependencies - the Speech library and the sibling SpeechOnnx
library - and the following NuGet dependency:

- **Microsoft.ML.OnnxRuntime** (version 1.28.0) supplies the managed ONNX Runtime API this
  package's recognition engine runs its loaded sessions against. Per the sibling SpeechOnnx
  package's own convention, this package never references an accelerated execution-provider
  package itself; a consuming application that wants GPU acceleration adds the matching native
  runtime package itself and passes the matching provider name through `AddNemotronSttModels`'s
  `preferredExecutionProviderNames` parameter

See _OTS Integration Design_, _Microsoft.ML.OnnxRuntime Design_, and _SpeechOnnx Design_ for
details.

## Risk Control Measures

N/A - SpeechOnnxNemotronStt provides no safety-critical functionality requiring risk control
measures (IEC 62304 §5.3.3). It supplies a speech-recognition model and inference backend with no
clinical or safety role.

## Data Flow

**Model registration path:**

1. **Input**: A host constructs a Speech `SpeechModelCatalog` and calls `AddNemotronSttModels()` on
   it, optionally supplying an ordered list of preferred execution provider names
2. **Registration**: The extension method passes the one `OnnxNemotronMultilingualRecognitionModel`
   instance to `SpeechModelCatalog.AddModels(...)`
3. **Output**: The same catalog instance is returned and now enumerates the model alongside its
   install state

**Model install path:**

1. **Input**: Speech's `SpeechModelDownloader` fetches and checksum-verifies every file this
   model's `DownloadDescriptor` declares directly into the staged directory, each at its own
   declared relative install path (each `.onnx.data` file beside its `.onnx`)
2. **Output**: Because every declared file is already directly usable with no extraction step,
   this model needs no `InstallAsync` override; the staged directory is promoted to the installed
   location unchanged

**Backend construction path:**

1. **Input**: Speech's `SpeechRecognizerFactory` loads an engine for the installed model through
   its model-driven default backend factory
2. **Vocabulary**: `OnnxNemotronMultilingualRecognitionModel.CreateBackend` loads `vocab.txt`,
   resolves the selected language (falling back to `en-US`), and derives its `lang_id`
3. **Sessions**: the encoder session is created through `OnnxExecutionProviderSelector.Create`
   with `NemotronEncoder.RunProbeInference` as the probe, and the decoder and joint sessions are
   created on the CPU with one intra-op thread and spinning disabled
4. **Construction**: the model constructs `OnnxNemotronRecognitionEngine` from a `NemotronEncoder`,
   a `NemotronRnntGreedyDecoder` over a `NemotronRnntNetwork`, and the vocabulary; a failure
   disposes any session already created and is thrown back to Speech's factory, which degrades it
   to an honest unavailable recognizer

**Recognition path (audio to text):**

1. **Input**: Speech's recognition worker passes 16 kHz mono samples to
   `OnnxNemotronRecognitionEngine.AcceptSamples`, which only buffers them through the
   `SilenceRunLimiter`, `DitherNoise`, and `NemotronFeatureExtractor`
2. **Chunking**: `TryDecode` takes each complete 56-frame chunk, prepends the 9-frame pre-encode
   cache to form a 65-frame encoder input, and runs the encoder with its cache tensors fed back
3. **Decoding**: `NemotronRnntGreedyDecoder` turns the encoder frames into token ids through the
   decoder and joint networks
4. **Output**: `NemotronVocabulary.Detokenize` converts the tokens to text, reported to Speech as a
   provisional result when it changes and as a final result on an endpoint or flush

## Design Constraints

- **Speech must never depend on SpeechOnnxNemotronStt**: the dependency direction is one-way. Speech
  carries no `ProjectReference` and no ONNX Runtime package, and names no type from this system,
  so it remains independently publishable and consumable with other model providers
- **No accelerated execution-provider package reference**: this library references only the base
  `Microsoft.ML.OnnxRuntime` package, matching the sibling SpeechOnnx package's convention; a
  consuming application supplies its own accelerated native runtime package for its own target RID
- **Metadata and download descriptors only**: no model weights are bundled or redistributed; a
  consumer fetches the model files through Speech's download machinery on their own explicit
  action, and the model visibly declares its own license
- **Code license**: the package's own code is MIT-licensed; the model weights are governed by the
  model's own license (see the license note below)
- **Compliance**: all functionality must be traceable to requirements
- **Quality**: zero warnings, complete documentation; automated unit tests never depend on model
  files, and a single real-model test self-skips when the model is not installed

### License Note (Unverified)

The model declares `LicenseName = "NVIDIA Open Model License"` with the NVIDIA Open Model License
URL, matching the sibling SpeechSherpa package's Nemotron model. The upstream Hugging Face model
card for `onnx-community/nemotron-3.5-asr-streaming-0.6b-onnx-int4` was unreachable from the
development sandbox, so this value is **UNVERIFIED**: the card's front matter may declare a
different license such as `mit`. It must be confirmed against the live model card before release.

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
matching native runtime for its own target RID; its absence degrades silently to the CPU
fallback. Only CPU execution has been verified; DirectML and other GPU execution providers could
not be verified.

### Integration Patterns

- **NuGet Packaging**: published as a separate package that depends on both the Speech package and
  the sibling SpeechOnnx package
- **Extension-method registration**: a host opts in with
  `new SpeechModelCatalog().AddNemotronSttModels()`
- **Model-owned backend construction**: the model constructs its own backend, so the Speech
  library's default backend factory needs no knowledge of this system
