### ChatterboxTurboReferenceVoice Verification

#### Verification Approach

Verified by `ChatterboxTurboReferenceVoiceTests.cs` using generated sine tones, WAV files written
to a scratch directory and a small FLAC fixture (`TestData/voice-mono-16-16000.flac`).

#### Test Environment

- **Framework**: xUnit v3; project `test/DemaConsulting.Speech.Onnx.ChatterboxTurbo.Tests`
- **Isolation**: scratch directory, deleted on dispose; no network

#### Acceptance Criteria

Mono 24 kHz samples are kept; other layouts are down-mixed and resampled preserving tone; long clips
are trimmed and short clips rejected; WAV and FLAC files load; lazy voices and mood clips decode on first
use with failures naming the file; mood names are trimmed and lower-cased, blank mood entries are rejected and a
null mood map is ignored; bad files and arguments throw the
documented exceptions.

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

**Tests**: `ChatterboxTurboReferenceVoice_FromSamples_InvalidArguments_Throw` and
`ChatterboxTurboReferenceVoice_FromFile_TooShortOrBlankArguments_Throw`

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

**Tests**: `ChatterboxTurboReferenceVoice_FromFileLazy_CorruptFile_FailsOnFirstUseNamingFile`,
`ChatterboxTurboReferenceVoice_FromFileLazy_ValidFile_LoadsOnceOnFirstUse` and
`ChatterboxTurboReferenceVoice_FromFileLazy_ConcurrentFirstUse_ReturnsOneSharedClip`

##### Mood clips load lazily and a missing mood is reported

**Tests**: `ChatterboxTurboReferenceVoice_WithMoodFiles_LoadsMoodClipsLazilyAndReportsMissingMoods`,
`ChatterboxTurboReferenceVoice_WithMoodFiles_TooShortClip_FailsOnFirstUse`

##### Mood names are trimmed and lower-cased

**Tests**: `ChatterboxTurboReferenceVoice_WithMoodFiles_NamesAreTrimmedAndLowerCased` and
`ChatterboxTurboReferenceVoice_WithMoodSamples_NormalizesClipAndTrimsName`

##### Blank mood entries are rejected and a null map is a no-op

**Test**: `ChatterboxTurboReferenceVoice_WithMoodFiles_BlankEntriesAndNullMap_RejectedOrIgnored`

Blank mood names or paths throw an `ArgumentException`; a null mood map returns a voice with no mood clips.

##### A later clip of the same mood name replaces the earlier one

**Test**: `ChatterboxTurboReferenceVoice_WithMoodFiles_SameNameAgain_ReplacesEarlierClip`

##### Mood lookup is case-insensitive and trimmed

**Test**: `ChatterboxTurboReferenceVoice_MoodLookup_IsCaseInsensitiveAndTrimmed`

Verifies `HasMood` and `TryGetMoodSamples` match `sad`, `SAD` and ` sAd ` to the same clip, and that null, empty,
whitespace and unknown names report the mood as absent.
