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
ONNX export performs only the final waveform-generation forward pass, so this package turns text
into phoneme token ids itself, in addition to the ONNX Runtime inference engine. It does so with two
sibling libraries of this repository and one embedded file of its own: the English text analysis of
`DemaConsulting.Speech.Text.En` (numbers, currency, dates and abbreviations spoken as words, and the
reading of each heteronym), the phonemizer and the Kokoro symbol renderer of
`DemaConsulting.Speech.Phonemes.En` (the CMU Pronouncing Dictionary, plus a letter-to-sound model for
the words the dictionary does not have), and Kokoro's own phoneme-to-token-id vocabulary. The package's
`.csproj` confirms four project references - `DemaConsulting.Speech` (for the
`ISynthesisModel`/`ISynthesisBackend`/`SpeechModelCatalog` contracts), `DemaConsulting.Speech.Onnx`
(for `OnnxExecutionProviderSelector`), `DemaConsulting.Speech.Text.En` and
`DemaConsulting.Speech.Phonemes.En` - so this system depends on all four sibling systems.

The library consists of two subsystems:

- **ModelManagementSubsystem**: the `AddKokoroModels()` catalog extension method, the
  `OnnxKokoroEnglishSynthesisModel` concrete `ISynthesisModel` implementation (with the shared English
  text analysis it hands to the session), and the embedded-vocabulary `KokoroPhonemeVocabulary` - see
  *SpeechOnnxKokoro ModelManagementSubsystem Design*
- **SynthesisSubsystem**: `KokoroPhonemePipeline` (text and annotations to phoneme token ids, in parts)
  and `OnnxKokoroSynthesisEngine`, the real implementation of Speech's public `ISynthesisBackend` and
  `IAnnotatedSynthesisBackend` seams over ONNX Runtime's managed inference API - see *SpeechOnnxKokoro
  SynthesisSubsystem Design*

## External Interfaces

SpeechOnnxKokoro exposes a small public API - the catalog extension method and the one model
class - and implements Speech's public `ISynthesisModel` model seam and its equally public
`ISynthesisBackend` backend seam. Both seams are genuinely public, by design, so a third-party
package can implement either level without any special assembly access; this assembly is not
granted `InternalsVisibleTo` by the Speech library and behaves as an ordinary external consumer of
Speech's public API, exactly as a genuine third-party model package would (see *Speech
ModelManagementSubsystem Design*).

| Interface | Direction | Format | Constraints |
| --- | --- | --- | --- |
| `SpeechModelCatalogKokoroExtensions.AddKokoroModels()` | Inbound | Extension method | Throws only for a null catalog |
| `OnnxKokoroEnglishSynthesisModel` | Inbound | Public class | Stateless model declaration |
| `ISynthesisModel.TextAnalyzer` | Inbound | Public property | One analysis shared by all instances |
| `ISynthesisModel.CreateBackend(...)` | Inbound/Outbound | Public method call | Throws when unusable |
| `ISynthesisBackend`, `IAnnotatedSynthesisBackend` | Inbound | Public interfaces | One caller at a time |
| `SpeechModelCatalog.AddModels(...)` | Outbound | Method call/return | Consumed; builder-phase only |
| `OnnxExecutionProviderSelector.Create(...)` | Outbound | Static method call | Consumed building the session |
| `EnglishTextAnalyzer` (Text.En) | Outbound | Object | Consumed; shared, thread-safe |
| `EnglishPhonemizer`, `MisakiRenderer` (Phonemes.En) | Outbound | Method calls | Consumed; immutable, thread-safe |
| Microsoft.ML.OnnxRuntime managed API | Outbound | Method call/return | Requires a runtime for non-CPU execution |

## Dependencies

SpeechOnnxKokoro has four project dependencies - the Speech library, the sibling SpeechOnnx library, and
the SpeechTextEn and SpeechPhonemesEn libraries - and the following NuGet dependency:

- **Microsoft.ML.OnnxRuntime** supplies the managed ONNX Runtime API this package's synthesis
  engine runs its loaded session against. Per the sibling SpeechOnnx package's own convention,
  this package never references an accelerated execution-provider package itself; a consuming
  application that wants GPU acceleration adds the matching native runtime package itself and
  passes the matching provider name through `AddKokoroModels`'s
  `preferredExecutionProviderNames` parameter

See *OTS Integration Design*, *Microsoft.ML.OnnxRuntime Design*, and *SpeechOnnx Design* for
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
   `OnnxExecutionProviderSelector.Create` with the installed ONNX model graph path, this
   instance's own preferred execution provider names, and
   `OnnxKokoroSynthesisEngine.RunProbeInference` as the probe run against each accelerated
   candidate session before it is accepted - catching a provider that loads successfully but
   fails on the actual graph at `Run()` time (observed with DirectML and the `ConvTranspose`
   operator), something session construction alone cannot detect
3. **Voice loading**: every declared voice's style-vector `.bin` file is read into memory and
   keyed by its resolved integer speaker id
4. **Construction**: the model constructs `OnnxKokoroSynthesisEngine` from the loaded session, a
   `KokoroPhonemePipeline` over a fresh `KokoroPhonemeVocabulary`, and the loaded voice styles
5. **Output**: the engine is returned to Speech as an engine-neutral backend; a load failure is
   thrown back to Speech's factory, which degrades it to an honest unavailable synthesizer

**Synthesis path (text to audio)**:

1. **Analysis**: Speech's `SynthesisSession` runs the model's shared `TextAnalyzer` (the Text.En
   `EnglishTextAnalyzer`, with the normalization and reading annotations) over the whole request,
   splits the text into sentence-sized segments and hands each segment, speed, the model-resolved
   speaker id and, when the segment carries annotations, those annotations to
   `OnnxKokoroSynthesisEngine.GenerateAnnotated` (or the segment alone to `Generate`)
2. **Phonemization**: the engine's `KokoroPhonemePipeline` phonemizes the segment with the Phonemes.En
   `EnglishPhonemizer`, which speaks the spoken replacement of a number or abbreviation, the chosen
   reading of a heteronym, a dictionary word from the CMU Pronouncing Dictionary and any other word from
   its spelling through the letter-to-sound model, so nothing is omitted; the `MisakiRenderer` (US
   profile, function words in their weak forms) renders Kokoro's phoneme symbols, split into parts of at
   most 510 tokens between words when the segment is long
3. **Tokenization**: `KokoroPhonemeVocabulary` converts each part's phoneme string into the model's
   integer token ids (dropping any unmapped character), and the engine pads the sequence with the
   vocabulary's pad token at both ends
4. **Style selection**: for each part the engine selects the speaker's style-vector row indexed by that
   part's own phoneme-token count, mirroring the proven Python reference pipeline
5. **Inference**: the engine runs one ONNX Runtime forward pass per part over `input_ids`, `style`,
   and `speed` tensors - the same three tensor names (shared as internal constants between
   the generation and the engine's `RunProbeInference` probe, so the two call sites can never drift
   apart)
6. **Output**: the raw waveform tensors of the parts are joined and returned to Speech as `BackendAudio`
   at the model's fixed 24000 Hz sample rate, which Speech resamples to the playback device's format
   when needed; text without any sound returns empty audio without running the graph

## Design Constraints

- **Speech must never depend on SpeechOnnxKokoro**: the dependency direction is one-way. Speech
  carries no `ProjectReference` and no ONNX Runtime package, and names no type from this system,
  so it remains independently publishable and consumable with other model providers
- **This system owns its entire pipeline**: unlike SpeechSherpa's sherpa-onnx-backed models, this
  system's ONNX export performs only the waveform-generation forward pass; the conversion of text to
  token ids is this system's own responsibility, confined to `KokoroPhonemePipeline` and
  `KokoroPhonemeVocabulary`, which use the shared Text.En analysis and the Phonemes.En phonemizer and
  renderer instead of data of their own
- **Reading-aware speech through the session only**: the reading of a heteronym and the spoken form of a
  number are decided by the text analysis, which only the session runs. The plain `Generate(text)` call
  has no analysis and speaks default readings without normalization (a documented behavior, not a
  defect); a host that wants analyzed speech goes through a session
- **Function words in their weak forms**: the pipeline renders the small function words as in running
  speech (a single internal switch, on). Kokoro was trained on phonemes that way; measured against the
  reference phonemizer's output on the tuning sentences the phone error rate is 1.99 percent with weak
  forms and 4.88 percent with full forms (1.43 against 4.50 on the validation sentences). This departs
  from the original plan of full forms on measurement only, and is flagged for the owner's listening check
- **British voices speak American phonemes**: the phonemes are always General American, so a British
  voice (`bf_*`, `bm_*`) speaks American pronunciations in its own voice quality (the `r` in `car` is
  pronounced). Their listening quality has not been evaluated separately; British English phonemes are
  deferred. The caveat is stated in the package README and in the model's remarks
- **No accelerated execution-provider package reference**: this library references only the base
  `Microsoft.ML.OnnxRuntime` package, matching the sibling SpeechOnnx package's convention; a
  consuming application supplies its own accelerated native runtime package for its own target RID
- **Metadata and download descriptors only**: no model weights are bundled or redistributed; a
  consumer fetches the ONNX graph and every voice style vector through Speech's download
  machinery on their own explicit action, and the model visibly declares its own license
- **Third-party data and notices**: the package embeds one data file that is not covered by the
  repository's MIT License: the Kokoro v1.0 tokenizer (from the Apache-2.0 licensed
  `onnx-community/Kokoro-82M-v1.0-ONNX` export). The pronunciation data (derived from the CMU
  Pronouncing Dictionary) and the heteronym model are not embedded here: they come from the Phonemes.En
  and Text.En packages, whose own notices carry their licenses. The package therefore names a license
  file, `PACKAGE-LICENSE.md`, with the SPDX expression `MIT AND Apache-2.0` (MIT for the code, Apache-2.0
  for the embedded tokenizer), and ships `THIRD-PARTY-NOTICES.md` with the verbatim Apache-2.0 text and a
  pointer to the notices of the two packages it depends on; both files are packed at the package root.
  They are packaging files, not software items. No pronunciation data of the misaki project is used
- **Prerelease-only package**: the package depends on the prerelease Phonemes.En and Text.En packages, so
  it is packed only for a prerelease version and a stable version builds no package
- **Documented phonemization limitations**: the text analysis classifies a heteronym from its sentence
  and sometimes labels two occurrences alike (for example `read ... read` in one sentence), Markdown links
  speak their URL, and some number forms are not yet normalized; these are limits of the shared analysis,
  recorded with it (see *SpeechTextEn Design*), not of this system
- **Compliance**: all functionality must be traceable to requirements
- **Quality**: zero warnings, complete documentation; the system is verified by the system tests in
  `SpeechOnnxKokoroTests` over a fixture ONNX graph, not by real speech output - see *SpeechOnnxKokoro
  System Verification* for the honestly-documented boundary

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
matching native runtime for its own target RID; its absence degrades silently to the CPU fallback.

### Integration Patterns

- **NuGet Packaging**: published as a separate package that depends on both the Speech package and
  the sibling SpeechOnnx package
- **Extension-method registration**: a host opts in with
  `new SpeechModelCatalog().AddKokoroModels()`
- **Model-owned backend construction**: the model constructs its own backend, so the Speech
  library's default backend factory needs no knowledge of this system
