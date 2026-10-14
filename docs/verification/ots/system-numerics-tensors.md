## System.Numerics.Tensors Verification

This document provides the verification evidence for the System.Numerics.Tensors OTS software
item. Requirements for this OTS item are defined in the System.Numerics.Tensors OTS Software
Requirements document.

### Required Functionality

System.Numerics.Tensors provides the `TensorPrimitives` operations the Speech library uses for float
math: PCM-to-float conversion and division in `WavFileAudioCaptureDevice`, the dot product inside
`WindowedSincLowpassFilter`, and the volume multiply-and-clamp in `SynthesisSession`.

### Verification Approach

Automated verification exercises the Speech library code paths that call the package, using
deterministic synthetic signals and small fixtures so no hardware or network is involved. Each
requirement's observable numeric result is the result demonstrated by the test that asserts it:

- **SampleConversion**: `WavFileAudioCaptureDevice_Start_KnownPcmSamples_DeliversNormalizedFloats` shows PCM
  values 0, 32767, -32767, and 16384 become the same values as floats before scaling.
- **SampleDivision**: the same test shows each delivered sample equals the PCM value divided by 32767.
- **SampleMultiplication**: `SynthesisSession_SynthesizeAsync_VolumeOverride_ScalesAndClampsSamples` shows each
  in-range sample equals the input times the volume ratio.
- **SampleClamping**: the same test shows each scaled sample beyond full scale is limited to the -1 to 1 range.
- **DotProduct**: `WindowedSincLowpassFilter_ApplyLowpassFilter_InteriorSample_EqualsKernelDotProduct` shows an
  interior filter output equals the hand-summed product of the kernel and the signal window.

### Test Scenarios

#### WavFileAudioCaptureDevice_Start_FixtureFile_DeliversExpectedTotalSampleCount

**Scenario**: A 16-bit PCM mono 16 kHz fixture WAV file is streamed through
`WavFileAudioCaptureDevice`.

**Expected**: The total number of delivered samples equals the file's data size divided by two
bytes per sample. This scenario checks the sample count only; sample values are verified by the
known-samples scenario below.

#### WavFileAudioCaptureDevice_Start_KnownPcmSamples_DeliversNormalizedFloats

**Scenario**: A WAV file holding the PCM values 0, 32767, -32767, and 16384 is streamed through
`WavFileAudioCaptureDevice`.

**Expected**: Each delivered sample equals the PCM value divided by 32767.

#### WindowedSincLowpassFilter_ApplyLowpassFilter_InteriorSample_EqualsKernelDotProduct

**Scenario**: A five-tap kernel is applied to a ten-sample signal.

**Expected**: The output at an interior index equals the sum of the element-wise products of the
kernel and the five-sample window of the signal centered on that index.

#### SynthesisSession_SynthesizeAsync_VolumeOverride_ScalesAndClampsSamples

**Scenario**: A volume override above unity is applied to synthesized samples.

**Expected**: Samples are scaled by the ratio and clamped to the range -1 to 1.
