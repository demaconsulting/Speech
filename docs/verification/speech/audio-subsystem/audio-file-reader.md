### AudioFileReader Verification

#### Verification Approach

Verified by `AudioFileReaderTests.cs` (container detection, errors, mono read) and by
`FlacDecoderTests.cs`, whose fixture theory compares libFLAC-encoded files with lossless WAV files
of the same samples.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK; project `test/DemaConsulting.Speech.Tests`
- **Isolation**: in-memory images and files in the test output or temp directory; no network

#### Acceptance Criteria

Containers are detected by content, errors map to the documented exceptions with the path in the
message, `ReadMono` returns native-rate mono, and every libFLAC fixture decodes bit-exactly to its
WAV twin.

#### Test Scenarios

##### Supported extensions

**Test**: `AudioFileReader_SupportedExtensions_ListsWavAndFlac`

##### Container is detected by content

**Test**: `AudioFileReader_Read_DetectsContainerByContent`

##### Errors are reported

**Test**: `AudioFileReader_Read_Errors_AreReported`

##### Unsupported WAV names the path

**Test**: `AudioFileReader_Read_UnsupportedWav_ThrowsNotSupportedWithPath`

##### Mono read down-mixes at the native rate

**Test**: `AudioFileReader_ReadMono_Stereo_DownMixesAtNativeRate`

##### libFLAC fixtures decode bit-exactly

**Test**: `FlacDecoder_Decode_SoundfileFixture_MatchesWavBitExact`
