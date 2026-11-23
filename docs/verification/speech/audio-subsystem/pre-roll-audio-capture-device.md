### PreRollAudioCaptureDevice

#### Verification Approach

Verified through direct unit tests in `PreRollAudioCaptureDeviceTests.cs`. The underlying device
is an NSubstitute `IAudioCaptureDevice` whose `FrameCaptured` event the test raises directly, so
blocks "captured while stopped" and "captured while started" are produced deterministically with
no audio hardware and no timing. Formats are chosen so the 300 ms default pre-roll is a small,
exact sample count (1 kHz mono: 300 samples; 10 Hz stereo: 3 frames), making the expected
pre-roll content exact. The unavailable case uses the library's own
`UnavailableAudioCaptureDevice`.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Test doubles**: NSubstitute capture devices
- No additional setup beyond the standard test runner.

#### Acceptance Criteria

Tests pass when audio captured while stopped is delivered as the first block of the next run and
before any live block; only the newest pre-roll duration is delivered, oldest first, in whole
interleaved frames; audio delivered in one run is never delivered again; a non-zero pre-roll
starts the underlying device once and never stops it between runs, while a zero pre-roll stops it
on every `Stop` and delivers nothing captured while stopped; disposal stops the underlying device
once and leaves the device unavailable and unable to start; the default pre-roll is 300 ms and the
reported format is the underlying device's; blocks the underlying device raises during its own first
start are delivered; a finite WAV file source is rejected; invalid constructor arguments throw the documented
exceptions; and an unavailable underlying device is reported as unavailable and rejected by
`Start`.

#### Test Scenarios

##### Start: Audio Captured While Stopped Is Delivered First

**Test**: `PreRollAudioCaptureDevice_Start_AfterStop_DeliversAudioCapturedWhileStoppedFirst`

##### Start: Long Pause Delivers Only the Most Recent Pre-Roll

**Test**: `PreRollAudioCaptureDevice_Start_AfterLongPause_DeliversOnlyMostRecentPreRoll`

##### Start: Stereo Pre-Roll Keeps Whole Frames

**Test**: `PreRollAudioCaptureDevice_Start_StereoDevice_PreRollKeepsWholeFrames`

##### Start: Audio Delivered in a Previous Run Is Not Replayed

**Test**: `PreRollAudioCaptureDevice_Start_AudioDeliveredInPreviousRun_IsNotReplayed`

##### Stop: Non-Zero Pre-Roll Keeps the Underlying Device Running

**Test**: `PreRollAudioCaptureDevice_Stop_WithPreRoll_KeepsUnderlyingDeviceRunning`

##### Stop: Zero Pre-Roll Stops the Underlying Device and Delivers No Pre-Roll

**Test**: `PreRollAudioCaptureDevice_Stop_ZeroPreRoll_StopsUnderlyingDeviceAndDeliversNoPreRoll`

##### Dispose: Stops the Underlying Device and Reports Unavailable

**Test**: `PreRollAudioCaptureDevice_Dispose_StopsUnderlyingDeviceAndReportsUnavailable`

##### Constructor: Defaults and Reported Format

**Test**: `PreRollAudioCaptureDevice_Constructor_Defaults_ReportsDefaultPreRollAndUnderlyingFormat`

##### Constructor: Invalid Arguments Throw

**Test**: `PreRollAudioCaptureDevice_Constructor_InvalidArguments_Throw`

##### Start: Blocks Raised During the Underlying Device's Own Start Are Delivered

**Test**: `PreRollAudioCaptureDevice_Start_DeviceRaisesFramesDuringItsOwnStart_DeliversThem`

##### Constructor: Finite File Source Is Rejected

**Test**: `PreRollAudioCaptureDevice_Constructor_FiniteFileSource_ThrowsArgumentException`

##### Start: Unavailable Underlying Device Throws AudioDeviceUnavailableException

**Test**: `PreRollAudioCaptureDevice_Start_UnavailableUnderlyingDevice_ThrowsAudioDeviceUnavailableException`
