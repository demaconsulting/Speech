# SpeechOnnxPiper System Design

This document provides the system-level design for the SpeechOnnxPiper library.

![SpeechOnnxPiper Structure](SpeechOnnxPiperView.svg)

## Architecture

SpeechOnnxPiper is a .NET library, published as the `DemaConsulting.Speech.Onnx.Piper` NuGet package (a pre-release;
see *Release*), that supplies the Piper (VITS) English (US) voice `en_US-libritts_r-medium`, trained on the LibriTTS-R
corpus, as a text-to-speech model for the Speech library. It implements the Speech library's `ISynthesisModel` and
`ISynthesisBackend` contracts and runs the voice's ONNX graph directly through ONNX Runtime: there is no bundled native
inference engine and no espeak-ng. It is a sibling system to Speech, SpeechOnnx, SpeechOnnxKokoro and the other model
packages, a separately built and packaged software item with its own dependencies, and the Speech library never
depends on it.

Like SpeechOnnxKokoro, this package owns the entire text-to-speech pipeline of its voice, because the Piper graph does
no phonemization: it takes phoneme ids. The package therefore combines three other libraries of this repository with
the inference itself:

- the English text analysis of SpeechTextEn (the model's `ITextAnalyzer`), which writes out numbers, dates, currency and
  abbreviations and chooses the reading of each heteronym (the noun or the verb of "record");
- the phonemizer and the espeak-style IPA renderer of SpeechPhonemesEn (`EnglishPhonemizer`, `EspeakIpaRenderer`), which
  pronounce each word from the CMU Pronouncing Dictionary, the sound rules or a trained letter-to-sound model and write
  the result in the symbols the voice was trained on;
- the voice's own symbol table, read from its configuration file, which turns each symbol into the integer id the graph
  takes.

The speaking style is therefore this repository's, not espeak-ng's: it follows the voice's phoneme inventory closely,
but it is a listening-quality approximation of the reductions, stress and flapping of espeak-ng, and the measurements of
how close it is are recorded in `docs/evaluation/phonemes-en/renderers.md` and `docs/evaluation/phonemes-en/summary.md`,
not claimed here.

The library consists of two subsystems:

- **ModelManagementSubsystem**: the `AddPiperModels()` catalog extension method, the `OnnxPiperLibriTtsEnSynthesisModel`
  concrete `ISynthesisModel` (which installs the voice from its `models`-release bundle) and `PiperVoiceConfig`, the
  reader of the voice configuration - see *SpeechOnnxPiper ModelManagementSubsystem Design*
- **SynthesisSubsystem**: `PiperPhonemePipeline` (text to framed phoneme ids) and `OnnxPiperSynthesisEngine`, the
  implementation of Speech's public `ISynthesisBackend` and `IAnnotatedSynthesisBackend` seams over ONNX Runtime's
  managed API - see *SpeechOnnxPiper SynthesisSubsystem Design*

ModelManagementSubsystem uses SynthesisSubsystem (`CreateBackend` builds the pipeline and the engine) and
SynthesisSubsystem uses `PiperVoiceConfig` of ModelManagementSubsystem (the symbol table and the scales); both
subsystems are small and live in one assembly.

## External Interfaces

SpeechOnnxPiper exposes two public types: the catalog extension class and the one model class. It implements Speech's
public `ISynthesisModel` model seam and its equally public `ISynthesisBackend` and `IAnnotatedSynthesisBackend` backend
seams. The engine, the pipeline and the configuration reader are internal; the test assembly sees them through
`InternalsVisibleTo`, and the non-shipped model bundler sees the model's internal `BundleContents` (the vendor-file
pins) the same way. The assembly is not granted `InternalsVisibleTo` by the Speech library and behaves as
an ordinary external consumer of Speech's public API.

| Interface | Direction | Format | Constraints |
| --- | --- | --- | --- |
| `SpeechModelCatalog.AddPiperModels(...)` | Inbound | Extension method | Throws only for a null catalog |
| `OnnxPiperLibriTtsEnSynthesisModel` | Inbound | Public class | Stateless model declaration |
| `ISynthesisModel.InstallAsync(...)` | Inbound | Public method call | `InvalidDataException` if not verified |
| `ISynthesisModel.CreateBackend(...)` | Inbound/Outbound | Public method call | `InvalidDataException` if unusable |
| `ISynthesisBackend`, `IAnnotatedSynthesisBackend` | Inbound | Public interfaces | One caller at a time; disposable |
| `SpeechModelCatalog.AddModels(...)` | Outbound | Method call/return | Consumed; builder-phase only |
| `ITextAnalyzer` (SpeechTextEn `EnglishTextAnalyzer`) | Outbound | Object | Shared, immutable and thread-safe |
| `EnglishPhonemizer`, `EspeakIpaRenderer` (SpeechPhonemesEn) | Outbound | Method calls | Immutable and thread-safe |
| `OnnxExecutionProviderSelector.Create(...)` | Outbound | Static method call | Consumed building the session |
| Microsoft.ML.OnnxRuntime managed API | Outbound | Method call/return | Non-CPU execution needs a native runtime |
| `SpeechModelBundle.InstallAsync(...)` | Outbound | Static method call | Consumed during installation only |

### Inference Contract

The graph is the standard Piper (VITS) export, the original `rhasspy/piper-voices` file. A single forward pass takes
four inputs and returns the waveform:

| Input name | Type and shape | Content |
| --- | --- | --- |
| `input` | `int64` `[1, N]` | The framed phoneme ids |
| `input_lengths` | `int64` `[1]` | N |
| `scales` | `float` `[3]` | Noise scale, length scale, noise width |
| `sid` | `int64` `[1]` | The speaker |

The output is the raw waveform at the voice configuration's sample rate. These names were read from the input metadata
of the loaded real graph; the stand-in graph used by the tests has the same four inputs. The engine reads every voice
fact (the sample rate, the speaker count, the scales and the symbol table) from the `.onnx.json` configuration and never
from the graph's own metadata, so it does not depend on the metadata that the sherpa-onnx re-packaged graph of earlier
installs added. The vendor graph and that re-packaged graph have the same inputs and outputs, and with the noise turned
off they produce the same waveform to within 2e-5 (less than one 16-bit sample step) for the same phoneme ids.

### Voice Facts

- **Sample rate**: 22,050 Hz, mono; `CreateBackend` rejects a configuration with another rate
- **Speakers**: 904, identified only by number (0 to 903); LibriTTS-R publishes no speaker names, the default is speaker
  0, and the quality differs between speakers; `CreateBackend` rejects a configuration with fewer speakers
- **Phoneme type**: `espeak`; the symbol table (159 symbols in the voice's configuration) is read from the configuration
  at load time
- **Inference defaults**: the noise scale, length scale and noise width come from the configuration's `inference` block
  (Piper's own defaults when it has none); the host's speed divides the length scale. The installed voice's file
  declares 0.333, 1.0 and 0.333, the same values the Sherpa model class sets; they measured clearly more stable than
  Piper's own defaults (0.667, 1.0 and 0.8) and no value tried measured clearly better (see *Piper Sampling Noise* in
  `docs/evaluation/phonemes-en/summary.md`), so a voice file that declares other scales must be overridden to these
  values
- **Parameters**: exactly one, the `speaker` numeric parameter (integer, 0 to 903, default 0); speed is the library's
  own speed argument. The audio-tag support is `None`

## Dependencies

SpeechOnnxPiper has four project dependencies and one NuGet dependency:

- **DemaConsulting.Speech** supplies the model and backend contracts, `SpeechModelCatalog` and the annotation type
- **DemaConsulting.Speech.Onnx** (SpeechOnnx) supplies `OnnxExecutionProviderSelector`, which builds the session,
  probes accelerated providers and falls back to the CPU
- **DemaConsulting.Speech.Text.En** (SpeechTextEn) supplies the `EnglishTextAnalyzer` (with the normalization and
  reading annotations selected) that the model exposes as its `TextAnalyzer`
- **DemaConsulting.Speech.Phonemes.En** (SpeechPhonemesEn) supplies `EnglishPhonemizer` and the `EspeakIpaRenderer`
  (`EspeakIpaProfile.EnUs`), including `RenderParts`, which cuts a long text into parts of a bounded size
- **Microsoft.ML.OnnxRuntime** supplies the managed ONNX Runtime API that the engine runs the session through. Following
  the SpeechOnnx convention this package never references an accelerated execution-provider package; a host that wants
  acceleration adds the matching native runtime itself and passes the provider name to `AddPiperModels`

Through SpeechTextEn the package also depends, transitively, on the part-of-speech package, which is pre-release; hence
the pre-release-only packing guard (see *Release*). See *OTS Integration Design*, *Microsoft.ML.OnnxRuntime Design*,
*SpeechOnnx Design*, *SpeechTextEn Design* and *SpeechPhonemesEn Design* for details.

## Risk Control Measures

N/A - SpeechOnnxPiper provides no safety-critical functionality requiring risk control measures (IEC 62304 §5.3.3). It
supplies a speech-synthesis model and backend with no clinical or safety role.

## Data Flow

**Model registration path:**

1. **Input**: a host constructs a Speech `SpeechModelCatalog` and calls `AddPiperModels()`, optionally with an ordered
   list of preferred execution provider names
2. **Registration**: the extension method passes the one `OnnxPiperLibriTtsEnSynthesisModel` to
   `SpeechModelCatalog.AddModels(...)`
3. **Output**: the same catalog is returned and enumerates the model with its install state

**Model install path:**

1. **Download**: Speech's `SpeechModelDownloader` fetches the one file of the model's `DownloadDescriptor`, the pinned
   bundle zip on the project's `models` release, and verifies its SHA-256
2. **Installation**: `InstallAsync` hands the staged zip to Speech's `SpeechModelBundle`, which extracts the graph, the
   configuration and the model card to the staged directory's root, checks each against its compiled SHA-256 pin, keeps
   the bundle's license files under `licenses/piper-en-us-libritts-r-medium-20261122-fp32-b1/` and deletes the zip; the
   staged directory is then promoted to the installed location

**Backend construction path:**

1. **Configuration**: `CreateBackend` reads the voice configuration and rejects a voice that is not the 904-speaker,
   22,050 Hz voice with an `InvalidDataException`
2. **Pipeline**: it builds the `PiperPhonemePipeline` for that configuration
3. **Session**: it calls `OnnxExecutionProviderSelector.Create` with the graph path, the preferred providers and the
   engine's probe inference, which is run on each accelerated candidate before it is accepted
4. **Engine**: it constructs the `OnnxPiperSynthesisEngine` from the session, the configuration and the pipeline. The
   configuration and the pipeline are created before the session, so a configuration that is invalid fails without a
   native session ever existing, and the constructor, the only step after the session exists, cannot fail

**Synthesis path (text to audio):**

1. **Input**: Speech's `SynthesisSession` passes one rendered text segment (a sentence: the model's capability profile
   joined its clause pieces), with the annotations of the model's text
   analysis, the speed and the model-resolved speaker to the engine
2. **Phonemization**: the pipeline phonemizes the text with the annotations (the normalized replacements, the chosen
   readings), renders it as espeak-style IPA in parts of at most 384 symbols and encodes each part with the voice's
   symbol table: the begin id and the pad id, each symbol's ids followed by the pad id, the end id
3. **Inference**: the engine runs the graph once per part with the voice's noise scales, the length scale divided by the
   speed, and the speaker clamped into 0 to 903
4. **Output**: the waveforms are joined in order and returned as `BackendAudio` at 22,050 Hz, which Speech resamples to
   the playback device's format when needed. A text with nothing to say returns empty audio without running the graph

## Voice Download

The voice's own distribution is Hugging Face (`rhasspy/piper-voices`, folder `en/en_US/libritts_r/medium`). DEMA
Consulting redistributes its three original files, unmodified, as one bundle on the project's `models` GitHub release,
`piper-en-us-libritts-r-medium-20261122-fp32-b1.zip` (78,627,157 bytes, SHA-256
`5196a8aa97f9053e0bdbbc2299007d48d55f37a74e88199869daebb710d8bd0f`), so a user needs no Hugging Face access:

| File | Size (bytes) | SHA-256 |
| --- | --- | --- |
| `en_US-libritts_r-medium.onnx` | 78,580,914 | `10bb85e071d616fcf4071f369f1799d0491492ab3c5d552ec19fb548fac13195` |
| `en_US-libritts_r-medium.onnx.json` | 20,123 | `b471dc60d2d8335e819c393d196d6fbf792817f40051257b269878505bc9afb3` |
| `MODEL_CARD` | 279 | `0ccde6927e5bb4d743f4ea39618a9387ba18cca3351220a8a9cfdbc68b30fcb9` |

The bundle adds the CC BY 4.0 legal code as `LICENSE`, a `NOTICE` with the full attribution and lineage (see *Package
License Files*), a `README.txt` and a `model.json` recording each file's original URL. The model's internal
`BundleContents` holds the three per-file pins and is the single source of truth: the runtime checks every extracted
file against it, and the non-shipped `tools/ModelBundler` packs exactly these files. The bundle name's upstream
release token is the snapshot date (`20261122`), not `v1.0.0`: `rhasspy/piper-voices` has a `v1.0.0` tag, but it could
not be verified that the tag holds these exact files, so by the bundling rules the date the files were taken names the
snapshot.

**Earlier installs.** Earlier versions of this package installed the voice from the sherpa-onnx archive
`vits-piper-en_US-libritts_r-medium.tar.bz2`, keeping only its graph (re-packaged by sherpa-onnx with added metadata)
and its configuration (byte-identical to the vendor file). The installed layout (the graph and the configuration at the
installed root) is unchanged and the model store's install check is manifest-based, so such an install keeps working
with no migration; a reinstall replaces it with the vendor files, the model card and the license files.

## Unknown Symbols and Long Text

- **Never silent.** A phoneme symbol that is not in the voice's symbol table cannot be encoded. As Piper itself does, it
  is skipped, but never silently: each `PiperUtterance` lists the distinct symbols it lost, and the engine counts them
  over its lifetime (`UnknownSymbolCount`, `UnknownSymbols`). The renderer is tested to emit only symbols of this
  voice's table, so a non-zero count is a defect to investigate, for example a host pronunciation that uses a symbol the
  voice does not know. A word that is in no dictionary is not skipped either: the phonemization package pronounces it
  with a letter-to-sound model trained on the dictionary alone.
- **Long text.** VITS is trained on sentence-sized input with the commas in the text. The Speech library splits a
  request at every sentence end and also at every comma, semicolon and colon; the model's capability profile joins the
  clause pieces of a sentence again (up to 300 characters, the core chunker's own budget being 200: the core cuts a long
  clause at a dash or a conjunction, and the joining puts the pieces together again), so a sentence of several clauses
  is one segment and one utterance with its commas as symbols (the clause pieces alone were often swallowed by the
  voice, see *Listening Fixes 2* in `docs/evaluation/phonemes-en/renderers.md`; the longer utterances were also
  recognized with fewer word errors, see *Listening Fixes 3*). A segment that still phonemizes to more than 384 symbols
  (a long number written out, a long run of words without a break) is cut by `EspeakIpaRenderer.RenderParts` between
  words, at the best phrase boundary (after a clause mark, else before a conjunction, never between a function word and
  the word after it), and spoken as several utterances whose audio is joined; no text is dropped.

## Design Constraints

- **Speech must never depend on SpeechOnnxPiper**: the dependency direction is one way. Speech carries no project
  reference and no ONNX Runtime package, and names no type from this system
- **This system owns its entire pipeline**: phonemization, symbol encoding and inference are this system's
  responsibility, built on the sibling text analysis and phonemization libraries, so the voice runs with no espeak-ng
- **No accelerated execution-provider package reference**: only the base `Microsoft.ML.OnnxRuntime` package
- **Metadata and download descriptors only**: the package embeds no voice file; a consumer fetches the voice through
  Speech's download machinery on their own explicit action, and the model visibly declares its license. DEMA Consulting
  redistributes the unmodified upstream files (CC BY 4.0) as one bundle on the project's `models` GitHub release, with
  the license text, the full attribution and lineage `NOTICE`, a `README.txt` and a `model.json` recording each file's
  original source
- **Honest limits**: the voice has 904 speakers of widely varying quality; the output differs from run to run (the model
  draws noise); the pronunciation approximates espeak-ng, it does not reproduce it; US English only

### Platform Support

The library targets the same frameworks as the Speech library:

| Target Framework | Runtime / Environment |
| --- | --- |
| `net8.0` | .NET 8 LTS |
| `net9.0` | .NET 9 |
| `net10.0` | .NET 10 |
| `net11.0` | .NET 11 |

It runs wherever the Speech library and the base `Microsoft.ML.OnnxRuntime` package's CPU provider run. An accelerated
provider requires the consuming application to supply the matching native runtime for its own target RID; its absence
degrades to the CPU fallback.

### Integration Patterns

- **NuGet packaging**: a separate package depending on the Speech, SpeechOnnx, SpeechTextEn and SpeechPhonemesEn
  packages
- **Extension-method registration**: a host opts in with `new SpeechModelCatalog().AddPiperModels()`
- **Model-owned backend construction**: the model constructs its own backend, so the Speech library's default backend
  factory needs no knowledge of this system

### Release

The package is packable only for a pre-release version while its dependency chain (SpeechTextEn, which depends on the
part-of-speech package) is pre-release: a stable package may not depend on a pre-release package (NuGet NU5104). A
stable version skips the project and the other packages still pack.

### Package License Files

The package names a license file, `PACKAGE-LICENSE.md`, instead of a license expression, because the voice it downloads
is under a license other than MIT. The file starts with the SPDX expression `MIT AND CC-BY-4.0`, contains the MIT
license text for the code and states the CC BY 4.0 license of the voice. `THIRD-PARTY-NOTICES.md` gives the attribution
that CC BY 4.0 requires: the voice, its license and link, the LibriTTS-R corpus citation (Koizumi et al., 2023; CC BY
4.0; OpenSLR resource 141), the Piper project (Rhasspy), the lineage notice (the voice was fine-tuned from the English
Lessac medium voice, trained on the Blizzard Challenge 2013 Lessac data, which is licensed for non-commercial research
use only, so consumers must review the upstream terms), the `models`-release bundle the voice is taken from with its
hash, the statement that the files are not changed, and a pointer to the notices of the SpeechTextEn and
SpeechPhonemesEn packages, whose data this package does not reproduce. Both files are packed at the package root and the
nuspec names the license file; they are packaging files, not software items, and the FileAssert test of the built
package inspects them.
