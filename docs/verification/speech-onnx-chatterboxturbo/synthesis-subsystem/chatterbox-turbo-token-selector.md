### ChatterboxTurboTokenSelector Verification

#### Verification Approach

Verified by `ChatterboxTurboTokenSelectorTests.cs` with hand-computed logits.

#### Test Environment

- **Framework**: xUnit v3; project `test/DemaConsulting.Speech.Onnx.ChatterboxTurbo.Tests`
- **Isolation**: pure functions; no files or network

#### Acceptance Criteria

Without history the argmax wins; repeated positive logits are divided and repeated negative logits
multiplied; ties resolve to the lowest id; empty logits and a null history are rejected with exceptions; NaN and
negative-infinity logits yield a valid token without recursion; a blocked token is skipped (even when it
is in the history), the stop token is never blocked, and a single blocked id is returned.

#### Test Scenarios

##### No history returns the argmax

**Test**: `ChatterboxTurboTokenSelector_Select_NoHistory_ReturnsArgmax`

##### A repeated positive logit is penalized

**Test**: `ChatterboxTurboTokenSelector_Select_RepeatedPositive_IsPenalized`

##### A repeated negative logit is pushed down

**Test**: `ChatterboxTurboTokenSelector_Select_RepeatedNegative_IsPushedDown`

##### Ties resolve to the lowest id

**Test**: `ChatterboxTurboTokenSelector_Select_Ties_ResolveToLowestId`; a tie among penalized ids resolves to the
unpenalized id and out-of-range history ids are ignored (`..._PenalizedTiesAndOutOfRangeHistory_ResolveToUnpenalizedId`).

##### Invalid input is rejected

**Tests**: `ChatterboxTurboTokenSelector_Select_EmptyLogits_ThrowsArgumentException` and
`ChatterboxTurboTokenSelector_Select_NullHistory_ThrowsArgumentNullException`.

##### Non-finite logits terminate

**Tests**: `ChatterboxTurboTokenSelector_Select_AllNaN_ReturnsLowestId` (expects id 0),
`..._AllNegativeInfinity_ReturnsLowestUnblockedId` (id 0 blocked, expects 1) and
`..._NaNAmongRealScores_IsNeverChosen` (expects the only real score).

##### A blocked token is skipped

**Tests**: `ChatterboxTurboTokenSelector_Select_BlockedToken_IsSkipped` and
`ChatterboxTurboTokenSelector_Select_BlockedTokenInHistory_IsSkipped`.

##### The stop token is never blocked

**Test**: `ChatterboxTurboTokenSelector_Select_BlockedStopToken_IsIgnored`

##### A single blocked id is returned

**Test**: `ChatterboxTurboTokenSelector_Select_AllBlocked_FallsBackToArgmax`
