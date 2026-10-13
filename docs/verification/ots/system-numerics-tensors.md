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
- A WAV file holding known PCM values yields exactly those values divided by 32767, which checks
  the numerical result of the conversion and division
- Resampling a tone changes its rate while keeping its frequency (within 0.02 of the ideal tone
  away from the edges), which exercises the convolution
- Downsampling a constant signal preserves its level, a direct numeric check of the dot product
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

#### AudioResampler_Resample_ChangesRateKeepingTone

**Scenario**: A tone is resampled between several rate pairs.

**Expected**: The output length matches the new rate and the tone's frequency is preserved: away
from the edges, each sample is within 0.02 of the ideal 440 Hz tone. The rate pairs are 44100 to
24000, 16000 to 24000, and 48000 to 16000 Hz.

#### AudioResampler_Resample_ConstantSignalDownsampled_PreservesLevel

**Scenario**: A constant 0.5 signal is downsampled from 16000 Hz to 8000 Hz.

**Expected**: Away from the edges every output sample equals 0.5 within 0.001.

#### SynthesisSession_SynthesizeAsync_VolumeOverride_ScalesAndClampsSamples

**Scenario**: A volume override above unity is applied to synthesized samples.

**Expected**: Samples are scaled by the ratio and clamped to the range -1 to 1.
