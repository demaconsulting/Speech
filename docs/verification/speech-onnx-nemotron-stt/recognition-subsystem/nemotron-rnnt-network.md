### NemotronRnntNetwork Verification

#### Verification Approach

Verified by deterministic unit tests against tiny synthetic ONNX decoder and joint models that
honor the real tensor contracts (built in `NemotronNetworkFixture.cs`), covering token and
recurrent-state feedback, the joint inputs, reset, the default and configured blank penalty on the
real joint output, penalty validation, and disposal. The pure-logic penalized arg-max,
`SelectToken`, is also tested directly on score arrays (in `NemotronRnntNetworkTests.cs` and
`NemotronRnntGreedyDecoderTests.cs`).

#### Test Environment

xUnit v3 under the .NET SDK, in `test/DemaConsulting.Speech.Onnx.NemotronStt.Tests`
(`NemotronRnntNetworkTests.cs`). The synthetic models are built in memory; no network, files, or
audio hardware are used.

#### Acceptance Criteria

The blank score is penalized by the configured blank penalty before arg-max (a blank leading by
less than the penalty loses, by more wins, and non-blank scores are not penalized); a network
created without a penalty applies the default blank penalty, and a penalty of zero selects the
highest-scoring token, even blank; a negative, NaN, or infinite penalty is rejected with
`ArgumentOutOfRangeException`; the first prediction uses the blank-primed decoder output; each fed
token and the previous recurrent state reach the next decoder call; the encoder frame is scored
against the decoder output; reset restores zero recurrent states; use after disposal is rejected
with `ObjectDisposedException`; and null sessions are rejected with `ArgumentNullException`.

The default blank penalty, 1.0, was chosen by a sweep of the evaluation tool from 0 to 3 in steps
of 0.5 over the committed evaluation data (see `docs/evaluation/nemotron-stt/README.md`). The unit
tests express their score margins relative to the default, so they hold for any default; the
value itself is covered at system level, where the real-model accuracy, invented-word, and
quiet-speech gates are measured at the shipped defaults (see *SpeechOnnxNemotronStt System
Verification*).

#### Test Scenarios

##### Blank within the penalty prefers the non-blank token

**Scenario**: blank leads the best non-blank token by half the default penalty.

**Test**: `SelectToken_BlankWithinPenalty_PrefersNonBlank`

##### Blank beyond the penalty is returned

**Scenario**: blank leads the best non-blank token by twice the default penalty.

**Test**: `SelectToken_BlankBeyondPenalty_ReturnsBlank`

##### Non-blank ids are not penalized

**Test**: `SelectToken_NonBlankIds_AreNotPenalized`

##### Network applies the default blank penalty

**Test**: `PredictToken_ScoreEqualsBlank_AppliesPenalty`

##### A zero penalty selects the highest-scoring token

**Expected**: with no penalty, blank wins when it scores highest; with the default penalty, the
close non-blank token wins.

**Test**: `NemotronRnntNetwork_SelectToken_ZeroPenalty_PrefersHighestLogit`

##### A configured zero penalty reaches the production joint output

**Expected**: a network created with a penalty of zero returns blank when blank scores highest and
exposes the configured penalty.

**Test**: `NemotronRnntNetwork_PredictToken_ZeroPenalty_ReturnsBlankWhenBlankScoresHighest`

##### Negative or non-finite penalties are rejected

**Scenario**: penalties of -0.5, NaN, and positive infinity.

**Test**: `NemotronRnntNetwork_Constructor_NegativeOrNonFiniteBlankPenalty_ThrowsArgumentOutOfRangeException`

##### Network primed decoder output reaches the joint

**Test**: `Reset_PredictToken_UsesPrimedDecoderOutput`

##### Network feeds the token and recurrent state

**Test**: `Advance_FeedsTokenAndRecurrentState`

##### Network scores the encoder frame

**Test**: `PredictToken_UsesEncoderFrame`

##### Network reset clears the recurrent state

**Test**: `Reset_AfterAdvance_ClearsRecurrentState`

##### Network rejects use after disposal

**Test**: `Dispose_BlocksFurtherUse`

##### Network null sessions throw

**Test**: `Constructor_NullSessions_Throw`
