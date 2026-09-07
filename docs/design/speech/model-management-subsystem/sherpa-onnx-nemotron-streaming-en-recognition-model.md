### SherpaOnnxNemotronStreamingEnRecognitionModel

**Purpose**: Be the library's second real, concrete `IRecognitionModel`, backing sherpa-onnx's
NVIDIA Nemotron cache-aware FastConformer-RNNT streaming transducer model
`nemotron-speech-streaming-en-0.6b-560ms-int8-2026-04-25` (int8-quantized, ~560ms chunk latency).

**Data Model**: No instance state - a stateless declaration of this one model's identity,
download descriptor, and engine configuration. `ModelId` =
`nemotron-speech-streaming-en-0.6b-560ms-int8-2026-04-25`. `DownloadDescriptor` declares exactly
one file: the model's own `.tar.bz2` archive from `k2-fsa/sherpa-onnx`'s `asr-models` GitHub
Releases tag (463,945,051 bytes, SHA-256
`78e2b79fcf7271553a74402a76b771b09ea40117a39566a79f52235b23db6358`, independently confirmed
against a real download in this project's development sandbox, not fabricated).

**Key Methods**:

- **Id / DisplayName / Role / Parameters / AudioTagSupport**: fixed values - `Role =
  Recognition`, no tunable parameters, `AudioTagSupport = None`. `DisplayName` names only the
  model itself (`"NVIDIA Nemotron English (Streaming, 560ms)"`) and no longer embeds license
  text - the license distinction is now carried structurally by `LicenseName`/`LicenseUrl` below,
  discoverable without a caller needing to parse `DisplayName` or read XML documentation.
- **LicenseName / LicenseUrl**: `LicenseName = "NVIDIA Open Model License"` and `LicenseUrl`
  pointing at NVIDIA's own published open model license agreement URL (see the "License"
  section below), so the license distinction is visible anywhere the demo app's catalog/settings
  UI renders this model, and programmatically discoverable by any host.
- **DownloadDescriptor**: the single-file `.tar.bz2` archive descriptor described above, pointing
  at sherpa-onnx's/k2-fsa's own official GitHub Releases mirror - never a redistribution of the
  model bytes by this project itself.
- **InstallAsync(stagedFilesDirectory, cancellationToken)**: delegates to the shared
  `TarBz2ArchiveExtractor`, identically to `SherpaOnnxZipformerEnRecognitionModel`.
- **IRecognitionModel.AudioFormat** *(public explicit interface member)*: mono `16000` Hz (the
  common convention for this model family; not independently confirmed against an unreachable
  HuggingFace model card).
- **IRecognitionModel.CreateEngineConfig(installedModelDirectory)** *(internal)*: builds an
  `OnlineRecognizerConfig` wiring `OnlineModelConfig.Transducer.Encoder`/`Decoder`/`Joiner` to
  this model's `encoder.int8.onnx`/`decoder.int8.onnx`/`joiner.int8.onnx` files (int8-only - this
  model ships no fp32 variant) plus `Tokens`, with `DecodingMethod = "greedy_search"` and
  `EnableEndpoint = 1`. **Deliberately leaves `ModelConfig.ModelType` unset**: tracing
  sherpa-onnx's own native dispatch source (`OnlineRecognizerImpl::Create` in
  `online-recognizer-impl.cc`) confirms the native library selects the correct
  NeMo-cache-aware decoding path automatically, by inspecting metadata baked into the decoder
  `.onnx` file itself at load time - the one structural difference from
  `SherpaOnnxZipformerEnRecognitionModel`'s `CreateEngineConfig`.
- **IRecognitionModel.NormalizeText(text, isFinal)**: not overridden - this model keeps
  `IRecognitionModel`'s pass-through default. This was decided empirically, not assumed:
  synthesizing real audio with the shipped LibriTTS voice and feeding it through the real
  `SherpaOnnxRecognitionEngine` showed this model's raw output already reads as normal prose
  (mixed case, sentence-initial word capitalized, contractions already apostrophized - for
  example, `"Don't think that's working and I can't tell why it isn't"`), unlike
  `SherpaOnnxZipformerEnRecognitionModel`'s UPPERCASE "yelling" raw output. The only gap observed
  was missing terminal punctuation, outside the "yelling" style `UppercaseTranscriptRestorer`
  targets.
- **IRecognitionModel.PostEndpointWarmupWindowMs** *(internal)*: overridden to `800` (ms) -
  the only model in the catalog to opt into `SherpaOnnxRecognitionEngine`'s post-endpoint
  warm-up-replay feature (see the RecognitionSubsystem design chapter for the full mechanism).
  This model's streaming encoder was confirmed to have a ~550ms warm-up blackout after `Reset()`
  during which genuinely spoken audio can be silently lost, and its endpoint detector was
  confirmed to fire as a false positive mid-utterance on real recordings - a defect
  `SherpaOnnxZipformerEnRecognitionModel` never exhibited on the same recordings. `800` was
  chosen empirically (see the Error Handling/verification evidence below): a window-size sweep
  from 400-1500ms found 600ms the minimum that reliably recovers the previously-dropped words,
  with no duplication observed at any size up to 1500ms, so 800ms leaves comfortable margin above
  the minimum without approaching where any issue was observed. `Rule1MinTrailingSilence`/
  `Rule2MinTrailingSilence`/`Rule3MinUtteranceLength` above remain unchanged - this fix decouples
  "when reset fires" from "whether audio is lost across it" rather than tuning endpoint
  sensitivity. **Replay is only attempted by the shared engine when genuine recognized text has
  occurred since the stream's most recent reset** - a regression fix for a leading/inter-utterance-
  silence corruption defect found and resolved after this value was first selected (see the
  RecognitionSubsystem design chapter's "Replay eligibility is gated on genuine recognized text
  since the last reset" section for the full mechanism and its resolution); the `800` value itself
  is unaffected by that fix.

**Error Handling**: Identical to `SherpaOnnxZipformerEnRecognitionModel` - `ArgumentException` for
a null/empty directory argument on `InstallAsync`/`CreateEngineConfig`; extraction failures
propagate unchanged to `SpeechModelDownloader`.

**License - materially different from every other model in this pass**: the underlying NVIDIA
model (`nvidia/nemotron-speech-streaming-en-0.6b` on HuggingFace) is released under the **NVIDIA
Open Model License**, a custom, NVIDIA-authored, non-OSI license - *not* Apache-2.0 or any other
widely-used permissive OSS license like the Zipformer model above. Confirmed via web-search
citing the model's own published HuggingFace README (`huggingface.co` itself is unreachable from
this project's development sandboxes for a first-hand read). This fact is now also exposed
structurally, not only in this prose, via `LicenseName = "NVIDIA Open Model License"` and
`LicenseUrl = https://www.nvidia.com/en-us/agreements/enterprise-software/nvidia-open-model-license/`
(the same URL cited above). This project does **not** bundle or
redistribute NVIDIA's model weights: this class stores only metadata (identity, description,
capability profile) and a `SpeechModelDownloadDescriptor` pointing at sherpa-onnx's own official
GitHub Releases mirror; the actual model bytes are fetched by the consumer's own machine, on
their own explicit `SpeechModelCatalog.DownloadAsync` call, exactly like every other model in
this catalog. Whether the license's field-of-use/redistribution terms permit a given consumer's
specific intended use is a legal/compliance question outside this library's competence to
certify - flagged as Risk #1 in the phase 7 planning report
(`.agent-logs/planning-speech-phase7-c48a07.md`) and not resolved by this class's existence, which
is a build-time metadata/download registration only.

**Dependencies**: `IRecognitionModel`, `SpeechModelDownloadDescriptor`, `SpeechModelDownloadFile`,
`TarBz2ArchiveExtractor`, `org.k2fsa.sherpa.onnx`'s `OnlineRecognizerConfig`.

**Callers**: `SpeechModelCatalog.KnownModels` (registers this instance);
`SpeechModelDownloader` (invokes `InstallAsync` after checksum verification);
`SherpaOnnxRecognitionEngine` (consumes the internal `CreateEngineConfig` member and the public
`AudioFormat` declaration through the RecognitionSubsystem).
