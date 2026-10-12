### AudioResampler Verification

#### Verification Approach

Verified by `AudioResamplerTests.cs` using generated sine tones, comparing resampled output with the
ideal tone at the output rate.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK; project `test/DemaConsulting.Speech.Tests`
- **Isolation**: in-memory images and files in the test output or temp directory; no network

#### Acceptance Criteria

Down-mix averages channels, resampling preserves duration and tone within 0.02 of full scale, equal
rates copy, and invalid arguments throw.

#### Test Scenarios

##### Down-mix averages channels

**Test**: `AudioResampler_DownMix_AveragesChannels`

##### Resampling keeps the tone

**Test**: `AudioResampler_Resample_ChangesRateKeepingTone`

##### Trivial cases

**Test**: `AudioResampler_Resample_SameRateOrEmpty_AreTrivial`

**Test**: `AudioResampler_Resample_ConstantSignalExtremeDownsample_KeepsUnityGainAtEdges`

##### Stereo clip to mono at the target rate

**Test**: `AudioResampler_ToMonoResampled_StereoClip_ProducesMonoAtTargetRate`
