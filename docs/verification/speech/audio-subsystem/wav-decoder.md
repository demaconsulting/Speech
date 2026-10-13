### WavDecoder Verification

#### Verification Approach

Verified by `WavDecoderTests.cs` (`test/DemaConsulting.Speech.Tests/AudioSubsystem`) through
`AudioFileReader`, using WAV images built in memory with small helper builders.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK; project `test/DemaConsulting.Speech.Tests`
- **Isolation**: in-memory byte images only; no files and no network

#### Acceptance Criteria

PCM 8/16/24/32-bit and float32 decode to the expected values, extensible headers (PCM and float
sub-formats) and unknown chunks are tolerated, a trailing partial frame is dropped, multichannel
order is kept, and unsupported, malformed or oversized input is rejected.

#### Test Scenarios

##### PCM depths scale to the unit range

**Arrange**: Mono WAV images at 8, 16, 24 and 32 bits holding -1.0, 0.0 and 0.25 of full scale.

**Expected**: Samples `[-1, 0, 0.25]`, with the bit depth and 8000 Hz rate reported.

**Test**: `WavDecoder_Decode_PcmDepths_ScaleToUnitRange`

##### Float32 passes through

**Arrange**: A 2-channel float32 image holding `0.5` and `-2`.

**Expected**: Samples `[0.5, -2]` unchanged, 2 channels, 1 frame.

**Test**: `WavDecoder_Decode_Float32_PassesThrough`

##### Extensible header with extra chunks

**Arrange**: A `LIST` chunk of odd size 3, a 40-byte `WAVE_FORMAT_EXTENSIBLE` `fmt` chunk
(22050 Hz, PCM sub-format) and a data chunk.

**Expected**: Samples `[0.5]` at 22050 Hz; the unknown chunk and its padding are skipped.

**Test**: `WavDecoder_Decode_ExtensibleWithExtraChunks_Decodes`

##### Overstated data size is clamped

**Arrange**: Stereo 16-bit data of six bytes (one frame plus two stray bytes) with a declared
data size of 1,000,000.

**Expected**: One whole frame, samples `[0, 0.5]`; the stray bytes are dropped.

**Test**: `WavDecoder_Decode_OverstatedDataSize_IsClamped`

##### Unsupported encodings throw

**Arrange**: Format tag/bit-depth pairs (2, 16), (1, 12) and (3, 64).

**Expected**: `NotSupportedException` for each.

**Test**: `WavDecoder_Decode_UnsupportedEncoding_Throws`

##### Malformed images throw

**Arrange**: Seven malformed images: `NoData` (no data chunk), `DataBeforeFmt` (data chunk precedes
fmt), `ShortFmt` (8-byte fmt), `NoRate` (rate 0), `NoChannels` (0 channels), `TruncatedFile` (image
cut at 30 bytes) and `TruncatedExtensible` (extensible tag with a 16-byte fmt).

**Expected**: `InvalidDataException` whose message contains, respectively, `no 'data' chunk`,
`before its 'fmt ' chunk`, `truncated 'fmt ' chunk`, `no channels or no sample rate` (twice),
`truncated 'fmt ' chunk` and `truncated extensible 'fmt ' chunk`.

**Test**: `WavDecoder_Decode_MalformedImages_Throw` (theory, one case per image)

##### Sample cap is enforced

**Arrange**: 100 data bytes (50 samples) with `MaxSamples = 10`.

**Expected**: `InvalidDataException`.

**Test**: `WavDecoder_Decode_ExceedsMaxSamples_Throws`

##### Sample cap boundary is inclusive

**Arrange**: A 10-sample image.

**Expected**: `MaxSamples = 10` decodes all 10 samples; `MaxSamples = 9` throws
`InvalidDataException`.

**Test**: `WavDecoder_Decode_ExactSampleCap_IsAcceptedAndOneUnderIsRejected`

##### Extreme PCM values

**Arrange**: The largest positive and most negative 16- and 24-bit samples.

**Expected**: The positive values scale to just below 1 (`32767/32768`, `8388607/8388608`); the
negative extremes scale to exactly -1.

**Test**: `WavDecoder_Decode_ExtremePcmValues_ScaleAsExpected`

##### Zero-length data

**Arrange**: A valid image with an empty data chunk.

**Expected**: An empty clip at 8000 Hz.

**Test**: `WavDecoder_Decode_ZeroLengthData_YieldsEmptyClip`

##### Multichannel order is kept

**Arrange**: 24-bit stereo data, left `+0.5` then right `-0.5`.

**Expected**: Samples `[0.5, -0.5]`, 2 channels.

**Test**: `WavDecoder_Decode_TwentyFourBitStereo_KeepsChannelOrder`

##### Sample rate above the signed 32-bit range

**Arrange**: A `fmt` chunk declaring 2,147,483,648 Hz.

**Expected**: `InvalidDataException` containing `no channels or no sample rate`.

**Test**: `WavDecoder_Decode_SampleRateAboveIntMax_Throws`

##### Chunk running past the end

**Arrange**: A valid `fmt` chunk followed by a `LIST` chunk declaring 100 bytes with only 2 present.

**Expected**: `InvalidDataException` containing `no 'data' chunk`.

**Test**: `WavDecoder_Decode_ChunkRunsPastEnd_ReportsNoDataChunk`

##### Extensible float sub-format

**Arrange**: A 40-byte extensible `fmt` chunk whose sub-format tag is 3 (float), 32 bits, with a
`-0.25` sample.

**Expected**: Samples `[-0.25]`.

**Test**: `WavDecoder_Decode_ExtensibleFloatSubFormat_DecodesAsFloat`

##### Extensible unsupported encodings throw

**Arrange**: Extensible headers with (sub-format, bits) pairs (2, 16), (1, 12) and (3, 64).

**Expected**: `NotSupportedException` for each.

**Test**: `WavDecoder_Decode_ExtensibleUnsupportedEncoding_Throws`

##### Trailing partial frame with an exact data size

**Arrange**: Stereo 16-bit data of six bytes with an accurate declared size.

**Expected**: One whole frame `[0, 0.5]`; the two stray bytes are dropped.

**Test**: `WavDecoder_Decode_TrailingPartialFrame_IsDropped`

##### Non-WAV header passed directly to the decoder

**Arrange**: 20 zero bytes passed straight to `WavDecoder.Decode` (the reader normally dispatches on
the header first).

**Expected**: `InvalidDataException` whose message contains `not a RIFF/WAVE`.

**Test**: `WavDecoder_Decode_NonWavHeader_Throws`

##### Float NaN passes through

**Arrange**: A float32 sample holding NaN.

**Expected**: The decoded sample is NaN.

**Test**: `WavDecoder_Decode_FloatNaN_PassesThroughUnchanged`
