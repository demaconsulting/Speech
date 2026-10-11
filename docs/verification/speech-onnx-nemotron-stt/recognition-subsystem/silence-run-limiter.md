### SilenceRunLimiter Verification

#### Verification Approach

Verified by deterministic in-memory unit tests feeding synthetic silence, noise, and tone to the
limiter and dither (`SilenceRunLimiterTests.cs`, `DitherNoiseTests.cs`).

#### Test Environment

xUnit v3 under the .NET SDK, in `test/DemaConsulting.Speech.Onnx.NemotronStt.Tests`.

#### Acceptance Criteria

Long quiet runs (digital silence or low-level noise) are truncated to the 400 ms cap; tone,
immediate speech, and very quiet speech pass through intact; the adaptive threshold is clamped
to its bounds and non-adaptive mode keeps the default; audio flows during calibration; chunked
feeding equals single feeding; flush emits partial frames; reset restores the initial state;
`QuietRunSamples` follows the input clock; and the dither is deterministic, resettable, matches
its amplitude, and leaves samples unchanged at zero amplitude.

#### Test Scenarios

##### Digital silence is truncated to the cap

**Test**: `Process_DigitalSilence_TruncatesToCap`

##### Low-level noise is truncated to the cap

**Test**: `Process_LowLevelNoise_TruncatesToCap`

##### A tone burst passes through

**Test**: `Process_ToneBurst_PassesThrough`

##### Silence then tone keeps all tone samples

**Test**: `Process_SilenceThenTone_KeepsAllToneSamples`

##### Immediate speech keeps everything

**Test**: `Process_ImmediateSpeechStart_KeepsEverything`

##### Very quiet speech is not dropped

**Test**: `Process_VeryQuietSpeech_IsNotDropped`

##### Loud calibration clamps the threshold to the maximum

**Test**: `Process_LoudCalibration_ThresholdClampedToMaximum`

##### Silent calibration clamps the threshold to the minimum

**Test**: `Process_SilentCalibration_ThresholdClampedToMinimum`

##### Audio still flows during calibration

**Test**: `Process_DuringCalibration_AudioStillFlows`

##### Non-adaptive mode keeps the default threshold

**Test**: `Process_NonAdaptive_KeepsDefaultThreshold`

##### Chunked feed equals a single feed

**Test**: `Process_ChunkedFeed_EqualsSingleFeed`

##### Flush emits a partial frame

**Test**: `Flush_PartialFrame_IsEmitted`

##### Reset restores the initial state

**Test**: `Reset_AfterUse_RestoresInitialState`

##### Quiet run tracks the input clock

**Test**: `QuietRunSamples_TracksInputClock`

##### Dither is deterministic and resettable

**Test**: `Apply_SameSeed_IsDeterministicAndResettable`

##### Dither statistics match the amplitude

**Test**: `Apply_Statistics_MatchAmplitude`

##### Zero amplitude leaves samples unchanged

**Test**: `Apply_ZeroAmplitude_LeavesSamples`

##### Different seeds differ

**Test**: `Apply_DifferentSeeds_Differ`
