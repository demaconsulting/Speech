### OnnxPiperLibriTtsEnSynthesisModel

**Purpose**: The production `ISynthesisModel` for the Piper English (US) voice `en_US-libritts_r-medium`, run directly
through ONNX Runtime, with the installation of the voice's downloaded archive. It is one of the two public types of the
package.

**Data Model**:

- Constants: `ModelId` = `piper-onnx-libritts-r-en-us-medium` and `SpeakerParameterId` = `speaker`
- Declared facts: `DisplayName`, `Role` = Synthesis, `LicenseName` = `CC BY 4.0`, `LicenseUrl` =
  <https://creativecommons.org/licenses/by/4.0/>, `AudioTagSupport` = `None`, the preferred audio format mono 22,050 Hz,
  and `TextAnalyzer`, one `EnglishTextAnalyzer` (normalization and reading annotations selected) created on first use
  and shared by every instance
- `CapabilityProfile`: the shared `PiperCapabilityProfile` (internal helper of this unit, below)
- `Parameters`: one `NumericParameter` for the speaker: integer, 0 to 903, step 1, default 0
- `DownloadDescriptor`: one `SpeechModelDownloadFile`: the HTTPS URL of
  `vits-piper-en_US-libritts_r-medium.tar.bz2` published by the sherpa-onnx project, the SHA-256
  `10dc268f3e371696d721486123e2705a9fc1faa113491979fde4d88dba1f1b1c` and the install path of the archive
- The preferred execution provider names given to the constructor (`null` for CPU only)

**Key Methods**:

- **ResolveSpeakerId(parameterValues)**: the speaker from the `speaker` value; a boxed `double`, `int` or `float` is
  accepted and rounded to the nearest whole number (half away from zero); a missing bag, a missing value, a value of
  another type, a value that is not finite and a value outside 0 to 903 give speaker 0. It never throws
- **InstallAsync(stagedFilesDirectory)**: hands the staged archive to `PiperArchiveExtractor`, described below
- **CreateBackend(installedModelDirectory)**: reads `en_US-libritts_r-medium.onnx.json` with `PiperVoiceConfig.Load`;
  throws `InvalidDataException` when the configuration is unusable or its sample rate is not 22,050 Hz or its number of
  speakers is below 904; builds the `PiperPhonemePipeline`; creates the session with
  `OnnxExecutionProviderSelector.Create` (the preferred providers, the engine's probe inference as validation); returns
  the `OnnxPiperSynthesisEngine`. The configuration and the pipeline are created before the session, so a rejected voice
  never has a session to leak

**PiperCapabilityProfile (internal helper of this unit).** The model's `IModelCapabilityProfile`: the library's
`DefaultModelCapabilityProfile` renders the plan (tags, pauses, parameter overrides, ellipsis pauses, annotations), then
`JoinClauses` joins every segment that ends in a clause mark (`,`, `;` or `:`, where the core chunker split a sentence)
to the segment after it, with one space between them and the annotations of the second shifted to their place, so the
voice speaks a sentence as one utterance with its commas in place, as it was trained to. Two segments stay apart when
anything sets them apart: a segment without text (a pause), silence after the first or before the second, different
parameter overrides, a first segment that does not end in a clause mark (a sentence end, an ellipsis, a budget split),
or a joined text longer than `SentenceChunker.DefaultMaxChunkLength` (200 characters, the core chunker's own budget, so
a long sentence still reaches the engine in playback-sized segments; the engine then cuts what phonemizes to more than
256 symbols, preferably after a clause mark). Why: as separate utterances the clause pieces are often swallowed by the
voice (a one-word clause such as `coffee,` or `clocks,` loses its first sound or its last vowel, a clause that starts
with `r` or a weak word is lost more often), and as one utterance the first word of a clause was recognized 97.5 percent
of the time against 90.6 percent (see `docs/evaluation/phonemes-en/renderers.md`, *Listening Fixes 2*). It needs nothing
of the core library but its public chunking and plan types; nothing in the core changed.

**PiperArchiveExtractor (internal helper of this unit).** `ExtractAndDeleteAsync(archivePath, destinationDirectory)`
reads the `.tar.bz2` archive with SharpCompress (the .NET base class library has no BZip2 decoder, which is the reason
the package references it). It writes only the entries whose file name ends with `.onnx` or `.onnx.json`, and writes
each directly into the destination directory under its bare file name (`Path.GetFileName` of the entry key): the
archive's own folder structure plays no part in where a file lands, so a hostile entry path such as `../../evil.onnx`
cannot leave the directory. Every other entry (the espeak-ng data, the token table, the model card) is skipped and never
written. It throws `InvalidDataException` when the archive holds more than one file of a name or does not hold exactly
one `.onnx` and one `.onnx.json` file, leaving the archive in place (the downloader discards the staging directory); on
success it deletes the archive.

**Voice Download decision (D4).** The voice's own distribution (Hugging Face) could not be reached from the development
machine, so no file hash from it was verified. This unit therefore pins the verified sherpa-onnx archive of the same
voice (see *SpeechOnnxPiper System Design*, *Voice Download*). If Hugging Face becomes reachable, switch the descriptor
to two files pinned by revision and SHA-256, delete `InstallAsync` and `PiperArchiveExtractor` and drop the
SharpCompress reference; `CreateBackend` and the engine would not change.

**Speaker numbers.** LibriTTS-R publishes no speaker names, so the speakers are identified only by number and their
quality differs; speaker 0 is the default and is not claimed to be the best.

**License.** CC BY 4.0, from the voice's own model card (dataset OpenSLR 141). The license requires attribution when the
voice is redistributed or when material made with it is shared where the license's attribution applies; the credit is in
the package's `THIRD-PARTY-NOTICES.md` (see *SpeechOnnxPiper System Design*).

**Error Handling**: `InvalidDataException` for an incomplete archive, an unusable configuration or a voice of another
shape; `ArgumentException` for an empty path argument; a missing graph surfaces the ONNX Runtime error of the session
creation.

**Dependencies**: `PiperVoiceConfig`, `PiperPhonemePipeline`, `OnnxPiperSynthesisEngine`,
`OnnxExecutionProviderSelector`, `EnglishTextAnalyzer`, `DefaultModelCapabilityProfile` and `SentenceChunker` of the
Speech library, SharpCompress.

**Callers**: `SpeechModelCatalogPiperExtensions`, the Speech library's model store and synthesizer factory, and the
tests.
