using System.Buffers.Binary;
using System.IO.Compression;

namespace DemaConsulting.Speech.Text.En.HeteronymSubsystem;

/// <summary>
///     The validated header of a heteronym model file (<c>HETW</c> version 1).
/// </summary>
/// <param name="Flags">The flag bits; only <see cref="ModelBinaryFormat.FlagDeflate"/> is defined.</param>
/// <param name="FeatureSetVersion">The feature-set version the model was trained with.</param>
/// <param name="InventoryVersion">The inventory revision the model was trained for.</param>
/// <param name="HashBits">The number of hash bits, 16 to 24.</param>
/// <param name="Temperature">The calibration temperature; finite and positive.</param>
/// <param name="Scale">The fixed-point scale of the integer weights; finite and positive.</param>
/// <param name="RowCount">The number of stored (non-zero) weights.</param>
/// <param name="PayloadLength">The exact number of payload bytes after inflation.</param>
/// <param name="StoredLength">The number of stored bytes after the header.</param>
/// <param name="Checksum">The FNV-1a checksum of the stored bytes.</param>
/// <param name="TrainerVersion">The informational version of the trainer that wrote the file; 0 when unknown.</param>
/// <param name="DataManifestSha256">The informational SHA-256 of the training data manifest; 32 bytes, all zero when unknown.</param>
internal sealed record ModelHeader(
    int Flags,
    int FeatureSetVersion,
    int InventoryVersion,
    int HashBits,
    float Temperature,
    float Scale,
    int RowCount,
    int PayloadLength,
    int StoredLength,
    uint Checksum,
    uint TrainerVersion,
    byte[] DataManifestSha256);

/// <summary>
///     The byte-level rules of the heteronym model file: the header, the checksum, the varints
///     and the strict Deflate region, used by both the loader and the writer so the two cannot
///     disagree.
/// </summary>
/// <remarks>
///     <para>
///     <b>Layout</b> (little endian, 80-byte header): magic <c>HETW</c> (0), format version 1 (4), flags (6;
///     bit 0 payload is raw Deflate), feature-set version (8), inventory version (10), hash bits (12),
///     three reserved zero bytes (13), temperature float32 (16), scale float32 (20), row count (24),
///     payload length (28), stored length (32), FNV-1a checksum of the stored bytes (36), trainer
///     version (40), a reserved zero word (44) and the 32-byte data manifest hash (48). The payload
///     is the bucket gaps as varints followed by the weights as int16.
///     </para>
///     <para>
///     <b>Safety.</b> Every size that drives an allocation is checked against a fixed cap before
///     the allocation (<see cref="MaxPayloadLength"/>, the hash bits, a ratio to the stored length),
///     the checksum of the stored bytes is verified before anything is inflated, and the inflater
///     is strict: it must produce exactly the declared number of bytes, must reach its own end of
///     stream inside the stored region and must use the whole region. Every failure is an
///     <see cref="InvalidDataException"/> with a message that names the defect and never the data.
///     The type is static and thread-safe.
///     </para>
/// </remarks>
internal static class ModelBinaryFormat
{
    /// <summary>The length of the fixed header in bytes.</summary>
    internal const int HeaderLength = 80;

    /// <summary>The only format version this build reads and writes.</summary>
    internal const int FormatVersion = 1;

    /// <summary>The flag bit that marks a raw Deflate payload.</summary>
    internal const int FlagDeflate = 1;

    /// <summary>The fewest hash bits a model may use.</summary>
    internal const int MinHashBits = 16;

    /// <summary>The most hash bits a model may use.</summary>
    internal const int MaxHashBits = 24;

    /// <summary>The largest inflated payload, in bytes (32 MiB).</summary>
    internal const int MaxPayloadLength = 32 * 1024 * 1024;

    /// <summary>The largest accepted file: the largest payload plus room for Deflate's block overhead and the header.</summary>
    internal const int MaxFileLength = MaxPayloadLength + (1024 * 1024) + HeaderLength;

    /// <summary>The largest temperature or scale value.</summary>
    internal const float MaxFloatField = 1e6f;

    /// <summary>The size of the data-manifest hash field.</summary>
    internal const int ManifestLength = 32;

    /// <summary>The most bytes of a varint that holds a 32-bit value.</summary>
    private const int MaxVarintBytes = 5;

    /// <summary>The largest ratio of inflated to stored bytes Deflate can reach (1032 to 1), used to reject a hostile length early.</summary>
    private const long MaxDeflateRatio = 1032;

    /// <summary>The magic bytes at the start of every model file.</summary>
    private static ReadOnlySpan<byte> Magic => "HETW"u8;

    /// <summary>
    ///     Reads a whole stream into memory, refusing more than <see cref="MaxFileLength"/> bytes.
    /// </summary>
    /// <param name="stream">The stream, read from its current position to its end. Must not be null.</param>
    /// <returns>The bytes.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is null.</exception>
    /// <exception cref="InvalidDataException">The stream holds more than the largest accepted file.</exception>
    internal static byte[] ReadAll(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        // Read at most the cap plus one byte so an endless stream is stopped, and a file over the cap is reported.
        using var buffer = new MemoryStream();
        var chunk = new byte[81_920];
        while (true)
        {
            var read = stream.Read(chunk, 0, chunk.Length);
            if (read == 0)
            {
                break;
            }

            if (buffer.Length + read > MaxFileLength)
            {
                throw new InvalidDataException("The heteronym model file is larger than the maximum of " + MaxFileLength + " bytes.");
            }

            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }

    /// <summary>
    ///     Validates the header and the declared lengths of a model file, without touching the payload.
    /// </summary>
    /// <param name="image">The whole file. Must not be null.</param>
    /// <returns>The validated header.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="image"/> is null.</exception>
    /// <exception cref="InvalidDataException">
    ///     The magic, format version, flags, feature-set version (below 1), inventory version (below 1), hash
    ///     bits, reserved bytes, temperature, scale, row count, payload length or stored length is invalid, or
    ///     the file is truncated or has trailing bytes. The checksum is verified by <see cref="ReadPayload"/>.
    /// </exception>
    /// <remarks>
    ///     Validates structure only: a feature-set version other than the one this build extracts is returned
    ///     in the header, not rejected, so the caller can tell an incompatible model from a malformed one.
    /// </remarks>
    internal static ModelHeader ParseHeader(byte[] image)
    {
        ArgumentNullException.ThrowIfNull(image);

        // The magic first, so a foreign file is reported as such even when it is short.
        if (image.Length < Magic.Length || !image.AsSpan(0, Magic.Length).SequenceEqual(Magic))
        {
            throw new InvalidDataException("Not a heteronym model file: the magic 'HETW' is missing.");
        }

        if (image.Length < HeaderLength)
        {
            throw new InvalidDataException($"The heteronym model file is truncated: the header needs {HeaderLength} bytes but the file has {image.Length}.");
        }

        var data = image.AsSpan();
        var version = BinaryPrimitives.ReadUInt16LittleEndian(data[4..]);
        if (version != FormatVersion)
        {
            throw new InvalidDataException($"Unsupported heteronym model format version {version}; this build reads version {FormatVersion}.");
        }

        var flags = BinaryPrimitives.ReadUInt16LittleEndian(data[6..]);
        if ((flags & ~FlagDeflate) != 0)
        {
            throw new InvalidDataException($"Invalid heteronym model file: unknown flag bits 0x{flags:X4}.");
        }

        // The feature-set and inventory versions are checked for structure only (a version starts at 1); whether
        // they fit this build is a compatibility question for the classifier, so a well-formed model of another
        // feature set or a newer inventory still loads and is reported as incompatible rather than invalid.
        var featureSet = BinaryPrimitives.ReadUInt16LittleEndian(data[8..]);
        if (featureSet < 1)
        {
            throw new InvalidDataException("Invalid heteronym model file: the feature-set version must be at least 1.");
        }

        var inventoryVersion = BinaryPrimitives.ReadUInt16LittleEndian(data[10..]);
        if (inventoryVersion < 1)
        {
            throw new InvalidDataException("Invalid heteronym model file: the inventory version must be at least 1.");
        }

        int hashBits = data[12];
        if (hashBits is < MinHashBits or > MaxHashBits)
        {
            throw new InvalidDataException($"Invalid heteronym model file: the hash bits {hashBits} are outside {MinHashBits} to {MaxHashBits}.");
        }

        if (data[13] != 0 || data[14] != 0 || data[15] != 0 || BinaryPrimitives.ReadUInt32LittleEndian(data[44..]) != 0)
        {
            throw new InvalidDataException("Invalid heteronym model file: a reserved field is not zero.");
        }

        var temperature = BinaryPrimitives.ReadSingleLittleEndian(data[16..]);
        if (!IsValidFloatField(temperature))
        {
            throw new InvalidDataException("Invalid heteronym model file: the temperature must be finite and greater than zero.");
        }

        var scale = BinaryPrimitives.ReadSingleLittleEndian(data[20..]);
        if (!IsValidFloatField(scale))
        {
            throw new InvalidDataException("Invalid heteronym model file: the scale must be finite and greater than zero.");
        }

        // The lengths: each must fit its cap before it is used for anything.
        var rowCountValue = BinaryPrimitives.ReadUInt32LittleEndian(data[24..]);
        var payloadLengthValue = BinaryPrimitives.ReadUInt32LittleEndian(data[28..]);
        var storedLengthValue = BinaryPrimitives.ReadUInt32LittleEndian(data[32..]);
        var maxRows = Math.Min(1L << hashBits, MaxPayloadLength / 3);
        if (rowCountValue > maxRows)
        {
            throw new InvalidDataException("Invalid heteronym model file: the row count is larger than the hash table or the payload cap allows.");
        }

        if (payloadLengthValue > MaxPayloadLength)
        {
            throw new InvalidDataException($"Invalid heteronym model file: the payload length {payloadLengthValue} is larger than the maximum {MaxPayloadLength}.");
        }

        // Every row needs one to five gap bytes and two weight bytes.
        if (payloadLengthValue < rowCountValue * 3L || payloadLengthValue > rowCountValue * 7L)
        {
            throw new InvalidDataException("Invalid heteronym model file: the payload length does not fit the row count.");
        }

        var available = image.Length - HeaderLength;
        if (storedLengthValue > (uint)available)
        {
            throw new InvalidDataException($"The heteronym model file is truncated: {storedLengthValue} payload bytes are declared but {available} are present.");
        }

        if (storedLengthValue < (uint)available)
        {
            throw new InvalidDataException($"The heteronym model file has {available - (long)storedLengthValue} unexpected trailing bytes.");
        }

        var deflated = (flags & FlagDeflate) != 0;
        if (!deflated && storedLengthValue != payloadLengthValue)
        {
            throw new InvalidDataException("Invalid heteronym model file: an uncompressed payload must have equal stored and payload lengths.");
        }

        if (deflated && payloadLengthValue > (storedLengthValue * MaxDeflateRatio) + 16)
        {
            throw new InvalidDataException("Invalid heteronym model file: the payload length is larger than the stored bytes can inflate to.");
        }

        return new ModelHeader(
            flags,
            featureSet,
            inventoryVersion,
            hashBits,
            temperature,
            scale,
            (int)rowCountValue,
            (int)payloadLengthValue,
            (int)storedLengthValue,
            BinaryPrimitives.ReadUInt32LittleEndian(data[36..]),
            BinaryPrimitives.ReadUInt32LittleEndian(data[40..]),
            data.Slice(48, ManifestLength).ToArray());
    }

    /// <summary>
    ///     Verifies the checksum of the stored bytes and returns the inflated payload.
    /// </summary>
    /// <param name="image">The whole file.</param>
    /// <param name="header">The header that <see cref="ParseHeader"/> validated for this image.</param>
    /// <returns>The payload, exactly <see cref="ModelHeader.PayloadLength"/> bytes.</returns>
    /// <exception cref="InvalidDataException">The checksum differs, or the Deflate stream is invalid, cut off, shorter or longer than declared, or followed by bytes.</exception>
    /// <remarks>
    ///     The checksum covers the stored bytes, so any change to the compressed region is detected
    ///     before a single byte is inflated. The strict inflater still rejects a file whose checksum was
    ///     recomputed after tampering.
    /// </remarks>
    internal static byte[] ReadPayload(byte[] image, ModelHeader header)
    {
        // The checksum is verified first: nothing is inflated from bytes that do not match it.
        if (Fnv1a(image.AsSpan(HeaderLength, header.StoredLength)) != header.Checksum)
        {
            throw new InvalidDataException("The heteronym model file is corrupt: the checksum does not match.");
        }

        if ((header.Flags & FlagDeflate) == 0)
        {
            return image.AsSpan(HeaderLength, header.PayloadLength).ToArray();
        }

        return Inflate(image, HeaderLength, header.StoredLength, header.PayloadLength);
    }

    /// <summary>
    ///     Computes the FNV-1a 32-bit hash of bytes.
    /// </summary>
    /// <param name="data">The bytes.</param>
    /// <returns>The hash.</returns>
    internal static uint Fnv1a(ReadOnlySpan<byte> data)
    {
        var hash = FeatureHashing.FnvOffsetBasis;
        foreach (var value in data)
        {
            hash = (hash ^ value) * FeatureHashing.FnvPrime;
        }

        return hash;
    }

    /// <summary>
    ///     Compresses bytes into a raw Deflate stream at the smallest size.
    /// </summary>
    /// <param name="data">The bytes.</param>
    /// <returns>The compressed bytes; never empty (an empty input becomes one empty final block).</returns>
    internal static byte[] Compress(byte[] data)
    {
        using var output = new MemoryStream();
        using (var deflate = new DeflateStream(output, CompressionLevel.SmallestSize, true))
        {
            deflate.Write(data, 0, data.Length);
        }

        // Some runtimes write nothing for an empty input; a stored file must still hold a complete stream.
        return output.Length == 0 ? [0x03, 0x00] : output.ToArray();
    }

    /// <summary>
    ///     Appends an unsigned LEB128 varint.
    /// </summary>
    /// <param name="output">The stream to append to.</param>
    /// <param name="value">The value.</param>
    internal static void WriteVarint(Stream output, uint value)
    {
        while (value >= 0x80)
        {
            output.WriteByte((byte)(value | 0x80));
            value >>= 7;
        }

        output.WriteByte((byte)value);
    }

    /// <summary>
    ///     Reads an unsigned LEB128 varint that must fit a positive 32-bit signed integer.
    /// </summary>
    /// <param name="data">The buffer.</param>
    /// <param name="position">The position of the first byte; advanced past the varint.</param>
    /// <param name="end">The exclusive end of the readable region.</param>
    /// <returns>The value, never negative.</returns>
    /// <exception cref="InvalidDataException">The varint is cut off, longer than five bytes, carries bits above 32 or exceeds <see cref="int.MaxValue"/>.</exception>
    internal static int ReadVarint(byte[] data, ref int position, int end)
    {
        uint value = 0;
        for (var index = 0; index < MaxVarintBytes; index++)
        {
            if (position >= end)
            {
                throw new InvalidDataException("The model payload is truncated inside a varint.");
            }

            var next = data[position++];

            // The fifth byte may only carry the top four bits of a 32-bit value.
            if (index == MaxVarintBytes - 1 && next > 0x0F)
            {
                throw new InvalidDataException("The model payload has an overlong varint.");
            }

            value |= (uint)(next & 0x7F) << (7 * index);
            if ((next & 0x80) == 0)
            {
                if (value > int.MaxValue)
                {
                    throw new InvalidDataException("The model payload has a varint larger than 2147483647.");
                }

                return (int)value;
            }
        }

        throw new InvalidDataException("The model payload has an overlong varint.");
    }

    /// <summary>
    ///     Writes a model file around a payload.
    /// </summary>
    /// <param name="header">The header fields; the lengths, checksum and flags are computed here and the fields of the record for them are ignored.</param>
    /// <param name="payload">The uncompressed payload.</param>
    /// <param name="compress">Whether to store the payload as raw Deflate.</param>
    /// <returns>The file bytes.</returns>
    internal static byte[] WriteImage(ModelHeader header, byte[] payload, bool compress)
    {
        var stored = compress ? Compress(payload) : payload;
        var image = new byte[HeaderLength + stored.Length];
        var span = image.AsSpan();
        Magic.CopyTo(span);
        BinaryPrimitives.WriteUInt16LittleEndian(span[4..], FormatVersion);
        BinaryPrimitives.WriteUInt16LittleEndian(span[6..], compress ? (ushort)FlagDeflate : (ushort)0);
        BinaryPrimitives.WriteUInt16LittleEndian(span[8..], (ushort)header.FeatureSetVersion);
        BinaryPrimitives.WriteUInt16LittleEndian(span[10..], (ushort)header.InventoryVersion);
        span[12] = (byte)header.HashBits;
        BinaryPrimitives.WriteSingleLittleEndian(span[16..], header.Temperature);
        BinaryPrimitives.WriteSingleLittleEndian(span[20..], header.Scale);
        BinaryPrimitives.WriteUInt32LittleEndian(span[24..], (uint)header.RowCount);
        BinaryPrimitives.WriteUInt32LittleEndian(span[28..], (uint)payload.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(span[32..], (uint)stored.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(span[36..], Fnv1a(stored));
        BinaryPrimitives.WriteUInt32LittleEndian(span[40..], header.TrainerVersion);
        header.DataManifestSha256.CopyTo(span[48..]);
        stored.CopyTo(span[HeaderLength..]);
        return image;
    }

    /// <summary>
    ///     Tells whether a header float field (temperature or scale) is acceptable.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns><see langword="true"/> when finite, greater than zero and at most <see cref="MaxFloatField"/>.</returns>
    internal static bool IsValidFloatField(float value) => value > 0 && value <= MaxFloatField;

    /// <summary>
    ///     Inflates a raw Deflate region into a buffer of exactly the declared size and only accepts a complete
    ///     stream that fills the region exactly.
    /// </summary>
    /// <param name="image">The buffer that holds the compressed region.</param>
    /// <param name="offset">The offset of the region.</param>
    /// <param name="count">The length of the region.</param>
    /// <param name="expectedLength">The declared uncompressed length.</param>
    /// <returns>The uncompressed bytes.</returns>
    /// <remarks>
    ///     A <see cref="DeflateStream"/> cannot be asked whether its stream ended properly: a stream that is
    ///     cut off can deliver all its data and then report the end exactly as a complete stream does, and it
    ///     reads its input in large chunks. The region is therefore served through a
    ///     <see cref="RegionStream"/> that withholds its last byte until every earlier byte was read and a
    ///     read still wants data. An inflater asks for more input only when it has consumed everything it was
    ///     given and still needs data, so a read beyond the region means the stream has no end inside it
    ///     (cut off), and a last byte that was never asked for means the stream ended earlier and the rest is
    ///     trailing bytes. At most the declared length plus one byte is inflated, so a hostile stream can
    ///     never produce more than the capped, declared size.
    /// </remarks>
    /// <exception cref="InvalidDataException">The data is not valid Deflate, does not inflate to exactly the declared length, is cut off, or does not use the whole region.</exception>
    private static byte[] Inflate(byte[] image, int offset, int count, int expectedLength)
    {
        var result = new byte[expectedLength];
        using var source = new RegionStream(image, offset, count);
        try
        {
            using var deflate = new DeflateStream(source, CompressionMode.Decompress);

            // Fill the buffer completely; a short read means the stream ended early or was cut off.
            var filled = 0;
            while (filled < expectedLength)
            {
                var read = deflate.Read(result, filled, expectedLength - filled);
                if (read == 0)
                {
                    throw new InvalidDataException(source.ReadPastEnd
                        ? "The compressed model payload is cut off: its Deflate stream has no end of stream."
                        : "The compressed model payload is shorter than declared.");
                }

                filled += read;
            }

            // Anything after the declared length means the header lies about the size.
            if (deflate.ReadByte() >= 0)
            {
                throw new InvalidDataException("The compressed model payload is longer than declared.");
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or IOException or IndexOutOfRangeException or NotSupportedException)
        {
            // The inflaters of the supported runtimes report bad data as InvalidDataException; anything else is still bad data.
            throw new InvalidDataException("The compressed model payload is not valid Deflate data.", ex);
        }

        // All declared bytes are present; the stream must now have ended by itself, using all of the region.
        if (source.ReadPastEnd)
        {
            throw new InvalidDataException("The compressed model payload is cut off: its Deflate stream has no end of stream.");
        }

        if (!source.LastByteRead)
        {
            throw new InvalidDataException("The compressed model payload is followed by unexpected trailing bytes inside the stored region.");
        }

        return result;
    }

    /// <summary>
    ///     A read-only stream over a byte range that records whether a reader asked for more than the range
    ///     holds and whether it asked for the last byte, and hands out the last byte only after every earlier
    ///     byte was read.
    /// </summary>
    private sealed class RegionStream : Stream
    {
        /// <summary>The buffer that holds the range.</summary>
        private readonly byte[] _buffer;

        /// <summary>The exclusive end of the range in <see cref="_buffer"/>.</summary>
        private readonly int _end;

        /// <summary>The index of the next byte to hand out.</summary>
        private int _next;

        /// <summary>
        ///     Initializes a new instance.
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <param name="offset">The offset of the range.</param>
        /// <param name="count">The length of the range.</param>
        internal RegionStream(byte[] buffer, int offset, int count)
        {
            _buffer = buffer;
            _next = offset;
            _end = offset + count;
        }

        /// <summary>Gets a value indicating whether a read asked for data after every byte of the range was handed out.</summary>
        internal bool ReadPastEnd { get; private set; }

        /// <summary>Gets a value indicating whether the last byte of the range was handed out (an empty range counts as read).</summary>
        internal bool LastByteRead => _next == _end;

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
        public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

        /// <inheritdoc/>
        public override int Read(Span<byte> buffer)
        {
            // A request for nothing never counts as asking for data.
            if (buffer.IsEmpty)
            {
                return 0;
            }

            // Hand out everything but the last byte; the last byte only goes out alone, when all else was read.
            var remaining = _end - _next;
            if (remaining <= 0)
            {
                ReadPastEnd = true;
                return 0;
            }

            var n = remaining > 1 ? Math.Min(buffer.Length, remaining - 1) : 1;
            _buffer.AsSpan(_next, n).CopyTo(buffer);
            _next += n;
            return n;
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
