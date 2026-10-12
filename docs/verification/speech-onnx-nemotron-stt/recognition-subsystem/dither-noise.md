### DitherNoise Verification

#### Verification Approach

Verified by deterministic in-memory unit tests that apply the dither to synthetic sample buffers
(`DitherNoiseTests.cs`).

#### Test Environment

xUnit v3 under the .NET SDK, in `test/DemaConsulting.Speech.Onnx.NemotronStt.Tests`.

#### Acceptance Criteria

The dither is deterministic and resettable for a given seed, differs between seeds, matches its
configured amplitude statistically, and leaves samples unchanged at zero amplitude.

#### Test Scenarios

##### Dither is deterministic and resettable

**Test**: `Apply_SameSeed_IsDeterministicAndResettable`

##### Dither statistics match the amplitude

**Test**: `Apply_Statistics_MatchAmplitude`

##### Zero amplitude leaves samples unchanged

**Test**: `Apply_ZeroAmplitude_LeavesSamples`

##### Different seeds differ

**Test**: `Apply_DifferentSeeds_Differ`
