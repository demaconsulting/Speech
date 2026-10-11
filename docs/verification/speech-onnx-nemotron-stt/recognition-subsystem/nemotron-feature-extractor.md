### NemotronFeatureExtractor Verification

#### Verification Approach

Verified by deterministic in-memory unit tests against a golden reference signal and
equivalence checks.

#### Test Environment

xUnit v3 under the .NET SDK, in `test/DemaConsulting.Speech.Onnx.NemotronStt.Tests`
(`NemotronFeatureExtractorTests.cs`).

#### Acceptance Criteria

Features match the reference log-mel for a golden signal; chunked input equals one-shot input;
very short input is zero-extended to produce frames; no audio produces no frames; silence yields
the log-epsilon floor; and reset reproduces identical features.

#### Test Scenarios

##### Golden signal matches the reference

**Test**: `Extract_GoldenSignal_MatchesReference`

##### Chunked equals one-shot

**Test**: `Extract_Chunked_EqualsOneShot`

##### Very short input produces frames on flush

**Test**: `Flush_VeryShortInput_ProducesFrames`

##### No audio produces no frames

**Test**: `Flush_NoAudio_ProducesNoFrames`

##### Silence is the log epsilon

**Test**: `Extract_Silence_IsLogEpsilon`

##### Reset reproduces features

**Test**: `Reset_AfterUse_ReproducesFeatures`
