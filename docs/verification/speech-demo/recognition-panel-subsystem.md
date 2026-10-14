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
attempted (and is a safe no-op while idle), so the refresh succeeds deterministically; and the
capture-debug recorder contains every diagnostic fault (late frames, write failures, finalization failures, repeated
disposal) so
listening is never affected, while the WAV writer produces valid, clamped, correctly rejected
output as described in the scenarios.

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

### Supporting Types: Capture Debug Recording

The temporary capture-debug facility (`CaptureDebugRecorder` and `WavFileWriter`) is verified
against a per-test temporary folder, with the process-wide capture-debug environment variable
saved and restored, in a non-parallel test collection.

#### CaptureDebugRecorder_TryStart_EnvironmentVariableUnset_ReturnsNull

**Scenario**: The capture-debug environment variable is unset.

**Expected**: No recorder is created, so nothing is written to disk.

#### CaptureDebugRecorder_TryStart_RecordsFramesThenDisposed_WritesValidFinalizedWavFile

**Scenario**: The variable names a folder; frames are recorded and the recorder is disposed.

**Expected**: One WAV file with a valid RIFF/WAVE header at the captured sample rate and channel
count, holding every recorded sample.

#### CaptureDebugRecorder_Dispose_NoFramesCaptured_StillWritesValidFinalizedWavFile

**Scenario**: The recorder is disposed without any frame.

**Expected**: A valid, empty WAV file is still written.

#### CaptureDebugRecorder_Dispose_CalledTwice_DoesNotThrow

**Scenario** (boundary): The recorder is disposed twice.

**Expected**: No exception.

#### CaptureDebugRecorder_FrameCallback_AfterDispose_IsDiscardedWithoutThrowing

**Scenario** (error/race): A captured frame is delivered to the recorder's callback after the
recorder has been disposed.

**Expected**: The frame is discarded and no exception reaches the caller (the audio callback).

#### CaptureDebugRecorder_FrameCaptured_WriteThrowsUnauthorized_StopsWritingAndFinalizesOnce

**Scenario** (error): The WAV writer throws `UnauthorizedAccessException` on a write.

**Expected**: The failure is contained and logged, no exception escapes, no further frames are
written, and the file is finalized exactly once.

#### CaptureDebugRecorder_FrameCaptured_WriterStalled_DropsFramesBeyondCapacityWithoutBlocking

**Scenario** (error/backpressure): The writer blocks on its first frame while more frames arrive
than the bounded queue can hold; the writer is released by a signal afterwards.

**Expected**: Every capture callback returns without waiting, exactly the overflow frames are
counted as dropped, and the frames that fit are written once the writer resumes.

#### CaptureDebugRecorder_Dispose_WriterFinalizationThrows_IsContained

**Scenario** (error): Finalizing the WAV output throws during disposal.

**Expected**: The exception is caught and logged; `Dispose()` does not throw and the process is
not terminated.

### Supporting Types: WavFileWriter

#### WavFileWriter_Dispose_AfterWritingSamples_WritesValidRiffWaveHeader

**Scenario** (normal): Samples are written and the writer is disposed.

**Expected**: The file has a valid RIFF/WAVE header whose sizes, sample rate, and channel count
match the data written.

#### WavFileWriter_WriteSamples_NormalizedFloatSamples_RoundTripAsPcm16

**Scenario** (normal): Normalized floating-point samples are written.

**Expected**: Each sample is stored as the expected 16-bit PCM value and round-trips.

#### WavFileWriter_WriteSamples_OutOfRangeSample_ClampsToBoundary

**Scenario** (boundary): Samples beyond the normalized range (above 1.0, below -1.0) are written.

**Expected**: They are clamped to the 16-bit PCM boundary values without overflow.

#### WavFileWriter_Constructor_EmptyPath_ThrowsArgumentException

**Scenario** (error): The writer is constructed with an empty path.

**Expected**: `ArgumentException`.

#### WavFileWriter_Constructor_NonPositiveSampleRate_ThrowsArgumentOutOfRangeException

**Scenario** (error/boundary): The writer is constructed with a sample rate of zero or less.

**Expected**: `ArgumentOutOfRangeException`.

#### WavFileWriter_Constructor_NonPositiveChannelCount_ThrowsArgumentOutOfRangeException

**Scenario** (error/boundary): The writer is constructed with a channel count of zero or less.

**Expected**: `ArgumentOutOfRangeException`.

#### WavFileWriter_Dispose_CalledTwice_IsIdempotent

**Scenario** (boundary): The writer is disposed twice.

**Expected**: No exception, and the file is not corrupted by the second call.
