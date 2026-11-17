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
mutations to valid files of both representations. Allocation is measured with the bytes allocated on the current thread
to prove that no hostile length drives a large allocation. No fake is needed.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK, on every target framework of the library
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Project**: `test/DemaConsulting.Speech.Text.En.Tests`
- **Isolation**: every test builds its own bytes; the fuzz test uses a fixed seed so a failure is reproducible; the
  culture test restores the current culture; the concurrency test starts its own tasks. The tests that build a 32 MiB
  payload of zeros need a few tens of megabytes of memory

#### Acceptance Criteria

A model round-trips with every header field and weight preserved, compressed and raw, and a model without weights is
valid; absent and out-of-table buckets read as zero; the compressed and the raw form of one model rewrite to identical
uncompressed bytes; loading from a stream equals loading from bytes and an endless stream is rejected; a null argument
throws `ArgumentNullException`; every defect of the header (magic, short header, format version, flags, feature-set
version, inventory version, hash bits, reserved fields, temperature and scale, row count, payload length against its cap
and its row count, stored length, an uncompressed length mismatch, a length no Deflate stream could reach) is rejected
with a message that names it and without any allocation driven by the hostile value; a changed stored byte or checksum
fails the checksum; every defect of the payload (a gap of zero, a bucket outside the table, a bad varint, a weight of 0
or -32768, a payload that does not fit its structure) is rejected; a Deflate stream that is cut off, has trailing bytes
in its region, inflates to fewer or more bytes than declared or is an inflation bomb is rejected, even when the lengths
and the checksum are forged to match, after at most the declared length plus one byte is inflated; arbitrary seeded
mutations only load or throw `InvalidDataException` with bounded allocation; the writer sorts its input, drops zero
weights, refuses what the loader would refuse and writes the same bytes to a stream; a loaded model is read concurrently
without differences; and the culture has no effect.

#### Test Scenarios

##### Round trip and lookup

**Tests**: `SparsePerceptronModel_Load_RoundTrip_PreservesHeaderAndWeights` (a theory over the compressed and the raw
form), `SparsePerceptronModel_GetWeight_AbsentAndOutOfTableBuckets_AreZero`,
`SparsePerceptronModelWriter_ToUncompressedImage_CompressedAndRaw_AreIdentical`,
`SparsePerceptronModel_Load_Stream_EqualsLoadBytes`, `SparsePerceptronModel_Load_NoWeights_IsValid` (a theory over both
forms)

A model with hash bits 16, temperature 1.5, scale 64, inventory version 2, a trainer version and a manifest hash loads
with those values, the mask 65,535 and every weight (including the buckets 0, 63, 64, 65, 127, 128, 1,000, 40,000 and
65,535); the Deflate flag in the file equals the representation. Buckets next to stored ones and beyond the table
(65,536 and `uint.MaxValue`) read as zero. The compressed and the raw form of one model give byte-identical uncompressed
images. A stream gives the same model as its bytes and is not disposed. A model with no weights loads in both forms.

##### Arguments and stream limits

**Tests**: `SparsePerceptronModel_Load_NullArgument_ThrowsArgumentNullException`,
`SparsePerceptronModel_Load_EndlessStream_IsRejected`

A null stream and a null byte array throw. A stream that never ends is rejected as larger than the largest accepted file
after reading only that much.

##### Header

**Tests**: `SparsePerceptronModel_Load_WrongMagic_IsRejectedAsForeignFile` (a theory over lengths 0, 2, 4 and 200),
`SparsePerceptronModel_Load_ShortHeader_IsRejectedAsTruncated`,
`SparsePerceptronModel_Load_InvalidHeaderField_IsRejected` (a theory over eleven field values),
`SparsePerceptronModel_Load_OtherFeatureSetVersion_LoadsAndPreservesVersion` (a theory over the compressed and the raw
form),
`SparsePerceptronModel_Load_InvalidTemperatureOrScale_IsRejected` (a theory over eleven values),
`SparsePerceptronModel_Load_BoundaryTemperatureAndScale_AreAccepted`

A file that does not start with `HETW` is reported as not a model whatever its length, even when it is empty or two
bytes long; a file with the magic and fewer than 80 bytes is reported as truncated. The field theory rejects format
version 2, the flag bits 0x0002 and 0x8000, feature-set version 0 (a well-formed file of another feature-set version
loads and keeps its version), inventory version 0, hash bits 15 and 25, and a non-zero byte in any of the three reserved
bytes or in the reserved word, each with a message that names the field. The float theory rejects a temperature or scale
that is NaN, infinite, zero, negative or above 1e6. The values 1e6 and a tiny positive value are accepted.

##### Lengths and allocation

**Tests**: `SparsePerceptronModel_Load_RowCountAboveTable_IsRejected`,
`SparsePerceptronModel_Load_PayloadLengthAboveCap_IsRejectedWithoutAllocating`,
`SparsePerceptronModel_Load_PayloadLengthNotFittingRowCount_IsRejected` (a theory over 29 and 71),
`SparsePerceptronModel_Load_StoredLengthMismatch_IsRejected`,
`SparsePerceptronModel_Load_RawPayloadLengthsDiffer_IsRejected`,
`SparsePerceptronModel_Load_PayloadLengthBeyondDeflateRatio_IsRejected`

A row count above the hash table or the payload cap is rejected. A payload length above 32 MiB is rejected before any
inflation and without a large allocation. A payload length of 29 bytes for 10 rows (below three bytes per row) or 71
(above seven) is rejected as not fitting. A stored length shorter than the bytes present reports unexpected trailing
bytes and a longer one reports truncation. An uncompressed payload whose stored and payload lengths differ is rejected.
A declared payload that the stored bytes could not inflate to even at the maximum ratio is rejected without inflating.

##### Checksum

**Tests**: `SparsePerceptronModel_Load_ChangedStoredByte_FailsChecksum` (a theory over both forms),
`SparsePerceptronModel_Load_ChangedChecksumField_FailsChecksum`

Changing one stored byte, in the compressed and in the raw form, or the checksum field itself fails with the checksum
message, which is raised before anything is inflated.

##### Payload structure

**Tests**: `SparsePerceptronModel_Load_ZeroGap_IsRejected`, `SparsePerceptronModel_Load_BucketOutsideTable_IsRejected`,
`SparsePerceptronModel_Load_BadVarints_AreRejected`, `SparsePerceptronModel_Load_InvalidWeight_IsRejected` (a theory
over 0 and -32768), `SparsePerceptronModel_Load_PayloadNotMatchingStructure_IsRejected`

With the lengths and the checksum consistent, a gap of zero, a bucket beyond the table, a varint longer than five bytes
or with bits above 32 or cut off, a stored weight of 0 or -32768, and a payload longer or shorter than its gaps and
weights are each rejected with the message that names the defect.

##### Strict Deflate handling

**Tests**: `SparsePerceptronModel_Load_CutOffDeflateWithForgedLengthAndChecksum_IsRejected` (a theory over 1, 2 and 5
removed bytes), `SparsePerceptronModel_Load_TrailingBytesInsideDeflateRegion_IsRejected`,
`SparsePerceptronModel_Load_DeflatePayloadSizeDiffersFromDeclared_IsRejected`,
`SparsePerceptronModel_Load_InflationBomb_IsRejectedWithoutLargeAllocation`,
`SparsePerceptronModel_Load_LargestAllowedPayloadOfZeros_IsRejectedAsBadData`

A Deflate stream with its last 1, 2 or 5 bytes removed is rejected as cut off although the stored length and the
checksum were recomputed to match (the case a plain Deflate reader accepts). Bytes after the end of the Deflate stream
inside the stored region are rejected as trailing. A stream that inflates to one byte fewer than declared reports that
it is too short or cut off, and one that inflates to one byte more reports that it is longer than declared. A stream of
64 MiB of zeros under a header that declares 300,000 bytes is rejected as longer than declared with less than 8 MB
allocated. A valid stream of 32 MiB of zeros at the largest allowed size is inflated and then rejected for its first gap
of zero, with less than 48 MiB allocated.

##### Seeded fuzz

**Test**: `SparsePerceptronModel_Load_SeededFuzz_OnlyLoadsOrThrowsInvalidDataException`

Six thousand mutations of four valid files (two models, each compressed and raw) by a deterministic generator (byte
flips, truncation, extension and changes of header fields, with and without a repaired checksum) each either load or
throw `InvalidDataException` and nothing else; more than half are rejected; the worst allocation of one attempt stays
below 48 MiB.

##### Writer

**Tests**: `SparsePerceptronModelWriter_Compile_UnsortedInputWithZeros_SortsAndDropsZeros`,
`SparsePerceptronModelWriter_Compile_InvalidInput_Throws`,
`SparsePerceptronModelWriter_Write_Stream_WritesCompiledBytes`

Unsorted input with a zero weight gives a model with three rows and the right weights. The writer throws for a null
input, a duplicate bucket (also with one weight zero), a weight of -32768, a bucket outside the table, hash bits 15 and
25, inventory versions 0 and 65,536, a NaN, zero or negative temperature or scale and a manifest hash of 31 bytes. The
stream overload writes exactly the compiled bytes and rejects a null stream.

##### Concurrency and culture

**Tests**: `SparsePerceptronModel_GetWeight_ConcurrentReaders_SeeTheSameWeights`,
`SparsePerceptronModel_Load_UnderArabicCulture_LoadsSameModel`

Sixteen tasks reading every weight of one loaded model all obtain the same sum. Under `ar-SA` a model loads with the
same temperature and weights, because floats are binary and never parsed from text.
