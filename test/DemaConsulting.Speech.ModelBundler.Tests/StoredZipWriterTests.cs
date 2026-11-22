using System.Buffers.Binary;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace DemaConsulting.Speech.ModelBundler.Tests;

/// <summary>
///     Unit tests for <see cref="StoredZipWriter"/> and <see cref="Crc32"/>: deterministic bytes, a golden archive
///     that proves cross-platform identity, readability by the BCL, fixed header fields and the size limits.
/// </summary>
public sealed class StoredZipWriterTests
{
    /// <summary>
    ///     The SHA-256 of the archive <see cref="WriteFixtureAsync"/> produces. CI runs this test on Windows, Linux and
    ///     macOS, so a match everywhere proves the writer's output does not depend on the platform.
    /// </summary>
    private const string GoldenSha256 = "2698222aa9e7ee6f77f68726acdc8cdeab5a4d6db84dba76b2c669fdc8c19b01";

    /// <summary>The fixed entry date of the fixture archive.</summary>
    private static readonly DateOnly FixtureDate = new(2026, 11, 22);

    /// <summary>The fixture entries, in archive order.</summary>
    private static readonly (string Name, byte[] Data)[] FixtureEntries =
    [
        ("README.txt", Encoding.ASCII.GetBytes("Fixture readme.\n")),
        ("LICENSE", Encoding.ASCII.GetBytes("Fixture license.\n")),
        ("data/values.bin", [.. Enumerable.Range(0, 256).Select(index => (byte)index)]),
        ("empty.txt", []),
    ];

    /// <summary>Validates that writing the same entries twice produces identical bytes.</summary>
    [Fact]
    public async Task StoredZipWriter_FinishAsync_SameEntries_ProducesIdenticalBytes()
    {
        // Arrange / Act: write the fixture twice
        var first = await WriteFixtureAsync();
        var second = await WriteFixtureAsync();

        // Assert: the archives are byte-identical
        Assert.Equal(first, second);
    }

    /// <summary>Validates that the fixture archive has the pinned SHA-256 (cross-platform byte identity).</summary>
    [Fact]
    public async Task StoredZipWriter_FinishAsync_FixtureEntries_MatchesGoldenSha256()
    {
        // Arrange / Act: write the fixture
        var archive = await WriteFixtureAsync();

        // Assert: its digest equals the pinned golden digest
        Assert.Equal(GoldenSha256, Convert.ToHexStringLower(SHA256.HashData(archive)));
    }

    /// <summary>Validates that the BCL zip reader reads every entry back, in order, stored, with the fixed date.</summary>
    [Fact]
    public async Task StoredZipWriter_FinishAsync_FixtureEntries_IsReadableByZipArchive()
    {
        // Arrange: write the fixture
        var archive = await WriteFixtureAsync();

        // Act: open it with System.IO.Compression
        using var zip = new ZipArchive(new MemoryStream(archive), ZipArchiveMode.Read);

        // Assert: names, order, content, stored size and timestamp all round-trip
        Assert.Equal(FixtureEntries.Select(entry => entry.Name), zip.Entries.Select(entry => entry.FullName));
        foreach (var (name, data) in FixtureEntries)
        {
            var entry = zip.GetEntry(name)!;
            using var stream = await entry.OpenAsync(TestContext.Current.CancellationToken);
            using var copy = new MemoryStream();
            await stream.CopyToAsync(copy, TestContext.Current.CancellationToken);
            Assert.Equal(data, copy.ToArray());
            Assert.Equal(entry.Length, entry.CompressedLength);
            Assert.Equal(new DateTime(2026, 11, 22, 0, 0, 0, DateTimeKind.Unspecified), entry.LastWriteTime.DateTime);
        }
    }

    /// <summary>
    ///     Validates the fixed header fields: version 2.0, flags 0, method 0, DOS time 0, no extra fields, external
    ///     attributes 0 and no archive comment.
    /// </summary>
    [Fact]
    public async Task StoredZipWriter_FinishAsync_FixtureEntries_WritesFixedHeaderFields()
    {
        // Arrange / Act: write the fixture and locate the first local header and the first central header
        var archive = await WriteFixtureAsync();
        var span = archive.AsSpan();
        var end = archive.Length - StoredZipWriter.EndRecordSize;
        var centralOffset = (int)BinaryPrimitives.ReadUInt32LittleEndian(span[(end + 16)..]);

        // Assert: local header fields
        Assert.Equal(0x04034B50u, BinaryPrimitives.ReadUInt32LittleEndian(span));
        Assert.Equal(20, BinaryPrimitives.ReadUInt16LittleEndian(span[4..]));
        Assert.Equal(0, BinaryPrimitives.ReadUInt16LittleEndian(span[6..]));
        Assert.Equal(0, BinaryPrimitives.ReadUInt16LittleEndian(span[8..]));
        Assert.Equal(0, BinaryPrimitives.ReadUInt16LittleEndian(span[10..]));
        Assert.Equal(0, BinaryPrimitives.ReadUInt16LittleEndian(span[28..]));

        // Assert: central header fields
        var central = span[centralOffset..];
        Assert.Equal(0x02014B50u, BinaryPrimitives.ReadUInt32LittleEndian(central));
        Assert.Equal(20, BinaryPrimitives.ReadUInt16LittleEndian(central[4..]));
        Assert.Equal(20, BinaryPrimitives.ReadUInt16LittleEndian(central[6..]));
        Assert.Equal(0, BinaryPrimitives.ReadUInt16LittleEndian(central[30..]));
        Assert.Equal(0, BinaryPrimitives.ReadUInt16LittleEndian(central[32..]));
        Assert.Equal(0u, BinaryPrimitives.ReadUInt32LittleEndian(central[38..]));

        // Assert: end record with every entry and no comment
        Assert.Equal(0x06054B50u, BinaryPrimitives.ReadUInt32LittleEndian(span[end..]));
        Assert.Equal(FixtureEntries.Length, BinaryPrimitives.ReadUInt16LittleEndian(span[(end + 10)..]));
        Assert.Equal(0, BinaryPrimitives.ReadUInt16LittleEndian(span[(end + 20)..]));
    }

    /// <summary>Validates that the computed archive size equals the written length.</summary>
    [Fact]
    public async Task StoredZipWriter_ComputeArchiveSize_FixtureEntries_EqualsWrittenLength()
    {
        // Arrange / Act: write the fixture and compute its size from the entry sizes
        var archive = await WriteFixtureAsync();
        var computed = StoredZipWriter.ComputeArchiveSize(FixtureEntries.Select(entry => (entry.Name.Length, (long)entry.Data.Length)));

        // Assert: both agree
        Assert.Equal(archive.Length, computed);
    }

    /// <summary>Validates that each entry returns the SHA-256 of the bytes it wrote.</summary>
    [Fact]
    public async Task StoredZipWriter_AddEntryAsync_Data_ReturnsSha256OfData()
    {
        // Arrange: a writer over memory
        using var output = new MemoryStream();
        var writer = new StoredZipWriter(output, FixtureDate, StoredZipWriter.Zip32Limit);
        var data = FixtureEntries[2].Data;

        // Act: add one entry
        var sha = await writer.AddEntryAsync("data.bin", data, TestContext.Current.CancellationToken);

        // Assert: the returned digest is the data's digest
        Assert.Equal(SHA256.HashData(data), sha);
    }

    /// <summary>Validates that an archive reaching its size limit is refused.</summary>
    [Fact]
    public async Task StoredZipWriter_AddEntryAsync_ArchiveReachesLimit_ThrowsInvalidOperationException()
    {
        // Arrange: a writer limited to 100 bytes
        using var output = new MemoryStream();
        var writer = new StoredZipWriter(output, FixtureDate, archiveSizeLimit: 100);

        // Act / Assert: a 200-byte entry is refused
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => writer.AddEntryAsync("big.bin", new byte[200], TestContext.Current.CancellationToken));
    }

    /// <summary>Validates that an unsafe, non-ASCII or empty-segment entry name is refused.</summary>
    /// <param name="name">The invalid name.</param>
    [Theory]
    [InlineData("")]
    [InlineData("/absolute")]
    [InlineData("a\\b")]
    [InlineData("a//b")]
    [InlineData("C:x")]
    [InlineData("caf\u00e9")]
    [InlineData("trailing/")]
    public async Task StoredZipWriter_AddEntryAsync_InvalidName_ThrowsArgumentException(string name)
    {
        // Arrange: a writer over memory
        using var output = new MemoryStream();
        var writer = new StoredZipWriter(output, FixtureDate, StoredZipWriter.Zip32Limit);

        // Act / Assert: the name is refused
        await Assert.ThrowsAsync<ArgumentException>(() => writer.AddEntryAsync(name, [1], TestContext.Current.CancellationToken));
    }

    /// <summary>Validates that two names differing only by case are refused.</summary>
    [Fact]
    public async Task StoredZipWriter_AddEntryAsync_DuplicateNameIgnoringCase_ThrowsArgumentException()
    {
        // Arrange: a writer holding README.txt
        using var output = new MemoryStream();
        var writer = new StoredZipWriter(output, FixtureDate, StoredZipWriter.Zip32Limit);
        await writer.AddEntryAsync("README.txt", [1], TestContext.Current.CancellationToken);

        // Act / Assert: README.TXT is refused
        await Assert.ThrowsAsync<ArgumentException>(() => writer.AddEntryAsync("README.TXT", [2], TestContext.Current.CancellationToken));
    }

    /// <summary>Validates that no entry can be added once the archive is finished.</summary>
    [Fact]
    public async Task StoredZipWriter_AddEntryAsync_AfterFinish_ThrowsInvalidOperationException()
    {
        // Arrange: a finished writer
        using var output = new MemoryStream();
        var writer = new StoredZipWriter(output, FixtureDate, StoredZipWriter.Zip32Limit);
        await writer.FinishAsync(TestContext.Current.CancellationToken);

        // Act / Assert: adding is refused
        await Assert.ThrowsAsync<InvalidOperationException>(() => writer.AddEntryAsync("late.txt", [1], TestContext.Current.CancellationToken));
    }

    /// <summary>Validates that a limit that would need Zip64 is refused.</summary>
    [Fact]
    public void StoredZipWriter_Constructor_LimitAbove4GiB_ThrowsArgumentOutOfRangeException()
    {
        // Arrange: a seekable output
        using var output = new MemoryStream();

        // Act / Assert: a limit above the 32-bit range is refused
        Assert.Throws<ArgumentOutOfRangeException>(() => new StoredZipWriter(output, FixtureDate, StoredZipWriter.Zip32Limit + 1));
    }

    /// <summary>Validates the CRC-32 implementation against the standard check value.</summary>
    [Fact]
    public void Crc32_Compute_StandardCheckInput_ReturnsCheckValue()
    {
        // Arrange: the standard check input
        var input = Encoding.ASCII.GetBytes("123456789");

        // Act: compute the checksum
        var crc = Crc32.Compute(input);

        // Assert: it is the published CRC-32 check value
        Assert.Equal(0xCBF43926u, crc);
    }

    /// <summary>Writes the fixture archive to memory.</summary>
    /// <returns>The archive bytes.</returns>
    private static async Task<byte[]> WriteFixtureAsync()
    {
        using var output = new MemoryStream();
        var writer = new StoredZipWriter(output, FixtureDate, StoredZipWriter.Zip32Limit);
        foreach (var (name, data) in FixtureEntries)
        {
            await writer.AddEntryAsync(name, data, TestContext.Current.CancellationToken);
        }

        await writer.FinishAsync(TestContext.Current.CancellationToken);
        return output.ToArray();
    }
}
