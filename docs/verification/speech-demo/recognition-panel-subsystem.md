## SpeechDemo RecognitionPanelSubsystem Verification

### Verification Approach

The RecognitionPanelSubsystem is verified through deterministic unit tests. `RecognitionPanelViewModel`
is tested against NSubstitute/fake doubles of `IModelCatalogService`, `IAudioDeviceService`,
`IRecognizerSessionFactory`, `ISpeechRecognizerEngine`, and `IRecognitionSession`, which lets every
async Start/Stop lifecycle transition, state derivation, the transcript sequencing, the two-tier
engine/session cache invalidation, and every unavailable-state path be produced on demand with no
downloaded model, no native runtime, and no real microphone. `RecognizerSessionFactory` is tested
directly. Subsystem-level scenarios, below, cover behavior that spans units: composing the engine
through the seam, starting a session, and cooperating with the shared device-selection panel.
Unit behavior is verified in each unit's own verification document
(`recognition-panel-subsystem/recognition-panel-view-model.md` and
`recognition-panel-subsystem/recognizer-session-factory.md`).

The system-level integration test additionally proves the panel composes over the real seam
without a downloaded model. `CaptureDebugRecorder` and `WavFileWriter` are verified by
`CaptureDebugRecorderTests` and `WavFileWriterTests`.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Isolation**: `RecognizerSessionFactoryTests` roots the library's model store in a fresh
  directory under the test output folder
- **Test doubles**: NSubstitute fakes of `IModelCatalogService`, `IAudioDeviceService`, and
  `IRecognizerSessionFactory`; hand-written `FakeSpeechRecognizerEngine` and
  `FakeRecognitionSession` (from `Fakes/`) standing in for `ISpeechRecognizerEngine` and
  `IRecognitionSession`; `FakeSpeechModel`

### Acceptance Criteria

A RecognitionPanelSubsystem test run passes when: a successful Start composes an engine through the
seam, creates a session, and enters the listening state with the session's `StartAsync()` invoked
exactly once; an engine the seam reports as unavailable is explained and disposed; and the panel's
registered pre-refresh hook stops an actively listening session before a shared device refresh is
attempted (and is a safe no-op while idle), so the refresh succeeds deterministically.

### Test Scenarios

#### RecognitionPanelViewModel_Start_SuccessfulSession_EntersListeningState

**Scenario**: A successful Start with a working engine and session.

**Expected**: `State` becomes `Listening` and the session's `StartAsync()` is invoked exactly
once.

#### RecognitionPanelViewModel_Start_RecognizerUnavailable_ReportsErrorStateAndDisposes

**Scenario**: The session seam loads an engine that honestly reports itself unavailable.

**Expected**: `RecognizerUnavailableMessage` and `Error` state, and the unavailable engine is
disposed.

#### RecognitionPanelViewModel_PreRefreshHook_WhileListening_StopsSessionBeforeDeviceRefreshSucceeds

**Scenario**: The panel is actively listening, sharing a `DeviceSelectionViewModel` whose device
service refuses `RefreshDevices()` with `AudioDeviceInUseException` only while a "still
listening" flag is true; the session's `StopAsync()` flips that flag false. The shared
`DeviceSelectionViewModel.RefreshAsync()` is then invoked, as the "Refresh devices" button would.

**Expected**: The panel's registered pre-refresh hook calls `StopAsync()` on the session before
the device refresh is attempted, so the refresh completes without throwing; the panel returns to
`Idle` with `CanStop` false.

#### RecognitionPanelViewModel_PreRefreshHook_WhileIdle_IsNoOpAndDeviceRefreshSucceeds

**Scenario**: The registered pre-refresh hook runs while no listening session was ever started
(no engine, no session created).

**Expected**: The refresh completes without throwing, no session is created, and the panel
remains `Idle`.
