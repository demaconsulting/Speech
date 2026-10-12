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

The repetition penalty is 1.2. Each scenario lists the logits, the history (generated ids) and the
hand-computed expected id.

##### No history returns the argmax

**Test**: `ChatterboxTurboTokenSelector_Select_NoHistory_ReturnsArgmax`

Logits `[0.1, 2, 1]`, empty history: id 1.

##### A repeated positive logit is penalized

**Test**: `ChatterboxTurboTokenSelector_Select_RepeatedPositive_IsPenalized`

Logits `[0, 2, 1.8]`, history `{1}`: id 1 scores 2 / 1.2 = 1.67, below 1.8, so id 2.

##### A repeated negative logit is pushed down

**Test**: `ChatterboxTurboTokenSelector_Select_RepeatedNegative_IsPushedDown`

Logits `[-1, -1.1]`, history `{0}`: id 0 scores -1 x 1.2 = -1.2, below -1.1, so id 1.

##### Ties resolve to the lowest id

**Test**: `ChatterboxTurboTokenSelector_Select_Ties_ResolveToLowestId`

Logits `[1, 1, 1]`, empty history: id 0. With `[1, 1, 1]` and history `{0, 1, 99}`
(`..._PenalizedTiesAndOutOfRangeHistory_ResolveToUnpenalizedId`) ids 0 and 1 are penalized to 0.83 and
the out-of-range id 99 is ignored: id 2.

##### Invalid input is rejected

**Tests**: `ChatterboxTurboTokenSelector_Select_EmptyLogits_ThrowsArgumentException` (empty logits
throw `ArgumentException`) and `ChatterboxTurboTokenSelector_Select_NullHistory_ThrowsArgumentNullException`
(null history throws `ArgumentNullException`).

##### Non-finite logits terminate

**Tests**: `ChatterboxTurboTokenSelector_Select_AllNaN_ReturnsLowestId` (three NaN logits: id 0),
`..._AllNegativeInfinity_ReturnsLowestUnblockedId` (three negative-infinity logits with id 0 blocked: id 1)
and `..._NaNAmongRealScores_IsNeverChosen` (`[NaN, -5, NaN]`: id 1).

##### A blocked token is skipped

**Tests**: `ChatterboxTurboTokenSelector_Select_BlockedToken_IsSkipped` (logits `[0.1, 2, 1]`, block id 1:
id 2) and `ChatterboxTurboTokenSelector_Select_BlockedTokenInHistory_IsSkipped` (logits `[5, 1, 0.5]`,
history `{0}`, block id 0: id 1).

##### The stop token is never blocked

**Test**: `ChatterboxTurboTokenSelector_Select_BlockedStopToken_IsIgnored`

Only the stop-token logit is 5 and the stop token is passed as the blocked id: the stop token is returned.

##### A single blocked id is returned

**Test**: `ChatterboxTurboTokenSelector_Select_AllBlocked_FallsBackToArgmax`

Logits `[3]`, block id 0: id 0.
