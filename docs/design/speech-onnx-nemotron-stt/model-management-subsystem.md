## ModelManagementSubsystem Design

![ModelManagementSubsystem Structure](NemotronModelManagementSubsystemView.svg)

### Overview

The SpeechOnnxNemotronStt ModelManagementSubsystem supplies this package's one concrete,
ONNX-Runtime-backed speech model as an implementation of the Speech library's `IRecognitionModel`
extension point (see *Speech ModelManagementSubsystem Design*), together with the vocabulary unit
that gives the model its language identifier, blank token id, and detokenization. It contains the
following units:

- **SpeechModelCatalogNemotronSttExtensions**: the public `AddNemotronSttModels()` extension method on
  the Speech library's `SpeechModelCatalog`, registering this subsystem's one model in one call
- **OnnxNemotronMultilingualRecognitionModel**: a real, concrete `IRecognitionModel` for the
  `onnx-community/nemotron-3.5-asr-streaming-0.6b-onnx-int4` export, declaring a `language`
  `ChoiceParameter` and owning the `models`-release bundle that carries the model's eleven files
- **NemotronVocabulary**: loads `vocab.txt`, derives the language id and the blank id, tells
  whether a token starts a word, and detokenizes emitted token ids into text

### Interfaces

The subsystem exposes `SpeechModelCatalogNemotronSttExtensions` and
`OnnxNemotronMultilingualRecognitionModel` as its public API (`NemotronVocabulary` is internal).
It consumes the Speech library's `SpeechModelCatalog`, `ISpeechModel`, `IRecognitionModel`,
`SpeechModelDownloadDescriptor`, `SpeechModelDownloadFile`, `SpeechModelBundle`, and parameter types from the *Speech
ModelManagementSubsystem Design*, `AudioFormat` from Speech's AudioSubsystem, and the sibling
SpeechOnnx system's `OnnxExecutionProviderSelector` (see *SpeechOnnx OnnxRuntimeSubsystem
Design*). `OnnxNemotronMultilingualRecognitionModel`'s public `CreateBackend` implementation
constructs this system's own `OnnxNemotronRecognitionEngine` (see *SpeechOnnxNemotronStt
RecognitionSubsystem Design*), returned to the Speech library only as its engine-neutral, equally
public `IRecognitionBackend` seam.

### Design

`SpeechModelCatalogNemotronSttExtensions.AddNemotronSttModels(this SpeechModelCatalog catalog,
IReadOnlyList<string>? preferredExecutionProviderNames = null)` is this subsystem's single public
entry point, for example `new SpeechModelCatalog().AddNemotronSttModels()`. It calls the Speech
library's `SpeechModelCatalog.AddModels(...)` once with a new
`OnnxNemotronMultilingualRecognitionModel(preferredExecutionProviderNames)` instance and returns
the same catalog instance so registrations chain fluently. It throws `ArgumentNullException` for
a null catalog. The class lives in the `DemaConsulting.Speech.Onnx.NemotronStt` namespace at the
project root, rather than inside this subsystem's folder, because it is the package's public
composition entry point rather than a model; it is traced to this subsystem because its only
purpose is to register this subsystem's one model. Per this repository's established pattern
(mirroring `SpeechModelCatalogKokoroExtensions` and `SpeechModelCatalogSherpaExtensions`), this
extension-method file has no separate unit-level design/reqstream/verification/sysml2 file of its
own; it is documented inline here, as a subsystem unit.

`OnnxNemotronMultilingualRecognitionModel` declares its identity
(`Id = "nemotron-3.5-asr-streaming-0.6b-onnx-int4"`, which equals the download mirror's folder
name, and `DisplayName`), `Role = Recognition`, `AudioTagSupport = None`, and a mono 16000 Hz audio
format. It declares exactly one tunable parameter, a `language` `ChoiceParameter` with `en-US`
(default) and `en-GB`; an unrecognized or missing selection degrades to `en-US`. Its
`DownloadDescriptor` names one HTTPS file, the SHA-256-pinned
`nemotron-3.5-asr-streaming-0.6b-20261011-int4-b1.zip` bundle on the project's `models` GitHub release
(the public static `Bundles` list). The bundle carries eleven vendor files, each pinned with an
independently computed SHA-256 checksum and installed at a path equal to its file name, so each
`.onnx.data` external-data file sits beside its `.onnx` under the identical base name as ONNX Runtime
requires; the model's `InstallAsync` override extracts and verifies them through Speech's
`SpeechModelBundle.InstallAsync`. See
`onnx-nemotron-multilingual-recognition-model.md` for the full detail, including the license note
(OpenMDW-1.1) and the internal `CreateBackend` overload that takes explicit engine options.

`NemotronVocabulary` reads `vocab.txt` (one token per line, line index equals token id) and
exposes the blank id, the locale-to-language-id lookup, the word-start test `StartsWord` (used by
the recognition engine to split an over-long utterance only between words), and detokenization;
see `nemotron-vocabulary.md`.
