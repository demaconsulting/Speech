### AudioClip Verification

#### Verification Approach

Verified by `AudioClipTests.cs` (`test/DemaConsulting.Speech.Tests/AudioSubsystem`) with small
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

**Test**: `AudioClip_InvalidValues_Throw` (also asserts `with { Samples = null }` throws)

##### Empty samples have zero duration

**Stimulus**: Construct `new AudioClip([], 8000, 2, 16)`.

**Expected**: `FrameCount` is 0 and `Duration` is 0.0; no exception.

**Test**: `AudioClip_EmptySamples_HasZeroDuration`

##### Derived members are consistent

**Stimulus**: A 2-channel, 4 Hz clip of eight samples including out-of-range (`2`, `-2`) and
`NaN` values; call `ToMono()` and `ToPcm16()`.

**Expected**: `FrameCount` 4 and `Duration` 1.0; mono `[0, 0.5, 0, NaN]`; PCM is exactly
`[32767, -32768, 16384, 16384, 32767, -32768, 0, 0]` (all eight samples asserted: `1.0 -> 32767`,
`-1.0 -> -32768`, out-of-range values clamped, and `NaN -> 0`).

**Test**: `AudioClip_DerivedMembers_AreConsistent`

##### Non-multiple sample count

**Stimulus**: A 2-channel, 4 Hz clip of three samples `[1, 0, 0.5]`.

**Expected**: `FrameCount` 1, `Duration` 0.25, `ToMono()` `[0.5]` (the stray sample is dropped) and
`ToPcm16()` has three samples.

**Test**: `AudioClip_NonMultipleFrameCount_TruncatesToWholeFrames`

##### BitsPerSample is not validated

**Stimulus**: `with { BitsPerSample = -1 }` on a valid clip, and a clip constructed with 0 bits.

**Expected**: Both succeed and report the given value.

**Test**: `AudioClip_BitsPerSample_IsNotValidated`

##### Mono down-mix averages channels

**Stimulus**: A 2-channel clip `[1, 0, 0.5, 0.5]`; call `ToMono()`.

**Expected**: `[0.5, 0.5]`.

**Test**: `AudioClip_ToMono_AveragesChannels`

##### Mono clip down-mix is an independent copy

**Stimulus**: A mono clip `[0.25, -0.25]`; call `ToMono()` and change the result.

**Expected**: The clip's own `Samples` are unchanged.

**Test**: `AudioClip_ToMono_MonoClip_ReturnsIndependentCopy`

##### NaN propagates through the down-mix

**Stimulus**: A stereo clip `[NaN, 1, 0.5, 0.5]`; call `ToMono()`.

**Expected**: The first mono sample is NaN and the second is 0.5 (incidental behavior, recorded
so a change is noticed).

**Test**: `AudioClip_ToMono_NaNSample_PropagatesToFrame`

##### PCM16 conversion clamps out-of-range samples

**Stimulus**: A mono clip `[2, -2, 0]`; call `ToPcm16()`.

**Expected**: `[32767, -32768, 0]`.

**Test**: `AudioClip_ToPcm16_ClampsOutOfRangeSamples`

##### PCM16 conversion scales by 32768 with rounding

**Stimulus**: A mono clip `[1, -1, 0.5, 1/65536, -1/65536]`; call `ToPcm16()`.

**Expected**: `[32767, -32768, 16384, 0, 0]` (+1.0 clamps; half a step rounds to even).

**Test**: `AudioClip_ToPcm16_ScalesWithRounding`
