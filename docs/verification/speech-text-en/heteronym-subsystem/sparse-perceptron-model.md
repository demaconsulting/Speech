### SparsePerceptronModel Verification

#### Verification Approach

`SparsePerceptronModel`, `SparsePerceptronModelWriter` and the `HETW` rules they share through `ModelBinaryFormat` are
verified by automated unit tests in
`test/DemaConsulting.Speech.Text.En.Tests/HeteronymSubsystem/SparsePerceptronModelTests.cs`. The files under test are
produced by the real writer from a standard set of weights (the first and last buckets, the word boundaries 63 and 64,
negative values and the largest magnitudes), and the defect tests change one field or one region of such a file. The
hardest cases forge a file so that every cheap check passes: the stored length and the checksum are recomputed after the
tampering, so only the strict inflater and the structural decoding can reject it, and a Deflate stream is built by hand
for the inflation bomb and for the largest allowed payload of zeros. A seeded fuzz test applies thousands of random
mutations to valid files of both representations. No fake is needed.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK, on every target framework of the library
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Project**: `test/DemaConsulting.Speech.Text.En.Tests`
- **Isolation**: every test builds its own bytes; the fuzz test uses a fixed seed so a failure is reproducible; the
  culture test restores the current culture. The tests that build a 32 MiB payload of zeros need a few tens of
  megabytes of memory

#### Acceptance Criteria

- **Round trip**: a model round-trips with every header field and weight preserved, compressed and raw; absent and
  out-of-table buckets read as zero; a model without weights is valid; loading from a stream equals loading from bytes;
  a model of another feature-set version loads with that version; the culture has no effect; null arguments and an
  endless stream are rejected; the compressed and the raw form of one model rewrite to identical uncompressed bytes;
  the writer sorts its input, drops zero weights and refuses what the loader would refuse
- **Strict format**: every defect of the header, the lengths, the checksum, the payload structure and the Deflate
  stream is rejected with a message that names it, even when the lengths and the checksum are forged to match
- **Only the documented exception**: arbitrary seeded mutations only load or throw `InvalidDataException`

#### Test Scenarios

##### Round trip and writer

**Tests**: `SparsePerceptronModel_Load_RoundTrip_PreservesHeaderAndWeights`,
`SparsePerceptronModel_GetWeight_AbsentAndOutOfTableBuckets_AreZero`,
`SparsePerceptronModel_Load_Stream_EqualsLoadBytes`, `SparsePerceptronModel_Load_NoWeights_IsValid`,
`SparsePerceptronModel_Load_NullArgument_ThrowsArgumentNullException`,
`SparsePerceptronModel_Load_UnderArabicCulture_LoadsSameModel`,
`SparsePerceptronModel_Load_OtherFeatureSetVersion_LoadsAndPreservesVersion`,
`SparsePerceptronModelWriter_Compile_UnsortedInputWithZeros_SortsAndDropsZeros`,
`SparsePerceptronModelWriter_Compile_InvalidInput_Throws`,
`SparsePerceptronModelWriter_ToUncompressedImage_CompressedAndRaw_AreIdentical`

A compressed model with hash bits 16, temperature 1.5, scale 64, inventory version 2, a trainer version and a manifest
digest loads with those values, the mask 65,535 and every weight (including the buckets 0, 63, 64, 65, 127, 128, 1,000,
40,000 and 65,535). Buckets next to stored ones and beyond the table (65,536 and `uint.MaxValue`) read as zero. A
stream gives the same model as its bytes and is not disposed; a model with no weights loads. Null arguments throw.
Under `ar-SA` a model loads with the same temperature and weights, because floats are binary and never parsed from
text. A well-formed file that records another feature-set version loads and keeps it, so the caller can report it as
incompatible rather than invalid. Unsorted input with a zero weight gives a model with the right rows. The writer
throws for a null input, a duplicate bucket, a weight of -32768, a bucket outside the table, hash bits 15 and 25,
inventory versions 0 and 65,536, a NaN, zero or negative temperature or scale and a manifest digest of 31 bytes. The
compressed and the raw form of one model give byte-identical uncompressed images.

##### Header and lengths

**Tests**: `SparsePerceptronModel_Load_WrongMagic_IsRejectedAsForeignFile`,
`SparsePerceptronModel_Load_ShortHeader_IsRejectedAsTruncated`,
`SparsePerceptronModel_Load_InvalidHeaderField_IsRejected`,
`SparsePerceptronModel_Load_InvalidTemperatureOrScale_IsRejected`,
`SparsePerceptronModel_Load_BoundaryTemperatureAndScale_AreAccepted`,
`SparsePerceptronModel_Load_RowCountAboveTable_IsRejected`,
`SparsePerceptronModel_Load_StoredLengthMismatch_IsRejected`,
`SparsePerceptronModel_Load_PayloadLengthAboveCap_IsRejected`,
`SparsePerceptronModel_Load_PayloadLengthNotFittingRowCount_IsRejected`

A file that does not start with `HETW` is reported as not a model, even when it is two bytes long; a file with the
magic and fewer than 80 bytes is reported as truncated. The field theory rejects format version 2, an unknown flag bit,
feature-set version 0, inventory version 0, hash bits 15 and 25 and a non-zero reserved word, each with a message that
names the field. A temperature or scale that is NaN, zero, negative or above 1e6 is rejected, and 1e6 and a tiny
positive value are accepted. A row count above the hash table, a payload length above 32 MiB (rejected before any
inflation) and a payload length of 29 or 71 bytes for 10 rows are rejected. A stored length shorter than the bytes
present reports trailing bytes, a longer one reports truncation, and an uncompressed payload whose stored and payload
lengths differ is rejected.

##### Checksum and payload structure

**Tests**: `SparsePerceptronModel_Load_ChangedStoredByte_FailsChecksum`,
`SparsePerceptronModel_Load_ZeroGap_IsRejected`,
`SparsePerceptronModel_Load_BucketOutsideTable_IsRejected`, `SparsePerceptronModel_Load_BadVarints_AreRejected`,
`SparsePerceptronModel_Load_InvalidWeight_IsRejected`,
`SparsePerceptronModel_Load_PayloadNotMatchingStructure_IsRejected`

Changing one stored byte, or the checksum field, in both representations fails with the checksum message, which is
raised before anything is inflated. With the lengths and the checksum consistent, a gap of zero, a bucket beyond the
table, a varint longer than five bytes, with bits above 32 or cut off, a stored weight of 0 or -32768, and a payload
longer or shorter than its gaps and weights are each rejected with the message that names the defect.

##### Strict Deflate handling

**Tests**: `SparsePerceptronModel_Load_CutOffDeflateWithForgedLengthAndChecksum_IsRejected`,
`SparsePerceptronModel_Load_TrailingBytesInsideDeflateRegion_IsRejected`,
`SparsePerceptronModel_Load_InflationBomb_IsRejected`,
`SparsePerceptronModel_Load_LargestAllowedPayloadOfZeros_IsRejectedAsBadData`,
`SparsePerceptronModel_Load_EndlessStream_IsRejected`

A Deflate stream with its last 1 or 5 bytes removed is rejected as cut off although the stored length and the checksum
were recomputed to match (the case a plain Deflate reader accepts). Bytes after the end of the Deflate stream inside the
stored region are rejected as trailing, and a declared payload one byte longer or shorter than the stream holds is
rejected. A declared payload that the stored bytes could not inflate to even at the maximum ratio is rejected without
inflating, and a stream of 64 MiB of zeros under a header that declares 300,000 bytes is rejected as longer than
declared. A valid stream of 32 MiB of zeros at the largest allowed size is inflated and then rejected for its first gap
of zero. A stream that never ends is rejected as larger than the largest accepted file after reading only that much.

##### Seeded fuzz

**Test**: `SparsePerceptronModel_Load_SeededFuzz_OnlyLoadsOrThrowsInvalidDataException`

Six thousand mutations of four valid files (two models, each compressed and raw) by a deterministic generator (byte
flips, truncation, extension and changes of header fields, with and without a repaired checksum) each either load or
throw `InvalidDataException` and nothing else; more than half are rejected.
