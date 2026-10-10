## ModelManagementSubsystem Design

![ModelManagementSubsystem Structure](KokoroModelManagementSubsystemView.svg)

### Overview

The SpeechOnnxKokoro ModelManagementSubsystem supplies this package's one concrete,
ONNX-Runtime-backed speech model as an implementation of the Speech library's `ISynthesisModel`
extension point (see _Speech ModelManagementSubsystem Design_), together with the two supporting
units that give this model its own complete text-to-phoneme-to-token pipeline - a responsibility
the sibling SpeechSherpa system does not need, because sherpa-onnx's native library bundles
phonemization itself. It contains the following units:

- **SpeechModelCatalogKokoroExtensions**: the public `AddKokoroModels()` extension method on the
  Speech library's `SpeechModelCatalog`, registering this subsystem's one model in one call
- **OnnxKokoroEnglishSynthesisModel**: a real, concrete `ISynthesisModel`, backing the bare ONNX
  export `onnx-community/Kokoro-82M-v1.0-ONNX` (Apache-2.0), declaring 29 American/British
  English voices as a `ChoiceParameter` and owning the download descriptor for the ONNX model
  graph and every voice's style-vector file
- **KokoroLexiconPhonemizer**: converts English text into Kokoro v1.0's own IPA phoneme string by
  looking each word up in an embedded, precomputed word-to-phoneme lexicon, rather than
  reimplementing grapheme-to-phoneme (G2P) linguistics by hand
- **KokoroPhonemeVocabulary**: loads Kokoro v1.0's own character-level IPA phoneme vocabulary from
  an embedded copy of its `tokenizer.json`, and converts a phoneme string into the integer token
  id sequence the ONNX model expects

### Interfaces

The subsystem exposes `SpeechModelCatalogKokoroExtensions` and `OnnxKokoroEnglishSynthesisModel`
as its public API (`KokoroLexiconPhonemizer` and `KokoroPhonemeVocabulary` are internal). It
consumes the Speech library's `SpeechModelCatalog`, `ISpeechModel`, `ISynthesisModel`,
`SpeechModelDownloadDescriptor`, `SpeechModelDownloadFile`, and parameter types from the _Speech
ModelManagementSubsystem Design_, `AudioFormat` from Speech's AudioSubsystem, and the sibling
SpeechOnnx system's `OnnxExecutionProviderSelector` (see _SpeechOnnx OnnxRuntimeSubsystem
Design_). `OnnxKokoroEnglishSynthesisModel`'s public `CreateBackend` implementation constructs
this system's own `OnnxKokoroSynthesisEngine` (see _SpeechOnnxKokoro SynthesisSubsystem Design_),
returned to the Speech library only as its engine-neutral, equally public `ISynthesisBackend`
seam.

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
`SpeechModelCatalogSherpaExtensions`'s treatment in _SpeechSherpa ModelManagementSubsystem
Design_), this extension-method file has no separate unit-level design/reqstream/verification/
sysml2 file of its own; it is documented inline here, as a subsystem unit.

`OnnxKokoroEnglishSynthesisModel` declares its identity (`Id = "kokoro-onnx-v1_0-en"`,
`DisplayName`), `Role = Synthesis`, `LicenseName = "Apache-2.0"` with a canonical `LicenseUrl`, and
`AudioTagSupport = None` (Kokoro has no native inline Natural Language Audio Tag support, so the
library's default capability profile already provides generically correct behavior with no
bespoke override). It declares exactly one tunable parameter - a `voice` `ChoiceParameter` with 29
options spanning its verified American and British English voices, deliberately excluding the
model's remaining 25 non-English voices because this class's embedded `KokoroLexiconPhonemizer`
only phonemizes English text. Its `DownloadDescriptor` names 30 individual HTTPS files (the ONNX
model graph plus 29 voice style-vector `.bin` files), each with an independently-computed SHA-256
checksum and its own relative install path - unlike SpeechSherpa's archive-based models, none of
these files needs post-download extraction, so this class needs no `InstallAsync` override at all
(the interface's no-op default applies). `CreateBackend(installedModelDirectory)` builds the ONNX
Runtime session via `OnnxExecutionProviderSelector.Create`, reads every declared voice's
style-vector file into memory keyed by its resolved integer speaker id, and constructs
`OnnxKokoroSynthesisEngine` from the session, a fresh `KokoroPhonemeVocabulary`, a fresh
`KokoroLexiconPhonemizer`, and the loaded voice styles. `ResolveSpeakerId` maps the selected
`voice` parameter value to its index in the model's own declared voice order, falling back to the
default voice's index for a null bag, a missing key, or an unrecognized value - never throwing.
See `onnx-kokoro-english-synthesis-model.md` for the full confirmed voice ordering and download
provenance.

`KokoroLexiconPhonemizer` loads an embedded, gzip-compressed TSV lexicon (precomputed offline by
running the real `misaki.en.G2P` tool - the same phonemizer Kokoro's own reference pipeline uses -
over every single-pronunciation, purely-alphabetic word in the public-domain CMUdict word list)
once at construction, into an in-memory dictionary. Its `Phonemize(text)` method tokenizes input
text, after the internal `KokoroTextNormalizer` helper has spoken numbers, currency, times,
markdown, URLs and abbreviations, into words, whitespace runs, and other single characters;
resolves each word by lexicon hit or a spelling correction, appending its phoneme string; records
a word with no near match as an unknown word and omits it; collapses whitespace to a single
vocabulary space character; and passes through a closed set of punctuation characters Kokoro's own
vocabulary declares. It never guesses a pronunciation for a word with no near match - those words
are omitted and reported, while corrected or near-match words are spoken and not reported. This
is an honest, documented Stage-1 limitation, as is its single, no-context pronunciation per
spelling (affecting homographs such as "read"/"lead").

`KokoroPhonemeVocabulary` loads an embedded copy of Kokoro v1.0's own `tokenizer.json`, parses its
`model.vocab` JSON object into a `char`-to-integer-id dictionary (confirmed to contain no
byte-pair-encoding or sub-word merging - unlike a typical text LLM tokenizer, this is a simple
per-character vocabulary), and exposes `MaxPhonemeTokens` (510, leaving room for the model's
leading/trailing pad token within its 512-token context window) and `PadTokenId` (0). Its
`ToTokenIds(phonemes)` method converts a phoneme string into the model's token id sequence,
dropping any character not present in the vocabulary (matching the proven Python reference
pipeline's own behavior exactly) and truncating to `MaxPhonemeTokens` entries.
