### NemotronRnntGreedyDecoder Verification

#### Verification Approach

Verified by deterministic unit tests against a scripted managed `IRnntNetwork` fake. The
ONNX-backed `NemotronRnntNetwork` is not covered by these tests except for its pure-logic
penalized arg-max, `NemotronRnntNetwork.SelectToken`, which is tested directly on logit arrays.

#### Test Environment

xUnit v3 under the .NET SDK, in `test/DemaConsulting.Speech.Onnx.NemotronStt.Tests`
(`NemotronRnntGreedyDecoderTests.cs`).

#### Acceptance Criteria

Reset resets the network; all-blank frames emit nothing; emitted tokens are collected and advance
the network; at most ten symbols are emitted per frame; the blank score is penalized by 3.0 before arg-max
(a blank leading by less than the penalty loses, by more wins, and non-blank scores are not
penalized); dispose disposes the network; and null
arguments throw.

#### Test Scenarios

##### Reset resets the network

**Test**: `Reset_ResetsNetwork`

##### All blank emits nothing

**Test**: `Decode_AllBlank_EmitsNothing`

##### Tokens are collected and advanced

**Test**: `Decode_Tokens_AreCollectedAndAdvanced`

##### Never blank stops at ten symbols per frame

**Test**: `Decode_NeverBlank_StopsAtTenSymbolsPerFrame`

##### Blank within the penalty prefers the non-blank token

**Test**: `SelectToken_BlankWithinPenalty_PrefersNonBlank`

##### Blank beyond the penalty is returned

**Test**: `SelectToken_BlankBeyondPenalty_ReturnsBlank`

##### Non-blank ids are not penalized

**Test**: `SelectToken_NonBlankIds_AreNotPenalized`

##### Dispose disposes the network

**Test**: `Dispose_DisposesNetwork`

##### Null arguments throw

**Test**: `NullArguments_Throw`
