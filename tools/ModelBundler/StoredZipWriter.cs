using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace DemaConsulting.Speech.ModelBundler;

/// <summary>
///     Writes a deterministic zip archive of stored (uncompressed) entries: the same entries in the same order always
///     produce the same bytes, on every operating system and .NET version.
/// </summary>
/// <remarks>
///     <para>
///     <see cref="System.IO.Compression.ZipArchive"/> cannot be used to write bundles, because its output depends on
///     the platform (it stamps the host operating system into "version made by") and on the runtime's Deflate
///     implementation. This writer fixes every field instead:
///     </para>
///     <list type="bullet">
///         <item>compression method 0 (stored) with a CRC-32 per entry;</item>
///         <item>one fixed DOS timestamp, the bundle date at 00:00:00, for every entry;</item>
///         <item>fixed "version made by" (2.0, MS-DOS host) and "version needed" (2.0) values;</item>
///         <item>general-purpose flags 0, internal and external attributes 0;</item>
///         <item>no extra fields, no comments, no data descriptors and no Zip64 records.</item>
///     </list>
///     <para>
///     Entries are written in the order they are added; the caller fixes that order. Because Zip64 is never written,
///     every entry size and offset, and the archive itself, must stay below 4 GiB, and the archive must also stay
///     below the caller's size limit. Each entry's local header is written with placeholder CRC and sizes, and patched
///     once the data is copied, so the output stream must be seekable. Not thread-safe: one writer serves one
///     archive.
///     </para>
/// </remarks>
internal sealed class StoredZipWriter
{
    /// <summary>The exclusive bound of every 32-bit zip size and offset field (<c>0xFFFFFFFF</c> means Zip64).</summary>
    internal const long Zip32Limit = uint.MaxValue;

    /// <summary>The largest entry count a non-Zip64 end record can hold (<c>0xFFFF</c> means Zip64).</summary>
    internal const int MaximumEntryCount = ushort.MaxValue - 1;

    /// <summary>The fixed size of a local file header, without the name.</summary>
    internal const int LocalHeaderSize = 30;

    /// <summary>The fixed size of a central directory header, without the name.</summary>
    internal const int CentralHeaderSize = 46;

    /// <summary>The fixed size of the end of central directory record, without a comment.</summary>
    internal const int EndRecordSize = 22;

    /// <summary>The "version made by" value: specification 2.0 on an MS-DOS (FAT) host, as most zip tools write.</summary>
    private const ushort VersionMadeBy = 20;

    /// <summary>The "version needed to extract" value: specification 2.0.</summary>
    private const ushort VersionNeeded = 20;

    /// <summary>The local file header signature (<c>PK\x03\x04</c>).</summary>
    private const uint LocalHeaderSignature = 0x04034B50;

    /// <summary>The central directory header signature (<c>PK\x01\x02</c>).</summary>
    private const uint CentralHeaderSignature = 0x02014B50;

    /// <summary>The end of central directory signature (<c>PK\x05\x06</c>).</summary>
    private const uint EndRecordSignature = 0x06054B50;

    /// <summary>The offset of the CRC-32 field inside a local file header.</summary>
    private const int LocalHeaderCrcOffset = 14;

    /// <summary>The copy buffer size for stream entries.</summary>
    private const int BufferSize = 1024 * 1024;

    /// <summary>The archive being written; seekable.</summary>
    private readonly Stream _output;

    /// <summary>The fixed DOS date field of every entry.</summary>
    private readonly ushort _dosDate;

    /// <summary>The exclusive upper bound of the whole archive length, in bytes.</summary>
    private readonly long _archiveSizeLimit;

    /// <summary>The central directory records of the entries written so far, in order.</summary>
    private readonly List<CentralRecord> _entries = [];

    /// <summary>The entry names written so far, compared without regard to case.</summary>
    private readonly HashSet<string> _names = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The archive's start position in <see cref="_output"/>; offsets are relative to it.</summary>
    private readonly long _start;

    /// <summary>Whether <see cref="FinishAsync"/> has written the central directory.</summary>
    private bool _finished;

    /// <summary>
    ///     Initializes a new instance of the <see cref="StoredZipWriter"/> class.
    /// </summary>
    /// <param name="output">The seekable, writable stream that receives the archive from its current position.</param>
    /// <param name="date">The fixed entry date (1980 to 2107); every entry is stamped with this date at 00:00:00.</param>
    /// <param name="archiveSizeLimit">
    ///     The exclusive upper bound of the archive length in bytes, at most <see cref="Zip32Limit"/>.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="output"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="output"/> is not seekable and writable.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown when <paramref name="date"/> cannot be a DOS date or <paramref name="archiveSizeLimit"/> is out of range.
    /// </exception>
    public StoredZipWriter(Stream output, DateOnly date, long archiveSizeLimit)
    {
        ArgumentNullException.ThrowIfNull(output);
        if (!output.CanSeek || !output.CanWrite)
        {
            throw new ArgumentException("The zip output stream must be seekable and writable.", nameof(output));
        }

        if (date.Year < 1980 || date.Year > 2107)
        {
            throw new ArgumentOutOfRangeException(nameof(date), date, "A DOS date must be between 1980 and 2107.");
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(archiveSizeLimit);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(archiveSizeLimit, Zip32Limit);

        // DOS date: bits 9-15 years since 1980, bits 5-8 month, bits 0-4 day; the DOS time is always 0 (midnight)
        _output = output;
        _dosDate = (ushort)(((date.Year - 1980) << 9) | (date.Month << 5) | date.Day);
        _archiveSizeLimit = archiveSizeLimit;
        _start = output.Position;
    }

    /// <summary>
    ///     Computes the exact length of an archive this writer would produce for entries of the given name lengths and
    ///     data sizes, so a caller can refuse an oversized archive before writing anything.
    /// </summary>
    /// <param name="entries">The ASCII name length and data size of each entry.</param>
    /// <returns>The archive length in bytes.</returns>
    public static long ComputeArchiveSize(IEnumerable<(int NameLength, long DataSize)> entries) =>
        entries.Sum(entry => LocalHeaderSize + entry.NameLength + entry.DataSize + CentralHeaderSize + entry.NameLength)
        + EndRecordSize;

    /// <summary>Adds an entry whose content is in memory.</summary>
    /// <param name="name">The entry name (see <see cref="AddEntryAsync(string, Stream, CancellationToken)"/> for the rules).</param>
    /// <param name="data">The entry content.</param>
    /// <param name="cancellationToken">A token that aborts the copy.</param>
    /// <returns>The SHA-256 of the written content.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> breaks a naming rule.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the writer is finished or a zip limit would be exceeded.</exception>
    public async Task<byte[]> AddEntryAsync(string name, byte[] data, CancellationToken cancellationToken)
    {
        using var stream = new MemoryStream(data, writable: false);
        return await AddEntryAsync(name, stream, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    ///     Adds an entry by copying a stream to its end, computing the entry's CRC-32 and SHA-256 as it is copied.
    /// </summary>
    /// <remarks>
    ///     The returned SHA-256 is of the bytes actually written, so a caller can prove the archived content equals a
    ///     pin even if the source changed after it was first verified.
    /// </remarks>
    /// <param name="name">
    ///     The entry name: <c>/</c>-separated, printable ASCII only, no empty segment, no leading <c>/</c>, no <c>\</c>
    ///     or <c>:</c>, at most 65,535 characters, and unique in the archive without regard to case.
    /// </param>
    /// <param name="data">The readable stream of the entry content, read from its current position to its end.</param>
    /// <param name="cancellationToken">A token that aborts the copy.</param>
    /// <returns>The SHA-256 of the written content.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> breaks a naming rule.</exception>
    /// <exception cref="InvalidOperationException">
    ///     Thrown when the writer is finished, the entry count, an entry size or offset would reach 4 GiB, or the
    ///     archive would reach its size limit.
    /// </exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is canceled.</exception>
    public async Task<byte[]> AddEntryAsync(string name, Stream data, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(data);
        EnsureNotFinished();
        var nameBytes = ValidateName(name);
        if (_entries.Count >= MaximumEntryCount)
        {
            throw new InvalidOperationException($"A zip without Zip64 holds at most {MaximumEntryCount} entries.");
        }

        // Write the local header with placeholder CRC and sizes; they are patched once the data is known
        var headerOffset = _output.Position - _start;
        EnsureWithinLimits(headerOffset + LocalHeaderSize + nameBytes.Length);
        await _output.WriteAsync(CreateLocalHeader(nameBytes, crc: 0, size: 0), cancellationToken).ConfigureAwait(false);

        // Copy the data, hashing it for the CRC-32 the zip needs and the SHA-256 the caller verifies
        var crc = Crc32.Initial;
        long size = 0;
        using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[BufferSize];
        int read;
        while ((read = await data.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
        {
            size += read;
            EnsureWithinLimits(headerOffset + LocalHeaderSize + nameBytes.Length + size);
            crc = Crc32.Append(crc, buffer.AsSpan(0, read));
            sha.AppendData(buffer, 0, read);
            await _output.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
        }

        // Patch the CRC and both sizes into the local header, then return to the end of the data
        crc = Crc32.Finish(crc);
        var end = _output.Position;
        var patch = new byte[12];
        BinaryPrimitives.WriteUInt32LittleEndian(patch.AsSpan(0), crc);
        BinaryPrimitives.WriteUInt32LittleEndian(patch.AsSpan(4), (uint)size);
        BinaryPrimitives.WriteUInt32LittleEndian(patch.AsSpan(8), (uint)size);
        _output.Position = _start + headerOffset + LocalHeaderCrcOffset;
        await _output.WriteAsync(patch, cancellationToken).ConfigureAwait(false);
        _output.Position = end;

        _entries.Add(new CentralRecord(nameBytes, crc, size, headerOffset));
        return sha.GetHashAndReset();
    }

    /// <summary>
    ///     Writes the central directory and the end record, completing the archive. No entry may be added afterwards.
    /// </summary>
    /// <param name="cancellationToken">A token that aborts the write.</param>
    /// <returns>A task that completes when the archive is complete (the stream is not flushed or closed).</returns>
    /// <exception cref="InvalidOperationException">Thrown when the writer is already finished or the archive would reach its size limit.</exception>
    public async Task FinishAsync(CancellationToken cancellationToken)
    {
        EnsureNotFinished();

        // Central directory: one record per entry, in the order the entries were written
        var directoryOffset = _output.Position - _start;
        var directorySize = _entries.Sum(entry => (long)CentralHeaderSize + entry.Name.Length);
        EnsureWithinLimits(directoryOffset + directorySize + EndRecordSize);
        foreach (var entry in _entries)
        {
            await _output.WriteAsync(CreateCentralHeader(entry), cancellationToken).ConfigureAwait(false);
        }

        // End of central directory record (single disk, no comment)
        var end = new byte[EndRecordSize];
        BinaryPrimitives.WriteUInt32LittleEndian(end.AsSpan(0), EndRecordSignature);
        BinaryPrimitives.WriteUInt16LittleEndian(end.AsSpan(8), (ushort)_entries.Count);
        BinaryPrimitives.WriteUInt16LittleEndian(end.AsSpan(10), (ushort)_entries.Count);
        BinaryPrimitives.WriteUInt32LittleEndian(end.AsSpan(12), (uint)directorySize);
        BinaryPrimitives.WriteUInt32LittleEndian(end.AsSpan(16), (uint)directoryOffset);
        await _output.WriteAsync(end, cancellationToken).ConfigureAwait(false);
        _finished = true;
    }

    /// <summary>Refuses any change once the central directory is written.</summary>
    /// <exception cref="InvalidOperationException">Thrown when <see cref="FinishAsync"/> has completed.</exception>
    private void EnsureNotFinished()
    {
        if (_finished)
        {
            throw new InvalidOperationException("The zip is already finished.");
        }
    }

    /// <summary>Validates an entry name and returns its ASCII bytes.</summary>
    /// <param name="name">The entry name.</param>
    /// <returns>The name's bytes.</returns>
    /// <exception cref="ArgumentException">Thrown when the name breaks a rule or repeats an earlier name.</exception>
    private byte[] ValidateName(string name)
    {
        // Printable ASCII keeps the name independent of any code page and of the UTF-8 flag
        if (name.Length == 0
            || name.Length > ushort.MaxValue
            || name[0] == '/'
            || name.Any(character => character < 0x20 || character > 0x7E || character is '\\' or ':')
            || name.Split('/').Any(segment => segment.Length == 0))
        {
            throw new ArgumentException($"'{name}' is not a valid stored zip entry name.", nameof(name));
        }

        if (!_names.Add(name))
        {
            throw new ArgumentException($"The zip already holds an entry named '{name}' (compared without regard to case).", nameof(name));
        }

        return Encoding.ASCII.GetBytes(name);
    }

    /// <summary>Refuses an archive position that would need Zip64 or reach the size limit.</summary>
    /// <param name="length">The archive length the next write would reach.</param>
    /// <exception cref="InvalidOperationException">Thrown when the length reaches a limit.</exception>
    private void EnsureWithinLimits(long length)
    {
        if (length >= _archiveSizeLimit)
        {
            throw new InvalidOperationException(
                $"The zip would reach {length:N0} bytes, which is not below its limit of {_archiveSizeLimit:N0} bytes.");
        }
    }

    /// <summary>Creates a local file header.</summary>
    /// <param name="name">The entry name bytes.</param>
    /// <param name="crc">The CRC-32 of the data.</param>
    /// <param name="size">The data size (stored, so compressed and uncompressed sizes are equal).</param>
    /// <returns>The header bytes, followed by the name.</returns>
    private byte[] CreateLocalHeader(byte[] name, uint crc, long size)
    {
        var header = new byte[LocalHeaderSize + name.Length];
        var span = header.AsSpan();
        BinaryPrimitives.WriteUInt32LittleEndian(span[0..], LocalHeaderSignature);
        BinaryPrimitives.WriteUInt16LittleEndian(span[4..], VersionNeeded);
        BinaryPrimitives.WriteUInt16LittleEndian(span[6..], 0);             // general-purpose flags
        BinaryPrimitives.WriteUInt16LittleEndian(span[8..], 0);             // method 0: stored
        BinaryPrimitives.WriteUInt16LittleEndian(span[10..], 0);            // DOS time: 00:00:00
        BinaryPrimitives.WriteUInt16LittleEndian(span[12..], _dosDate);
        BinaryPrimitives.WriteUInt32LittleEndian(span[14..], crc);
        BinaryPrimitives.WriteUInt32LittleEndian(span[18..], (uint)size);   // compressed size
        BinaryPrimitives.WriteUInt32LittleEndian(span[22..], (uint)size);   // uncompressed size
        BinaryPrimitives.WriteUInt16LittleEndian(span[26..], (ushort)name.Length);
        BinaryPrimitives.WriteUInt16LittleEndian(span[28..], 0);            // extra field length
        name.CopyTo(span[LocalHeaderSize..]);
        return header;
    }

    /// <summary>Creates a central directory header for a written entry.</summary>
    /// <param name="entry">The written entry.</param>
    /// <returns>The header bytes, followed by the name.</returns>
    private byte[] CreateCentralHeader(CentralRecord entry)
    {
        var header = new byte[CentralHeaderSize + entry.Name.Length];
        var span = header.AsSpan();
        BinaryPrimitives.WriteUInt32LittleEndian(span[0..], CentralHeaderSignature);
        BinaryPrimitives.WriteUInt16LittleEndian(span[4..], VersionMadeBy);
        BinaryPrimitives.WriteUInt16LittleEndian(span[6..], VersionNeeded);
        BinaryPrimitives.WriteUInt16LittleEndian(span[8..], 0);             // general-purpose flags
        BinaryPrimitives.WriteUInt16LittleEndian(span[10..], 0);            // method 0: stored
        BinaryPrimitives.WriteUInt16LittleEndian(span[12..], 0);            // DOS time: 00:00:00
        BinaryPrimitives.WriteUInt16LittleEndian(span[14..], _dosDate);
        BinaryPrimitives.WriteUInt32LittleEndian(span[16..], entry.Crc);
        BinaryPrimitives.WriteUInt32LittleEndian(span[20..], (uint)entry.Size);
        BinaryPrimitives.WriteUInt32LittleEndian(span[24..], (uint)entry.Size);
        BinaryPrimitives.WriteUInt16LittleEndian(span[28..], (ushort)entry.Name.Length);
        BinaryPrimitives.WriteUInt16LittleEndian(span[30..], 0);            // extra field length
        BinaryPrimitives.WriteUInt16LittleEndian(span[32..], 0);            // comment length
        BinaryPrimitives.WriteUInt16LittleEndian(span[34..], 0);            // disk number start
        BinaryPrimitives.WriteUInt16LittleEndian(span[36..], 0);            // internal attributes
        BinaryPrimitives.WriteUInt32LittleEndian(span[38..], 0);            // external attributes
        BinaryPrimitives.WriteUInt32LittleEndian(span[42..], (uint)entry.LocalHeaderOffset);
        entry.Name.CopyTo(span[CentralHeaderSize..]);
        return header;
    }

    /// <summary>What the central directory records about one written entry.</summary>
    /// <param name="Name">The entry name bytes.</param>
    /// <param name="Crc">The CRC-32 of the data.</param>
    /// <param name="Size">The data size.</param>
    /// <param name="LocalHeaderOffset">The offset of the entry's local header from the archive start.</param>
    private sealed record CentralRecord(byte[] Name, uint Crc, long Size, long LocalHeaderOffset);
}
