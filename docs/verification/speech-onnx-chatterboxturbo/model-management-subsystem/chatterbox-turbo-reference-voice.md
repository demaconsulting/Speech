### ChatterboxTurboReferenceVoice Verification

#### Verification Approach

Verified by `ChatterboxTurboReferenceVoiceTests.cs` using generated sine tones, WAV files written
to a scratch directory and a small FLAC fixture (`TestData/voice-mono-16-16000.flac`).

#### Test Environment

- **Framework**: xUnit v3; project `test/DemaConsulting.Speech.Onnx.ChatterboxTurbo.Tests`
- **Isolation**: scratch directory, deleted on dispose; no network

#### Acceptance Criteria

Mono 24 kHz samples are kept with their values; other layouts are down-mixed and resampled preserving the tone
(frequency checked by zero crossings); long clips
are trimmed and short clips rejected; WAV and FLAC files load; lazy voices and mood clips decode on first
use, exactly once even when first used concurrently by several threads (a deleted file does not affect later
reads), with failures naming the file, not cached and retried; mood names are trimmed and lower-cased and listed
in ordinal order, blank mood entries are rejected, a null mood map is ignored and a later clip of the same
mood name replaces the earlier one; bad files and arguments throw the
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

**Tests**: `ChatterboxTurboReferenceVoice_FromSamples_NullOrBlankArguments_Throw`,
`ChatterboxTurboReferenceVoice_FromSamples_NonPositiveRateOrChannels_Throw` and
`ChatterboxTurboReferenceVoice_FromFile_TooShortOrBlankArguments_Throw`

##### PCM16 WAV loads

**Test**: `ChatterboxTurboReferenceVoice_FromFile_Pcm16Wav_LoadsVoice`

##### Stereo WAV loads

**Test**: `ChatterboxTurboReferenceVoice_FromFile_StereoWav_LoadsVoice`

##### Float32 WAV loads

**Test**: `ChatterboxTurboReferenceVoice_FromFile_Float32Wav_LoadsVoice`

##### FLAC loads at its natural duration

**Test**: `ChatterboxTurboReferenceVoice_FromFile_Flac_LoadsVoice`

##### Bad files fail with an identifying error

**Tests**: `ChatterboxTurboReferenceVoice_FromFile_MissingFile_ThrowsFileNotFound`,
`ChatterboxTurboReferenceVoice_FromFile_UnsupportedEncoding_ThrowsNotSupported` and
`ChatterboxTurboReferenceVoice_FromFile_NonAudioFile_ThrowsInvalidData`

##### Lazy file voices defer decoding

**Tests**: `ChatterboxTurboReferenceVoice_FromFileLazy_CorruptFile_FailsOnFirstUseNamingFile`,
`ChatterboxTurboReferenceVoice_FromFileLazy_ValidFile_LoadsOnceOnFirstUse` (the file is deleted after the first
use and the later read still returns the same clip) and
`ChatterboxTurboReferenceVoice_FromLoader_ConcurrentFirstUse_DecodesOnceAndSharesClip` (eight threads released
together by a barrier decode through a counting loader: exactly one decode, one shared clip),
`ChatterboxTurboReferenceVoice_FromLoader_DecodesOnlyOnFirstUseAndOnlyOnce` and
`ChatterboxTurboReferenceVoice_FromLoader_FailureIsNotCached` (a failed decode is retried on the next use)

##### Mood clips load lazily and a missing mood is reported

**Tests**: `ChatterboxTurboReferenceVoice_WithMoodFiles_LoadsMoodClipsLazilyAndReportsMissingMoods`,
`ChatterboxTurboReferenceVoice_WithMoodFiles_TooShortClip_FailsOnFirstUse`

##### Mood names are trimmed and lower-cased

**Tests**: `ChatterboxTurboReferenceVoice_WithMoodFiles_NamesAreTrimmedAndLowerCased` and
`ChatterboxTurboReferenceVoice_WithMoodSamples_NormalizesClipAndTrimsName`

##### Blank mood entries are rejected and a null map is a no-op

**Test**: `ChatterboxTurboReferenceVoice_WithMoodFiles_BlankEntriesAndNullMap_RejectedOrIgnored`; the test-only
helper's null and blank mood rejection is covered by `ChatterboxTurboReferenceVoice_WithMoodSamples_BlankMood_Throws`.

Blank mood names or paths throw an `ArgumentException`; a null mood map returns a voice with no mood clips.

##### A later clip of the same mood name replaces the earlier one

**Test**: `ChatterboxTurboReferenceVoice_WithMoodFiles_SameNameAgain_ReplacesEarlierClip`

##### Mood names are listed in ordinal order

**Test**: `ChatterboxTurboReferenceVoice_Moods_AreListedInOrdinalAscendingOrder`

##### Mood lookup is case-insensitive and trimmed

**Test**: `ChatterboxTurboReferenceVoice_MoodLookup_IsCaseInsensitiveAndTrimmed`

Verifies `HasMood` and `TryGetMoodSamples` match `sad`, `SAD` and ` sAd ` to the same clip, and that null, empty,
whitespace and unknown names report the mood as absent.
