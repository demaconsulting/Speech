## ModelManagementSubsystem Design

![ModelManagementSubsystem Structure](ModelManagementSubsystemView.svg)

### Overview

The ModelManagementSubsystem provides the storage/download machinery and the model catalog/
contract machinery for speech models. Sub-phase 2a implements a per-user on-disk store with
atomic install/replace semantics, and a queued downloader that fetches, SHA-256-verifies, and
atomically installs a model's declared files. Sub-phase 2b adds the generic model catalog/
contract seam on top: a typed tunable-parameter descriptor set, the common `ISpeechModel`
contract with its `IRecognitionModel`/`ISynthesisModel` role markers, and `SpeechModelCatalog`
itself. Later passes added the model-owned extension hooks concrete models build on - an
`ISynthesisModel.ResolveSpeakerId` hook for model-owned voice/speaker selection, a recognition-only
`IRecognitionModel.NormalizeText(text, isFinal)` hook (a default pass-through, distinct from the
shared `ISpeechModel.NormalizeText(string)` used on the synthesis side) for restoring readable
prose from a model's raw output, and a two-argument, parameter-value-aware default hook on
`IRecognitionModel.CreateBackend`, bringing the recognition side to parity with the synthesis
side's `ResolveSpeakerId` seam.

`IRecognitionModel`/`ISynthesisModel` are this library's model extension point. The library itself
ships zero built-in models: `SpeechModelCatalog` starts empty and a host registers the models it
wants through `SpeechModelCatalog.AddModels(...)`. Each model's backing class constructs its own
engine-neutral `IRecognitionBackend`/`ISynthesisBackend` through its public `CreateBackend`
member, so neither this subsystem nor any other part of the library names an inference-engine
type. `CreateBackend` and the backend seams it returns are public specifically so a third-party
package can implement `IRecognitionModel`/`ISynthesisModel` to add a new recognition or synthesis
backend without needing `InternalsVisibleTo` access. The concrete sherpa-onnx-backed models
shipped in this repository - two recognition models and two synthesis models - together with
their shared `.tar.bz2` archive-extraction and transcript-restoration helpers, live in the
sibling `SpeechSherpa` system; see _SpeechSherpa ModelManagementSubsystem Design_.

It contains the following units:

- **SpeechModelStore** / **SpeechModelStoreOptions** / **SpeechModelStoreException**: the
  per-user on-disk layout, atomic `current/` swap, install-state query, uninstall, and
  best-effort cleanup, plus the storage-root override options and the exception thrown only by
  an explicit uninstall that cannot complete
- **SpeechModelDownloader** / **SpeechModelDownloaderOptions** / **DownloadMirror**: the queued
  (one-at-a-time) fetch → SHA-256 verify → atomic-install orchestrator, defining honest
  `SpeechModelDownloadOutcome` result states (including network/HTTP/I-O failure classification),
  plus the optional configuration seam and mirror-description type that let a host redirect every
  model download through an internal, possibly-authenticated HTTPS mirror
- **SpeechModelDownloadFile** / **SpeechModelDownloadDescriptor**: HTTPS-only URL(s) plus
  SHA-256 checksum descriptor types describing what a downloader fetches and verifies
- **SpeechModelDownloadProgress**: the `IProgress<T>`-compatible download-progress payload
- **IModelDownloadClient** / **HttpModelDownloadClient**: the mockable HTTP-fetch seam and its
  real `HttpClient`-backed implementation
- **SpeechModelDescriptorEnums** (`SpeechModelRole`, `SpeechModelState`,
  `SpeechModelAudioTagSupport`): the fixed, small declaration-shape enums a model and the catalog
  are built around
- **SpeechModelParameters** (`ISpeechModelParameter`, `NumericParameter`, `ChoiceParameter`,
  `BooleanParameter`): the typed, self-describing tunable-parameter descriptor hierarchy
- **SpeechModelParameterDiagnostics**: the shared internal helper, invoked once up front by both
  composition factories' innermost `LoadAsync` overloads, that validates a supplied
  `parameterValues` bag against a model's declared `Parameters`, throwing `ArgumentException` for
  an invalid value and reporting (via an `Info` diagnostic) an unknown key rather than silently
  dropping it
- **SpeechModelContract** (`ISpeechModel`, `IRecognitionModel`, `ISynthesisModel`): the common
  per-model contract plus its two role-specific interfaces - `IRecognitionModel` exposing a
  public `AudioFormat` plus its public engine-construction members, and `ISynthesisModel` exposing
  a public best-effort `PreferredAudioFormat` plus its public synthesis hooks
- **SpeechModelDescriptor**: the immutable catalog read-model pairing one `ISpeechModel` with its
  current `SpeechModelState`
- **SpeechModelCatalog**: enumerates the host-registered known-model list alongside each model's
  install state, appends models through `AddModels(...)`, and orchestrates downloading a known
  model by id through `SpeechModelDownloader`

### Interfaces

The subsystem exposes `SpeechModelStore`, `SpeechModelStoreOptions`, `SpeechModelStoreException`,
`SpeechModelDownloader`, `SpeechModelDownloaderOptions`, `DownloadMirror`,
`SpeechModelDownloadOutcome`, `SpeechModelDownloadResult`,
`SpeechModelDownloadFile`, `SpeechModelDownloadDescriptor`, `SpeechModelDownloadProgress`,
`IModelDownloadClient`, `HttpModelDownloadClient`, `SpeechModelRole`, `SpeechModelState`,
`SpeechModelAudioTagSupport`, `ISpeechModelParameter`, `NumericParameter`, `ChoiceParameter`,
`ChoiceParameterOption`, `BooleanParameter`, `ISpeechModel`, `IRecognitionModel`,
`ISynthesisModel`, `SpeechModelDescriptor`, and `SpeechModelCatalog` as its public API
(`SpeechModelParameterDiagnostics` is internal). It consumes `ISpeechDiagnostics` from the
Diagnostics subsystem to report structural download-failure facts without ever exposing raw
model bytes. It also consumes `AudioFormat` from the AudioSubsystem as a narrow plain-data
dependency for public model format declarations.

### Design

`SpeechModelStore` owns the on-disk layout resolving the download-while-in-use concern
(download-while-in-use). For each model id, it manages `{root}/{model-id}/current/` (the
stable, installed content, only ever replaced by a directory rename),
`{root}/{model-id}/install-manifest.json` (a sidecar written only after a successful swap so
`IsInstalled` can report cheaply without re-hashing gigabytes on every query), and
`{root}/{model-id}/.tmp/{operation-id}/` (scratch space for an in-progress
download/install, never read as installed content). `current/` is never touched while a
download is being fetched and verified, so an in-use recognizer/synthesizer continues reading
unaffected content until the swap completes.

`SpeechModelDownloader` is the only caller of `SpeechModelStore`'s internal staging/swap
members. It serializes downloads one at a time via an internal single-flight lock, fetches each
declared file through the injected `IModelDownloadClient`, verifies its SHA-256 checksum, and -
when its `ISpeechModel`-aware `DownloadAsync` overload is used - invokes the model's own
`InstallAsync` hook to unpack the staged files in place (a no-op by default), before only then
handing the fully verified (and, if applicable, unpacked) staging directory to `SpeechModelStore`
for the atomic swap. A checksum mismatch, transport failure, install-hook failure, or
cancellation discards the staging directory and leaves any prior successful install of the same
model completely untouched - `SpeechModelDownloadOutcome` never reports `Installed` for a
corrupted, partial, checksum-mismatched, or install-hook-failed download. An optional
`SpeechModelDownloaderOptions.Mirror` (a `DownloadMirror`, defaulted to `null`) lets a host
redirect every file's effective request URI beneath a single internal mirror - resolved by an
internal `ResolveEffectiveUri` helper - and have the mirror's Basic/NTLM credentials or bearer
token applied only to requests actually sent to it; this is the one uniform seam through which an
IT-restricted network's blocked public model hosts (for example a TLS-interception policy
breaking `huggingface.co`) can be worked around without any per-model code change. A non-
`Installed`, non-`ChecksumMismatch` outcome is further classified by a dedicated
`ClassifyFailure` helper into `HttpError` (a real non-success HTTP response), `NetworkBlocked` (a
TLS/certificate failure, a DNS/connection failure, or `HttpClient`'s own internal timeout - as
distinct from the caller's own cancellation, which always propagates unchanged as
`OperationCanceledException`), `IoFailure` (a local disk/permission failure), or the final
generic `Failed` fallback - so a host can react to _why_ a download failed, not just that it did.

`IModelDownloadClient` mirrors the existing `IPortAudioApi`/`IPortAudioStream` seam pattern: a
small library-owned interface with one real implementation (`HttpModelDownloadClient`, backed by
`System.Net.Http.HttpClient`) and hand-written fakes used in tests for fast, deterministic
coverage of `SpeechModelDownloader`'s orchestration logic without any real network access.
`HttpModelDownloadClient` itself is additionally verified against a genuine loopback
`System.Net.HttpListener` server to prove it truly performs an HTTP download with progress
reporting, including the real HTTP Basic challenge/response negotiation triggered by a
credentials-bearing `DownloadMirror`. `IModelDownloadClient.DownloadAsync`'s optional `mirrorAuth`
parameter (a `DownloadMirror?`, defaulted to `null`) carries only "this request's auth, if any" -
the interface and every implementation remain completely unaware of `ISpeechModel`/model id/
catalog concepts, since only `SpeechModelDownloader` (the owner of a configured mirror) decides
when to supply it.

`ISpeechModel` is the common contract every model's backing class implements (through either
`IRecognitionModel` or `ISynthesisModel`, never directly): identity (`Id`/`DisplayName`), `Role`,
its declared `Parameters` (`ISpeechModelParameter` instances - `NumericParameter`,
`ChoiceParameter`, or `BooleanParameter`, each self-validating an internally consistent range/
option-set/default at construction), its declared `AudioTagSupport` (a declaration only - the
Layer 2 rendering logic is Phase 4), its `DownloadDescriptor`, its `InstallAsync` hook (a no-op
default, overridable to unpack an archive payload), its `NormalizeText` hook (an identity
default, overridable for Phase 4 text normalization), and its `LicenseName`/`LicenseUrl`
default-hook members (`"Unknown"`/`null` by default, overridable to declare a model's real
license name and an optional canonical URL to its full text). `IRecognitionModel` exposes a
public plain-data `AudioFormat` declaration alongside its public
`CreateBackend(installedModelDirectory)`, which returns the equally public `IRecognitionBackend`
seam; this lets hosts compose capture devices around a model's required format while a
third-party model implementation constructs and returns its own backend with no special assembly
access. `ISynthesisModel` similarly exposes a public best-effort `PreferredAudioFormat` hint
alongside its public `CreateBackend`, `CapabilityProfile`, and
`ResolveSpeakerId(parameterValues)` members. The hint is intentionally non-authoritative: the real
synthesis output rate is still the loaded engine's `ISynthesisBackend.SampleRate`.

`SpeechModelCatalog` composes a mutable known-model list - empty for the public constructor, since
the library ships zero built-in models, and populated by the host through `AddModels(...)`, which
returns the same catalog instance so registrations chain fluently (for example the sibling
`SpeechSherpa` system's `AddSherpaModels()` extension method) - with a `SpeechModelStore` and a
`SpeechModelDownloader`. Registration is a builder-phase step: every `AddModels` call must complete
before the catalog is shared for concurrent enumeration or download. `Enumerate()`
builds one immutable `SpeechModelDescriptor` snapshot per known model, resolving each model's
`SpeechModelState` by combining `SpeechModelStore.IsInstalled` (installed/not-installed) with
in-memory tracking of which model ids currently have a `DownloadAsync` call in flight
(`Downloading`) and which model ids' most recent attempt did not result in an installed model
(`FailedOrCorrupt`) - tracking scoped to one catalog instance's lifetime, since a bare on-disk
store has no durable concept of "currently downloading" or "last attempt failed". `DownloadAsync(
modelId, ...)` looks up the named model and delegates to `SpeechModelDownloader.DownloadAsync(
model, ...)` - the `ISpeechModel`-aware overload, so the model's own `InstallAsync` hook always
runs - throwing only when `modelId` matches no known model - an explicit, user-invoked action,
never composition or enumeration.
