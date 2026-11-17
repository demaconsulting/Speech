using System.Buffers.Binary;
using System.Globalization;
using System.IO.Compression;
using DemaConsulting.Speech.Text.En.HeteronymSubsystem;

namespace DemaConsulting.Speech.Text.En.Tests.HeteronymSubsystem;

/// <summary>
///     Unit tests for <see cref="SparsePerceptronModel"/>, <see cref="SparsePerceptronModelWriter"/> and the
///     <c>HETW</c> file rules they share through <see cref="ModelBinaryFormat"/>: round trips, strict
///     validation of every field, strict Deflate handling and a seeded fuzz of the loader.
/// </summary>
public sealed class SparsePerceptronModelTests
{
    /// <summary>The weights of the standard test model: first and last buckets, word boundaries and negative values.</summary>
    private static readonly (uint Bucket, short Weight)[] StandardWeights =
    [
        (0u, 5), (1u, -7), (63u, 64), (64u, -64), (65u, 3), (127u, 100), (128u, -1), (1_000u, 32_767), (40_000u, -32_767), (65_535u, 9),
    ];

    /// <summary>Proves a model round-trips through the compressed and the raw representation, with every header field and weight preserved.</summary>
    /// <param name="compress">Whether the payload is stored as Deflate.</param>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SparsePerceptronModel_Load_RoundTrip_PreservesHeaderAndWeights(bool compress)
    {
        // Arrange
        var manifest = Enumerable.Range(1, 32).Select(i => (byte)i).ToArray();
        var image = SparsePerceptronModelWriter.Compile(16, 2, 1.5f, 64f, StandardWeights, compress, trainerVersion: 77, dataManifestSha256: manifest);

        // Act
        var model = SparsePerceptronModel.Load(image);

        // Assert
        Assert.Equal(16, model.HashBits);
        Assert.Equal(1.5f, model.Temperature);
        Assert.Equal(64f, model.Scale);
        Assert.Equal(2, model.InventoryVersion);
        Assert.Equal(HeteronymFeatureExtractor.FeatureSetVersion, model.FeatureSetVersion);
        Assert.Equal(77u, model.TrainerVersion);
        Assert.Equal(StandardWeights.Length, model.RowCount);
        Assert.Equal(65_535u, model.Mask);
        Assert.Equal(manifest, model.GetDataManifestSha256());
        Assert.All(StandardWeights, pair => Assert.Equal(pair.Weight, model.GetWeight(pair.Bucket)));
        Assert.Equal((image[6] & 1) == 1, compress);
    }

    /// <summary>Proves buckets with no stored weight read as zero, including at word boundaries and beyond the table.</summary>
    [Fact]
    public void SparsePerceptronModel_GetWeight_AbsentAndOutOfTableBuckets_AreZero()
    {
        // Arrange
        var model = SparsePerceptronModel.Load(SparsePerceptronModelWriter.Compile(16, 1, 1f, 64f, StandardWeights));

        // Act / Assert
        foreach (var bucket in new uint[] { 2, 62, 66, 126, 129, 999, 1_001, 65_534 })
        {
            Assert.Equal(0, model.GetWeight(bucket));
        }

        Assert.Equal(0, model.GetWeight(65_536));
        Assert.Equal(0, model.GetWeight(uint.MaxValue));
    }

    /// <summary>Proves the compressed and the raw form of one model rewrite to byte-identical uncompressed images.</summary>
    [Fact]
    public void SparsePerceptronModelWriter_ToUncompressedImage_CompressedAndRaw_AreIdentical()
    {
        // Arrange
        var compressed = SparsePerceptronModelWriter.Compile(16, 2, 1f, 64f, StandardWeights, compress: true);
        var raw = SparsePerceptronModelWriter.Compile(16, 2, 1f, 64f, StandardWeights, compress: false);

        // Act
        var fromCompressed = SparsePerceptronModelWriter.ToUncompressedImage(compressed);
        var fromRaw = SparsePerceptronModelWriter.ToUncompressedImage(raw);

        // Assert
        Assert.Equal(raw, fromRaw);
        Assert.Equal(raw, fromCompressed);
        Assert.NotEqual(compressed.Length, raw.Length);
    }

    /// <summary>Proves loading from a stream gives the same model as loading from bytes, and does not dispose the stream.</summary>
    [Fact]
    public void SparsePerceptronModel_Load_Stream_EqualsLoadBytes()
    {
        // Arrange
        var image = SparsePerceptronModelWriter.Compile(16, 1, 1f, 64f, StandardWeights);
        using var stream = new MemoryStream(image);

        // Act
        var model = SparsePerceptronModel.Load(stream);

        // Assert
        Assert.True(stream.CanRead);
        Assert.All(StandardWeights, pair => Assert.Equal(pair.Weight, model.GetWeight(pair.Bucket)));
    }

    /// <summary>Proves a model with no weights is valid, in both representations.</summary>
    /// <param name="compress">Whether the payload is stored as Deflate.</param>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SparsePerceptronModel_Load_NoWeights_IsValid(bool compress)
    {
        // Act
        var model = SparsePerceptronModel.Load(SparsePerceptronModelWriter.Compile(16, 1, 1f, 64f, [], compress));

        // Assert
        Assert.Equal(0, model.RowCount);
        Assert.Equal(0, model.GetWeight(0));
    }

    /// <summary>Proves null arguments are rejected.</summary>
    [Fact]
    public void SparsePerceptronModel_Load_NullArgument_ThrowsArgumentNullException()
    {
        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => SparsePerceptronModel.Load((byte[])null!));
        Assert.Throws<ArgumentNullException>(() => SparsePerceptronModel.Load((Stream)null!));
        Assert.Throws<ArgumentNullException>(() => SparsePerceptronModelWriter.ToUncompressedImage(null!));
    }

    /// <summary>Proves a stream larger than the largest accepted file is rejected without being read to its end.</summary>
    [Fact]
    public void SparsePerceptronModel_Load_EndlessStream_IsRejected()
    {
        // Arrange
        using var endless = new EndlessStream();

        // Act
        var exception = Assert.Throws<InvalidDataException>(() => SparsePerceptronModel.Load(endless));

        // Assert
        Assert.Contains("larger than the maximum", exception.Message, StringComparison.Ordinal);
        Assert.InRange(endless.BytesServed, 1, ModelBinaryFormat.MaxFileLength + 100_000);
    }

    /// <summary>Proves the magic is checked first, so a foreign or tiny file is reported as not a model.</summary>
    /// <param name="length">The length of the file.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(200)]
    public void SparsePerceptronModel_Load_WrongMagic_IsRejectedAsForeignFile(int length)
    {
        // Arrange
        var foreign = Enumerable.Repeat((byte)'X', length).ToArray();

        // Act / Assert
        AssertInvalid(foreign, "the magic 'HETW' is missing");
    }

    /// <summary>Proves a file with the right magic but fewer bytes than the header is reported as truncated.</summary>
    [Fact]
    public void SparsePerceptronModel_Load_ShortHeader_IsRejectedAsTruncated()
    {
        // Arrange
        var image = Standard(compress: false)[..79];

        // Act / Assert
        AssertInvalid(image, "truncated: the header needs 80 bytes");
    }

    /// <summary>Proves each invalid header field is rejected with a message that names it.</summary>
    /// <param name="offset">The byte offset of the field.</param>
    /// <param name="bytes">The little-endian bytes to write there.</param>
    /// <param name="expected">The text the message must contain.</param>
    [Theory]
    [InlineData(4, new byte[] { 2, 0 }, "Unsupported heteronym model format version 2")]
    [InlineData(6, new byte[] { 2, 0 }, "unknown flag bits 0x0002")]
    [InlineData(6, new byte[] { 0, 0x80 }, "unknown flag bits 0x8000")]
    [InlineData(8, new byte[] { 0, 0 }, "the feature-set version must be at least 1")]
    [InlineData(10, new byte[] { 0, 0 }, "the inventory version must be at least 1")]
    [InlineData(12, new byte[] { 15 }, "the hash bits 15 are outside 16 to 24")]
    [InlineData(12, new byte[] { 25 }, "the hash bits 25 are outside 16 to 24")]
    [InlineData(13, new byte[] { 1 }, "a reserved field is not zero")]
    [InlineData(15, new byte[] { 0x80 }, "a reserved field is not zero")]
    [InlineData(44, new byte[] { 1, 0, 0, 0 }, "a reserved field is not zero")]
    [InlineData(44, new byte[] { 0, 0, 0, 0x80 }, "a reserved field is not zero")]
    public void SparsePerceptronModel_Load_InvalidHeaderField_IsRejected(int offset, byte[] bytes, string expected)
    {
        // Arrange
        var image = Standard(compress: false);
        bytes.CopyTo(image, offset);

        // Act / Assert
        AssertInvalid(image, expected);
    }

    /// <summary>Proves a well-formed file that records another feature-set version still loads, with that version preserved, so the caller can report it as incompatible rather than invalid.</summary>
    /// <param name="compress">Whether the payload is stored as Deflate.</param>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SparsePerceptronModel_Load_OtherFeatureSetVersion_LoadsAndPreservesVersion(bool compress)
    {
        // Arrange: the header is not covered by the checksum, so patching the field keeps the file well-formed
        var image = Standard(compress);
        BinaryPrimitives.WriteUInt16LittleEndian(image.AsSpan(8), 7);

        // Act
        var model = SparsePerceptronModel.Load(image);

        // Assert
        Assert.Equal(7, model.FeatureSetVersion);
        Assert.Equal(StandardWeights.Length, model.RowCount);
        Assert.All(StandardWeights, pair => Assert.Equal(pair.Weight, model.GetWeight(pair.Bucket)));
    }

    /// <summary>Proves a temperature or scale that is NaN, infinite, zero, negative or above the maximum is rejected.</summary>
    /// <param name="offset">16 for the temperature, 20 for the scale.</param>
    /// <param name="value">The invalid value.</param>
    [Theory]
    [InlineData(16, float.NaN)]
    [InlineData(16, float.PositiveInfinity)]
    [InlineData(16, float.NegativeInfinity)]
    [InlineData(16, 0f)]
    [InlineData(16, -1f)]
    [InlineData(16, 1.0000001e6f)]
    [InlineData(20, float.NaN)]
    [InlineData(20, float.PositiveInfinity)]
    [InlineData(20, 0f)]
    [InlineData(20, -64f)]
    [InlineData(20, 2e6f)]
    public void SparsePerceptronModel_Load_InvalidTemperatureOrScale_IsRejected(int offset, float value)
    {
        // Arrange
        var image = Standard(compress: false);
        BinaryPrimitives.WriteSingleLittleEndian(image.AsSpan(offset), value);

        // Act / Assert
        AssertInvalid(image, offset == 16 ? "the temperature must be finite and greater than zero" : "the scale must be finite and greater than zero");
    }

    /// <summary>Proves the boundary values of temperature and scale (1e6, tiny positive) are accepted.</summary>
    [Fact]
    public void SparsePerceptronModel_Load_BoundaryTemperatureAndScale_AreAccepted()
    {
        // Act
        var model = SparsePerceptronModel.Load(SparsePerceptronModelWriter.Compile(16, 1, 1e6f, float.Epsilon, StandardWeights));

        // Assert
        Assert.Equal(1e6f, model.Temperature);
        Assert.Equal(float.Epsilon, model.Scale);
    }

    /// <summary>Proves a row count above the hash table size (or the payload cap) is rejected.</summary>
    [Fact]
    public void SparsePerceptronModel_Load_RowCountAboveTable_IsRejected()
    {
        // Arrange: 65,537 rows cannot fit 2^16 buckets
        var image = Standard(compress: false);
        BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(24), 65_537);

        // Act / Assert
        AssertInvalid(image, "the row count is larger than the hash table or the payload cap allows");
    }

    /// <summary>Proves a payload length above the cap is rejected before any inflation is attempted.</summary>
    [Fact]
    public void SparsePerceptronModel_Load_PayloadLengthAboveCap_IsRejected()
    {
        // Arrange
        var image = Standard(compress: true);
        BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(28), (uint)ModelBinaryFormat.MaxPayloadLength + 1);

        // Act
        var exception = Assert.Throws<InvalidDataException>(() => SparsePerceptronModel.Load(image));

        // Assert
        Assert.Contains("is larger than the maximum", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>Proves a payload length that cannot fit the row count (too small for the weights, or larger than any gap encoding) is rejected.</summary>
    /// <param name="payloadLength">The declared payload length.</param>
    [Theory]
    [InlineData(29)]
    [InlineData(71)]
    public void SparsePerceptronModel_Load_PayloadLengthNotFittingRowCount_IsRejected(int payloadLength)
    {
        // Arrange: 10 rows need 30 to 70 payload bytes
        var image = Standard(compress: false);
        BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(28), (uint)payloadLength);

        // Act / Assert
        AssertInvalid(image, "the payload length does not fit the row count");
    }

    /// <summary>Proves a stored length shorter than the bytes after the header reports trailing bytes, and a longer one reports truncation.</summary>
    [Fact]
    public void SparsePerceptronModel_Load_StoredLengthMismatch_IsRejected()
    {
        // Arrange
        var shorter = Standard(compress: false);
        var longer = Standard(compress: false);
        var stored = BinaryPrimitives.ReadUInt32LittleEndian(shorter.AsSpan(32));
        BinaryPrimitives.WriteUInt32LittleEndian(shorter.AsSpan(32), stored - 1);
        BinaryPrimitives.WriteUInt32LittleEndian(longer.AsSpan(32), stored + 1);

        // Act / Assert
        AssertInvalid(shorter, "1 unexpected trailing bytes");
        AssertInvalid(longer, "truncated");
    }

    /// <summary>Proves an uncompressed payload must have equal stored and payload lengths.</summary>
    [Fact]
    public void SparsePerceptronModel_Load_RawPayloadLengthsDiffer_IsRejected()
    {
        // Arrange
        var image = Standard(compress: false);
        var payload = BinaryPrimitives.ReadUInt32LittleEndian(image.AsSpan(28));
        BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(28), payload + 1);

        // Act / Assert
        AssertInvalid(image, "an uncompressed payload must have equal stored and payload lengths");
    }

    /// <summary>Proves a declared payload length that the stored bytes cannot inflate to (a hostile length) is rejected without inflating.</summary>
    [Fact]
    public void SparsePerceptronModel_Load_PayloadLengthBeyondDeflateRatio_IsRejected()
    {
        // Arrange: claim 180,000 payload bytes (60,000 rows) from a tiny stored region
        var image = Standard(compress: true);
        var rows = 60_000u;
        BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(24), rows);
        BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(28), rows * 3);

        // Act / Assert
        AssertInvalid(image, "larger than the stored bytes can inflate to");
    }

    /// <summary>Proves a changed stored byte fails the checksum, which is verified before inflation, in both representations.</summary>
    /// <param name="compress">Whether the payload is stored as Deflate.</param>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SparsePerceptronModel_Load_ChangedStoredByte_FailsChecksum(bool compress)
    {
        // Arrange
        var image = Standard(compress);
        image[^1] ^= 0x01;

        // Act / Assert
        AssertInvalid(image, "the checksum does not match");
    }

    /// <summary>Proves the checksum field itself is checked.</summary>
    [Fact]
    public void SparsePerceptronModel_Load_ChangedChecksumField_FailsChecksum()
    {
        // Arrange
        var image = Standard(compress: false);
        image[36] ^= 0xFF;

        // Act / Assert
        AssertInvalid(image, "the checksum does not match");
    }

    /// <summary>Proves the payload structure is validated even when the file is otherwise consistent: a gap of zero.</summary>
    [Fact]
    public void SparsePerceptronModel_Load_ZeroGap_IsRejected()
    {
        // Arrange: one row with gap 0 and weight 5
        var payload = new byte[] { 0, 5, 0 };

        // Act / Assert
        AssertInvalid(Craft(payload, rows: 1), "a bucket gap of zero");
    }

    /// <summary>Proves a bucket beyond the hash table is rejected.</summary>
    [Fact]
    public void SparsePerceptronModel_Load_BucketOutsideTable_IsRejected()
    {
        // Arrange: first gap 65,537 puts the bucket at 65,536, one past the end of a 16-bit table
        var payload = new byte[] { 0x81, 0x80, 0x04, 5, 0 };

        // Act / Assert
        AssertInvalid(Craft(payload, rows: 1), "a bucket outside the hash table");
    }

    /// <summary>Proves a varint longer than five bytes or with bits above 32 is rejected, and so is one that is cut off.</summary>
    [Fact]
    public void SparsePerceptronModel_Load_BadVarints_AreRejected()
    {
        // Act / Assert
        AssertInvalid(Craft([0xFF, 0xFF, 0xFF, 0xFF, 0x1F, 5, 0], rows: 1), "an overlong varint");
        AssertInvalid(Craft([0x80, 0x80, 0x80, 0x80, 0x80, 5, 0], rows: 1), "an overlong varint");
        AssertInvalid(Craft([0xFF, 0xFF, 0xFF, 0xFF, 0x0F, 5, 0], rows: 1), "a varint larger than 2147483647");
        AssertInvalid(Craft([0x81, 5, 0], rows: 1), "truncated inside a varint");
    }

    /// <summary>Proves a stored weight of zero or -32768 is rejected.</summary>
    /// <param name="weight">The invalid weight.</param>
    [Theory]
    [InlineData((short)0)]
    [InlineData(short.MinValue)]
    public void SparsePerceptronModel_Load_InvalidWeight_IsRejected(short weight)
    {
        // Arrange
        var bytes = new byte[] { 1, 0, 0 };
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(1), weight);

        // Act / Assert
        AssertInvalid(Craft(bytes, rows: 1), "a weight that is zero or -32768");
    }

    /// <summary>Proves a payload longer or shorter than its gaps and weights is rejected.</summary>
    [Fact]
    public void SparsePerceptronModel_Load_PayloadNotMatchingStructure_IsRejected()
    {
        // Arrange: one row (gap 1, weight 5) with one spare gap byte, and two declared rows with only one row of payload
        var spare = new byte[] { 1, 1, 5, 0 };
        var tooShort = new byte[] { 1, 5, 0 };

        // Act / Assert
        AssertInvalid(Craft(spare, rows: 1), "does not match its gaps and weights");
        AssertInvalid(Craft(tooShort, rows: 2), "the payload length does not fit the row count");
    }

    /// <summary>Proves a Deflate stream that is cut off is rejected even when the stored length and checksum are forged to match.</summary>
    /// <param name="removed">How many trailing bytes of the Deflate stream are removed.</param>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(5)]
    public void SparsePerceptronModel_Load_CutOffDeflateWithForgedLengthAndChecksum_IsRejected(int removed)
    {
        // Arrange
        var image = Standard(compress: true);
        var stored = image.AsSpan(ModelBinaryFormat.HeaderLength)[..^removed].ToArray();

        // Act / Assert
        AssertInvalid(Forge(image, stored));
    }

    /// <summary>Proves bytes after the end of the Deflate stream, inside the stored region, are rejected even when the lengths and checksum are forged to match.</summary>
    [Fact]
    public void SparsePerceptronModel_Load_TrailingBytesInsideDeflateRegion_IsRejected()
    {
        // Arrange
        var image = Standard(compress: true);
        var stored = image.AsSpan(ModelBinaryFormat.HeaderLength).ToArray().Concat(new byte[] { 0, 0, 0, 0 }).ToArray();

        // Act / Assert
        AssertInvalid(Forge(image, stored));
    }

    /// <summary>Proves a Deflate payload that inflates to fewer bytes than declared, or more, is rejected.</summary>
    [Fact]
    public void SparsePerceptronModel_Load_DeflatePayloadSizeDiffersFromDeclared_IsRejected()
    {
        // Arrange: the declared payload length is one more, then one less, than the stream really holds
        var longer = Standard(compress: true);
        var shorter = Standard(compress: true);
        var length = BinaryPrimitives.ReadUInt32LittleEndian(longer.AsSpan(28));
        BinaryPrimitives.WriteUInt32LittleEndian(longer.AsSpan(28), length + 1);
        BinaryPrimitives.WriteUInt32LittleEndian(shorter.AsSpan(28), length - 1);

        // Act / Assert: fewer bytes than declared is reported either as a short stream or a stream without its end
        AssertInvalid(longer);
        AssertInvalid(shorter, "longer than declared");
    }

    /// <summary>Proves an inflation bomb (a stream that inflates to far more than declared) is rejected.</summary>
    [Fact]
    public void SparsePerceptronModel_Load_InflationBomb_IsRejected()
    {
        // Arrange: 64 MiB of zeros compress to a small stream; the header declares only 300,000 payload bytes
        var bomb = DeflateZeros(64 * 1024 * 1024);
        var image = Standard(compress: true);
        image[12] = 24;
        BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(24), 100_000);
        BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(28), 300_000);
        var forged = Forge(image, bomb);

        // Act
        var exception = Assert.Throws<InvalidDataException>(() => SparsePerceptronModel.Load(forged));

        // Assert
        Assert.Contains("longer than declared", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>Proves a stream that is valid Deflate but not a valid payload (all zero gaps at the largest allowed size) is rejected as bad data after a bounded inflation.</summary>
    [Fact]
    public void SparsePerceptronModel_Load_LargestAllowedPayloadOfZeros_IsRejectedAsBadData()
    {
        // Arrange: 32 MiB of zeros and the matching row count at 24 hash bits
        var rows = ModelBinaryFormat.MaxPayloadLength / 3;
        var payload = ModelBinaryFormat.MaxPayloadLength - (ModelBinaryFormat.MaxPayloadLength % 3);
        var stored = DeflateZeros(payload);
        var header = new byte[ModelBinaryFormat.HeaderLength];
        Standard(compress: true).AsSpan(0, header.Length).CopyTo(header);
        header[12] = 24;
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(24), (uint)rows);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(28), (uint)payload);
        var image = Forge([.. header, .. stored], stored);

        // Act
        var exception = Assert.Throws<InvalidDataException>(() => SparsePerceptronModel.Load(image));

        // Assert: the payload is inflated and then rejected for its first gap of zero
        Assert.Contains("a bucket gap of zero", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>Proves arbitrary, seeded mutations of valid files (byte flips, truncation, extension, header-field changes with and without a repaired checksum, in both representations) only ever load or throw <see cref="InvalidDataException"/>.</summary>
    [Fact]
    public void SparsePerceptronModel_Load_SeededFuzz_OnlyLoadsOrThrowsInvalidDataException()
    {
        // Arrange: valid images of both representations and a deterministic generator
        var images = new[] { Standard(compress: true), Standard(compress: false), LargerModel(compress: true), LargerModel(compress: false) };
        var random = new XorShift(0x5EED_CAFE_1234_5678UL);
        var loaded = 0;
        var rejected = 0;

        // Act
        for (var attempt = 0; attempt < 6_000; attempt++)
        {
            var source = images[attempt % images.Length];
            var mutated = Mutate(source, random, attempt);
            try
            {
                SparsePerceptronModel.Load(mutated);
                loaded++;
            }
            catch (InvalidDataException)
            {
                rejected++;
            }
        }

        // Assert: any other exception type would have failed the test; most mutations are caught, some are harmless
        Assert.Equal(6_000, loaded + rejected);
        Assert.True(rejected > 3_000, $"only {rejected} of 6000 mutations were rejected");
    }

    /// <summary>Proves the writer sorts its input, drops zero weights, and records the row count.</summary>
    [Fact]
    public void SparsePerceptronModelWriter_Compile_UnsortedInputWithZeros_SortsAndDropsZeros()
    {
        // Arrange
        (uint, short)[] weights = [(500, 3), (7, 0), (2, -4), (65_535, 1)];

        // Act
        var model = SparsePerceptronModel.Load(SparsePerceptronModelWriter.Compile(16, 1, 1f, 64f, weights));

        // Assert
        Assert.Equal(3, model.RowCount);
        Assert.Equal(-4, model.GetWeight(2));
        Assert.Equal(3, model.GetWeight(500));
        Assert.Equal(1, model.GetWeight(65_535));
        Assert.Equal(0, model.GetWeight(7));
    }

    /// <summary>Proves the writer refuses input the loader would refuse.</summary>
    [Fact]
    public void SparsePerceptronModelWriter_Compile_InvalidInput_Throws()
    {
        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => SparsePerceptronModelWriter.Compile(16, 1, 1f, 64f, null!));
        Assert.Throws<ArgumentException>(() => SparsePerceptronModelWriter.Compile(16, 1, 1f, 64f, [(5u, (short)1), (5u, (short)2)]));
        Assert.Throws<ArgumentException>(() => SparsePerceptronModelWriter.Compile(16, 1, 1f, 64f, [(5u, (short)1), (5u, (short)0)]));
        Assert.Throws<ArgumentException>(() => SparsePerceptronModelWriter.Compile(16, 1, 1f, 64f, [(5u, short.MinValue)]));
        Assert.Throws<ArgumentOutOfRangeException>(() => SparsePerceptronModelWriter.Compile(16, 1, 1f, 64f, [(65_536u, (short)1)]));
        Assert.Throws<ArgumentOutOfRangeException>(() => SparsePerceptronModelWriter.Compile(15, 1, 1f, 64f, []));
        Assert.Throws<ArgumentOutOfRangeException>(() => SparsePerceptronModelWriter.Compile(25, 1, 1f, 64f, []));
        Assert.Throws<ArgumentOutOfRangeException>(() => SparsePerceptronModelWriter.Compile(16, 0, 1f, 64f, []));
        Assert.Throws<ArgumentOutOfRangeException>(() => SparsePerceptronModelWriter.Compile(16, 65_536, 1f, 64f, []));
        Assert.Throws<ArgumentOutOfRangeException>(() => SparsePerceptronModelWriter.Compile(16, 1, float.NaN, 64f, []));
        Assert.Throws<ArgumentOutOfRangeException>(() => SparsePerceptronModelWriter.Compile(16, 1, 0f, 64f, []));
        Assert.Throws<ArgumentOutOfRangeException>(() => SparsePerceptronModelWriter.Compile(16, 1, 1f, -1f, []));
        Assert.Throws<ArgumentException>(() => SparsePerceptronModelWriter.Compile(16, 1, 1f, 64f, [], dataManifestSha256: new byte[31]));
    }

    /// <summary>Proves the stream overload of the writer writes exactly the compiled bytes.</summary>
    [Fact]
    public void SparsePerceptronModelWriter_Write_Stream_WritesCompiledBytes()
    {
        // Arrange
        using var output = new MemoryStream();

        // Act
        SparsePerceptronModelWriter.Write(output, 16, 1, 1f, 64f, StandardWeights, compress: false);

        // Assert
        Assert.Equal(SparsePerceptronModelWriter.Compile(16, 1, 1f, 64f, StandardWeights, compress: false), output.ToArray());
        Assert.Throws<ArgumentNullException>(() => SparsePerceptronModelWriter.Write(null!, 16, 1, 1f, 64f, []));
    }

    /// <summary>Proves a loaded model is immutable and thread-safe: many threads read the same weights concurrently.</summary>
    [Fact]
    public async Task SparsePerceptronModel_GetWeight_ConcurrentReaders_SeeTheSameWeights()
    {
        // Arrange
        var model = SparsePerceptronModel.Load(SparsePerceptronModelWriter.Compile(16, 1, 1f, 64f, StandardWeights));

        // Act
        var results = await Task.WhenAll(Enumerable.Range(0, 16)
            .Select(_ => Task.Run(() => StandardWeights.Sum(pair => (long)model.GetWeight(pair.Bucket)) + model.GetWeight(2), TestContext.Current.CancellationToken)));

        // Assert
        Assert.All(results, result => Assert.Equal(StandardWeights.Sum(pair => (long)pair.Weight), result));
    }

    /// <summary>Proves the model loads the same under a culture with unusual number formatting (floats are binary, never parsed from text).</summary>
    [Fact]
    public void SparsePerceptronModel_Load_UnderArabicCulture_LoadsSameModel()
    {
        // Arrange
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("ar-SA");

            // Act
            var model = SparsePerceptronModel.Load(SparsePerceptronModelWriter.Compile(16, 1, 1.25f, 64f, StandardWeights));

            // Assert
            Assert.Equal(1.25f, model.Temperature);
            Assert.Equal(5, model.GetWeight(0));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    /// <summary>
    ///     Builds the standard test model.
    /// </summary>
    /// <param name="compress">Whether to store the payload as Deflate.</param>
    /// <returns>The file bytes.</returns>
    private static byte[] Standard(bool compress) =>
        SparsePerceptronModelWriter.Compile(16, 2, 1f, 64f, StandardWeights, compress);

    /// <summary>
    ///     Builds a model with a few thousand weights, so mutations hit varied payload structure.
    /// </summary>
    /// <param name="compress">Whether to store the payload as Deflate.</param>
    /// <returns>The file bytes.</returns>
    private static byte[] LargerModel(bool compress)
    {
        var weights = Enumerable.Range(0, 3_000).Select(i => ((uint)(i * 21) % 65_536u, (short)((i % 200) - 99 == 0 ? 1 : (i % 200) - 99))).DistinctBy(pair => pair.Item1);
        return SparsePerceptronModelWriter.Compile(16, 2, 2f, 64f, weights, compress);
    }

    /// <summary>
    ///     Builds a raw (uncompressed) file around an arbitrary payload, with a consistent header.
    /// </summary>
    /// <param name="payload">The payload bytes.</param>
    /// <param name="rows">The row count to declare.</param>
    /// <returns>The file bytes.</returns>
    private static byte[] Craft(byte[] payload, int rows)
    {
        var header = new ModelHeader(0, 1, 2, 16, 1f, 64f, rows, payload.Length, 0, 0, 0, new byte[32]);
        return ModelBinaryFormat.WriteImage(header, payload, compress: false);
    }

    /// <summary>
    ///     Replaces the stored bytes of a file and repairs the stored length and checksum so the file is consistent except for the stored content.
    /// </summary>
    /// <param name="image">The file whose header is reused.</param>
    /// <param name="stored">The new stored bytes.</param>
    /// <returns>The forged file.</returns>
    private static byte[] Forge(byte[] image, byte[] stored)
    {
        var forged = new byte[ModelBinaryFormat.HeaderLength + stored.Length];
        image.AsSpan(0, ModelBinaryFormat.HeaderLength).CopyTo(forged);
        stored.CopyTo(forged, ModelBinaryFormat.HeaderLength);
        BinaryPrimitives.WriteUInt32LittleEndian(forged.AsSpan(32), (uint)stored.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(forged.AsSpan(36), ModelBinaryFormat.Fnv1a(stored));
        return forged;
    }

    /// <summary>
    ///     Compresses a run of zero bytes into a raw Deflate stream without holding the whole run in memory.
    /// </summary>
    /// <param name="count">The number of zero bytes.</param>
    /// <returns>The Deflate stream.</returns>
    private static byte[] DeflateZeros(long count)
    {
        using var output = new MemoryStream();
        using (var deflate = new DeflateStream(output, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            var chunk = new byte[1 << 20];
            for (long written = 0; written < count; written += chunk.Length)
            {
                deflate.Write(chunk, 0, (int)Math.Min(chunk.Length, count - written));
            }
        }

        return output.ToArray();
    }

    /// <summary>
    ///     Asserts that loading throws <see cref="InvalidDataException"/> whose message contains a text.
    /// </summary>
    /// <param name="image">The file bytes.</param>
    /// <param name="expected">The expected message fragment; empty to require only the exception type.</param>
    private static void AssertInvalid(byte[] image, string expected = "")
    {
        var exception = Assert.Throws<InvalidDataException>(() => SparsePerceptronModel.Load(image));
        Assert.Contains(expected, exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Produces one seeded mutation of a valid file.
    /// </summary>
    /// <param name="source">The valid file.</param>
    /// <param name="random">The deterministic generator.</param>
    /// <param name="attempt">The attempt number, which selects the mutation family.</param>
    /// <returns>The mutated file.</returns>
    private static byte[] Mutate(byte[] source, XorShift random, int attempt)
    {
        var image = (byte[])source.Clone();
        switch (attempt % 6)
        {
            case 0:
                // Flip one to four random bits anywhere.
                for (var i = 0; i <= (int)(random.Next() % 4); i++)
                {
                    image[random.Next() % (ulong)image.Length] ^= (byte)(1 << (int)(random.Next() % 8));
                }

                return image;
            case 1:
                // Truncate at a random length.
                return image[..(int)(random.Next() % (ulong)image.Length)];
            case 2:
                // Append random bytes.
                return [.. image, .. Enumerable.Range(0, 1 + (int)(random.Next() % 16)).Select(_ => (byte)random.Next())];
            case 3:
                // Overwrite a random header byte.
                image[random.Next() % (ulong)ModelBinaryFormat.HeaderLength] = (byte)random.Next();
                return image;
            case 4:
                // Change a stored byte and repair the checksum, so the mutation reaches the inflater and the payload decoder.
                if (image.Length > ModelBinaryFormat.HeaderLength)
                {
                    image[ModelBinaryFormat.HeaderLength + (int)(random.Next() % (ulong)(image.Length - ModelBinaryFormat.HeaderLength))] ^= (byte)(1 + (random.Next() % 255));
                    BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(36), ModelBinaryFormat.Fnv1a(image.AsSpan(ModelBinaryFormat.HeaderLength)));
                }

                return image;
            default:
                // Overwrite a 32-bit length field (row count, payload length or stored length) with a random value.
                BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(24 + (4 * (int)(random.Next() % 3))), (uint)random.Next());
                return image;
        }
    }

    /// <summary>
    ///     A small deterministic pseudo-random generator, independent of the runtime's <see cref="Random"/> implementation.
    /// </summary>
    private sealed class XorShift
    {
        /// <summary>The generator state; never zero.</summary>
        private ulong _state;

        /// <summary>
        ///     Initializes a new instance.
        /// </summary>
        /// <param name="seed">The nonzero seed.</param>
        internal XorShift(ulong seed)
        {
            _state = seed;
        }

        /// <summary>
        ///     Gets the next 64-bit value.
        /// </summary>
        /// <returns>The value.</returns>
        internal ulong Next()
        {
            _state ^= _state >> 12;
            _state ^= _state << 25;
            _state ^= _state >> 27;
            return _state * 0x2545F4914F6CDD1DUL;
        }
    }

    /// <summary>
    ///     A readable stream that never ends, counting how much it served.
    /// </summary>
    private sealed class EndlessStream : Stream
    {
        /// <summary>Gets the number of bytes served so far.</summary>
        internal long BytesServed { get; private set; }

        /// <inheritdoc/>
        public override bool CanRead => true;

        /// <inheritdoc/>
        public override bool CanSeek => false;

        /// <inheritdoc/>
        public override bool CanWrite => false;

        /// <inheritdoc/>
        public override long Length => throw new NotSupportedException();

        /// <inheritdoc/>
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        /// <inheritdoc/>
        public override int Read(byte[] buffer, int offset, int count)
        {
            BytesServed += count;
            return count;
        }

        /// <inheritdoc/>
        public override void Flush()
        {
        }

        /// <inheritdoc/>
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        /// <inheritdoc/>
        public override void SetLength(long value) => throw new NotSupportedException();

        /// <inheritdoc/>
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
