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

Every golden case matches exactly; empty text yields only the trailing ids; tags are atomic; zero
trailing ids appends no end-of-text id; a duplicate merge keeps its first rank; invalid or malformed
documents, a missing end-of-text token and non-gzip input throw `InvalidDataException`; a negative
trailing count throws `ArgumentOutOfRangeException`; a piece without a vocabulary entry throws
`InvalidOperationException`; null arguments throw.

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

##### Malformed merges throw

**Test**: `ByteLevelBpeTokenizer_Constructor_MalformedMerge_ThrowsInvalidData`

##### A missing end-of-text token throws

**Test**: `ByteLevelBpeTokenizer_Constructor_MissingEndOfText_ThrowsInvalidData`

##### A negative trailing count throws

**Test**: `ByteLevelBpeTokenizer_Constructor_NegativeTrailingCount_ThrowsArgumentOutOfRange`

##### A piece without a vocabulary entry throws

**Test**: `ByteLevelBpeTokenizer_Encode_PieceWithoutVocabularyEntry_ThrowsInvalidOperation`

##### A duplicate merge keeps its first rank

**Test**: `ByteLevelBpeTokenizer_Constructor_DuplicateMerge_KeepsFirstRank`

##### A non-gzip stream throws

**Test**: `ByteLevelBpeTokenizer_FromGzipStream_NotGzip_ThrowsInvalidData`

##### Null arguments throw

**Test**: `ByteLevelBpeTokenizer_NullArguments_Throw`
