### AudioClip Verification

#### Verification Approach

Verified by `AudioFileReaderTests.cs` (`test/DemaConsulting.Speech.Tests/AudioSubsystem`) with small
hand-built clips constructed directly from float arrays.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK; project `test/DemaConsulting.Speech.Tests`
- **Isolation**: in-memory float arrays only; no files and no network

#### Acceptance Criteria

Frame count, duration, mono down-mix and clamped PCM16 conversion are correct; null samples and a
non-positive sample rate or channel count are rejected both at construction and through a `with`
expression; an empty sample array is a valid zero-duration clip.

#### Test Scenarios

##### Invalid values are rejected

**Stimulus**: Construct a clip with null samples, with sample rate 0 and -1, and with channel
count 0 and -2; apply `with { SampleRate = 0 }` and `with { ChannelCount = 0 }` to a valid clip.

**Expected**: `ArgumentNullException` for null samples; `ArgumentOutOfRangeException` whose
`ParamName` is `SampleRate` or `ChannelCount` for the bad numbers and for both `with` cases; a
valid `with { SampleRate = 16000 }` succeeds.

**Test**: `AudioClip_InvalidValues_Throw` (`AudioFileReaderTests.cs`)

##### Empty samples have zero duration

**Stimulus**: Construct `new AudioClip([], 8000, 2, 16)`.

**Expected**: `FrameCount` is 0 and `Duration` is 0.0; no exception.

**Test**: `AudioClip_EmptySamples_HasZeroDuration` (`AudioFileReaderTests.cs`)

##### Derived members are consistent

**Stimulus**: A 2-channel, 4 Hz clip of eight samples including out-of-range (`2`, `-2`) and
`NaN` values; call `ToMono()` and `ToPcm16()`.

**Expected**: `FrameCount` 4 and `Duration` 1.0; mono `[0, 0.5, 0, NaN]`; PCM has eight samples
with `1.0 -> 32767`, `-1.0 -> -32768`, out-of-range values clamped, and `NaN -> 0`.

**Test**: `AudioClip_DerivedMembers_AreConsistent` (`AudioFileReaderTests.cs`)
