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
  contract with its `IRecognitionModel`/`ISynthesisModel` role markers (the extension point
  concrete models implement), and `SpeechModelCatalog`'s enumeration of the host-registered
  known-model list (populated through `AddModels`) alongside install state — see
  _ModelManagementSubsystem Design_
- **RecognitionSubsystem**: the public async Engine/Session streaming speech-to-text contract
  (`ISpeechRecognizerEngine`/`IRecognitionSession`/`SpeechRecognitionResult`), the
  `SpeechRecognizerFactory` composition root whose `LoadAsync` returns either a real engine or
  an honest unavailable fallback, the `RecognitionSession` pipeline that converts
  captured audio to the model's required format and streams it through a mockable internal
  `IRecognitionBackend` seam, and `UnavailableSpeechRecognizerEngine`/
  `UnavailableRecognitionSession` — see _RecognitionSubsystem Design_
- **SynthesisSubsystem**: the closed, fixed Natural Language Audio Tag vocabulary and the
  model-independent Layer 1 parser (`AudioTagCatalog`/`AudioTagParser`) that recognizes bracket
  syntax against it, the Layer 2 rendering strategy
  (`IModelCapabilityProfile`/`DefaultModelCapabilityProfile`) that turns a parsed span sequence
  into a model-appropriate `SpeechPlan` of `SpeechSegment`s, the `SentenceChunker` used for
  pipeline-friendly chunk boundaries, and the public async Engine/Session streaming/playback
  contract (`ISpeechSynthesizerEngine`/`ISynthesisSession`), whose `SpeechSynthesizerFactory`
  composition root's `LoadAsync` returns either the real `SynthesisSession` pipeline
  or the honest `UnavailableSpeechSynthesizerEngine`/`UnavailableSynthesisSession` fallback —
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
- **SpeechModelCatalog**: enumerates the host-registered known-model list (populated through
  `AddModels`) alongside install state, and orchestrates downloading a known model by id
- **ISpeechRecognizerEngine** / **IRecognitionSession** / **SpeechRecognitionResult** /
  **SpeechRecognitionEvent**: the loaded-model engine and per-device session halves of the
  streaming speech-to-text contract, their state enums (`RecognitionSessionState`,
  `SessionStateChangedEventArgs`), and the immutable result/event types a session's
  `GetResultsAsync()` delivers
- **SpeechRecognizerFactory**: the composition entry point whose `LoadAsync` obtains a speech
  recognizer engine for an installed recognition model, with the engine's own
  `CreateSessionAsync` binding it to a capture device for a session's lifetime
- **UnavailableSpeechRecognizerEngine** / **UnavailableRecognitionSession** /
  **SpeechRecognizerUnavailableException** / **RecognitionEngineBusyException** /
  **RecognitionSessionFaultedException**: the honest unavailable engine/session fallbacks, the
  exception thrown when an operational member of an unavailable engine/session is invoked, the
  exception thrown by a concurrent `CreateSessionAsync` while the engine's one session lease is
  already held, and the exception/fault surfaced through a session's result stream when its
  dedicated worker faults
- **NaturalLanguageAudioTag** / **NaturalLanguageAudioTagKind** / **AudioTagDescriptor** /
  **AudioTagCatalog** / **TaggedTextSpanKind** / **TaggedTextSpan** / **AudioTagParser**: the
  closed, fixed inline audio-tag vocabulary and the model-independent parser that recognizes it
- **ISpeechSynthesizerEngine** / **ISynthesisSession** / **SynthesizedSpeech**: the loaded-model
  engine and per-device session halves of the streaming synthesis-and-playback contract, their
  state enums (`SynthesisSessionState`, `SessionStateChangedEventArgs`), and the immutable value
  type a session's `SpeakAsync`/`SynthesizeAsync` yields
- **SpeechSynthesizerFactory**: the composition entry point whose `LoadAsync` obtains a speech
  synthesizer engine for an installed synthesis model, with the engine's own
  `CreateSessionAsync` binding it to a playback device for a session's lifetime
- **UnavailableSpeechSynthesizerEngine** / **UnavailableSynthesisSession** /
  **SpeechSynthesizerUnavailableException** / **SynthesisEngineBusyException** /
  **SynthesisSessionFaultedException**: the honest unavailable engine/session fallbacks, the
  exception thrown when an operational member of an unavailable engine/session is invoked, the
  exception thrown by a concurrent `CreateSessionAsync` while the engine's one session lease is
  already held, and the exception/fault surfaced through a session's result stream when its
  dedicated worker faults

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
| `SpeechRecognizerFactory.LoadAsync(...)` | Inbound/Outbound | Method call | Never throws for unavailable states |
| `ISpeechRecognizerEngine.CreateSessionAsync(...)` | Inbound/Outbound | Method call | Never throws; throws if leased |
| `IRecognitionSession.StartAsync()`/`.StopAsync()` | Inbound | Method call | Throws only unavailable/dispose/restart |
| `IRecognitionSession.GetResultsAsync(...)` | Outbound | `IAsyncEnumerable<T>` | Single-consumer; flushes before stop |
| `IRecognitionSession.DisposeAsync()` | Inbound | Method call | Idempotent; implies `StopAsync()` |
| `AudioTagParser.Parse(...)` | Inbound/Outbound | Method call/return | Never throws; folds unmatched brackets |
| `SpeechSynthesizerFactory.LoadAsync(...)` | Inbound/Outbound | Method call | Never throws for unavailable states |
| `ISpeechSynthesizerEngine.CreateSessionAsync(...)` | Inbound/Outbound | Method call | Never throws; throws if leased |
| `ISynthesisSession.SpeakAsync`/`.SynthesizeAsync` | Inbound/Outbound | Method call | Unavailable-only; no overlap |
| `ISynthesisSession.DisposeAsync()` | Inbound | Method call | Idempotent |

## Dependencies

Speech has one runtime NuGet dependency: **PortAudioSharp2**. PortAudioSharp2 supplies the managed
PortAudio binding and transitively restores native runtime packages for `win-x64`, `linux-x64`,
`linux-aarch64`, `osx-x64`, and `osx-arm64`. No `win-arm64` PortAudio runtime package is available
in this phase. See _OTS Integration Design_ and _PortAudioSharp2 Design_ for details.

Speech carries no speech-inference engine dependency of its own. Concrete speech models and their
inference backends plug in through the `IRecognitionModel`/`ISynthesisModel` extension seam, whose
internal `CreateBackend` members return the library's own engine-neutral
`IRecognitionBackend`/`ISynthesisBackend` interfaces. `DemaConsulting.Speech.Sherpa` is one such
extension maintained in this repository: it supplies two recognition models and two synthesis
models backed by sherpa-onnx, and carries the `org.k2fsa.sherpa.onnx` and SharpCompress
dependencies the library itself no longer has - see _SpeechSherpa Design_.

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

1. **Input**: A host constructs a `SpeechModelCatalog`, registers models through `AddModels(...)`
   (or an extension method such as `SpeechSherpa`'s `AddSherpaModels()`), and calls `Enumerate()`
   or `DownloadAsync(modelId, ...)`
2. **Resolution**: `Enumerate()` builds one `SpeechModelDescriptor` per registered known model,
   resolving each model's
   `SpeechModelState` from `SpeechModelStore`'s installed/not-installed fact plus this catalog
   instance's own in-memory tracking of in-flight and most-recently-failed download attempts
3. **Delegation**: `DownloadAsync(modelId, ...)` looks up the named known model's own declared
   `DownloadDescriptor` and delegates to `SpeechModelDownloader`, marking the model
   `Downloading` for the duration and `FailedOrCorrupt` if the outcome is not `Installed`
4. **Output**: A caller observes `NotDownloaded` → `Downloading` → `Downloaded` (or
   `FailedOrCorrupt`) transitions across repeated `Enumerate()`/`GetState(...)` calls

**Streaming recognition path:**

1. **Input**: A host passes an installed `IRecognitionModel` and that model's installed-files
   directory to `SpeechRecognizerFactory.LoadAsync(...)`, then passes an `IAudioCaptureDevice`
   to the returned engine's `CreateSessionAsync(...)`
2. **Composition**: `LoadAsync` checks installation and model role, then loads the model's own
   engine configuration through the internal `IRecognitionBackendFactory` seam; any failure
   returns `UnavailableSpeechRecognizerEngine.Instance` with a structural diagnostic.
   `CreateSessionAsync` checks device availability and the engine's single-session lease,
   returning `UnavailableRecognitionSession.Instance` or throwing
   `RecognitionEngineBusyException` for a concurrent second lease attempt
3. **Capture**: `StartAsync()` subscribes to `FrameCaptured` and starts the device; each captured
   block is copied onto a bounded queue on the audio callback thread and nothing more
4. **Conversion and inference**: A dedicated long-running worker thread downmixes and resamples
   each block from the device's reported `ChannelCount`/`SampleRate` to the model's declared
   `AudioFormat` via `AudioFrameResampler`, feeds it to the `IRecognitionBackend`, and polls for
   results
5. **Output**: `GetResultsAsync()` yields each provisional and final `SpeechRecognitionResult`
   through a byte-capped backpressure buffer that coalesces provisional results but never drops a
   final one; `StopAsync()` drains the queue so no result derived from already-captured audio is
   lost before the result stream completes

**Streaming synthesis path:**

1. **Input**: A host passes an installed `ISynthesisModel` and that model's installed-files
   directory to `SpeechSynthesizerFactory.LoadAsync(...)`, then passes an `IAudioPlaybackDevice`
   to the returned engine's `CreateSessionAsync(...)`
2. **Composition**: `LoadAsync` checks installation and model role, then loads the model's own
   engine configuration through the internal `ISynthesisBackendFactory` seam; any failure returns
   `UnavailableSpeechSynthesizerEngine.Instance` with a structural diagnostic.
   `CreateSessionAsync` checks device availability and the engine's single-session lease,
   returning `UnavailableSynthesisSession.Instance` or throwing `SynthesisEngineBusyException`
   for a concurrent second lease attempt
3. **Parsing and rendering**: `AudioTagParser` recognizes inline audio-tag bracket syntax against
   the fixed vocabulary, and the selected model's `IModelCapabilityProfile` renders the parsed
   span sequence into a `SpeechPlan` of `SpeechSegment`s, honoring the model's own declared
   audio-tag support
4. **Chunked synthesis and playback**: `SentenceChunker` splits synthesis-ready text into
   pipeline-friendly chunks; each chunk streams through the `ISynthesisBackend` on a dedicated
   long-running worker thread and the resulting audio is resampled via `PlaybackAudioResampler`
   to the playback device's required format before being written to it
5. **Output**: The playback device renders the synthesized audio as it streams via
   `SpeakAsync(...)` (or is returned as `SynthesizedSpeech` segments via `SynthesizeAsync(...)`);
   `StopAsync()` halts an in-progress synthesis/playback cycle

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
- **Host-populated catalog, zero built-in models**: `SpeechModelCatalog` starts empty and a host
  registers the models it wants through `AddModels(...)` (for example the two recognition models
  and two synthesis models shipped today by the sibling `SpeechSherpa` package, via its
  `AddSherpaModels()` extension method), so the library itself names no concrete model or
  inference engine
- **Catalog state is instance-scoped, not durable**: `SpeechModelCatalog`'s `Downloading`/
  `FailedOrCorrupt` states reflect only in-flight/most-recent attempts on that catalog instance;
  they are never persisted and do not survive a process restart
- **Native-runtime failures degrade, never throw**: A model whose backend cannot load - for
  example a `SpeechSherpa` model on a machine or publish target whose native speech-inference
  runtime is absent - composes successfully and reports recognition or synthesis as unavailable,
  exactly as a missing model file does
- **Recognition owns audio-format conversion**: A capture device delivers whatever format its
  hardware resolved, and a recognition model accepts exactly one mono rate, so the
  RecognitionSubsystem - not the AudioSubsystem and not the host - converts between them. The
  converter uses channel averaging and linear interpolation, a deliberate simplicity/quality
  trade-off recorded in _RecognitionSession Design_
- **Real models are proven alongside their backends**: The library's recognition and synthesis
  pipelines are proven here against fake models and fake backends; real
  microphone-to-real-text and real-text-to-real-speech behavior is proven with the two
  recognition models and two synthesis models shipped today by the sibling `SpeechSherpa`
  package, alongside the sherpa-onnx backends they construct - see _SpeechSherpa Design_
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
