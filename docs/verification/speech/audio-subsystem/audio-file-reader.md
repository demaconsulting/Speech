### AudioFileReader Verification

#### Verification Approach

Verified by `AudioFileReaderTests.cs` (container detection, errors, options, byte cap, mono read) under
`test/DemaConsulting.Speech.Tests/AudioSubsystem`. The libFLAC fixture comparison lives with the
FlacDecoder unit.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK; project `test/DemaConsulting.Speech.Tests`
- **Isolation**: in-memory images and files in the test output or temp directory; no network

#### Acceptance Criteria

Containers are detected by content, errors map to the documented exceptions with the path in the
message, an input over the byte cap is rejected before decoding (file, seekable stream and
non-seekable stream), `VerifyChecksums` and `MaxSamples` are honored, `ReadMono` returns native-rate
mono, a stream is read from its current position, and an I/O failure propagates unwrapped.

#### Test Scenarios

##### Supported extensions

**Test**: `AudioFileReader_SupportedExtensions_ListsWavAndFlac`

**Expected**: `SupportedExtensions` equals `[".wav", ".flac"]`.

##### Container is detected by content

**Test**: `AudioFileReader_Read_DetectsContainerByContent`,
`AudioFileReader_Read_WavUnderUnrelatedExtension_DetectsWavByContent`

**Expected**: A FLAC fixture copied to a `.dat` file decodes as FLAC (44100 Hz, mono), and a
hand-built one-sample WAV under a `.dat` extension decodes as WAV (8000 Hz, sample `0.5`).

##### Missing file is reported

**Test**: `AudioFileReader_Read_MissingFile_ThrowsFileNotFound`

**Expected**: A path that does not exist throws `FileNotFoundException`.

##### Malformed input is reported

**Tests**: `AudioFileReader_Read_UnrecognizedFile_ThrowsInvalidDataWithPath`,
`AudioFileReader_Read_UnrecognizedStream_ThrowsInvalidDataWithoutPath`

**Expected**: A text file throws `InvalidDataException` whose message names the path; a
three-byte stream throws `InvalidDataException` with the reader's "neither a RIFF/WAVE nor a FLAC"
message and no path prefix.

##### Invalid arguments are rejected

**Tests**: `AudioFileReader_Read_NullStream_ThrowsArgumentNullException`,
`AudioFileReader_Read_BlankPath_ThrowsArgumentException`,
`AudioFileReader_Read_NegativeMaxSamplesStream_ThrowsArgumentOutOfRange`,
`AudioFileReader_Read_NegativeMaxSamples_Stream_ReportsOptionsParameter`,

**Expected**: A null stream throws `ArgumentNullException`; an empty or blank path throws
`ArgumentException`; a negative `MaxSamples` on the stream overload throws
`ArgumentOutOfRangeException`, and on the path overload one whose parameter name is
`options.MaxSamples`, except that a missing path reports `FileNotFoundException` first.

##### Stream position, ID3-prefixed FLAC and positions past the end

**Tests**: `AudioFileReader_Read_SeekableStreamAtNonZeroPosition_ReadsFromCurrentPosition`,
`AudioFileReader_Read_SeekableStreamPositionedPastEnd_ThrowsInvalidData`,
`AudioFileReader_Read_Id3PrefixedFlac_Decodes`

**Expected**: A seekable stream with leading junk and its position set to the audio decodes from
that position; a position past the end throws `InvalidDataException` (no negative-length
allocation); an ID3v2-prefixed FLAC stream decodes through the reader.

##### Directory, locked file and unsupported-WAV stream errors

**Tests**: `AudioFileReader_Read_DirectoryPath_ThrowsFileNotFound`,
`AudioFileReader_Read_FileLockedExclusively_ThrowsIOException`,
`AudioFileReader_Read_UnsupportedWavStream_ThrowsNotSupportedWithoutPath`

**Expected**: A directory path throws `FileNotFoundException`; a file held open without sharing
throws `IOException` unwrapped; an unsupported WAV read from a stream throws
`NotSupportedException` with no path prefix.

##### WAV stream and mono read errors

**Tests**: `AudioFileReader_Read_WavStream_Decodes`, `AudioFileReader_ReadMono_UnrecognizedStream_ThrowsInvalidData`,

**Expected**: A WAV fixture read from a stream decodes (44100 Hz, mono); `ReadMono` of a
three-byte stream throws `InvalidDataException` and of a missing path `FileNotFoundException`.

##### Checksum verification option

**Tests**: `AudioFileReader_Read_VerifyChecksumsDefault_DamagedFlac_Throws`,

**Expected**: A FLAC fixture whose final byte is corrupted throws `InvalidDataException` by default
and with `VerifyChecksums = true`, and decodes with `VerifyChecksums = false`.

##### Decoded sample limit

**Test**: `AudioFileReader_Read_MaxSamples_LimitsDecodedSamples`

**Expected**: With `MaxSamples = 100` the FLAC and WAV fixtures (well inside the byte cap) throw
`InvalidDataException` because they decode to more samples.

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

This scenario is owned by the FlacDecoder unit (`Speech-Audio-FlacDecoder-LibFlacFixtures`);
it is exercised through `AudioFileReader` and is listed in the FlacDecoder verification document.
