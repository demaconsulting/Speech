### FlacDecoder Verification

#### Verification Approach

Verified by `FlacDecoderTests.cs` in two ways: real libFLAC-encoded fixtures (`TestData/flac`,
produced by soundfile; see `TestData/NOTICE.md`) are compared bit-exactly with lossless WAV twins,
and a test-only FLAC encoder (`TestFlacEncoder`) writes streams that exercise every subframe type,
residual coding, stereo mode, header code and error path that the fixtures do not.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK; project `test/DemaConsulting.Speech.Tests`
- **Isolation**: in-memory images and files in the test output or temp directory; no network

#### Acceptance Criteria

All fixtures decode bit-exactly; every predictor, residual, stereo, depth, rate, block-size and
container variant round-trips; CRC failures, truncation, invalid fields, oversized blocks, a frame
larger than the declared total, a non-syncsafe ID3v2 size and the sample cap are rejected with
`InvalidDataException`.

#### Test Scenarios

##### libFLAC fixtures decode bit-exactly

**Test**: `FlacDecoder_Decode_SoundfileFixture_MatchesWavBitExact`

##### CRC check values

**Test**: `FlacCrc_Checksums_StandardCheckString_MatchPublishedValues`

##### Constant subframes

**Test**: `FlacDecoder_Decode_ConstantSubframe_RepeatsValue`

##### Verbatim subframes across frames

**Test**: `FlacDecoder_Decode_VerbatimMultiFrame_RoundTrips`

##### Fixed predictors

**Test**: `FlacDecoder_Decode_FixedPredictor_RoundTrips`

##### LPC predictors

**Test**: `FlacDecoder_Decode_LpcPredictor_RoundTrips`

##### Rice methods and escape partitions

**Test**: `FlacDecoder_Decode_ResidualCoding_RoundTrips`

##### Wasted bits

**Test**: `FlacDecoder_Decode_WastedBits_AreRestored`

##### Stereo decorrelation modes

**Test**: `FlacDecoder_Decode_StereoModes_RoundTrip`

##### Bit depths

**Test**: `FlacDecoder_Decode_BitDepths_RoundTrip`

##### Sample-rate codes

**Test**: `FlacDecoder_Decode_SampleRateCodes_ReportRate`

##### Block-size codes

**Test**: `FlacDecoder_Decode_BlockSizeCodes_RoundTrip`

##### Multi-byte frame numbers

**Test**: `FlacDecoder_Decode_ManyFrames_ParsesMultiByteFrameNumbers`

##### ID3, extra metadata and many channels

**Test**: `FlacDecoder_Decode_ContainerVariants_RoundTrip`

##### ID3v2-prefixed FLAC with a non-.flac extension, read by path

**Test**: `FlacDecoder_Decode_Id3PrefixedFileWithDatExtension_DecodesThroughReader`

##### ID3v2 size field that is not syncsafe

**Test**: `FlacDecoder_Decode_Id3SizeNotSyncsafe_Throws`

##### Marker detection, STREAMINFO values, metadata damage and subframe-header damage

**Tests**: `FlacDecoder_IsFlac_VariousInputs_DetectsMarkerOnly`,
`FlacDecoder_Decode_InvalidStreamInfoValues_Throw`, `FlacDecoder_Decode_MetadataDamage_ReportsTruncation`,
`FlacDecoder_Decode_DamagedSubframeHeader_ThrowsSpecificErrors`

##### Unknown total length

**Test**: `FlacDecoder_Decode_UnknownTotal_DecodesUntilData_Ends`

##### Bad checksums

**Test**: `FlacDecoder_Decode_BadChecksums_ThrowUnlessVerificationDisabled`

##### Truncated stream

**Test**: `FlacDecoder_Decode_Truncated_Throws`

##### Missing frames

**Test**: `FlacDecoder_Decode_MissingFrames_Throws`

##### Invalid header fields

**Test**: `FlacDecoder_Decode_InvalidFields_Throw`

##### Oversized block

**Test**: `FlacDecoder_Decode_BlockLargerThanMaximum_Throws`

##### Invalid predictor parameters

**Test**: `FlacDecoder_Decode_InvalidPredictorParameters_Throw`

##### Sample cap

**Test**: `FlacDecoder_Decode_ExceedsMaxSamples_Throws`

**Test**: `FlacDecoder_Decode_ExceedsArrayLimit_Throws` (uses the internal `Decode` overload that takes the
array-length ceiling, with a limit of 150 samples instead of `Array.MaxLength`, so no multi-gigabyte
allocation is needed)

##### Frame larger than the declared total

**Test**: `FlacDecoder_Decode_FrameExceedsDeclaredTotal_Throws`
