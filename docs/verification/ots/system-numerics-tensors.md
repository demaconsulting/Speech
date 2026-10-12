## System.Numerics.Tensors Verification

This document provides the verification evidence for the System.Numerics.Tensors OTS software
item. Requirements for this OTS item are defined in the System.Numerics.Tensors OTS Software
Requirements document.

### Required Functionality

System.Numerics.Tensors provides the vectorized `TensorPrimitives` operations the Speech library
uses for bulk float math: PCM-to-float conversion in `WavFileAudioCaptureDevice`, the windowed-sinc
convolution behind `AudioResampler`, and the volume scale-and-clamp in `SynthesisSession`.

### Verification Approach

Automated verification exercises the Speech library code paths that call the package, using
deterministic synthetic signals and small fixtures so no hardware or network is involved:

- A fixture WAV file read through the file-backed capture device yields the expected total number
  of normalized samples
- Resampling a tone changes its rate while keeping its frequency, which exercises the
  convolution
- A volume override scales and clamps synthesized samples

Automated tests do **not** claim proof of the package's internal vectorization; they prove that
the results the library derives from it are numerically correct.

### Test Scenarios

#### WavFileAudioCaptureDevice_Start_FixtureFile_DeliversExpectedTotalSampleCount

**Scenario**: A fixture WAV file is streamed through `WavFileAudioCaptureDevice`.

**Expected**: The expected total number of converted samples is delivered.

**Requirement coverage**: `Speech-OTS-SystemNumericsTensors-VectorizedAudioMath`.

#### AudioResampler_Resample_ChangesRateKeepingTone

**Scenario**: A tone is resampled between several rate pairs.

**Expected**: The output rate changes and the tone's frequency is preserved.

**Requirement coverage**: `Speech-OTS-SystemNumericsTensors-VectorizedAudioMath`.

#### SynthesisSession_SynthesizeAsync_VolumeOverride_ScalesAndClampsSamples

**Scenario**: A volume override above unity is applied to synthesized samples.

**Expected**: Samples are scaled and clamped to the valid range.

**Requirement coverage**: `Speech-OTS-SystemNumericsTensors-VectorizedAudioMath`.

### Requirements Coverage

- **`Speech-OTS-SystemNumericsTensors-VectorizedAudioMath`**:
  `WavFileAudioCaptureDevice_Start_FixtureFile_DeliversExpectedTotalSampleCount`,
  `AudioResampler_Resample_ChangesRateKeepingTone`,
  `SynthesisSession_SynthesizeAsync_VolumeOverride_ScalesAndClampsSamples`
