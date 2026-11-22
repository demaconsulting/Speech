### OnnxPiperLibriTtsEnSynthesisModel

**Purpose**: The production `ISynthesisModel` for the Piper English (US) voice `en_US-libritts_r-medium`, run directly
through ONNX Runtime, with the installation of the voice from its `models`-release bundle. It is one of the two public
types of the package.

**Data Model**:

- Constants: `ModelId` = `piper-onnx-libritts-r-en-us-medium` and `SpeakerParameterId` = `speaker`
- Declared facts: `DisplayName`, `Role` = Synthesis, `LicenseName` = `CC BY 4.0`, `LicenseUrl` =
  <https://creativecommons.org/licenses/by/4.0/>, `AudioTagSupport` = `None`, the preferred audio format mono 22,050 Hz,
  and `TextAnalyzer`, one `EnglishTextAnalyzer` (normalization, reading and part-of-speech annotations selected) created
  on first use and shared by every instance
- `CapabilityProfile`: the shared `PiperCapabilityProfile` (internal helper of this unit, below)
- `Parameters`: one `NumericParameter` for the speaker: integer, 0 to 903, step 1, default 0
- `BundleName` (internal) = `piper-en-us-libritts-r-medium-20261122-fp32-b1`
- `BundleContents` (internal, static): the three vendor files, each with its original `rhasspy/piper-voices` URL
  (`resolve/main/en/en_US/libritts_r/medium/`), SHA-256 pin and flat install path: `en_US-libritts_r-medium.onnx`
  (`10bb85e0…c13195`), `en_US-libritts_r-medium.onnx.json` (`b471dc60…bc9afb3`) and `MODEL_CARD` (`0ccde692…68b30fcb9`).
  It is the single source of the per-file pins; the non-shipped model bundler reads it through `InternalsVisibleTo`
- `Bundles` (public, static): the one `SpeechModelBundle`, served from `SpeechModelBundle.ModelsReleaseBaseUri`, pinned
  by the zip SHA-256 `5196a8aa97f9053e0bdbbc2299007d48d55f37a74e88199869daebb710d8bd0f`, with `BundleContents` as its
  contents
- `DownloadDescriptor`: `SpeechModelBundle.CreateDescriptor(Bundles)`, the one zip
- The preferred execution provider names given to the constructor (`null` for CPU only)

**Key Methods**:

- **ResolveSpeakerId(parameterValues)**: the speaker from the `speaker` value; a boxed `double`, `int` or `float` is
  accepted and rounded to the nearest whole number (half away from zero); a missing bag, a missing value, a value of
  another type, a value that is not finite and a value outside 0 to 903 give speaker 0. It never throws
- **InstallAsync(stagedFilesDirectory)**: delegates to `SpeechModelBundle.InstallAsync(Bundles, ...)`, which rejects an
  unsafe entry name, an extra or missing file and any file whose SHA-256 differs from its pin, extracts the three
  vendor files to the staged root, keeps the bundle's `LICENSE`, `NOTICE`, `README.txt` and `model.json` under
  `licenses/piper-en-us-libritts-r-medium-20261122-fp32-b1/` and deletes the zip
- **CreateBackend(installedModelDirectory)**: reads `en_US-libritts_r-medium.onnx.json` with `PiperVoiceConfig.Load`;
  throws `InvalidDataException` when the configuration is unusable or its sample rate is not 22,050 Hz or its number of
  speakers is below 904; builds the `PiperPhonemePipeline`; creates the session with
  `OnnxExecutionProviderSelector.Create` (the preferred providers, the engine's probe inference as validation); returns
  the `OnnxPiperSynthesisEngine`. The configuration and the pipeline are created before the session, so a rejected voice
  never has a session to leak

**PiperCapabilityProfile (internal helper of this unit).** The model's `IModelCapabilityProfile`: the library's
`DefaultModelCapabilityProfile` renders the plan (tags, pauses, parameter overrides, ellipsis pauses, annotations),
after the spans were cut into one group per analyzer-declared sentence (a break inside a plain-text span, or next to a
tag), each group rendered on its own, then `JoinClauses` joins every segment that ends in a clause mark (`,`, `;` or
`:`, where the core chunker split a sentence) to the segment after it within the same group, with one space between them
and the annotations of the second shifted to their place, so the voice speaks a sentence as one utterance with its
commas in place, as it was trained to. Two segments stay apart when anything sets them apart: a segment without text (a
pause), silence after the first or before the second, different parameter overrides, a first segment that ends a
sentence (it ends in `.`, `!`, `?` or an ellipsis; a segment that ends in a clause mark, a dash or a word, because the
core chunker cut a long clause there, is joined), a sentence boundary the analyzer declared (a sentence that ends in `:`
is not joined to the next paragraph, so the next sentence keeps its own utterance start), or a joined text longer than
`MaxJoinedLength` (300 characters, where the core chunker's own budget is 200: the core chunker cuts a long clause at a
dash, before a conjunction or after a word that is not a function word, and the joining puts such pieces together again
up to 300 characters, so a long sentence is a few whole-phrase utterances; the engine then cuts what phonemizes to more
than 384 symbols, at the best phrase boundary). Why: as separate utterances the clause pieces are often swallowed by the
voice (a one-word clause such as `coffee,` or `clocks,` loses its first sound or its last vowel, a clause that starts
with `r` or a weak word is lost more often), and as one utterance the first word of a clause was recognized 97.5 percent
of the time against 90.6 percent (see `docs/evaluation/phonemes-en/renderers.md`, *Listening Fixes 2*); the longer
utterances of 300 characters were also recognized with fewer word errors than the 200-character ones (*Listening Fixes
3*). It needs nothing of the core library but its public chunking and plan types.

**Voice files and earlier installs.** `CreateBackend` reads only the graph and the configuration; the model card is
installed for provenance. The engine takes every voice fact from the configuration and never reads the graph's own
metadata, so the vendor graph and the sherpa-onnx re-packaged graph of earlier installs (same inputs, added metadata)
both work, and an earlier install, which holds only the graph and the configuration at the installed root, keeps
working unchanged (see *SpeechOnnxPiper System Design*, *Voice Download*).

**Speaker numbers.** LibriTTS-R publishes no speaker names, so the speakers are identified only by number and their
quality differs; speaker 0 is the default and is not claimed to be the best.

**License.** CC BY 4.0, as declared by the voice's own model card (dataset OpenSLR 141). The license requires
attribution when the voice is redistributed or when material made with it is shared where the license's attribution
applies: LibriTTS-R (Koizumi et al., 2023; CC BY 4.0; OpenSLR 141) and the Piper project (Rhasspy). The model card also
states that the voice was fine-tuned from the English Lessac medium voice, which was trained on the Blizzard Challenge
2013 Lessac data, licensed for non-commercial research use only; the type's remarks, the `LicenseName` remarks, the
package's `THIRD-PARTY-NOTICES.md` and the bundle's `NOTICE` carry this lineage notice, and consumers must review the
upstream terms (see *SpeechOnnxPiper System Design*).

**Error Handling**: `InvalidDataException` for a bundle that fails verification, an unusable configuration or a voice of
another shape; `ArgumentException` for an empty path argument; a missing graph surfaces the ONNX Runtime error of the
session creation.

**Dependencies**: `PiperVoiceConfig`, `PiperPhonemePipeline`, `OnnxPiperSynthesisEngine`,
`OnnxExecutionProviderSelector`, `EnglishTextAnalyzer`, and `DefaultModelCapabilityProfile`, `SentenceChunker` and
`SpeechModelBundle` of the Speech library.

**Callers**: `SpeechModelCatalogPiperExtensions`, the Speech library's model store and synthesizer factory, and the
tests.
