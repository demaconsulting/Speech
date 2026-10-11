### ChatterboxTurboTokenSelector Verification

#### Verification Approach

Verified by `ChatterboxTurboTokenSelectorTests.cs` with hand-computed logits.

#### Test Environment

- **Framework**: xUnit v3; project `test/DemaConsulting.Speech.Onnx.ChatterboxTurbo.Tests`
- **Isolation**: pure functions; no files or network

#### Acceptance Criteria

Without history the argmax wins; repeated positive logits are divided and repeated negative logits
multiplied; ties resolve to the lowest id; invalid input is handled safely.

#### Test Scenarios

##### No history returns the argmax

**Test**: `ChatterboxTurboTokenSelector_Select_NoHistory_ReturnsArgmax`

##### A repeated positive logit is penalized

**Test**: `ChatterboxTurboTokenSelector_Select_RepeatedPositive_IsPenalized`

##### A repeated negative logit is pushed down

**Test**: `ChatterboxTurboTokenSelector_Select_RepeatedNegative_IsPushedDown`

##### Ties and invalid input are handled

**Test**: `ChatterboxTurboTokenSelector_Select_TiesAndInvalidInput_AreHandled`
