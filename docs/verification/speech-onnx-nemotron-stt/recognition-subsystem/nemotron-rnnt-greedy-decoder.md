### NemotronRnntGreedyDecoder Verification

#### Verification Approach

Verified by deterministic unit tests against a scripted managed `IRnntNetwork` fake. The
ONNX-backed `NemotronRnntNetwork`, including the penalized arg-max `SelectToken`, is verified in
its own unit verification document (`nemotron-rnnt-network.md`).

#### Test Environment

xUnit v3 under the .NET SDK, in `test/DemaConsulting.Speech.Onnx.NemotronStt.Tests`
(`NemotronRnntGreedyDecoderTests.cs`).

#### Acceptance Criteria

Reset resets the network; all-blank frames emit nothing; emitted tokens are collected and advance
the network; at most ten symbols are emitted per frame; dispose disposes the network; and null
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

##### Dispose disposes the network

**Test**: `Dispose_DisposesNetwork`

##### Null arguments throw

**Test**: `NullArguments_Throw`
