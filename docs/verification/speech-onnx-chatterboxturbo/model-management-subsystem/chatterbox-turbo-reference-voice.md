### ChatterboxTurboReferenceVoice Verification

#### Verification Approach

Verified by `ChatterboxTurboReferenceVoiceTests.cs` using generated sine tones, WAV files written
to a scratch directory and a small FLAC fixture (`TestData/voice-mono-16-16000.flac`).

#### Test Environment

- **Framework**: xUnit v3; project `test/DemaConsulting.Speech.Onnx.ChatterboxTurbo.Tests`
- **Isolation**: scratch directory, deleted on dispose; no network

#### Acceptance Criteria

Mono 24 kHz samples are kept; other layouts are down-mixed and resampled preserving tone; long clips
are trimmed and short clips rejected; WAV and FLAC files load; bad files and arguments throw the documented
exceptions.

#### Test Scenarios

##### Mono 24 kHz samples are kept

**Test**: `ChatterboxTurboReferenceVoice_FromSamples_Mono24k_KeepsSamples`

##### Stereo 48 kHz is down-mixed and resampled

**Test**: `ChatterboxTurboReferenceVoice_FromSamples_Stereo48k_IsDownMixedAndResampled`

##### Too-long clips are trimmed

**Test**: `ChatterboxTurboReferenceVoice_FromSamples_TooLong_IsTrimmed`

##### Too-short clips are rejected

**Test**: `ChatterboxTurboReferenceVoice_FromSamples_TooShort_Throws`

##### Invalid arguments throw

**Test**: `ChatterboxTurboReferenceVoice_FromSamples_InvalidArguments_Throw`

##### PCM16 WAV loads

**Test**: `ChatterboxTurboReferenceVoice_FromFile_Pcm16Wav_LoadsVoice`

##### Stereo WAV loads

**Test**: `ChatterboxTurboReferenceVoice_FromFile_StereoWav_LoadsVoice`

##### Float32 WAV loads

**Test**: `ChatterboxTurboReferenceVoice_FromFile_Float32Wav_LoadsVoice`

##### FLAC loads at its natural duration

**Test**: `ChatterboxTurboReferenceVoice_FromFile_Flac_LoadsVoice`

##### Bad files throw distinct exceptions

**Test**: `ChatterboxTurboReferenceVoice_FromFile_BadFiles_ThrowDistinctExceptions`

##### Lazy file voices defer decoding

**Test**: `ChatterboxTurboReferenceVoice_FromFileLazy_CorruptFile_FailsOnFirstUseNamingFile`
**Test**: `ChatterboxTurboReferenceVoice_FromFileLazy_ValidFile_LoadsOnceOnFirstUse`
