### SpeechModelContract (ISpeechModel, IRecognitionModel, ISynthesisModel)

**Purpose**: Define the common per-model contract every model's backing class implements
(identity, role, declared parameters, declared audio-tag support, download descriptor), with two
role-specific interfaces. `IRecognitionModel` exposes a public `AudioFormat` plus the public
engine-construction members the RecognitionSubsystem needs. `ISynthesisModel` is extended in
Sub-phase 4b with the equivalent public members the SynthesisSubsystem needs and a public
best-effort `PreferredAudioFormat` hint. Both interfaces are fully public - including their
engine-construction members - because third-party extension of the Speech library is a confirmed
goal: a host composes its `SpeechModelCatalog` from models it supplies itself via `AddModels`,
and a genuinely public contract lets an external package implement `IRecognitionModel`/
`ISynthesisModel` to add an entirely new recognition/synthesis backend, not merely another
instance of the sherpa-onnx-backed models this repository ships.

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
  before inference. Defaults to the identity function. `SynthesisSession` calls this
  hook before Layer 1 tag parsing, so a synthesis model may correct punctuation or spelling
  without the SynthesisSubsystem needing to know how.
- **ISpeechModel.LicenseName / LicenseUrl**: a model's declared license name/identifier and an
  optional canonical URL to its full text. Both are default-hooked members
  (`LicenseName => "Unknown"`, `LicenseUrl => null`), matching the `InstallAsync`/`NormalizeText`
  default-hook pattern above, so no existing test/demo fake implementing `ISpeechModel` needs to
  change. `LicenseName` is never null or empty; when a model's license is not certainly known,
  the uncertainty must be encoded directly in the string itself (for example
  `"Apache-2.0 (likely)"`), never silently upgraded to a certain claim. `LicenseUrl` is null when
  no canonical license URL is known or applicable. `SpeechModelDescriptor` forwards both, so a
  host enumerating `SpeechModelCatalog.Enumerate()` can discover licensing for any model without
  parsing `DisplayName`.
- **IRecognitionModel** / **ISynthesisModel**: `: ISpeechModel`. `IRecognitionModel` adds two
  public members in Phase 3 (see below), plus a public `NormalizeText(text, isFinal)` default
  hook in Phase 12 (see below). `ISynthesisModel` adds two public members in Sub-phase 4b
  (see below).
- **IRecognitionModel.AudioFormat** *(public)*: the mono audio format this model's recognition
  engine requires its input audio at. Declared per model rather than assumed, because streaming
  models are trained at a fixed feature rate and produce unusable results at any other; this is
  what lets callers request a capture device already matching the model and lets the
  RecognitionSubsystem resample whatever rate a capture device resolved into what this specific
  model needs when it does not.
- **IRecognitionModel.CreateBackend(installedModelDirectory)** *(public)*: constructs and
  returns this model's loaded `IRecognitionBackend`, combining the model's own compiled-in
  relative file names with the directory its verified files were installed into. The return type
  is the RecognitionSubsystem's engine-neutral `IRecognitionBackend` seam, so this contract names
  no inference-engine type at all - each concrete model owns its own engine-specific
  configuration privately (for example the sibling `SpeechSherpa` system's models build a
  sherpa-onnx configuration and wrap it in `SherpaOnnxRecognitionEngine`). Because constructing a
  backend loads the model into native memory, a missing native runtime or unusable model file
  surfaces here as an exception, which the RecognitionSubsystem's composition root catches and
  degrades to an honest unavailable recognizer. Implementations throw `ArgumentException` for a
  null or empty directory.
- **IRecognitionModel.CreateBackend(installedModelDirectory, parameterValues)** *(public)*:
  a two-argument overload of the member above, resolving a session-level, untyped key-value
  parameter bag (for example built from a host's settings UI via a declared
  `ISpeechModel.Parameters` entry) alongside the installed-files directory. Defaults to ignoring
  `parameterValues` and forwarding to the single-argument overload, identical to every existing
  model's current parameter-less behavior, mirroring `ISynthesisModel.ResolveSpeakerId`'s
  "generically correct for free, override only for bespoke per-model behavior" default-hook
  pattern. Neither shipped recognition model declares a parameter today, so both need zero code to
  keep their exact current behavior.
- **IRecognitionModel.NormalizeText(text, isFinal)**: a recognition-only hook, distinct from
  `ISpeechModel.NormalizeText(text)` above (that one runs before synthesis inference; this one
  runs on a model's own recognition output). Defaults to forwarding to
  `ISpeechModel.NormalizeText(text)` (a plain pass-through, unless a model separately overrides
  that shared method too). `isFinal` lets an override apply cheap, non-damaging normalization to
  a still-forming provisional result and full restoration only to a committed final result.
  `RecognitionSession` calls this hook on every decoded result's text before raising it,
  so a model whose raw output needs correction (for example, the sibling `SpeechSherpa` system's
  `SherpaOnnxZipformerEnRecognitionModel`, whose UPPERCASE, unpunctuated raw output is restored
  via that system's `UppercaseTranscriptRestorer`) can do so without the RecognitionSubsystem
  needing to know how.
- **IRecognitionModel.PostEndpointWarmupWindowMs** *(public)*: the duration, in milliseconds,
  of pre-endpoint audio the recognition engine should buffer and silently replay into a freshly
  reset stream immediately after an endpoint fires, to pre-warm the model's internal decoding
  state before genuinely new (post-pause) audio arrives. Defaults to `0` (feature disabled; a
  hard `Reset()` with no replay, matching every model's original behavior). This is a deliberate
  opt-in, not a global default: it exists to fix a confirmed post-`Reset()` warm-up word-loss
  defect specific to `SpeechSherpa`'s `SherpaOnnxNemotronStreamingEnRecognitionModel`, and
  forcing it on for a model like `SherpaOnnxZipformerEnRecognitionModel` was found to cause duplicated-text
  regressions, so only a model with its own independently confirmed defect should override it.
- **ISynthesisModel.CreateBackend(installedModelDirectory)** *(public)*: constructs and
  returns this model's loaded `ISynthesisBackend`, combining the model's own compiled-in relative
  file names with the directory its verified files were installed into. As with its
  recognition-direction counterpart above, the return type is the engine-neutral
  `ISynthesisBackend` seam, so this contract names no inference-engine type; a missing native
  runtime or unusable model file surfaces here as an exception, which the SynthesisSubsystem's
  composition root catches and degrades to an honest unavailable synthesizer.
- **ISynthesisModel.PreferredAudioFormat** *(public)*: a best-effort mono playback-format hint a
  host may use before the native engine is loaded. This is deliberately not authoritative: the
  true output rate remains the loaded engine's `ISynthesisBackend.SampleRate`, which may differ
  and therefore still drive playback resampling.
- **ISynthesisModel.CapabilityProfile** *(public)*: the `IModelCapabilityProfile` this model
  uses to render Natural Language Audio Tags into a `SpeechPlan`. Defaults to
  `DefaultModelCapabilityProfile.Instance`, a stateless singleton driven purely by
  `AudioTagSupport`/`Parameters`, so a model needs zero code to get generically-correct tag
  rendering; a model may override this hook only when it needs bespoke, non-generic rendering
  its native engine supports.
- **ISynthesisModel.ResolveSpeakerId(parameterValues)** *(public)*: resolves a session-level,
  untyped key-value parameter bag (for example built from a host's settings UI via a declared
  `ChoiceParameter`) to the engine's integer speaker id to synthesize with. Defaults to `0`,
  identical to every model's previous hard-coded behavior. Added so a multi-speaker model
  (starting with `SpeechSherpa`'s `SherpaOnnxKokoroEnglishSynthesisModel`) can own its own string-to-int voice
  mapping entirely inside its own backing class; implementations must never throw — an
  unrecognized or missing selection must degrade to a sensible default speaker id rather than
  fault synthesis.

Every member on both interfaces is fully public, including `CreateBackend`,
`PostEndpointWarmupWindowMs`, `CapabilityProfile`, and `ResolveSpeakerId`. The design still makes
each model's backing class responsible for "engine configuration for its own model architecture",
and the `CreateBackend` members return only the library-owned, engine-neutral
`IRecognitionBackend`/`ISynthesisBackend` seams, so this contract - and therefore the whole
library - names no sherpa-onnx type at all, even in its public surface. Making the full interface
public (rather than restricting it via `InternalsVisibleTo`) is a deliberate design decision:
third-party extension of the Speech library is a confirmed goal, so an external package must be
able to implement `IRecognitionModel`/`ISynthesisModel` to add its own recognition/synthesis
backend without needing special assembly-level access. The sibling `SpeechSherpa` system's
`DemaConsulting.Speech.Sherpa` assembly is simply the first implementer, supplying this
repository's built-in sherpa-onnx-backed models; it has no access that a third-party package
lacks. `AudioFormat` and `PreferredAudioFormat` are plain library-owned data values that were
already public for the same composition reasons.

**Error Handling**: N/A - a pure contract; each implementation's own construction/validation
rules apply. An exception thrown from a model's own `InstallAsync` override is handled by
`SpeechModelDownloader`, not by this contract, identically to a download failure.

**Dependencies**: `SpeechModelRole`, `SpeechModelAudioTagSupport`, `ISpeechModelParameter`,
`SpeechModelDownloadDescriptor`, `AudioFormat` from the AudioSubsystem, `IRecognitionBackend`
from the RecognitionSubsystem, and `ISynthesisBackend` and
`IModelCapabilityProfile`/`DefaultModelCapabilityProfile` from the SynthesisSubsystem.

**Callers**: `SpeechModelDescriptor`/`SpeechModelCatalog` depend only on `ISpeechModel`, so they
can enumerate and report install state for any model regardless of role. Hosts and the
AudioSubsystem may read `IRecognitionModel.AudioFormat` and `ISynthesisModel.PreferredAudioFormat`
to compose audio devices. The RecognitionSubsystem depends on `IRecognitionModel`'s public
`CreateBackend` members to load a model's backend; the SynthesisSubsystem depends on
`ISynthesisModel`'s public members to load a model's backend and render its Natural Language
Audio Tags. The concrete implementations shipped in this repository live in the sibling
`SpeechSherpa` system - see *SpeechSherpa ModelManagementSubsystem Design* - but any third-party
package may implement either interface directly to supply its own backend.
