## System.Numerics.Tensors Verification

This document provides the verification evidence for the System.Numerics.Tensors OTS software
item. Requirements for this OTS item are defined in the System.Numerics.Tensors OTS Software
Requirements document.

### Required Functionality

System.Numerics.Tensors provides the vectorized `TensorPrimitives` operations the Speech library
uses for bulk float math: PCM-to-float conversion in `WavFileAudioCaptureDevice`, the windowed-sinc
convolution inside `WindowedSincLowpassFilter` (used by `AudioFrameResampler` and
`PlaybackAudioResampler`), and the volume scale-and-clamp in `SynthesisSession`.

### Verification Approach

Automated verification exercises the Speech library code paths that call the package, using
deterministic synthetic signals and small fixtures so no hardware or network is involved:

- A fixture WAV file read through the file-backed capture device yields the expected total number
  of normalized samples
- A WAV file holding known PCM values yields exactly those values divided by 32767, which checks
  the numerical result of the conversion and division
- Downsampling a tone above the target Nyquist frequency through the recognition and synthesis
  resamplers attenuates it relative to an in-band tone, which exercises the lowpass convolution
  built on the dot product
- A volume override above unity scales and clamps synthesized samples

Automated tests do **not** claim proof of the package's internal vectorization; they prove that
the results the library derives from it are numerically correct.

### Test Scenarios

#### WavFileAudioCaptureDevice_Start_FixtureFile_DeliversExpectedTotalSampleCount

**Scenario**: A 16-bit PCM mono 16 kHz fixture WAV file is streamed through
`WavFileAudioCaptureDevice`.

**Expected**: The total number of delivered samples equals the file's data size divided by two
bytes per sample. Sample values are verified by the known-samples scenario below.

#### WavFileAudioCaptureDevice_Start_KnownPcmSamples_DeliversNormalizedFloats

**Scenario**: A WAV file holding the PCM values 0, 32767, -32767, and 16384 is streamed through
`WavFileAudioCaptureDevice`.

**Expected**: Each delivered sample equals the PCM value divided by 32767.

#### AudioFrameResampler_Resample_AboveTargetNyquistTone_IsAttenuated

**Scenario**: Equally loud 1 kHz and 12 kHz tones at 48 kHz are downsampled to 16 kHz through
`AudioFrameResampler`, whose anti-aliasing filter applies its kernel with `TensorPrimitives.Dot`.

**Expected**: The root mean square of the 12 kHz result is below 0.35 times that of the 1 kHz
result.

#### PlaybackAudioResampler_Resample_AboveTargetNyquistTone_IsAttenuated

**Scenario**: The same 1 kHz and 12 kHz tones at 48 kHz are downsampled to 16 kHz through
`PlaybackAudioResampler`, which uses the same `WindowedSincLowpassFilter`.

**Expected**: The root mean square of the 12 kHz result is below 0.35 times that of the 1 kHz
result.

#### SynthesisSession_SynthesizeAsync_VolumeOverride_ScalesAndClampsSamples

**Scenario**: A volume override above unity is applied to synthesized samples.

**Expected**: Samples are scaled by the ratio and clamped to the range -1 to 1.
