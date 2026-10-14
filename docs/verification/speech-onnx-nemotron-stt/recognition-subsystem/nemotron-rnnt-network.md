### NemotronRnntNetwork Verification

#### Verification Approach

Verified by deterministic unit tests against tiny synthetic ONNX decoder and joint models that
honor the real tensor contracts (built in `NemotronNetworkFixture.cs`), covering token and
recurrent-state feedback, the joint inputs, reset, the blank penalty on the real joint output,
and disposal. The pure-logic penalized arg-max, `SelectToken`, is also tested directly on score
arrays.

#### Test Environment

xUnit v3 under the .NET SDK, in `test/DemaConsulting.Speech.Onnx.NemotronStt.Tests`
(`NemotronRnntNetworkTests.cs`). The synthetic models are built in memory; no network, files, or
audio hardware are used.

#### Acceptance Criteria

The blank score is penalized by 3.0 before arg-max (a blank leading by less than the penalty
loses, by more wins, and non-blank scores are not penalized); the first prediction uses the
blank-primed decoder output; each fed token and the previous recurrent state reach the next
decoder call; the encoder frame is scored against the decoder output; reset restores zero
recurrent states; use after disposal is rejected with `ObjectDisposedException`; and null sessions
are rejected with `ArgumentNullException`.

#### Test Scenarios

##### Blank within the penalty prefers the non-blank token

**Test**: `SelectToken_BlankWithinPenalty_PrefersNonBlank`

##### Blank beyond the penalty is returned

**Test**: `SelectToken_BlankBeyondPenalty_ReturnsBlank`

##### Non-blank ids are not penalized

**Test**: `SelectToken_NonBlankIds_AreNotPenalized`

##### Network applies the blank penalty

**Test**: `PredictToken_ScoreEqualsBlank_AppliesPenalty`

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
