## ModelManagementSubsystem Verification

### Verification Approach

The ModelManagementSubsystem is verified through deterministic unit tests against a fake
`IModelDownloadClient` for `SpeechModelStore`'s atomic-swap/install-state logic and
`SpeechModelDownloader`'s queueing/verification/failure-state orchestration, plus a real
end-to-end test of `HttpModelDownloadClient` against an in-process `WireMock.Net`-stubbed HTTP
server. This proves both the pure orchestration logic (fast,
deterministic, no network) and the one real network-facing implementation (genuinely downloads
over HTTP) without requiring any external network access.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Isolation**: Each `SpeechModelStore`/`SpeechModelDownloader` test uses a unique scratch
  directory under `Path.GetTempPath()`, deleted on test disposal
- **Stubbed HTTP**: `HttpModelDownloadClientTests` starts an in-process `WireMock.Net`
  `WireMockServer`, stubbing one fixed response per test; no real network access

### Unit-Level Test Scenarios

See each unit's own verification document for its detailed test scenarios:
`speech-model-store.md`, `speech-model-store-options.md`, `speech-model-store-exception.md`,
`speech-model-downloader.md`, `speech-model-downloader-options.md`, `download-mirror.md`,
`speech-model-download-file.md`,
`speech-model-download-descriptor.md`, `speech-model-download-progress.md`,
`i-model-download-client.md`, `http-model-download-client.md`,
`speech-model-descriptor-enums.md`, `speech-model-parameters.md`, `speech-model-contract.md`,
`speech-model-descriptor.md`, `speech-model-catalog.md`.

### Acceptance Criteria

A ModelManagementSubsystem test run passes when: `SpeechModelStore` never reports a model installed
unless both `current/` and a valid manifest are present; a second `DownloadAsync` call for an
already-installed model id returns `Installed` immediately without any network or staging activity,
leaving a prior successful install completely untouched regardless of what the second call
supplies; a canceled download discards its staging directory and propagates
`OperationCanceledException` completely unchanged, never reclassified as a failure outcome; a
download/install failure is classified into `HttpError`, `NetworkBlocked`, `IoFailure`, or the
generic `Failed` fallback exactly as documented in `speech-model-downloader.md`; with no
`DownloadMirror` configured every file resolves and fetches from its own declared URI completely
unchanged, and with one configured every file resolves beneath the mirror, with its configured
authentication applied - by the `HttpModelDownloadClient` instance constructed with it - to every
request that instance issues; `HttpModelDownloadClient` genuinely
downloads exact bytes with monotonically increasing progress from a WireMock.Net-stubbed server,
throwing `HttpRequestException` for a non-2xx response, and sending a preemptive `Authorization`
header (Basic or Bearer) only when a mirror is supplied at construction; every tunable-parameter descriptor
rejects an internally inconsistent
range/option-set/default at construction; `SpeechModelParameterDiagnostics` throws
`ArgumentException` for a value invalid for a parameter a model declares while silently ignoring
(with only an `Info` diagnostic) a supplied key naming a parameter the model does not declare; and
`SpeechModelCatalog` correctly reports `NotDownloaded`, `Downloading`, `Downloaded`, and
`FailedOrCorrupt` for an injected fake model as a download is requested, in progress, completes, or
fails, using injected fakes for deterministic state-resolution coverage, and with models registered
through `AddModels` appearing in its enumeration. The concrete
`IRecognitionModel`/`ISynthesisModel` implementations shipped by this repository are not part of
this subsystem; they are verified by the sibling SpeechSherpa library (see _SpeechSherpa
ModelManagementSubsystem Verification_).
