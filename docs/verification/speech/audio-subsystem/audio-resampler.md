### AudioResampler Verification

#### Verification Approach

Verified by `AudioResamplerTests.cs` (`test/DemaConsulting.Speech.Tests/AudioSubsystem`) using
generated sine tones, comparing resampled output with the ideal tone at the output rate.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK; project `test/DemaConsulting.Speech.Tests`
- **Isolation**: in-memory sample arrays and clips only; no files or network

#### Acceptance Criteria

Down-mix averages channels and drops a trailing partial frame, resampling preserves duration and
tone within 0.02 of full scale, equal rates copy, down-sampling of a constant signal preserves its
level (and extreme down-sampling keeps unity gain), and invalid arguments (null clip, non-positive
rate or channel count) throw.

#### Test Scenarios

##### Down-mix averages channels

**Test**: `AudioResampler_DownMix_AveragesChannels`

**Expected**: `[1, 0, 0.5, 0.5]` down-mixes to `[0.5, 0.5]`; mono input passes through.

##### Down-mix rejects a non-positive channel count

**Test**: `AudioResampler_DownMix_NonPositiveChannelCount_Throws`

**Expected**: A channel count of 0 or -1 throws `ArgumentOutOfRangeException`.

##### Down-mix drops a trailing partial frame

**Test**: `AudioResampler_DownMix_PartialTrailingFrame_IsDropped`

**Expected**: Stereo input `[1, 0, 0.75]` yields `[0.5]`.

##### Resampling keeps the tone

**Test**: `AudioResampler_Resample_ChangesRateKeepingTone`

**Expected**: A 440 Hz tone resampled 44100->24000, 16000->24000 and 48000->16000 has the
expected length (within one sample) and matches the ideal tone within 0.02 in the middle half.

##### Equal rate and empty input are trivial

**Test**: `AudioResampler_Resample_SameRateOrEmpty_AreTrivial`

**Expected**: Equal rates return an equal but distinct copy; empty input returns empty. Rate
validation is covered by the dedicated scenario below.

##### Invalid sample rates are rejected

**Test**: `AudioResampler_Resample_NonPositiveRates_Throw`

**Expected**: A source or target rate of 0 throws `ArgumentOutOfRangeException`.

##### Down-sampling preserves the level of a constant signal

**Test**: `AudioResampler_Resample_ConstantSignalDownsampled_PreservesLevel`

**Expected**: A constant 0.5 signal at 16000 Hz resampled to 8000 Hz yields output whose samples in
the middle half all equal 0.5 within 0.001.

##### Extreme down-sampling keeps unity gain

**Test**: `AudioResampler_Resample_ConstantSignalExtremeDownsample_KeepsUnityGainAtEdges`

**Expected**: A constant 1.0 signal resampled 48000->100, 48000->1000 and 44100->8000 yields a
non-empty output whose every sample, edges included, lies within 0.99 to 1.01.

##### Stereo clip to mono at the target rate

**Test**: `AudioResampler_ToMonoResampled_StereoClip_ProducesMonoAtTargetRate`

**Expected**: A 16 kHz stereo clip resampled to 24 kHz yields about 6000 non-silent mono samples.

##### Mono resampling rejects a null clip

**Test**: `AudioResampler_ToMonoResampled_NullClip_Throws`

**Expected**: A null clip throws `ArgumentNullException`.

##### ToMonoResampled rejects a non-positive target rate

**Test**: `AudioResampler_ToMonoResampled_NonPositiveTargetRate_Throws`

**Expected**: Target rates of 0 and -1 throw `ArgumentOutOfRangeException`.

##### Known gap: nearest-sample fallback

The defensive nearest-sample fallback for a kernel weight sum of 0.05 or less is not exercised: the
rate ratios the unit accepts never produce such a sum (the extreme down-sampling scenario checks the
smallest edge kernels), so no test can reach it without changing the implementation.
