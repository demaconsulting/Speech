### OnnxNemotronMultilingualRecognitionModel

**Purpose**: Implement the Speech library's `IRecognitionModel` extension point for NVIDIA
Nemotron 3.5 ASR streaming (0.6B, int4 ONNX export), run directly through ONNX Runtime with no
bundled native inference engine - unlike the sibling `DemaConsulting.Speech.Sherpa` package's
sherpa-onnx-backed Nemotron model. It declares the model's identity, license, language
parameter, and download descriptor, and builds the recognition backend from an installed copy.

**Data Model**: Holds only the optional ordered list of preferred execution provider names
(`_preferredExecutionProviderNames`); the model is otherwise a stateless declaration. Constants:
`ModelId = "nemotron-3.5-asr-streaming-0.6b-onnx-int4"`, `LanguageParameterId = "language"`,
`DefaultLanguage = "en-US"`, and a private ordered table of the declared locales (`en-US`
"English (US)", `en-GB` "English (UK)").

**Key Members**:

- **Id / DisplayName / Role / AudioTagSupport / AudioFormat**: `Id` equals `ModelId`, which is
  also the download mirror's one folder name (mirror layout `<mirror>/<modelId>/<file>`);
  `Role = Recognition`; `AudioTagSupport = None`; `AudioFormat` is mono 16000 Hz.
- **LicenseName / LicenseUrl**: `"OpenMDW-1.1"` with `https://openmdw.ai/license/1-1/`, the
  license of the model weights stated on the upstream model card of
  `nvidia/nemotron-3.5-asr-streaming-0.6b` (checked on the live card on 2026-11-21). The files
  are the `onnx-community` int4 ONNX conversion, downloaded unchanged; that repository's MIT tag
  covers only the conversion artifacts, and the weights remain under the OpenMDW License.
- **Parameters**: exactly one `ChoiceParameter` with id `language`, options `en-US` (default) and
  `en-GB`.
- **DownloadDescriptor**: eleven HTTPS files served from the repository's `resolve/main` path,
  each with a SHA-256 checksum computed against the downloaded file and an install path equal to
  its file name: `encoder.onnx` (2,677,548 bytes), `encoder.onnx.data` (690,089,984),
  `decoder.onnx` (4,696), `decoder.onnx.data` (59,785,216), `joint.onnx` (2,136),
  `joint.onnx.data` (37,830,656), `vocab.txt` (64,024), `tokenizer.json` (642,525),
  `tokenizer_config.json` (183), `genai_config.json` (1,892), and `audio_processor_config.json`
  (413) - about 790 MB in total. Each `.onnx.data` file must sit beside its `.onnx` under the
  identical base name for ONNX Runtime to find it. No file needs extraction, so no
  `InstallAsync` override exists.
- **CreateBackend(installedModelDirectory[, parameterValues])**: loads `vocab.txt` through
  `NemotronVocabulary.Load`; resolves the selected language (missing or unrecognized falls back
  to `en-US`) and its `lang_id` through `TryGetLanguageId`, throwing `InvalidOperationException`
  when the vocabulary does not declare the locale; creates the encoder session through
  `OnnxExecutionProviderSelector.Create` with `NemotronEncoder.RunProbeInference` as the probe;
  creates the decoder and joint sessions on the CPU from `NemotronRnntNetwork.CreateSessionOptions()`
  (`IntraOpNumThreads = 1` and `session.intra_op.allow_spinning=0`, because those microsecond-scale
  calls are slower with a thread pool); and constructs `OnnxNemotronRecognitionEngine` from a
  `NemotronEncoder`, a `NemotronRnntGreedyDecoder` over a `NemotronRnntNetwork`, and the
  vocabulary. If any step fails, the sessions already created are disposed before the exception
  propagates, since no engine exists yet to own them. The `IRecognitionModel.CreateBackend`
  overload without a parameter bag uses the default language, and the public overload with a
  parameter bag uses the default engine options.
- **CreateBackend(installedModelDirectory, parameterValues, engineOptions)** (internal): the
  overload both public overloads delegate to. It validates its arguments before reading any file:
  `ArgumentException` for a null or empty directory, `ArgumentNullException` for null options,
  and `ArgumentOutOfRangeException` (through `NemotronRnntNetwork.ValidateBlankPenalty`) for a
  negative or non-finite `BlankPenalty`. It passes `engineOptions.BlankPenalty` to the
  `NemotronRnntNetwork` and the whole options record to the engine. It is internal so the engine
  constants stay out of the public API; the assembly's `InternalsVisibleTo` entry for the
  non-shipped evaluation tool (`DemaConsulting.Speech.SttEvaluation`) lets the tool measure
  alternative settings, such as the blank penalty or the endpoint rules, on the real model.

**Error Handling**: `CreateBackend` throws `ArgumentException` for a null or empty installed
directory, `ArgumentNullException` for null engine options, `ArgumentOutOfRangeException` for an
invalid blank penalty or other invalid engine option, and propagates
`FileNotFoundException`/`DirectoryNotFoundException` and ONNX Runtime exceptions for missing or
unusable files; the Speech library's `SpeechRecognizerFactory` degrades any such exception to the
honest unavailable recognizer.

**Execution Providers**: the encoder honors the preferred execution provider names with a probe
inference and CPU fallback; only CPU execution has been verified - DirectML and other GPU
execution providers could not be verified in the development environment. The decoder and joint
networks always run on the CPU.

**Dependencies**: `Microsoft.ML.OnnxRuntime`; `OnnxExecutionProviderSelector` from the sibling
SpeechOnnx system; `NemotronVocabulary`; `OnnxNemotronRecognitionEngine`, `NemotronEncoder`,
`NemotronRnntGreedyDecoder`, `NemotronRnntNetwork`, and `NemotronEngineOptions` from the
RecognitionSubsystem; and the Speech library's `IRecognitionModel`, parameter, and
download-descriptor types.

**Callers**: `SpeechModelCatalogNemotronSttExtensions.AddNemotronSttModels` constructs and registers the
instance; the Speech library's `SpeechRecognizerFactory` and `SpeechModelDownloader` consume it
through the generic model contract; the non-shipped ModelDownloader and evaluation tools construct
it directly (the evaluation tool also through the internal options overload).
