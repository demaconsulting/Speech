### NemotronEncoder Verification

#### Verification Approach

Verified by deterministic unit tests against a tiny synthetic ONNX model that honors the real
encoder's tensor contract (built in `NemotronEncoderFixture.cs`), so the production input and
output wiring and the cache feedback are exercised without the real model files.

#### Test Environment

xUnit v3 under the .NET SDK, in `test/DemaConsulting.Speech.Onnx.NemotronStt.Tests`
(`NemotronEncoderTests.cs`). The synthetic model is built in memory; no network, files, or audio
hardware are used.

#### Acceptance Criteria

The first call returns the encoded frames carrying the language id; each later call's output
reflects the caches fed back from the previous call; reset restores zero caches; the probe
inference leaves the session usable by a non-owning encoder; use after disposal is rejected with
`ObjectDisposedException`; and a null session is rejected with `ArgumentNullException`.

#### Test Scenarios

##### Encoder first chunk returns frames with the language

**Test**: `NemotronEncoder_Encode_FirstChunk_ReturnsFramesWithLanguage`

##### Encoder second chunk uses the previous caches

**Test**: `NemotronEncoder_Encode_SecondChunk_UsesPreviousCaches`

##### Encoder reset restarts the caches

**Test**: `NemotronEncoder_Reset_AfterEncoding_RestartsCaches`

##### Encoder probe inference leaves the session usable

**Test**: `NemotronEncoder_RunProbeInference_ValidSession_LeavesSessionUsable`

##### Encoder rejects use after disposal

**Test**: `NemotronEncoder_Encode_AfterDispose_Throws`

##### Encoder null session throws

**Test**: `NemotronEncoder_Constructor_NullSession_Throws`
