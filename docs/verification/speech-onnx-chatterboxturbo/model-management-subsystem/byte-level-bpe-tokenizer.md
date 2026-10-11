### ByteLevelBpeTokenizer Verification

#### Verification Approach

Verified by `ByteLevelBpeTokenizerTests.cs` against
`TestData/chatterbox-turbo-tokenizer-golden.json`, which holds 39 cases of text and expected ids
generated from the real Hugging Face tokenizer by the committed script
`TestData/generate_golden_ids.py`. A test also pins the SHA-256 of the upstream
`tokenizer.json` the embedded resource was derived from.

#### Test Environment

- **Framework**: xUnit v3; project `test/DemaConsulting.Speech.Onnx.ChatterboxTurbo.Tests`
- **Isolation**: no network; only the embedded resource and the golden file

#### Acceptance Criteria

Every golden case matches exactly; empty text yields only the trailing ids; tags are atomic; invalid
documents and non-gzip input throw `InvalidDataException`; null arguments throw.

#### Test Scenarios

##### Golden texts match the reference ids

**Test**: `ByteLevelBpeTokenizer_Encode_GoldenText_MatchesReferenceIds`

##### The golden set is broad

**Test**: `ByteLevelBpeTokenizer_GoldenCases_ContainManyCases`

##### Empty text yields only the trailing pair

**Test**: `ByteLevelBpeTokenizer_Encode_EmptyText_ReturnsOnlyTrailingPair`

##### The embedded resource matches the pinned upstream hash

**Test**: `ByteLevelBpeTokenizer_EmbeddedResource_MatchesPinnedUpstreamHash`

##### Tags are atomic

**Test**: `ByteLevelBpeTokenizer_Encode_Tag_IsAtomicAndSpaceIsSeparate`

##### Zero trailing ids adds no end-of-text

**Test**: `ByteLevelBpeTokenizer_FromGzipStream_ZeroTrailing_AddsNoEndOfText`

##### An invalid document throws

**Test**: `ByteLevelBpeTokenizer_Constructor_InvalidDocument_ThrowsInvalidData`

##### A non-gzip stream throws

**Test**: `ByteLevelBpeTokenizer_FromGzipStream_NotGzip_ThrowsInvalidData`

##### Null arguments throw

**Test**: `ByteLevelBpeTokenizer_NullArguments_Throw`
