### WavFileAudioCaptureDevice

#### Verification Approach

Verified through direct unit tests in `WavFileAudioCaptureDeviceTests.cs`, reusing the existing
`crossing-the-bar-16k-mono.wav` test fixture (16 kHz mono 16-bit PCM) already present in
`TestData/` for the fixture-based scenarios, plus small ad hoc files (including files written
through `WavFileAudioPlaybackDevice`) for negative/format-rejection scenarios, rather than
authoring new binary fixtures.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Fixture**: `test/DemaConsulting.Speech.Tests/TestData/crossing-the-bar-16k-mono.wav`
- No additional setup beyond the standard test runner.

#### Acceptance Criteria

Tests pass when starting capture against the fixture file delivers the exact total sample count
declared by the file's `data` chunk, `EndOfFileReached` fires exactly once and only after the
last `FrameCaptured` event, the reported format matches the fixture's known mono/16 kHz format,
the reported format is zero before `Start` is called, a non-existent file, a non-RIFF file, and a
stereo file all throw `InvalidOperationException` from `Start`, calling `Stop` from within a
`FrameCaptured` handler interrupts delivery before the file is fully consumed and suppresses
`EndOfFileReached`, the device always reports `IsAvailable = true`, and invalid constructor
arguments throw the documented exceptions.

#### Test Scenarios

##### Start: Fixture File Delivers Expected Total Sample Count

**Test**: `WavFileAudioCaptureDevice_Start_FixtureFile_DeliversExpectedTotalSampleCount`

##### Start: Fixture File Raises EndOfFileReached Exactly Once After Last Frame

**Test**: `WavFileAudioCaptureDevice_Start_FixtureFile_RaisesEndOfFileReachedExactlyOnceAfterLastFrame`

##### Start: Fixture File Reports Mono 16 kHz Format

**Test**: `WavFileAudioCaptureDevice_Start_FixtureFile_ReportsMonoSixteenKilohertzFormat`

##### Format: Before Start Returns Zero

**Test**: `WavFileAudioCaptureDevice_Format_BeforeStart_ReturnsZero`

##### Start: Missing File Throws InvalidOperationException

**Test**: `WavFileAudioCaptureDevice_Start_MissingFile_ThrowsInvalidOperationException`

##### Start: Not a RIFF File Throws InvalidOperationException

**Test**: `WavFileAudioCaptureDevice_Start_NotARiffFile_ThrowsInvalidOperationException`

##### Start: Stereo File Throws InvalidOperationException

**Test**: `WavFileAudioCaptureDevice_Start_StereoFile_ThrowsInvalidOperationException`

##### Stop: Called During FrameCaptured Interrupts Delivery Without EndOfFileReached

**Test**: `WavFileAudioCaptureDevice_Stop_CalledDuringFrameCaptured_InterruptsDeliveryWithoutEndOfFileReached`

##### Availability: Always Returns True

**Test**: `WavFileAudioCaptureDevice_IsAvailable_Always_ReturnsTrue`

##### Constructor: Null or Empty Path Throws ArgumentException

**Test**: `WavFileAudioCaptureDevice_Constructor_NullOrEmptyPath_ThrowsArgumentException`

##### Constructor: Non-Positive Frame Sample Count Throws ArgumentOutOfRangeException

**Test**: `WavFileAudioCaptureDevice_Constructor_NonPositiveFrameSampleCount_ThrowsArgumentOutOfRangeException`
