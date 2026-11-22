### NemotronEndpointDetector Verification

#### Verification Approach

Verified by deterministic unit tests that drive the detector directly with events: real decoded
frames (with or without an emitted token, 80 ms each as for the real model), synthetic padding
frames, and quiet input removed by the limiter. The detector is pure - it needs no model files, no
ONNX Runtime, and no collaborators - so nothing is mocked; each test feeds a sequence of events
and checks the reported endpoint reason and the exposed speech flag, trailing silence, and
utterance length. Its integration into the recognition engine is verified by the engine tests
(see *SpeechOnnxNemotronStt OnnxNemotronRecognitionEngine Verification*).

#### Test Environment

xUnit v3 under the .NET SDK, in `test/DemaConsulting.Speech.Onnx.NemotronStt.Tests`
(`NemotronEndpointDetectorTests.cs`), on every target framework; no files, network, or audio
hardware.

#### Acceptance Criteria

A fresh detector reports no endpoint with both clocks at zero; the default rules are 2400 ms of
trailing silence, 1200 ms of trailing silence after speech, and a 20000 ms maximum utterance; each
rule fires exactly when its duration is reached and not one frame before; silence without speech
never uses the shorter after-speech rule; a token restarts the trailing silence; removed quiet
input counts toward both clocks; padding frames never advance either clock, while a token decoded
on padding counts as speech; a rule set to zero is disabled while the others still apply; a
custom duration is honored; Reset starts a new segment, keeping speech only when it is carried;
and negative rule durations, a sample rate that is not positive, and negative frame or silence
durations throw `ArgumentOutOfRangeException`.

#### Test Scenarios

##### A fresh detector reports no endpoint

**Test**: `NemotronEndpointDetector_Check_NoFrames_ReturnsNone`

##### The default rules match the Sherpa-based backend

**Expected**: 2400 ms, 1200 ms, and 20000 ms.

**Test**: `NemotronEndpointDetector_Rules_Default_AreSherpaEquivalent`

##### Trailing silence after speech reaches the rule

**Scenario**: a token, then 1200 ms of blank frames.

**Expected**: the after-speech rule fires, with the trailing silence equal to 1200 ms.

**Test**:
`NemotronEndpointDetector_Check_TrailingBlanksAfterTokenReachAfterSpeechRule_ReturnsTrailingSilenceAfterSpeech`

##### Trailing silence after speech below the rule

**Test**: `NemotronEndpointDetector_Check_TrailingBlanksAfterTokenBelowRule_ReturnsNone`

##### Silence without speech below the long rule

**Scenario**: blank frames beyond 1200 ms but below 2400 ms, with no token.

**Expected**: no rule fires.

**Test**: `NemotronEndpointDetector_Check_SilenceWithoutTokenBelowLongRule_ReturnsNone`

##### Silence without speech reaches the long rule

**Test**: `NemotronEndpointDetector_Check_SilenceWithoutTokenReachesLongRule_ReturnsTrailingSilence`

##### The utterance reaches the maximum length

**Scenario**: tokens on every frame up to 20 s.

**Expected**: no endpoint one frame before the maximum; the maximum-length rule fires at it.

**Test**: `NemotronEndpointDetector_Check_UtteranceReachesMaximum_ReturnsMaxUtteranceLength`

##### Padding alone never endpoints

**Expected**: neither the trailing silence nor the utterance length advances for padding frames.

**Test**: `NemotronEndpointDetector_AdvancePaddingFrame_OnlyPadding_NeverEndpoints`

##### A token on padding counts as speech

**Test**: `NemotronEndpointDetector_AdvancePaddingFrame_TokenOnPadding_CountsAsSpeech`

##### Removed quiet input counts toward the trailing silence

**Scenario**: a token, 400 ms of decoded blank frames, and 800 ms of quiet input removed by the
limiter.

**Expected**: the after-speech rule fires and the utterance length includes the removed input.

**Test**: `NemotronEndpointDetector_AdvanceSilence_DroppedQuietAfterToken_CountsTowardTrailingSilence`

##### A token after silence restarts the trailing silence

**Test**: `NemotronEndpointDetector_AdvanceFrame_TokenAfterSilence_RestartsTrailingSilence`

##### Reset starts a new segment

**Expected**: both clocks restart and the segment holds no speech, except after a reset that
carries speech into the new segment.

**Test**: `NemotronEndpointDetector_Reset_AfterEndpoint_StartsNewSegment`

##### A rule set to zero is disabled

**Scenario**: the after-speech rule and the maximum length set to zero; 24 s of speech followed by
silence.

**Expected**: neither disabled rule fires; the long-silence rule fires at 2400 ms.

**Test**: `NemotronEndpointDetector_Check_RuleSetToZero_DisablesRule`

##### A custom after-speech rule is honored

**Expected**: a 400 ms rule fires at 400 ms of trailing silence and not before.

**Test**: `NemotronEndpointDetector_Check_CustomAfterSpeechRule_FiresAtConfiguredSilence`

##### Invalid rules and sample rate are rejected

**Test**: `NemotronEndpointDetector_Constructor_NegativeRule_ThrowsArgumentOutOfRangeException`

##### Negative frame and silence durations are rejected

**Test**: `NemotronEndpointDetector_AdvanceFrame_NegativeDuration_ThrowsArgumentOutOfRangeException`
