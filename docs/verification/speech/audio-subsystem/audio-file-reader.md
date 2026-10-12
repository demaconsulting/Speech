### AudioFileReader Verification

#### Verification Approach

Verified by `AudioFileReaderTests.cs` (container detection, errors, byte cap, mono read) and by
`FlacDecoderTests.cs`, whose fixture theory compares libFLAC-encoded files with lossless WAV files
of the same samples. Both files live under `test/DemaConsulting.Speech.Tests/AudioSubsystem`.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK; project `test/DemaConsulting.Speech.Tests`
- **Isolation**: in-memory images and files in the test output or temp directory; no network

#### Acceptance Criteria

Containers are detected by content, errors map to the documented exceptions with the path in the
message, an input over the byte cap is rejected before decoding (file, seekable stream and
non-seekable stream), `ReadMono` returns native-rate mono, and every libFLAC fixture decodes
bit-exactly to its WAV twin.

#### Test Scenarios

##### Supported extensions

**Test**: `AudioFileReader_SupportedExtensions_ListsWavAndFlac`

**Expected**: `SupportedExtensions` equals `[".wav", ".flac"]`.

##### Container is detected by content

**Test**: `AudioFileReader_Read_DetectsContainerByContent`,
`AudioFileReader_Read_WavUnderUnrelatedExtension_DetectsWavByContent`

**Expected**: A FLAC fixture copied to a `.dat` file decodes as FLAC (44100 Hz, mono), and a
hand-built one-sample WAV under a `.dat` extension decodes as WAV (8000 Hz, sample `0.5`).

##### Errors are reported

**Test**: `AudioFileReader_Read_Errors_AreReported`

**Expected**: A missing file throws `FileNotFoundException`; a text file or a three-byte stream
throws `InvalidDataException` (the file case names the path); a null stream throws
`ArgumentNullException`; an empty path throws `ArgumentException`; a negative `MaxSamples` throws
`ArgumentOutOfRangeException`.

##### Unsupported WAV names the path

**Test**: `AudioFileReader_Read_UnsupportedWav_ThrowsNotSupportedWithPath`

**Expected**: A WAV with format tag 2 (ADPCM) throws `NotSupportedException` whose message contains
the file path.

##### Oversized input is rejected before decoding

**Tests**: `AudioFileReader_Read_FileLargerThanByteCap_ThrowsBeforeDecoding`,
`AudioFileReader_Read_SeekableStreamLargerThanByteCap_Throws`,
`AudioFileReader_Read_NonSeekableStreamLargerThanByteCap_Throws`

**Expected**: With `MaxSamples = 0` the cap is the 1 MiB slack, so 2 MiB of zeros throws
`InvalidDataException` from the length alone (file: message contains "limit" and the path; seekable
stream: from its length; non-seekable stream: once the bounded read passes the cap).

##### Byte cap is clamped to the largest array length

**Test**: `AudioFileReader_Read_UnboundedMaxSamples_ClampsByteCapToArrayMaxLength`

**Expected**: With `MaxSamples = long.MaxValue`, a seekable stream that only reports a length of
`Array.MaxLength + 1` bytes throws `InvalidDataException` naming the `Array.MaxLength`-byte limit,
without reading any data or allocating.

##### Non-seekable stream within the cap decodes

**Test**: `AudioFileReader_Read_NonSeekableStream_Decodes`

**Expected**: A FLAC fixture wrapped in a non-seekable stream decodes (44100 Hz).

##### Mono read down-mixes at the native rate

**Test**: `AudioFileReader_ReadMono_Stereo_DownMixesAtNativeRate`

**Expected**: A stereo 48 kHz FLAC returns rate 48000 from both the path and stream overloads,
with one mono sample per frame and identical samples from both overloads.

##### libFLAC fixtures decode bit-exactly

**Test**: `FlacDecoder_Decode_SoundfileFixture_MatchesWavBitExact`

**Expected**: Each libFLAC fixture's decoded samples equal those of its lossless WAV twin.
