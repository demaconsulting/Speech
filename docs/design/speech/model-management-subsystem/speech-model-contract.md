### SpeechModelContract (ISpeechModel, IRecognitionModel, ISynthesisModel)

**Purpose**: Define the common per-model contract every model's backing class implements
(identity, role, declared parameters, declared audio-tag support, download descriptor), with two
role-specific interfaces. `IRecognitionModel` was extended in Phase 3 with the internal
engine-construction members the RecognitionSubsystem needs. `ISynthesisModel` is extended in
Sub-phase 4b with the equivalent members the SynthesisSubsystem needs, exactly as this contract's
Sub-phase 2b documentation anticipated.

**Data Model**: N/A (interfaces only).

**Key Methods**:

- **ISpeechModel.Id / DisplayName**: the model's stable identifier and display name.
- **ISpeechModel.Role**: which speech capability the model provides (`SpeechModelRole`).
- **ISpeechModel.Parameters**: the model's declared `ISpeechModelParameter` instances.
- **ISpeechModel.AudioTagSupport**: the model's declared `SpeechModelAudioTagSupport`.
- **ISpeechModel.DownloadDescriptor**: the `SpeechModelDownloadDescriptor` describing the file(s)
  to download to install this model.
- **ISpeechModel.InstallAsync(stagedFilesDirectory, cancellationToken)**: unpacks this model's
  verified, downloaded file(s) in place before `SpeechModelStore` atomically promotes the staging
  directory to `current/`. Defaults to a no-op (`Task.CompletedTask`), matching the common case of
  a model whose single downloaded file needs no unpacking; a model whose declared payload is an
  archive (zip/tar) overrides this to expand it and remove the original archive file. Invoked by
  `SpeechModelDownloader`'s `ISpeechModel`-aware `DownloadAsync` overload after checksum
  verification and before the atomic swap.
- **ISpeechModel.NormalizeText(text)**: applies this model's own text normalization/correction
  before inference. Defaults to the identity function. `SherpaOnnxSpeechSynthesizer` calls this
  hook before Layer 1 tag parsing, so a synthesis model may correct punctuation or spelling
  without the SynthesisSubsystem needing to know how.
- **IRecognitionModel** / **ISynthesisModel**: `: ISpeechModel`. `IRecognitionModel` adds two
  `internal` members in Phase 3 (see below), plus a public `NormalizeText(text, isFinal)` default
  hook in Phase 12 (see below). `ISynthesisModel` adds two `internal` members in Sub-phase 4b
  (see below).
- **IRecognitionModel.SampleRate** *(internal)*: the sample rate, in Hz, this model's recognition
  engine requires its input audio at. Declared per model rather than assumed, because streaming
  models are trained at a fixed feature rate and produce unusable results at any other; this is
  what lets the RecognitionSubsystem resample whatever rate a capture device resolved into what
  this specific model needs.
- **IRecognitionModel.CreateEngineConfig(installedModelDirectory)** *(internal)*: builds the
  sherpa-onnx streaming-recognizer configuration for this model, combining the model's own
  compiled-in relative file names with the directory its verified files were installed into.
  Pure with respect to library state: it allocates no native resources and never loads the model,
  so a missing native runtime or unusable model file fails in the RecognitionSubsystem (where it
  degrades to an honest unavailable recognizer) rather than here. Throws `ArgumentException` for a
  null or empty directory.
- **IRecognitionModel.NormalizeText(text, isFinal)**: a recognition-only hook, distinct from
  `ISpeechModel.NormalizeText(text)` above (that one runs before synthesis inference; this one
  runs on a model's own recognition output). Defaults to forwarding to
  `ISpeechModel.NormalizeText(text)` (a plain pass-through, unless a model separately overrides
  that shared method too). `isFinal` lets an override apply cheap, non-damaging normalization to
  a still-forming provisional result and full restoration only to a committed final result.
  `SherpaOnnxSpeechRecognizer` calls this hook on every decoded result's text before raising it,
  so a model whose raw output needs correction (for example,
  `SherpaOnnxZipformerEnRecognitionModel`'s UPPERCASE, unpunctuated raw output, restored via the
  shared `UppercaseTranscriptRestorer`) can do so without the RecognitionSubsystem needing to know
  how.
- **IRecognitionModel.PostEndpointWarmupWindowMs** *(internal)*: the duration, in milliseconds,
  of pre-endpoint audio the recognition engine should buffer and silently replay into a freshly
  reset stream immediately after an endpoint fires, to pre-warm the model's internal decoding
  state before genuinely new (post-pause) audio arrives. Defaults to `0` (feature disabled; a
  hard `Reset()` with no replay, matching every model's original behavior). This is a deliberate
  opt-in, not a global default: it exists to fix a confirmed post-`Reset()` warm-up word-loss
  defect specific to `SherpaOnnxNemotronStreamingEnRecognitionModel`, and forcing it on for a
  model like `SherpaOnnxZipformerEnRecognitionModel` was found to cause duplicated-text
  regressions, so only a model with its own independently confirmed defect should override it.
- **ISynthesisModel.CreateEngineConfig(installedModelDirectory)** *(internal)*: builds the
  sherpa-onnx offline-TTS configuration for this model, combining the model's own compiled-in
  relative file names with the directory its verified files were installed into. Pure with
  respect to library state for the same reasons as its recognition-direction counterpart above:
  it allocates no native resources and never loads the model, so a missing native runtime or
  unusable model file fails in the SynthesisSubsystem (where it degrades to an honest unavailable
  synthesizer) rather than here.
- **ISynthesisModel.CapabilityProfile** *(internal)*: the `IModelCapabilityProfile` this model
  uses to render Natural Language Audio Tags into a `SpeechPlan`. Defaults to
  `DefaultModelCapabilityProfile.Instance`, a stateless singleton driven purely by
  `AudioTagSupport`/`Parameters`, so a model needs zero code to get generically-correct tag
  rendering; a model may override this hook only when it needs bespoke, non-generic rendering
  its native engine supports.
- **ISynthesisModel.ResolveSpeakerId(parameterValues)** *(internal)*: resolves a session-level,
  untyped key-value parameter bag (for example built from a host's settings UI via a declared
  `ChoiceParameter`) to the sherpa-onnx integer speaker id to synthesize with. Defaults to `0`,
  identical to every model's previous hard-coded behavior. Added so a multi-speaker model
  (starting with `SherpaOnnxKokoroEnglishSynthesisModel`) can own its own string-to-int voice
  mapping entirely inside its own backing class; implementations must never throw — an
  unrecognized or missing selection must degrade to a sensible default speaker id rather than
  fault synthesis.

Every `internal` member is deliberately not public. architecture.md scopes the "must not leak
sherpa-onnx types" constraint to `ISpeechRecognizer`/`ISpeechSynthesizer`, and makes each model's
backing class responsible for "sherpa-onnx configuration for its own model architecture", so
returning a real recognizer/synthesizer configuration here is consistent with the approved
design. Keeping the members internal leaves this interface's public surface unchanged, keeps
every sherpa-onnx type out of the library's public API, and means only the library and its test
project can implement the interface - an intentional restriction matching architecture.md's "one
backing class per model; a new model requires a new library release" decision.
`ISynthesisModel.CapabilityProfile` is internal for the same reason even though
`IModelCapabilityProfile` itself carries no sherpa-onnx type, so the whole Layer 2 rendering seam
stays a library-internal extension point rather than a public one a host could otherwise be
tempted to implement directly against an unstable contract.

**Error Handling**: N/A - a pure contract; each implementation's own construction/validation
rules apply. An exception thrown from a model's own `InstallAsync` override is handled by
`SpeechModelDownloader`, not by this contract, identically to a download failure.

**Dependencies**: `SpeechModelRole`, `SpeechModelAudioTagSupport`, `ISpeechModelParameter`,
`SpeechModelDownloadDescriptor`, and (Sub-phase 4b) `IModelCapabilityProfile`/
`DefaultModelCapabilityProfile` from the SynthesisSubsystem.

**Callers**: `SpeechModelDescriptor`/`SpeechModelCatalog` depend only on `ISpeechModel`, so they
can enumerate and report install state for any model regardless of role. The RecognitionSubsystem
depends on `IRecognitionModel`'s two internal members to load a model's engine; the
SynthesisSubsystem depends on `ISynthesisModel`'s two internal members to load a model's engine
and render its Natural Language Audio Tags.
