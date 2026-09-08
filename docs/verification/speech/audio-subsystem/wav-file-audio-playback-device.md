### WavFileAudioPlaybackDevice

#### Verification Approach

Verified through direct unit tests in `WavFileAudioPlaybackDeviceTests.cs`, writing to real
temporary files on disk (no fakes needed, since the device's only dependency is the Base Class
Library's own file I/O types) and round-tripping written samples back through
`WavFileAudioCaptureDevice` to confirm byte-for-byte-equivalent PCM encoding.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- Temporary files are created under the OS temp directory and deleted in a `finally` block after
  each test; no additional setup beyond the standard test runner.

#### Acceptance Criteria

Tests pass when the device always reports `IsAvailable = true`, written samples round-trip
within 16-bit quantization tolerance, samples beyond the `[-1.0, 1.0]` clamp boundary clamp to
the nearest valid 16-bit extreme, the reported format matches the constructor arguments,
`PendingSampleCount` always reads zero, `Start`/`Stop` never throw, invalid constructor arguments
and a null `Write` buffer throw the documented exceptions, writing after disposal throws
`ObjectDisposedException`, and disposing more than once is safe.

#### Test Scenarios

##### Write Then Dispose: Round-Trips Samples Within Quantization Tolerance

**Test**: `WavFileAudioPlaybackDevice_WriteThenDispose_RoundTripsSamplesWithinQuantizationTolerance`

##### Write: Samples Beyond Clamp Boundary Clamp to Nearest Valid Value

**Test**: `WavFileAudioPlaybackDevice_Write_SamplesBeyondClampBoundary_ClampToNearestValidValue`

##### Constructor: Null or Empty Path Throws ArgumentException

**Test**: `WavFileAudioPlaybackDevice_Constructor_NullOrEmptyPath_ThrowsArgumentException`

##### Constructor: Non-Positive Sample Rate Throws ArgumentOutOfRangeException

**Test**: `WavFileAudioPlaybackDevice_Constructor_NonPositiveSampleRate_ThrowsArgumentOutOfRangeException`

##### Constructor: Non-Positive Channel Count Throws ArgumentOutOfRangeException

**Test**: `WavFileAudioPlaybackDevice_Constructor_NonPositiveChannelCount_ThrowsArgumentOutOfRangeException`

##### Availability: Always Returns True

**Test**: `WavFileAudioPlaybackDevice_IsAvailable_Always_ReturnsTrue`

##### Format: Read Reflects Constructor Arguments

**Test**: `WavFileAudioPlaybackDevice_Format_Read_ReflectsConstructorArguments`

##### Start Then Stop: Always Does Not Throw

**Test**: `WavFileAudioPlaybackDevice_StartThenStop_Always_DoesNotThrow`

##### Pending Sample Count: After Write Returns Zero

**Test**: `WavFileAudioPlaybackDevice_PendingSampleCount_AfterWrite_ReturnsZero`

##### Write: Null Samples Throws ArgumentNullException

**Test**: `WavFileAudioPlaybackDevice_Write_NullSamples_ThrowsArgumentNullException`

##### Write: After Dispose Throws ObjectDisposedException

**Test**: `WavFileAudioPlaybackDevice_Write_AfterDispose_ThrowsObjectDisposedException`

##### Dispose: Called Twice Does Not Throw

**Test**: `WavFileAudioPlaybackDevice_Dispose_CalledTwice_DoesNotThrow`
