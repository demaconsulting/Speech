## SpeechDemo SynthesisPanelSubsystem Verification

### Verification Approach

The SynthesisPanelSubsystem is verified at subsystem level by the `SynthesisPanelViewModel`
working with its seams: the catalog service, the device service and the session factory.
Unit-level behavior is verified in the unit verification documents for SynthesisPanelViewModel and
SynthesizerSessionFactory.

The seams are replaced by NSubstitute and hand-written fakes, so every lifecycle transition and
every unavailable-state path is produced on demand with no downloaded model, no native runtime and
no real speakers. The system-level integration test additionally proves the panel composes over the
real seam without a downloaded model.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Isolation**: Every test builds its own view model; no shared state exists between tests
- **Test doubles**: NSubstitute fakes of `IModelCatalogService`, `IAudioDeviceService` and
  `ISynthesizerSessionFactory`; hand-written `FakeSpeechSynthesizerEngine` and
  `FakeSynthesisSession`

### Acceptance Criteria

A SynthesisPanelSubsystem test run passes when: Play speaks the entered text through the selected
installed model and device; Stop interrupts an in-flight Play and reports it; an unavailable
playback device produces an error state; the panel refreshes its models while keeping the chosen
settings when the catalog or the reference voices change; and playback is stopped and awaited
before a shared device refresh.

### Test Scenarios

#### SynthesisPanelViewModel_Play_SuccessfulSession_TransitionsThroughLifecycleToIdle

**Scenario**: A full Play with a working engine and session.

**Expected**: `State` passes through `Synthesizing` then `Playing` before settling on `Idle`, no
status message is left, and the exact entered text reaches the session.

#### SynthesisPanelViewModel_Stop_DuringPlayback_CancelsSessionAndReportsStopped

**Scenario**: Stop is invoked while Play is in flight.

**Expected**: The session is stopped, the panel settles on `Idle` and reports the stopped message.

#### SynthesisPanelViewModel_Play_NoPlaybackDevice_ReportsErrorState

**Scenario**: No playback device is available.

**Expected**: The panel reports an error state rather than throwing.

#### SynthesisPanelViewModel_ModelInstalled_MatchingRole_TriggersRefresh

**Scenario**: The catalog raises `ModelInstalled` for a synthesis model.

**Expected**: The panel refreshes and offers the new model.

#### SynthesisPanelViewModel_ReferenceVoicesChanged_RefreshesAndKeepsChosenVoice

**Scenario**: The catalog raises `ReferenceVoicesChanged` with a rebuilt model.

**Expected**: The panel refreshes and keeps the voice the user had chosen.

#### SynthesisPanelViewModel_PreRefreshHook_WhilePlaying_StopsAndAwaitsExecutionTaskBeforeDeviceRefreshSucceeds

**Scenario**: A Play is in flight and the shared device list is refreshed.

**Expected**: Playback is stopped and awaited before the refresh proceeds, which then succeeds.
