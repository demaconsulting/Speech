### WavDecoder Verification

#### Verification Approach

Verified by `WavDecoderTests.cs` through `AudioFileReader` using WAV images built in memory.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK; project `test/DemaConsulting.Speech.Tests`
- **Isolation**: in-memory images and files in the test output or temp directory; no network

#### Acceptance Criteria

PCM 8/16/24/32-bit and float32 decode to the expected values, extensible headers and unknown chunks
are tolerated, and unsupported, malformed or oversized input is rejected.

#### Test Scenarios

##### PCM depths scale to the unit range

**Test**: `WavDecoder_Decode_PcmDepths_ScaleToUnitRange`

##### Float32 passes through

**Test**: `WavDecoder_Decode_Float32_PassesThrough`

##### Extensible header with extra chunks

**Test**: `WavDecoder_Decode_ExtensibleWithExtraChunks_Decodes`

##### Overstated data size is clamped

**Test**: `WavDecoder_Decode_OverstatedDataSize_IsClamped`

##### Unsupported encodings throw

**Test**: `WavDecoder_Decode_UnsupportedEncoding_Throws`

##### Malformed images throw

**Test**: `WavDecoder_Decode_MalformedImages_Throw`

##### Sample cap is enforced

**Test**: `WavDecoder_Decode_ExceedsMaxSamples_Throws`

##### Boundary and extreme values

**Tests**: `WavDecoder_Decode_ExactSampleCap_IsAcceptedAndOneUnderIsRejected`,
`WavDecoder_Decode_ExtremePcmValues_ScaleAsExpected`, `WavDecoder_Decode_ZeroLengthData_YieldsEmptyClip`,
`WavDecoder_Decode_TwentyFourBitStereo_KeepsChannelOrder`, `WavDecoder_Decode_FloatNaN_PassesThroughUnchanged`
