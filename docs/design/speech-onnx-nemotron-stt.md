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
SHA-256-verified on install. The export contains only the three neural networks of a
cache-aware streaming RNN-T recognizer, so this package supplies the rest of the pipeline itself:
the streaming log-mel feature extractor, the RNN-T greedy decoder, vocabulary handling, the
leading-silence mitigation, and endpointing.

The export is the int4 ONNX conversion published by the `onnx-community` organization on Hugging
Face. The unchanged files are downloaded as one bundle from the project's `models` GitHub release, and
the original URL of each file is recorded with its pin. The conversion carries an
ONNX Runtime GenAI `genai_config.json` and int4 `MatMulNBits` operators, but this package does not
use ONNX Runtime GenAI: it reads only the three graphs and `vocab.txt` and runs them with the base
ONNX Runtime API.

The library consists of two subsystems:

- **ModelManagementSubsystem**: the `AddNemotronSttModels()` catalog extension method, the
  `OnnxNemotronMultilingualRecognitionModel` concrete `IRecognitionModel` implementation, and the
  `NemotronVocabulary` loader - see *SpeechOnnxNemotronStt ModelManagementSubsystem Design*
- **RecognitionSubsystem**: `OnnxNemotronRecognitionEngine`, the real implementation of Speech's
  public `IRecognitionBackend` seam, and the `NemotronFeatureExtractor`,
  `NemotronRnntGreedyDecoder`, `NemotronEndpointDetector`, and `SilenceRunLimiter` units (with
  `DitherNoise`) it composes - see *SpeechOnnxNemotronStt RecognitionSubsystem Design*

The repository also holds a non-shipped evaluation tool and a segregated set of evaluation
recordings that measure this system on real speech; neither is part of the package (see
*Evaluation Tool and Test Data* below).

### Design Decisions

- **D1 - one catalog id with a language parameter**: the download mirror layout is
  `<mirror>/<modelId>/<bundle>.zip`, so a model id must equal the mirror's folder name, and every
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
- **D4 - decode-clock endpointing without a model reset**: an utterance ends when the recognizer
  stops producing tokens, not when the input turns quiet. A new unit, `NemotronEndpointDetector`,
  applies the same three rules as the sherpa-onnx online recognizer with its default values - 1.2 s
  of trailing silence after speech, 2.4 s of trailing silence in any case, and a 20 s maximum
  utterance - so the ONNX and Sherpa Nemotron backends segment speech alike. Trailing silence is
  counted in decoded blank encoder frames (80 ms each for this model) plus the quiet input the
  `SilenceRunLimiter` removed before feature extraction, credited at the point in the stream where
  it was removed, so a pause counts its full input duration. At an endpoint the text is reported as
  final and cleared, but the encoder caches, the prediction-network state, and the feature stream
  are kept. The earlier design measured quiet on the input clock, reset the whole model at each
  endpoint, and held and replayed the audio that followed; a reset discards the encoder's left
  context, so speech resuming right after an endpoint was decoded without it. Keeping the state
  matches how a cache-aware streaming model is meant to run and keeps every word exactly once. The
  maximum-length rule splits only between words (before the last word-start token, see
  `NemotronVocabulary.StartsWord`) and carries the last word into the next utterance, and silence
  alone never produces a result.
- **D5 - silence features, never zero log-mel rows, as padding**: the engine sometimes has to
  complete a partial encoder chunk - when a quiet run after speech reaches the limiter's 400 ms
  cap (no more audio will arrive to fill the chunk) and at flush, where the encoder's look-ahead
  also needs one more chunk to release the last words. It pads with dithered digital silence run
  through the feature extractor, the features a live stream would continue with. The earlier
  design appended all-zero log-mel rows; a zero log-mel value is a mel energy of 1, far louder
  than real silence (about -16.6 after the `2^-24` log guard), so the model heard loud input and
  answered with invented trailing words. Padding frames never advance the endpoint clocks.
- **D6 - NeMo-parity front end**: the feature extractor reproduces NeMo's `FilterbankFeatures`
  inference path with this model's settings: a constant (zero) center pad of 256 samples on both
  ends instead of a reflect pad, a symmetric 400-sample Hann window instead of a periodic one, the
  additive log guard `2^-24` (NeMo's default, equal to the export's `log_eps`) instead of `1e-10`,
  and therefore `len / 160 + 1` frames for `len` samples. Feeding the encoder the features it was
  trained on is a precondition for every accuracy claim; parity is verified against golden values
  derived independently from the NeMo formulas (NeMo itself is not run; see the verification
  design of the feature extractor).
- **D7 - evaluated blank penalty**: greedy decoding subtracts a penalty from the blank score before
  choosing a token. Without one the model drops quiet, isolated words; a large one makes it emit
  words early and repeat them. The default 1.0 was chosen by measurement, sweeping 0 to 3 in steps
  of 0.5 with the evaluation tool over the committed evaluation data (the sweep is recorded in
  `docs/evaluation/nemotron-stt/README.md`). The penalty is an
  internal engine option (`NemotronEngineOptions.BlankPenalty`), validated finite and not
  negative, so the tool can sweep it without a public API.
- **D8 - closing punctuation never opens a segment**: because the model state is kept at an
  endpoint, the model may emit the closing punctuation of the sentence just finalized (".", ",",
  "?") a little after the endpoint. Closing-punctuation tokens that would open the next segment
  are dropped from the text; they have already advanced the prediction network, so the model's
  context is unchanged, and they do not count as speech for the endpoint detector.

## External Interfaces

SpeechOnnxNemotronStt exposes a small public API - the catalog extension method and the one model
class - and implements Speech's public `IRecognitionModel` model seam and its equally public
`IRecognitionBackend` backend seam. Both seams are genuinely public, by design, so a third-party
package can implement either level without any special assembly access; this assembly is not
granted `InternalsVisibleTo` by the Speech library and behaves as an ordinary external consumer of
Speech's public API (see *Speech ModelManagementSubsystem Design*).

| Interface | Direction | Format | Constraints |
| --- | --- | --- | --- |
| `SpeechModelCatalogNemotronSttExtensions.AddNemotronSttModels()` | Inbound | Extension method | Null catalog throws |
| `OnnxNemotronMultilingualRecognitionModel` | Inbound | Public class | Stateless model declaration |
| `IRecognitionModel.CreateBackend(...)` | Inbound/Outbound | Public method call | Throws when unusable |
| `CreateBackend(directory, parameters, NemotronEngineOptions)` | Inbound | Internal call | Evaluation tool only |
| `IRecognitionBackend` | Inbound | Public interface | Not thread-safe; one caller at a time |
| `SpeechModelCatalog.AddModels(...)` | Outbound | Method call/return | Consumed; builder-phase only |
| `OnnxExecutionProviderSelector.Create(...)` | Outbound | Static method call | Consumed building the encoder session |
| Microsoft.ML.OnnxRuntime managed API | Outbound | Method call/return | Requires a runtime for non-CPU execution |

The internal `CreateBackend` overload of `OnnxNemotronMultilingualRecognitionModel` takes explicit
engine options (for example the blank penalty or the endpoint rules) and validates them before any
file is read. It is not public API: the assembly grants `InternalsVisibleTo` only to its own test
project and to the non-shipped evaluation tool, which uses the overload to measure alternative
settings on the real model.

## Dependencies

SpeechOnnxNemotronStt has two project dependencies - the Speech library and the sibling SpeechOnnx
library - and the following NuGet dependency:

- **Microsoft.ML.OnnxRuntime** supplies the managed ONNX Runtime API this
  package's recognition engine runs its loaded sessions against. Per the sibling SpeechOnnx
  package's own convention, this package never references an accelerated execution-provider
  package itself; a consuming application that wants GPU acceleration adds the matching native
  runtime package itself and passes the matching provider name through `AddNemotronSttModels`'s
  `preferredExecutionProviderNames` parameter

See *OTS Integration Design*, *Microsoft.ML.OnnxRuntime Design*, and *SpeechOnnx Design* for
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

1. **Input**: Speech's `SpeechModelDownloader` fetches and checksum-verifies the one bundle this
   model's `DownloadDescriptor` declares (`nemotron-3.5-asr-streaming-0.6b-20261011-int4-b1.zip` on the
   project's `models` GitHub release) into the staged directory, resuming an interrupted transfer on the
   next attempt
2. **Output**: The model's `InstallAsync` hands the staged zip to Speech's `SpeechModelBundle`, which
   extracts the eleven files to their relative install paths (each `.onnx.data` file beside its `.onnx`),
   checks each against its compiled SHA-256 pin, keeps the bundle's license files under
   `licenses/<bundle>/` and deletes the zip; the staged directory is then promoted to the installed
   location. The installed layout is the same as before bundles existed, so an earlier install keeps working

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
   `OnnxNemotronRecognitionEngine.AcceptSamples`, which only buffers them: one 20 ms limiter
   frame at a time through the `SilenceRunLimiter`, then `DitherNoise`, then the
   `NemotronFeatureExtractor`. Quiet input the limiter removes is recorded at the feature-frame
   position where it was removed; when a quiet run after speech reaches the limiter's cap, the
   partial chunk is completed with silence features and the padded frames are recorded as such
2. **Chunking**: `TryDecode` takes each complete 56-frame chunk, prepends the 9-frame pre-encode
   cache to form a 65-frame encoder input, and runs the encoder with its cache tensors fed back
3. **Decoding**: `NemotronRnntGreedyDecoder` decodes the encoder output frames one at a time
   through the decoder and joint networks; closing punctuation that would open a new segment is
   dropped
4. **Endpointing**: each decoded frame, and each recorded removal of quiet input reached by the
   decoding, is reported to `NemotronEndpointDetector` (padding frames without advancing its
   clocks); when a rule fires, the utterance's tokens are finalized and cleared while the model
   state is kept
5. **Output**: `NemotronVocabulary.Detokenize` converts the tokens to text, reported to Speech as a
   provisional result when it changes and as a final result at an endpoint or flush; at flush the
   partial chunk and one further chunk are filled with silence features and decoded, and every
   final of the flushed audio is joined into one result

## Design Constraints

- **Speech must never depend on SpeechOnnxNemotronStt**: the dependency direction is one-way. Speech
  carries no `ProjectReference` and no ONNX Runtime package, and names no type from this system,
  so it remains independently publishable and consumable with other model providers
- **No accelerated execution-provider package reference**: this library references only the base
  `Microsoft.ML.OnnxRuntime` package, matching the sibling SpeechOnnx package's convention; a
  consuming application supplies its own accelerated native runtime package for its own target RID
- **Metadata and download descriptors only**: the package embeds no model weights; a consumer
  fetches the model files through Speech's download machinery on their own explicit action, and the
  model visibly declares its own license. DEMA Consulting redistributes the unchanged files as one
  bundle on the project's `models` GitHub release, with the OpenMDW-1.1 and MIT texts and a `NOTICE`
- **Code license**: the package's own code is MIT-licensed; the model weights are governed by the
  model's own license (see the license note below)
- **Compliance**: all functionality must be traceable to requirements
- **Quality**: zero warnings, complete documentation; automated unit tests never depend on model
  files. The real-model system tests skip when the model is not installed, but the CI build
  downloads the model into its own cache entry on Windows, Linux, and macOS before they run (a
  failed download fails the job), so in CI they always run; they are compiled for `net10.0` only,
  because the engine logic is covered on every target framework by the model-free unit tests

### License Note

The model weights are licensed under the OpenMDW License 1.1, as stated on the upstream model card
of `nvidia/nemotron-3.5-asr-streaming-0.6b` (checked on the live card on 2026-11-21). The model
therefore declares `LicenseName = "OpenMDW-1.1"` and the address of the license text,
`https://openmdw.ai/license/1-1/`, as its `LicenseUrl`. The files are the int4 ONNX conversion
published as `onnx-community/nemotron-3.5-asr-streaming-0.6b-onnx-int4`, downloaded unchanged; the
MIT tag of that repository covers only the conversion artifacts, and the converted weights remain
under the OpenMDW License. The package embeds no model file. DEMA Consulting redistributes the unchanged
files as one bundle on the project's `models` GitHub release, as the OpenMDW License permits; the bundle
carries the OpenMDW-1.1 text, the MIT text of the conversion, a `NOTICE` that keeps the notices of origin,
a `README.txt` and a `model.json` recording each file's original URL and SHA-256, all installed under
`licenses/<bundle>/`.

### Platform Support

The library targets the same frameworks as the Speech library:

| Target Framework | Runtime / Environment |
| --- | --- |
| `net8.0` | .NET 8 LTS |
| `net9.0` | .NET 9 |
| `net10.0` | .NET 10 |
| `net11.0` | .NET 11 |

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

## Evaluation Tool and Test Data

![SpeechSttEvaluation Structure](SpeechSttEvaluationView.svg)

Recognition changes are judged by numbers on fixed, license-clean recordings rather than by ear.
Two repository items serve that purpose; neither is part of the package or of this system.

**The evaluation tool** (`tools/DemaConsulting.Speech.SttEvaluation`) is a non-shipped
`net10.0` console tool (`IsPackable=false`), modeled as its own tool system, SpeechSttEvaluation,
like the ModelDownloader and the Phonemes.En tools. Its one command, `evaluate`, streams each
recording through an installed model's `IRecognitionBackend` the way a recognition session does
(20 ms blocks, decoding after each block, the model's text hook on every result, one flush at the
end) and reports corpus word error rate, segment count, invented words (insertions after the last
aligned reference word, the signature of a tail hallucination), latency on the audio clock, and
real-time factor, over five scenarios: the LibriSpeech subset, the poem, paused speech (four
utterances separated by 1.5 s of silence), continuous speech of at least 45 s, and the subset
attenuated by 20 dB. Its options:

- `--check` applies the accuracy gates to this system's model: corpus word error rate at most 6%
  on the LibriSpeech subset and at most 10% on the poem. The thresholds live in
  `SttEvaluationGates`, which the real-model system tests compile in as well, so the tool and the
  tests apply the same gates (exit code 1 when a gate fails, 2 for a usage, data, or model error)
- `--compare` evaluates several installed models (for example the Sherpa Nemotron model) on the
  same audio and adds a cross word error rate between their transcripts. Comparisons run locally
  only; no output of a third-party model is ever committed
- `--engine-option Name=Value` sets a declared parameter of the model (such as `language`) or, for
  this system's model only, an internal engine option - `BlankPenalty`, `FlushTailChunks`,
  `TrailingSilenceMs`, `TrailingSilenceAfterSpeechMs`, or `MaxUtteranceMs` - through the internal
  `CreateBackend` overload; this is how the blank-penalty default was chosen. An unknown name, or
  an engine option given for another model, is an error
- every report (summaries, per-recording rows, transcripts, comparisons) is written only below the
  git-ignored `artifacts/` folder; the tool refuses an output folder outside it. The sweep that
  chose the blank penalty is summarized by hand in `docs/evaluation/nemotron-stt/README.md`

The word error rate calculator, the data-set loader, the streaming transcriber, and the gates are
owned by the tool but compiled (linked) into `DemaConsulting.Speech.Onnx.NemotronStt.Tests` (and
the calculator into `DemaConsulting.Speech.Sherpa.Tests`), so the tool and every accuracy test
measure with one implementation. These shared sources use public library APIs only and compile for
every target framework of the tests.

**The test data** (`data/stt-en/`) is a segregated folder of 24 utterances from the LibriSpeech
`test-clean` set (CC BY 4.0; one per speaker, 12 female and 12 male, selected by a deterministic
rule recorded in the folder's README and copied byte-identical from the archive) and the 50 s
recitation of "Crossing the Bar" the recognition tests have always used (CC0). `ATTRIBUTION.md`
holds the attributions, and `MANIFEST.tsv` pins every file by SHA-256 and size together with its
license, source, and reference transcript; the loader refuses any file that does not match. The
folder sits outside every `src/` project and is consumed only by test projects and the tool, all of
which are `IsPackable=false`, so it is never packed. The data is a tool and test input, not a
software item (see *Modeling Scope and Exclusions* in the introduction).
