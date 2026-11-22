## ModelManagementSubsystem Design

![ModelManagementSubsystem Structure](KokoroModelManagementSubsystemView.svg)

### Overview

The SpeechOnnxKokoro ModelManagementSubsystem supplies this package's one concrete,
ONNX-Runtime-backed speech model as an implementation of the Speech library's `ISynthesisModel`
extension point (see *Speech ModelManagementSubsystem Design*), together with the supporting unit that holds
Kokoro's own phoneme vocabulary. The conversion of text to phonemes is not done here: the model supplies the
shared English text analysis, and the SynthesisSubsystem's `KokoroPhonemePipeline` does the phonemization
(see *SpeechOnnxKokoro SynthesisSubsystem Design*). It contains the following units:

- **SpeechModelCatalogKokoroExtensions**: the public `AddKokoroModels()` extension method on the
  Speech library's `SpeechModelCatalog`, registering this subsystem's one model in one call
- **OnnxKokoroEnglishSynthesisModel**: a real, concrete `ISynthesisModel`, backing the bare ONNX
  export `onnx-community/Kokoro-82M-v1.0-ONNX` (Apache-2.0), declaring 29 American/British
  English voices as a `ChoiceParameter`, supplying the shared English text analysis (`TextAnalyzer`) and
  owning the `models`-release bundle that carries the ONNX model graph and every voice's style-vector file
- **KokoroPhonemeVocabulary**: loads Kokoro v1.0's own character-level IPA phoneme vocabulary from
  an embedded copy of its `tokenizer.json`, and converts a phoneme string into the integer token
  id sequence the ONNX model expects

### Interfaces

The subsystem exposes `SpeechModelCatalogKokoroExtensions` and `OnnxKokoroEnglishSynthesisModel`
as its public API (`KokoroPhonemeVocabulary` is internal). It
consumes the Speech library's `SpeechModelCatalog`, `ISpeechModel`, `ISynthesisModel`, `ITextAnalyzer`,
`SpeechModelDownloadDescriptor`, `SpeechModelDownloadFile`, and parameter types from the *Speech
ModelManagementSubsystem Design*, `AudioFormat` from Speech's AudioSubsystem, the Text.En
`EnglishTextAnalyzer`, and the sibling SpeechOnnx system's `OnnxExecutionProviderSelector` (see *SpeechOnnx
OnnxRuntimeSubsystem Design*). `OnnxKokoroEnglishSynthesisModel`'s public `CreateBackend` implementation
constructs this system's own `OnnxKokoroSynthesisEngine` (see *SpeechOnnxKokoro SynthesisSubsystem Design*),
returned to the Speech library only as its engine-neutral, equally public `ISynthesisBackend` seam.

### Design

`SpeechModelCatalogKokoroExtensions.AddKokoroModels(this SpeechModelCatalog catalog,
IReadOnlyList<string>? preferredExecutionProviderNames = null)` is this subsystem's single public
entry point, for example `new SpeechModelCatalog().AddKokoroModels()`. It calls the Speech
library's `SpeechModelCatalog.AddModels(...)` once with a new
`OnnxKokoroEnglishSynthesisModel(preferredExecutionProviderNames)` instance and returns the same
catalog instance so registrations chain fluently. It throws `ArgumentNullException` for a null
catalog. The class lives in the `DemaConsulting.Speech.Onnx.Kokoro` namespace at the project
root, rather than inside this subsystem's folder, because it is the package's public composition
entry point rather than a model; it is traced to this subsystem because its only purpose is to
register this subsystem's one model. Per this repository's established pattern (mirroring
`SpeechModelCatalogSherpaExtensions`'s treatment in *SpeechSherpa ModelManagementSubsystem
Design*), this extension-method file has no separate unit-level design/reqstream/verification/
sysml2 file of its own; it is documented inline here, as a subsystem unit.

`OnnxKokoroEnglishSynthesisModel` declares its identity (`Id = "kokoro-onnx-v1_0-en"`,
`DisplayName`), `Role = Synthesis`, `LicenseName = "Apache-2.0"` with a canonical `LicenseUrl`, and
`AudioTagSupport = None` (Kokoro has no native inline Natural Language Audio Tag support, so the
library's default capability profile already provides generically correct behavior with no
bespoke override). It declares exactly one tunable parameter - a `voice` `ChoiceParameter` with 29
options spanning its verified American and British English voices, deliberately excluding the
model's remaining 25 non-English voices because this package's phonemization only handles English text.
The British voices speak American phonemes in their own voice quality (see *SpeechOnnxKokoro System Design*).
Its `DownloadDescriptor` names one HTTPS file: the SHA-256-pinned `kokoro-en-v1.0-int8-b1.zip`
bundle on the project's `models` GitHub release (the public static `Bundles` list). The bundle's
contents are 30 vendor files (the ONNX model graph plus 29 voice style-vector `.bin` files), each
with its original Hugging Face address, an independently-computed SHA-256 checksum and its own
relative install path. Its `InstallAsync` override delegates to Speech's
`SpeechModelBundle.InstallAsync`, which extracts the files, verifies each against its pin and
deletes the zip, so the installed layout is unchanged from the earlier per-file download.
`CreateBackend(installedModelDirectory)` builds the ONNX
Runtime session via `OnnxExecutionProviderSelector.Create`, reads every declared voice's
style-vector file into memory keyed by its resolved integer speaker id, and constructs
`OnnxKokoroSynthesisEngine` from the session, a `KokoroPhonemePipeline` over a fresh
`KokoroPhonemeVocabulary`, and the loaded voice styles. `ResolveSpeakerId` maps the selected
`voice` parameter value to its index in the model's own declared voice order, falling back to the
default voice's index for a null bag, a missing key, or an unrecognized value - never throwing.
See `onnx-kokoro-english-synthesis-model.md` for the full confirmed voice ordering and download
provenance.

**Text analysis.** The model's `TextAnalyzer` is one `EnglishTextAnalyzer` of Text.En, created on first use and
shared by every instance (the analysis loads trained data, so one instance keeps every model instance cheap).
It is configured with the normalization, reading and part-of-speech annotations: the spoken forms of numbers,
currency, dates and abbreviations, the reading of each heteronym (`record` the noun or the verb) and the tag of each
word, which the session hands to the annotated engine. The renderer uses the tag to keep a content word of a
function-word spelling stressed (`my will`, `the can`) where it would otherwise use the weak form.

`KokoroPhonemeVocabulary` loads an embedded copy of Kokoro v1.0's own `tokenizer.json`, parses its
`model.vocab` JSON object into a `char`-to-integer-id dictionary (confirmed to contain no
byte-pair-encoding or sub-word merging - unlike a typical text LLM tokenizer, this is a simple
per-character vocabulary), and exposes `MaxPhonemeTokens` (510, leaving room for the model's
leading/trailing pad token within its 512-token context window) and `PadTokenId` (0). Its
`ToTokenIds(phonemes)` method converts a phoneme string into the model's token id sequence,
dropping any character not present in the vocabulary (matching the proven Python reference
pipeline's own behavior exactly) and truncating to `MaxPhonemeTokens` entries; the `KokoroPhonemePipeline`
never hands it more than the limit, because it splits long text into parts first.
