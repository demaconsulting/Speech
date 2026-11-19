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
  concrete `ISynthesisModel` (with the internal `PiperArchiveExtractor` that installs the downloaded archive) and
  `PiperVoiceConfig`, the reader of the voice configuration - see *SpeechOnnxPiper ModelManagementSubsystem Design*
- **SynthesisSubsystem**: `PiperPhonemePipeline` (text to framed phoneme ids) and `OnnxPiperSynthesisEngine`, the
  implementation of Speech's public `ISynthesisBackend` and `IAnnotatedSynthesisBackend` seams over ONNX Runtime's
  managed API - see *SpeechOnnxPiper SynthesisSubsystem Design*

ModelManagementSubsystem uses SynthesisSubsystem (`CreateBackend` builds the pipeline and the engine) and
SynthesisSubsystem uses `PiperVoiceConfig` of ModelManagementSubsystem (the symbol table and the scales); both
subsystems are small and live in one assembly.

## External Interfaces

SpeechOnnxPiper exposes two public types: the catalog extension class and the one model class. It implements Speech's
public `ISynthesisModel` model seam and its equally public `ISynthesisBackend` and `IAnnotatedSynthesisBackend` backend
seams. The engine, the pipeline, the configuration reader and the archive extractor are internal; the test assembly sees
them through `InternalsVisibleTo`. The assembly is not granted `InternalsVisibleTo` by the Speech library and behaves as
an ordinary external consumer of Speech's public API.

| Interface | Direction | Format | Constraints |
| --- | --- | --- | --- |
| `SpeechModelCatalog.AddPiperModels(...)` | Inbound | Extension method | Throws only for a null catalog |
| `OnnxPiperLibriTtsEnSynthesisModel` | Inbound | Public class | Stateless model declaration |
| `ISynthesisModel.InstallAsync(...)` | Inbound | Public method call | `InvalidDataException` if incomplete |
| `ISynthesisModel.CreateBackend(...)` | Inbound/Outbound | Public method call | `InvalidDataException` if unusable |
| `ISynthesisBackend`, `IAnnotatedSynthesisBackend` | Inbound | Public interfaces | One caller at a time; disposable |
| `SpeechModelCatalog.AddModels(...)` | Outbound | Method call/return | Consumed; builder-phase only |
| `ITextAnalyzer` (SpeechTextEn `EnglishTextAnalyzer`) | Outbound | Object | Shared, immutable and thread-safe |
| `EnglishPhonemizer`, `EspeakIpaRenderer` (SpeechPhonemesEn) | Outbound | Method calls | Immutable and thread-safe |
| `OnnxExecutionProviderSelector.Create(...)` | Outbound | Static method call | Consumed building the session |
| Microsoft.ML.OnnxRuntime managed API | Outbound | Method call/return | Non-CPU execution needs a native runtime |
| SharpCompress reader API | Outbound | Method call/return | Consumed during installation only |

### Inference Contract

The graph is the standard Piper (VITS) export, as the sherpa-onnx project converted it (the converted graph carries
added metadata). A single forward pass takes four inputs and returns the waveform:

| Input name | Type and shape | Content |
| --- | --- | --- |
| `input` | `int64` `[1, N]` | The framed phoneme ids |
| `input_lengths` | `int64` `[1]` | N |
| `scales` | `float` `[3]` | Noise scale, length scale, noise width |
| `sid` | `int64` `[1]` | The speaker |

The output is the raw waveform at the voice configuration's sample rate. These names were read from the input metadata
of the loaded real graph in a local check of the installed voice; the stand-in graph used by the tests has the same four
inputs.

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

SpeechOnnxPiper has four project dependencies and two NuGet dependencies:

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
- **SharpCompress** supplies the BZip2 tar reader. The .NET base class library has no BZip2 decoder, and the voice is
  published as a `.tar.bz2` archive (see *Voice Download*). It is used only by `PiperArchiveExtractor`

Through SpeechTextEn the package also depends, transitively, on the part-of-speech package, which is pre-release; hence
the pre-release-only packing guard (see *Release*). See *OTS Integration Design*, *Microsoft.ML.OnnxRuntime Design*,
*SharpCompress Design*, *SpeechOnnx Design*, *SpeechTextEn Design* and *SpeechPhonemesEn Design* for details.

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
   `.tar.bz2` archive, and verifies its SHA-256
2. **Installation**: `InstallAsync` extracts from the archive only the `.onnx` graph and the `.onnx.json` configuration,
   directly into the staged directory under their bare file names, requires exactly one of each, and deletes the
   archive; the staged directory is then promoted to the installed location

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
   readings), renders it as espeak-style IPA in parts of at most 256 symbols and encodes each part with the voice's
   symbol table: the begin id, each symbol's ids followed by the pad id, the end id
3. **Inference**: the engine runs the graph once per part with the voice's noise scales, the length scale divided by the
   speed, and the speaker clamped into 0 to 903
4. **Output**: the waveforms are joined in order and returned as `BackendAudio` at 22,050 Hz, which Speech resamples to
   the playback device's format when needed. A text with nothing to say returns empty audio without running the graph

## Voice Download

The voice's own distribution is Hugging Face (`rhasspy/piper-voices`), whose files are the graph and the configuration.
**Decision D4 (recorded here as the design decision of this package):** the Hugging Face host could not be reached from
the machine on which this package was developed (the TLS connection failed and the API, the file tree and the model
card could not be fetched), so no Hugging Face file hash could be verified and nothing from Hugging Face is pinned.
The package instead takes the same voice from the archive that the sherpa-onnx project publishes for it,
`vits-piper-en_US-libritts_r-medium.tar.bz2` (82,038,311 bytes, SHA-256
`10dc268f3e371696d721486123e2705a9fc1faa113491979fde4d88dba1f1b1c`), whose hash was verified, and whose `MODEL_CARD`
(read from the downloaded archive) states 904 speakers, the dataset OpenSLR 141 and the license CC BY 4.0. The archive
also contains the espeak-ng data (GPL-3.0) and the sherpa-onnx token table, which this package does not use: only the
graph and the configuration are extracted, so no espeak-ng data is ever written to the installed model.

If Hugging Face becomes reachable and its files can be verified, the descriptor should be changed to two files (the
graph and the configuration, each pinned by revision and SHA-256), the model's `InstallAsync` override removed,
`PiperArchiveExtractor` deleted and the SharpCompress reference dropped. The engine and the configuration reader would
not change; the graph's input names should be confirmed first (by loading the graph and reading its input metadata),
because the sherpa-onnx conversion adds metadata and its hash differs from the Hugging Face file's.

## Unknown Symbols and Long Text

- **Never silent.** A phoneme symbol that is not in the voice's symbol table cannot be encoded. As Piper itself does, it
  is skipped, but never silently: each `PiperUtterance` lists the distinct symbols it lost, and the engine counts them
  over its lifetime (`UnknownSymbolCount`, `UnknownSymbols`). The renderer is tested to emit only symbols of this
  voice's table, so a non-zero count is a defect to investigate, for example a host pronunciation that uses a symbol the
  voice does not know. A word that is in no dictionary is not skipped either: the phonemization package pronounces it
  with a letter-to-sound model trained on the dictionary alone.
- **Long text.** VITS is trained on sentence-sized input with the commas in the text. The Speech library splits a
  request at every sentence end and also at every comma, semicolon and colon; the model's capability profile joins the
  clause pieces of a sentence again (up to the core chunker's 200-character budget), so a sentence of several clauses is
  one segment and one utterance with its commas as symbols (the clause pieces alone were often swallowed by the voice,
  see *Listening Fixes 2* in `docs/evaluation/phonemes-en/renderers.md`). A segment that still phonemizes to more than
  256 symbols (a long number written out, a long run of words without a break) is cut by `EspeakIpaRenderer.RenderParts`
  between words, preferably after a clause mark, and spoken as several utterances whose audio is joined; no text is
  dropped.

## Design Constraints

- **Speech must never depend on SpeechOnnxPiper**: the dependency direction is one way. Speech carries no project
  reference and no ONNX Runtime package, and names no type from this system
- **This system owns its entire pipeline**: phonemization, symbol encoding and inference are this system's
  responsibility, built on the sibling text analysis and phonemization libraries, so the voice runs with no espeak-ng
- **No accelerated execution-provider package reference**: only the base `Microsoft.ML.OnnxRuntime` package
- **Metadata and download descriptors only**: no voice file is bundled or redistributed; a consumer fetches the voice
  through Speech's download machinery on their own explicit action, and the model visibly declares its license
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
that CC BY 4.0 requires: the voice, its license and link, the LibriTTS-R corpus citation (Koizumi et al.; OpenSLR
resource 141), the Piper project, the sherpa-onnx archive the voice is taken from with its hash, the statement that the
weights and the configuration are not changed, and a pointer to the notices of the SpeechTextEn and SpeechPhonemesEn
packages, whose data this package does not reproduce. Both files are packed at the package root and the nuspec names the
license file; they are packaging files, not software items, and the FileAssert test of the built package inspects them.
