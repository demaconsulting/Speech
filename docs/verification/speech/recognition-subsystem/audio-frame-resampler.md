### AudioFrameResampler

#### Verification Approach

The converter is verified as a pure function over plain float arrays (`AudioFrameResamplerTests`),
with a tolerance-based float comparer so assertions describe signal content rather than exact
binary floating-point representation. No doubles, devices, or models are involved.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- No additional setup beyond the standard test runner.

#### Acceptance Criteria

The unit is considered verified when it downmixes interleaved multi-channel audio to mono, converts
rates with above-target-Nyquist energy attenuated before downsampling, returns identity input
unchanged, and handles empty, single-sample, and rounds-to-zero inputs while rejecting
non-positive rates and channel counts.

#### Test Scenarios

##### Downmix

**Test**: `AudioFrameResampler_DownmixToMono_StereoInput_AveragesChannelsPerFrame`

**Test**: `AudioFrameResampler_DownmixToMono_TrailingPartialFrame_DiscardsPartialFrame`

**Test**: `AudioFrameResampler_DownmixToMono_SingleChannel_ReturnsSamplesUnchanged`

**Test**: `AudioFrameResampler_DownmixToMono_DestinationOverload_StereoInput_WritesAveragedFrames`

##### Rate conversion

**Test**: `AudioFrameResampler_Resample_Downsampling_ProducesProportionallyFewerSamples`

**Test**: `AudioFrameResampler_Resample_AboveTargetNyquistTone_IsAttenuated`

**Test**: `AudioFrameResampler_Resample_ShortInputDuringDownsampling_DoesNotThrow`

**Test**: `AudioFrameResampler_Resample_Upsampling_LinearlyInterpolatesBetweenSamples`

**Test**: `AudioFrameResampler_Convert_MonoAtTargetRate_ReturnsSamplesUnchanged`

**Test**: `AudioFrameResampler_Convert_MonoAtDifferentRate_ResamplesWithoutDownmix`

**Test**: `AudioFrameResampler_Convert_StereoAtHigherRate_DownmixesAndResamples`

##### Boundary handling

**Test**: `AudioFrameResampler_Convert_EmptyInput_ReturnsEmptyResult`

**Test**: `AudioFrameResampler_Resample_SingleSample_ClampsToThatSample`

**Test**: `AudioFrameResampler_Resample_OutputRoundsToZeroSamples_ReturnsEmptyResult`

**Test**: `AudioFrameResampler_Constructor_NonPositiveSourceRate_ThrowsArgumentOutOfRangeException`

**Test**: `AudioFrameResampler_Constructor_NonPositiveTargetRate_ThrowsArgumentOutOfRangeException`

**Test**: `AudioFrameResampler_Constructor_NonPositiveChannelCount_ThrowsArgumentOutOfRangeException`

**Test**: `AudioFrameResampler_DownmixToMono_NonPositiveChannelCount_ThrowsArgumentOutOfRangeException`

**Test**: `AudioFrameResampler_DownmixToMono_DestinationOverload_DestinationTooShort_ThrowsArgumentException`
