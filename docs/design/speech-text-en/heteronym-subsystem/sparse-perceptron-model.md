### SparsePerceptronModel

**Purpose**: Hold the trained weights of the heteronym classifier as an immutable, thread-safe sparse model and load
them from a strictly validated file. The unit owns the `HETW` file format (version 1), the loader that never trusts a
file, the writer that tests and the trainer use to produce files, and the weight lookup. It knows nothing of
readings, words or features: it maps a bucket number to an integer weight.

**Data Model**:

*Meaning of the numbers.* A model has `2^hashBits` buckets. A bucket has at most one weight, a signed 16-bit integer
that is never 0 and never -32768; only buckets with a non-zero weight are stored. A real score is the integer score
divided by the model's `scale`; the model's `temperature` calibrates the confidence (see *HeteronymClassifier
Design*). Both are positive finite 32-bit floats, at most 1e6.

*File layout* (little endian, a fixed 80-byte header followed by the stored payload):

| Offset | Size | Field | Rule |
| ---: | ---: | --- | --- |
| 0 | 4 | magic | the ASCII text `HETW`, checked first even in a short file |
| 4 | 2 | formatVersion | 1 |
| 6 | 2 | flags | bit 0 set: the payload is raw Deflate; any other bit is rejected |
| 8 | 2 | featureSetVersion | at least 1 (a version other than this build's loads; the classifier rejects it) |
| 10 | 2 | inventoryVersion | at least 1 (the classifier checks it against the inventory) |
| 12 | 1 | hashBits | 16 to 24 |
| 13 | 3 | reserved | zero |
| 16 | 4 | temperature | float32, finite, greater than 0, at most 1e6 |
| 20 | 4 | scale | float32, finite, greater than 0, at most 1e6 |
| 24 | 4 | rowCount | at most the smaller of `2^hashBits` and one third of the payload cap |
| 28 | 4 | payloadLength | the inflated payload size in bytes, exact, at most 32 MiB |
| 32 | 4 | storedLength | the number of bytes after the header; the whole rest of the file |
| 36 | 4 | checksum | FNV-1a 32-bit hash of the **stored** bytes |
| 40 | 4 | trainerVersion | informational; 0 when unknown |
| 44 | 4 | reserved | zero |
| 48 | 32 | dataManifestSha256 | informational; all zero when unknown |

*Payload* (before compression): first `rowCount` bucket gaps, each an unsigned LEB128 varint, then `rowCount` signed
16-bit little-endian weights. The first bucket is its gap minus 1, every later bucket is the previous bucket plus its
gap, so every gap is at least 1 and the buckets strictly increase. A varint has at most five bytes, its fifth byte is
at most 0x0F and its value fits a positive 32-bit integer. The payload length is exactly the size of the gaps plus two
bytes per row. When the Deflate flag is clear the stored bytes are the payload and the two lengths are equal.

*In memory*: an occupancy bitmap of `2^hashBits` bits in 64-bit words (at most 2 MiB at 24 bits), the number of weights
stored before each word (the rank base) and the packed weights. A lookup is one bit test plus one population count over
the bits below it, with no hashing and no allocation. The header values (hash bits, temperature, scale, versions,
trainer version, row count and a copy of the manifest hash) are exposed as properties; the manifest hash is returned as
a fresh copy so the instance stays immutable.

**Loading rules** (`Load(byte[])` and `Load(Stream)`; the checks run in this order and each failure is an
`InvalidDataException` with a fixed message that names the defect and never the data):

1. The stream form reads at most 32 MiB plus 1 MiB plus the header (the largest accepted file) and rejects more, so an
   endless stream is stopped. The stream is read to its end, neither disposed nor retained
2. **Magic**: a file that does not start with `HETW` (including an empty or two-byte file) is reported as not a model;
   a file with the magic but fewer than 80 bytes is reported as truncated
3. **Header fields**: the format version, the flags, the feature-set version, the inventory version, the hash bits, the
   reserved bytes, the temperature and the scale are each checked against the table above
4. **Lengths**, before any payload byte is read or any large buffer is allocated: the row count against `2^hashBits`
   and the payload cap; the payload length against the cap of 32 MiB; the payload length between 3 and 7 bytes per row
   (each row needs one to five gap bytes and two weight bytes); the stored length against the bytes actually present
   (fewer present is a truncated file, more present is trailing bytes); for an uncompressed payload the stored and the
   payload length must be equal; for a Deflate payload the payload length must not exceed what the stored length can
   inflate to (Deflate cannot exceed about 1,032 bytes out per byte in, plus a small constant), which rejects a hostile
   declared size without inflating
5. **Checksum**: the FNV-1a hash of the stored bytes must equal the header's, and this is verified **before** anything
   is inflated, so no byte of a corrupted compressed region reaches the inflater
6. **Strict inflation** (below), for a Deflate payload, into a buffer of exactly the declared payload length
7. **Structure**: the gaps are decoded first and every one must be a well-formed varint of at least 1 that keeps the
   bucket inside the table; the gaps must end exactly where the weights begin (the payload length must equal the gaps
   plus the weights); only then is the weight array allocated and read, and a weight of 0 or -32768 is rejected

*Strict inflation.* A `DeflateStream` cannot say whether a stream ended properly: a stream that is cut off can deliver
all its data and then report the end exactly as a complete one, and it reads its input in large chunks. The stored
region is therefore served through a private read-only `RegionStream` that withholds the last byte until every earlier
byte was read and a read still wants data. An inflater asks for more input only when it has consumed everything given
and still needs data, so a read beyond the region means the stream has no end inside it (cut off), and a last byte that
was never asked for means the stream ended earlier and the rest is trailing bytes. The inflater fills exactly the
declared length; it then must produce no further byte (otherwise the stream is longer than declared), must not have
been starved (otherwise it is cut off), and must have taken the whole region (otherwise trailing bytes remain). At most
the declared length plus one byte is ever inflated, so a hostile stream, such as an inflation bomb, can never produce
more than the capped, declared size. An exception of the inflater that is not already `InvalidDataException`
(`InvalidOperationException`, `ArgumentException`, `IOException`, `IndexOutOfRangeException` or
`NotSupportedException`) is wrapped in `InvalidDataException`, so the loader has one failure type.

*Allocation bounds.* Every allocation is sized by a validated header value: the bitmap and the rank base by the hash
bits (at most 2 MiB and 1 MiB), the payload buffer by the payload length (at most 32 MiB), and the weights by the row
count after the structure of the gaps is proven. No allocation depends on an unchecked count from the file.

**Key Methods**:

- **Load(image)** and **Load(stream)** (internal): the loader above. A null argument throws `ArgumentNullException`
- **GetWeight(bucket)**: the integer weight of a bucket, 0 for a bucket with no stored weight or one outside the table;
  allocation-free and thread-safe. The caller passes a bucket already masked to the hash bits (`Mask`)
- **SparsePerceptronModelWriter.Compile(hashBits, inventoryVersion, temperature, scale, weights, compress,
  trainerVersion, dataManifestSha256)** (internal): builds the bytes of a model from `(bucket, weight)` pairs in any
  order. It validates the header fields with the loader's limits, orders the pairs, rejects a duplicate bucket, a bucket
  outside the table and a weight of -32768, drops zero weights, refuses more rows than a file may hold, and writes the
  payload compressed (the smallest size) or raw with the lengths, the flag and the checksum computed. A writer refuses
  everything the loader refuses, so a model it produces always loads
- **SparsePerceptronModelWriter.Write(output, ...)**: compiles and writes the bytes to a stream
- **SparsePerceptronModelWriter.ToUncompressedImage(image)**: rewrites a valid file with the Deflate flag cleared and
  the checksum recomputed, every other field unchanged. The compressed and the raw form of one model give identical
  bytes, so a later staleness check can compare two models byte for byte without depending on the Deflate
  implementation of a runtime
- **ModelBinaryFormat** (internal, static): the header parser, the checksum, the varint reader and writer, the
  compressor and the strict inflater, used by the loader and the writer alike so the two cannot disagree

**Error Handling**: every defect of a file is an `InvalidDataException` with a fixed message that names the defect (for
example `The heteronym model file is corrupt: the checksum does not match.`) and never carries the data of the file.
Null arguments throw `ArgumentNullException`; the
writer throws `ArgumentOutOfRangeException` or `ArgumentException` for input the loader would refuse. The loader
contains nothing else: whatever the bytes are, the result is a loaded model or an `InvalidDataException`, which a
seeded mutation test over valid files proves. Floats are read as binary, never parsed from
text, so the current culture has no effect.

**Dependencies**: the framework only (`DeflateStream`, `BinaryPrimitives`, `BitOperations`), and the FNV constants of
`FeatureHashing`.

**Callers**: `HeteronymClassifier` (the weights and the header values), `HeteronymModelProvider` (loading), the later
trainer (the writer) and the tests.
