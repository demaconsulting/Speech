<!-- cspell:ignore ALSA portaudio -->
# System Design

This document provides the system-level design for the Speech.

![Speech Structure](SpeechView.svg)

## Architecture

Speech is a cross-platform .NET library providing local, offline speech-to-text and
text-to-speech services for desktop applications. In this phase, the implemented system
consists of five subsystems:

- **Diagnostics**: reports structural diagnostic events to an optional host-supplied sink,
  carrying structural facts only, never raw audio or text — see _Diagnostics Subsystem Design_
- **AudioSubsystem**: defines the public capture/playback device and probe contracts, the
  name-only device identity model, honest unavailable fallbacks, real PortAudio-backed device
  implementations, and the `AudioDeviceFactory` composition root — see _AudioSubsystem Design_
- **ModelManagementSubsystem**: per-user speech model storage with atomic
  `current/`/`install-manifest.json`/`.tmp/{guid}` install/repair/uninstall, the queued
  fetch-verify-install download orchestration with a mockable HTTP seam, and the model
  catalog/contract seam: typed tunable-parameter descriptors, the common `ISpeechModel`
  contract with its `IRecognitionModel`/`ISynthesisModel` role markers, and
  `SpeechModelCatalog`'s enumeration of the compiled-in known-model registry (four real,
  production models covering both roles) alongside install state — see
  _ModelManagementSubsystem Design_
- **RecognitionSubsystem**: the public streaming speech-to-text contract
  (`ISpeechRecognizer`/`SpeechRecognitionResult`), the `SpeechRecognizerFactory` composition root
  that returns either a real recognizer or an honest unavailable fallback, the
  `SherpaOnnxSpeechRecognizer` pipeline that converts captured audio to the model's required
  format and streams it through a mockable recognition-engine seam, and
  `UnavailableSpeechRecognizer` — see _RecognitionSubsystem Design_
- **SynthesisSubsystem**: the closed, fixed Natural Language Audio Tag vocabulary and the
  model-independent Layer 1 parser (`AudioTagCatalog`/`AudioTagParser`) that recognizes bracket
  syntax against it, the Layer 2 rendering strategy
  (`IModelCapabilityProfile`/`DefaultModelCapabilityProfile`) that turns a parsed span sequence
  into a model-appropriate `SpeechPlan` of `SpeechSegment`s, the `SentenceChunker` used for
  pipeline-friendly chunk boundaries, and the public `ISpeechSynthesizer` streaming/playback
  contract, whose `SpeechSynthesizerFactory` composition root returns either the real
  `SherpaOnnxSpeechSynthesizer` pipeline or the honest `UnavailableSpeechSynthesizer` fallback —
  see _SynthesisSubsystem Design_

Within AudioSubsystem, a child **PortAudio** subsystem isolates PortAudioSharp2 and the
supplementary native host-API bindings behind internal `IPortAudioApi` and `IPortAudioStream`
seams. This lets the public audio-device logic be unit tested with pure managed fakes while the
production path still uses the real PortAudio runtime.

## External Interfaces

The system exposes the following public API to external consumers:

- **ISpeechDiagnostics** / **NullSpeechDiagnostics**: the diagnostics reporting contract and its
  default no-op implementation
- **SpeechDiagnosticLevel**: the diagnostic event severity enum (`Info`, `Warning`, `Error`)
- **IAudioCaptureDevice** / **IAudioPlaybackDevice**: the capture and playback device contracts
- **IAudioCaptureDeviceProbe** / **IAudioPlaybackDeviceProbe**: contracts for enumerating
  available devices
- **AudioDeviceDescription** / **AudioDeviceSelection**: immutable device description and
  persistable device preference records
- **AudioDeviceFactory**: the composition entry point for obtaining capture/playback devices and
  probes
- **AudioDeviceUnavailableException**: thrown when an operational member of an unavailable device
  is invoked
- **SpeechModelStore** / **SpeechModelStoreOptions** / **SpeechModelStoreException**: per-user
  model storage with atomic install/repair/uninstall and a host-configurable root override
- **SpeechModelDownloader**: queued fetch-verify-install download orchestration
- **IModelDownloadClient** / **HttpModelDownloadClient**: the mockable HTTP download seam and its
  real implementation
- **SpeechModelDownloadFile** / **SpeechModelDownloadDescriptor**: HTTPS URL(s) and SHA-256
  checksum descriptor types for a model's downloadable files
- **SpeechModelDownloadProgress**: single-file transfer progress payload
- **SpeechModelRole** / **SpeechModelState** / **SpeechModelAudioTagSupport**: fixed
  declaration-shape enums for a model's role, install lifecycle state, and declared inline
  audio-tag support
- **ISpeechModelParameter** / **NumericParameter** / **ChoiceParameter** /
  **ChoiceParameterOption** / **BooleanParameter**: the typed, self-validating tunable-parameter
  descriptor hierarchy
- **ISpeechModel** / **IRecognitionModel** / **ISynthesisModel**: the common per-model contract
  and its two empty role-marker interfaces
- **SpeechModelDescriptor**: immutable catalog read-model pairing one `ISpeechModel` with its
  current `SpeechModelState`
- **SpeechModelCatalog**: enumerates the compiled-in known-model registry (four real,
  production models covering both roles) alongside install state, and orchestrates downloading
  a known model by id
- **ISpeechRecognizer** / **SpeechRecognitionResult** / **SpeechRecognitionEvent**: the streaming
  speech-to-text contract and the immutable result/event types it delivers
- **SpeechRecognizerFactory**: the composition entry point for obtaining a speech recognizer for
  an installed recognition model and a capture device
- **UnavailableSpeechRecognizer** / **SpeechRecognizerUnavailableException**: the honest
  unavailable recognizer fallback and the exception thrown when an operational member of an
  unavailable recognizer is invoked
- **NaturalLanguageAudioTag** / **NaturalLanguageAudioTagKind** / **AudioTagDescriptor** /
  **AudioTagCatalog** / **TaggedTextSpanKind** / **TaggedTextSpan** / **AudioTagParser**: the
  closed, fixed inline audio-tag vocabulary and the model-independent parser that recognizes it
- **ISpeechSynthesizer** / **SynthesizedSpeech**: the streaming synthesis-and-playback contract
  and the immutable value type it yields
- **SpeechSynthesizerFactory**: the composition entry point for obtaining a speech synthesizer
  for an installed synthesis model and a playback device
- **UnavailableSpeechSynthesizer** / **SpeechSynthesizerUnavailableException**: the honest
  unavailable synthesizer fallback and the exception thrown when an operational member of an
  unavailable synthesizer is invoked

| Interface | Direction | Format | Constraints |
| --- | --- | --- | --- |
| `ISpeechDiagnostics.Report(...)` | Inbound | Method call | Structural facts only |
| `AudioDeviceFactory(...)` | Inbound | Constructor call | Never throws |
| `AudioDeviceFactory.CreateCaptureDevice(...)` | Inbound/Outbound | Method call/return | Never throws |
| `AudioDeviceFactory.CreatePlaybackDevice(...)` | Inbound/Outbound | Method call/return | Never throws |
| `IAudioCaptureDevice.Start()`/`.Stop()` | Inbound | Method call | Throws only on unavailable or first-use faults |
| `IAudioPlaybackDevice.Start()/Stop()/Write(...)` | Inbound | Method call | Unavailable/first-use faults only |
| `IAudioCaptureDeviceProbe.Enumerate()` | Outbound | Method call/return | Never throws; preferred host API only |
| `IAudioPlaybackDeviceProbe.Enumerate()` | Outbound | Method call/return | Never throws; preferred host API only |
| `AudioDeviceSelection.Resolve(...)` | Inbound/Outbound | Method call/return | Throws for a null list |
| `SpeechModelStore.IsInstalled(...)` | Outbound | Method call/return | Never throws |
| `SpeechModelStore.CompleteInstall(...)` | Inbound | Method call | Atomic; never touches `current/` early |
| `SpeechModelStore.Uninstall(...)` | Inbound | Method call | Throws `SpeechModelStoreException` if blocked |
| `SpeechModelDownloader.DownloadAsync(...)` | Inbound/Outbound | Method call/return | One at a time; honest failures |
| `IModelDownloadClient.DownloadAsync(...)` | Inbound | Method call | Throws on any non-success/transport failure |
| `SpeechModelCatalog.Enumerate()` | Outbound | Method call/return | Never throws |
| `SpeechModelCatalog.DownloadAsync(...)` | Inbound/Outbound | Method call/return | Throws for unknown model id |
| `SpeechRecognizerFactory.Create(...)` | Inbound/Outbound | Method call/return | Never throws for unavailable states |
| `ISpeechRecognizer.Start()`/`.Stop()` | Inbound | Method call | Throws only on unavailable or first-use faults |
| `ISpeechRecognizer.ResultReceived` | Outbound | Event | Raised off the audio callback thread |
| `ISpeechRecognizer.Dispose()` | Inbound | Method call | Idempotent; implies `Stop()` |
| `AudioTagParser.Parse(...)` | Inbound/Outbound | Method call/return | Never throws; folds unmatched brackets |
| `SpeechSynthesizerFactory.Create(...)` | Inbound/Outbound | Method call/return | Never throws for unavailable states |
| `ISpeechSynthesizer.SpeakAsync(...)` | Inbound/Outbound | Method call/return | Throws only on unavailable/first-use |
| `ISpeechSynthesizer.Dispose()` | Inbound | Method call | Idempotent |

## Dependencies

Speech has three runtime NuGet dependencies: **PortAudioSharp2**, **SherpaOnnx**
(`org.k2fsa.sherpa.onnx`), and **SharpCompress**. PortAudioSharp2 supplies the managed PortAudio
binding and transitively restores native runtime packages for `win-x64`, `linux-x64`,
`linux-aarch64`, `osx-x64`, and `osx-arm64`. No `win-arm64` PortAudio runtime package is available
in this phase. SherpaOnnx supplies the managed local speech-inference API; the library references
only that managed package and never an `org.k2fsa.sherpa.onnx.runtime.{RID}` package directly,
though the managed package itself declares those per-platform runtime packages as its own
dependencies, so a consumer restores the ones it needs transitively. SharpCompress supplies the
managed BZip2-compressed tar (`.tar.bz2`) archive reader that `TarBz2ArchiveExtractor` uses to
unpack the recognition models' declared archive payloads after checksum verification. See
_OTS Integration Design_, _PortAudioSharp2 Design_, _SherpaOnnx Design_, and _SharpCompress
Design_ for details.

The following OTS items are used for building and verifying this system; they are not shipped as
part of the compiled NuGet package:

- **BuildMark** — generates build-notes documentation
- **FileAssert** — validates generated documents against acceptance criteria
- **Pandoc** — converts Markdown documentation to HTML
- **ReqStream** — enforces requirements-to-test traceability
- **ReviewMark** — enforces file review coverage and currency
- **SarifMark** — converts CodeQL SARIF results to markdown
- **SonarMark** — generates SonarCloud quality reports
- **SysML2Tools** — lints and renders the SysML2 architecture model
- **VersionMark** — captures and publishes tool-version information
- **WeasyPrint** — converts HTML documentation to PDF
- **xUnit** — executes unit and integration tests

## Risk Control Measures

N/A - Speech in this phase provides no safety-critical functionality requiring risk control
measures (IEC 62304 §5.3.3). This is revisited if a later phase introduces functionality with a
direct safety impact.

## Data Flow

**Diagnostics reporting path:**

1. **Input**: A host optionally supplies an `ISpeechDiagnostics` implementation to
   `AudioDeviceFactory`; when none is supplied, `NullSpeechDiagnostics.Instance` is used
2. **Processing**: `AudioDeviceFactory`, `PortAudioCaptureDevice`, and `PortAudioPlaybackDevice`
   report structural selection, start/stop, and fault events
3. **Output**: The supplied sink receives the event (or discards it, for the null sink); no raw
   audio samples or recognized/synthesized text are ever passed through this path

**Capture-device composition and runtime path:**

1. **Input**: A host constructs an `AudioDeviceFactory` and requests a capture device
2. **Selection**: `PortAudioEnvironment` resolves the preferred host API for the current OS, and
   `PortAudioCaptureDevice` resolves either the named device or that host API's default input
   device
3. **Streaming**: `PortAudioApi` opens a PortAudio callback stream and converts native float
   buffers into managed `IReadOnlyList<float>` sample blocks
4. **Output**: `FrameCaptured` raises managed sample blocks to the caller

**Playback-device composition and runtime path:**

1. **Input**: A host constructs an `AudioDeviceFactory` and requests a playback device
2. **Selection**: `PortAudioEnvironment` resolves the preferred host API for the current OS, and
   `PortAudioPlaybackDevice` resolves either the named device or that host API's default output
   device
3. **Streaming**: `Write(...)` enqueues samples; `PortAudioApi` drains them through a PortAudio
   callback stream and zero-fills any underrun
4. **Output**: The selected playback device renders the queued samples, or the device reports
   itself unavailable when no device could be resolved

**Model download and install path:**

1. **Input**: A caller supplies a `SpeechModelDownloadDescriptor` (HTTPS URL(s) + SHA-256
   checksums) to `SpeechModelDownloader.DownloadAsync(...)`
2. **Fetch**: The downloader serializes concurrent requests to one at a time, fetches each file
   via the injected `IModelDownloadClient` (`HttpModelDownloadClient` by default) into a
   `.tmp/{guid}` staging directory, and forwards progress with file index/count rewritten to the
   model's overall position
3. **Verify**: Each fetched file's SHA-256 checksum is verified against the descriptor; any
   mismatch, cancellation, or transport failure discards the staging directory and reports an
   honest failure outcome without touching `current/`
4. **Install hook**: When downloaded through the `ISpeechModel`-aware `DownloadAsync` overload
   (the only overload `SpeechModelCatalog` uses), the model's own `InstallAsync` unpacks the
   verified staged files in place (a no-op default for models that need no unpacking); a failure
   here is handled identically to a verification failure - the staging directory is discarded and
   `current/` is never touched
5. **Output**: Once every file is verified (and, if applicable, unpacked),
   `SpeechModelStore.CompleteInstall(...)` atomically renames the staging directory into
   `current/` and writes `install-manifest.json`, replacing any prior installation only after the
   replacement is fully verified

**Model catalog enumeration and download-state tracking path:**

1. **Input**: A host constructs a `SpeechModelCatalog` and calls `Enumerate()` or
   `DownloadAsync(modelId, ...)`
2. **Resolution**: `Enumerate()` builds one `SpeechModelDescriptor` per compiled-in known model
   (four real, production models covering both roles), resolving each model's
   `SpeechModelState` from `SpeechModelStore`'s installed/not-installed fact plus this catalog
   instance's own in-memory tracking of in-flight and most-recently-failed download attempts
3. **Delegation**: `DownloadAsync(modelId, ...)` looks up the named known model's own declared
   `DownloadDescriptor` and delegates to `SpeechModelDownloader`, marking the model
   `Downloading` for the duration and `FailedOrCorrupt` if the outcome is not `Installed`
4. **Output**: A caller observes `NotDownloaded` → `Downloading` → `Downloaded` (or
   `FailedOrCorrupt`) transitions across repeated `Enumerate()`/`GetState(...)` calls

**Streaming recognition path:**

1. **Input**: A host passes an installed `IRecognitionModel`, that model's installed-files
   directory, and an `IAudioCaptureDevice` to `SpeechRecognizerFactory.Create(...)`
2. **Composition**: The factory checks installation, model role, and device availability, then
   loads the model's own engine configuration through the internal `IRecognitionEngineFactory`
   seam; any failure returns `UnavailableSpeechRecognizer.Instance` with a structural diagnostic
3. **Capture**: `Start()` subscribes to `FrameCaptured` and starts the device; each captured
   block is copied onto a bounded queue on the audio callback thread and nothing more
4. **Conversion and inference**: A single background consumer downmixes and resamples each block
   from the device's reported `ChannelCount`/`SampleRate` to the model's declared `AudioFormat`
   via `AudioFrameResampler`, feeds it to the `IRecognitionEngine`, and polls for results
5. **Output**: `ResultReceived` raises each provisional and final `SpeechRecognitionResult` off
   the audio callback thread; `Stop()` drains the queue so no result derived from already-captured
   audio is lost

**Streaming synthesis path:**

1. **Input**: A host passes an installed `ISynthesisModel`, that model's installed-files
   directory, and an `IAudioPlaybackDevice` to `SpeechSynthesizerFactory.Create(...)`
2. **Composition**: The factory checks installation, model role, and device availability, then
   loads the model's own engine configuration through the internal `ISynthesisEngineFactory` seam;
   any failure returns `UnavailableSpeechSynthesizer.Instance` with a structural diagnostic
3. **Parsing and rendering**: `AudioTagParser` recognizes inline audio-tag bracket syntax against
   the fixed vocabulary, and the selected model's `IModelCapabilityProfile` renders the parsed
   span sequence into a `SpeechPlan` of `SpeechSegment`s, honoring the model's own declared
   audio-tag support
4. **Chunked synthesis and playback**: `SentenceChunker` splits synthesis-ready text into
   pipeline-friendly chunks; each chunk streams through the `ISynthesisEngine` and the resulting
   audio is resampled via `PlaybackAudioResampler` to the playback device's required format before
   being written to it
5. **Output**: The playback device renders the synthesized audio as it streams; `Stop()` halts an
   in-progress synthesis/playback cycle

## Design Constraints

- **Nothing throws at composition**: Constructing and holding any object this library exposes
  never throws; PortAudio initialization failure degrades to unavailable probes/devices
- **Own interfaces over direct OTS types**: Hosts and tests depend on library-owned interfaces,
  never directly on PortAudioSharp2 types
- **Zero project references, denylisted interface-layer packages**: The library must remain
  independently publishable and consumable by any .NET host, so its project file carries no
  `ProjectReference` at all, and a build-time check (`VerifyLibraryIsIndependent`) fails the
  build if one is added or if a `PackageReference` matches a denylisted UI/hosting-layer package
  prefix (`Avalonia`, `Microsoft.AspNetCore`, `Microsoft.WindowsDesktop`,
  `System.Windows.Forms`, `Microsoft.Maui`) that would tie a general-purpose offline speech
  library to one specific application shell
- **Single preferred host API per platform**: Windows uses WASAPI, Linux uses ALSA, and macOS
  uses CoreAudio to avoid duplicate device listings across PortAudio backends
- **Stable name-only identity**: Persisted device selections store only device names and fall
  back to the host API's default device when a saved name no longer resolves
- **Manual hardware boundary**: CI verifies seam logic only; real microphone/speaker I/O requires
  manual/local verification on hardware-equipped machines
- **Never touches `current/` until verified**: A model install/repair only replaces `current/`
  after every downloaded file's SHA-256 checksum has been verified in staging; a corrupted,
  partial, or checksum-mismatched download is never marked installed and never disturbs a prior
  successful installation
- **HTTPS-only model downloads**: `SpeechModelDownloadFile` rejects any non-HTTPS source URI at
  construction
- **Compiled-in catalog covers both roles**: `SpeechModelCatalog.KnownModels` ships four real,
  production models - two recognition models and two synthesis models - so
  `ISpeechModel`/`IRecognitionModel`/`ISynthesisModel` each have at least one shipping
  implementation; a host may add further models of its own by supplying its own known-model list
- **Catalog state is instance-scoped, not durable**: `SpeechModelCatalog`'s `Downloading`/
  `FailedOrCorrupt` states reflect only in-flight/most-recent attempts on that catalog instance;
  they are never persisted and do not survive a process restart
- **Manual native-runtime boundary for recognition**: The library references only the managed
  sherpa-onnx package and never a per-RID native runtime package directly. A machine or publish
  target whose native speech-inference runtime is absent composes successfully and reports
  recognition as unavailable, exactly as a missing model file does
- **Recognition owns audio-format conversion**: A capture device delivers whatever format its
  hardware resolved, and a recognition model accepts exactly one mono rate, so the
  RecognitionSubsystem - not the AudioSubsystem and not the host - converts between them. The
  converter uses channel averaging and linear interpolation, a deliberate simplicity/quality
  trade-off recorded in _SherpaOnnxSpeechRecognizer Design_
- **Recognition and synthesis models both ship in the compiled-in catalog**: The recognition
  pipeline is proven end to end against the two real, production recognition models
  (`SherpaOnnxZipformerEnRecognitionModel`, `SherpaOnnxNemotronStreamingEnRecognitionModel`), and
  the synthesis pipeline against the two real, production synthesis models
  (`SherpaOnnxVitsLibriTtsEnglishSynthesisModel`, `SherpaOnnxKokoroEnglishSynthesisModel`) - real
  microphone-to-real-text and real-text-to-real-speech behavior are both proven, not merely
  designed for
- **Compliance**: All functionality must be traceable to requirements
- **Quality**: Zero warnings, full targeted tests, complete documentation

### Platform Support

The library targets the following frameworks, enabling broad compatibility across modern .NET
runtimes:

| Target Framework | Runtime / Environment |
| --- | --- |
| `net8.0` | .NET 8 LTS |
| `net9.0` | .NET 9 |
| `net10.0` | .NET 10 |

The library is supported on the following operating systems:

- **Windows** — preferred host API: WASAPI
- **Linux** — preferred host API: ALSA
- **macOS** — preferred host API: CoreAudio

PortAudio runtime availability is limited by the transitive native packages carried by
`PortAudioSharp2`: `win-x64`, `linux-x64`, `linux-aarch64`, `osx-x64`, and `osx-arm64`.
Machines or publish targets outside that set degrade to unavailable audio devices.

### Integration Patterns

- **NuGet Packaging**: Standard .NET library packaging and distribution
- **Callback-based audio I/O**: PortAudio callback streams feed capture and playback through
  library-owned abstractions
- **Requirements Traceability**: All features linked to passing tests
- **Review Management**: Systematic file review using ReviewMark patterns
