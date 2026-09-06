### SherpaOnnxZipformerEnRecognitionModel

**Purpose**: Be the library's first real, concrete `IRecognitionModel`, backing sherpa-onnx's
streaming Zipformer2 transducer model `streaming-zipformer-en-2023-06-26` (converted from
k2-fsa/icefall's LibriSpeech-trained streaming Zipformer checkpoint).

**Data Model**: No instance state - a stateless declaration of this one model's identity,
download descriptor, and engine configuration. `ModelId` = `streaming-zipformer-en-2023-06-26`.
`DownloadDescriptor` declares exactly one file: the model's own `.tar.bz2` archive from
`k2-fsa/sherpa-onnx`'s `asr-models` GitHub Releases tag (310,414,022 bytes, SHA-256
`639e25b578e9e997131402199419c13a941f8e4e198e2da1ce57dbf5cf401282`, independently confirmed
against a real download in this project's development sandbox, not fabricated).

**Key Methods**:

- **Id / DisplayName / Role / Parameters / AudioTagSupport**: fixed values - `Role =
  Recognition`, no tunable parameters, `AudioTagSupport = None` (Natural Language Audio Tags are
  a synthesis-input concept, not applicable to recognition output).
- **DownloadDescriptor**: the single-file `.tar.bz2` archive descriptor described above.
- **InstallAsync(stagedFilesDirectory, cancellationToken)**: delegates to the shared
  `TarBz2ArchiveExtractor` to extract the downloaded archive in place and delete it, leaving the
  archive's own top-level folder (`sherpa-onnx-streaming-zipformer-en-2023-06-26/`) containing
  every file `CreateEngineConfig` references.
- **IRecognitionModel.AudioFormat** *(public explicit interface member)*: mono `16000` Hz, the
  rate this model was trained at.
- **IRecognitionModel.CreateEngineConfig(installedModelDirectory)** *(internal)*: builds an
  `OnlineRecognizerConfig` wiring `OnlineModelConfig.Transducer.Encoder`/`Decoder`/`Joiner` to
  this model's **int8-quantized** files (not the same archive's fp32 files, for a leaner default
  download) plus `Tokens`, with `ModelType = "zipformer2"`, `DecodingMethod =
  "greedy_search"`, `EnableEndpoint = 1`, endpointing tuned via `Rule1MinTrailingSilence = 2.4f`,
  `Rule2MinTrailingSilence = 1.2f`, and `Rule3MinUtteranceLength = 20f` (seconds of trailing
  silence/decoded audio at which the streaming decoder considers an utterance to have ended), and
  16 kHz/80-dim feature config. This exact
  configuration was proven end-to-end in this project's development sandbox: a real WAV file fed
  through the real `SherpaOnnxRecognitionEngine` using this model produced an exact word-for-word
  transcript match against the model's own published `test_wavs/trans.txt` ground truth.
- **IRecognitionModel.NormalizeText(text, isFinal)** *(explicit interface override)*: delegates to
  the shared `UppercaseTranscriptRestorer` (`RestoreFinal` when `isFinal`, `RestoreProvisional`
  otherwise). Added because this model's raw output was empirically confirmed - by synthesizing
  real audio with the shipped LibriTTS voice and feeding it through the real
  `SherpaOnnxRecognitionEngine` - to be UPPERCASE and unpunctuated (for example, `"I DON'T THINK
  THAT'S WORKING AND I CAN'T TELL WHY IT ISN'T"`), so `SherpaOnnxSpeechRecognizer` now surfaces
  readable, cased, punctuated prose instead of raw shouted output.
- **IRecognitionModel.PostEndpointWarmupWindowMs** *(internal)*: not overridden - this model
  deliberately keeps `IRecognitionModel`'s disabled `0` default. Investigated, not merely assumed:
  forcing the post-endpoint warm-up-replay feature on for this model produced genuine duplicated
  text (for example "THAT THAT IS THE QUESTION") on a real recording at every tested window from
  500ms upward, because a replayed word's still-forming onset can be committed as a token by this
  model's transducer decoder even while its `GetResult()` output is suppressed (see
  `.agent-logs/planning-nemotron-endpoint-word-loss-warmup-replay-fix-9d4b71.md`). This model has
  also shown no reproduction of the word-loss defect the feature exists to fix. Both facts make
  the disabled default the correct, permanent choice for this model, not a placeholder.

**Error Handling**: `InstallAsync` and `CreateEngineConfig` throw `ArgumentException` for a null
or empty directory argument, matching every other unit in this subsystem's validation
convention. Any extraction failure inside `TarBz2ArchiveExtractor` propagates unchanged and is
handled by `SpeechModelDownloader`, identically to a download failure.

**License**: Apache-2.0 - rated LIKELY, not independently confirmed against an explicit
model-specific `LICENSE`/model-card file, since `huggingface.co` (the model's origin,
`Zengwei/icefall-asr-librispeech-streaming-zipformer-2023-05-17`) is unreachable from this
project's development sandboxes. Corroborated by k2-fsa/icefall's own repository license
(Apache-2.0, confirmed by directly reading
`https://raw.githubusercontent.com/k2-fsa/icefall/master/LICENSE`) and by independent web-search
corroboration; a maintainer with access to the HuggingFace model card should re-confirm before
this model is relied upon in a context requiring a legally certain license determination.

**Dependencies**: `IRecognitionModel`, `SpeechModelDownloadDescriptor`, `SpeechModelDownloadFile`,
`TarBz2ArchiveExtractor`, `UppercaseTranscriptRestorer`, `org.k2fsa.sherpa.onnx`'s
`OnlineRecognizerConfig`.

**Callers**: `SpeechModelCatalog.KnownModels` (registers this instance);
`SpeechModelDownloader` (invokes `InstallAsync` after checksum verification);
`SherpaOnnxRecognitionEngine` (consumes the internal `CreateEngineConfig` member and the public
`AudioFormat` declaration through the RecognitionSubsystem).
