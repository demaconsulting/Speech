### AudioFileReader Verification

#### Verification Approach

Verified by `AudioFileReaderTests.cs` (container detection, errors, options, input size limit, mono read) under
`test/DemaConsulting.Speech.Tests/AudioSubsystem`. The libFLAC fixture comparison lives with the
FlacDecoder unit.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK; project `test/DemaConsulting.Speech.Tests`
- **Isolation**: in-memory images and files in the test output or temp directory; no network

#### Acceptance Criteria

Containers are detected by content, errors map to the documented exceptions with the path in the
message, an input larger than `MaxEncodedBytes` is rejected before it is fully read (file, seekable stream and
non-seekable stream), `VerifyChecksums`
and `MaxSamples` are honored (including for a valid but poorly compressed FLAC), `ReadMono` returns
native-rate mono, a stream is read from its current position, and an I/O failure propagates unwrapped.

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

**Expected**: With `MaxSamples = 100` the FLAC and WAV fixtures throw
`InvalidDataException` because they decode to more samples.

##### Unsupported WAV names the path

**Test**: `AudioFileReader_Read_UnsupportedWav_ThrowsNotSupportedWithPath`

**Expected**: A WAV with format tag 2 (ADPCM) throws `NotSupportedException` whose message contains
the file path.

##### Incompressible FLAC within and over the sample limit

**Tests**: `AudioFileReader_Read_IncompressibleFlacWithinMaxSamples_Decodes`,
`AudioFileReader_Read_IncompressibleFlacOverMaxSamples_Throws`

**Arrange**: A valid 4096-sample FLAC of noise Rice-coded with parameter 0 (about a kilobyte per
sample, over 4 MB in total, far above five bytes per sample plus 1 MiB).

**Expected**: With `MaxSamples = 4096` it decodes to 4096 samples; with `MaxSamples = 4095` it throws
`InvalidDataException`.

##### Input larger than MaxEncodedBytes is rejected

**Tests**: `AudioFileReader_Read_FileLargerThanMaxEncodedBytes_Throws`,
`AudioFileReader_Read_SeekableStreamLargerThanMaxEncodedBytes_Throws`,
`AudioFileReader_Read_NonSeekableStreamLargerThanMaxEncodedBytes_Throws`,
`AudioFileReader_Read_InputAtMaxEncodedBytes_Decodes`

**Expected**: With `MaxEncodedBytes = 1024`, a 1025-byte file (message names the path and the
`1024-byte limit`), a
seekable stream reporting 1025 bytes (nothing read), and a non-seekable 1 MiB stream (read stops at the limit) all
throw `InvalidDataException`; an input exactly at the limit is not rejected by the size check.

##### Negative MaxEncodedBytes is rejected

**Test**: `AudioFileReader_Read_NegativeMaxEncodedBytes_ReportsOptionsParameter`

**Expected**: A stream read and a path read both throw `ArgumentOutOfRangeException` with parameter name
`options.MaxEncodedBytes`.

##### Input larger than the largest array length is rejected

**Test**: `AudioFileReader_Read_UnboundedMaxSamples_ClampsByteCapToArrayMaxLength`

**Expected**: With `MaxSamples` and `MaxEncodedBytes` both `long.MaxValue`, a seekable stream that only
reports a length of
`Array.MaxLength + 1` bytes throws `InvalidDataException` naming the `Array.MaxLength`-byte limit,
without reading any data or allocating.

##### Non-seekable stream decodes

**Test**: `AudioFileReader_Read_NonSeekableStream_Decodes`

**Expected**: A FLAC fixture wrapped in a non-seekable stream decodes (44100 Hz).

##### Mono read down-mixes at the native rate

**Test**: `AudioFileReader_ReadMono_Stereo_DownMixesAtNativeRate`

**Expected**: A stereo 48 kHz FLAC returns rate 48000 from both the path and stream overloads,
with one mono sample per frame and identical samples from both overloads.

##### libFLAC fixtures decode bit-exactly

This scenario is owned by the FlacDecoder unit (`Speech-Audio-FlacDecoder-LibFlacFixtures`);
it is exercised through `AudioFileReader` and is listed in the FlacDecoder verification document.
