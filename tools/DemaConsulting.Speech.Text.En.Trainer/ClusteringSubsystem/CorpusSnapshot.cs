using System.Formats.Tar;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace DemaConsulting.Speech.Text.En.Trainer.ClusteringSubsystem;

/// <summary>The identity of a built snapshot archive.</summary>
/// <param name="Path">The full path of the archive, or empty when the archive was only measured and never written.</param>
/// <param name="Sha256">The lowercase hexadecimal SHA-256 of the archive bytes.</param>
/// <param name="Bytes">The size of the archive in bytes.</param>
/// <param name="Files">The number of member files.</param>
internal sealed record CorpusSnapshotInfo(string Path, string Sha256, long Bytes, int Files);

/// <summary>
///     The byte-reproducible snapshot archive of the normalized word-cluster corpus: an uncompressed POSIX
///     <c>ustar</c> archive of a generated notice, the source list and every used book's normalized body.
/// </summary>
/// <remarks>
///     <para>
///     The snapshot lets the corpus be re-created when the mirror changes or disappears, and its SHA-256 in the
///     clustering record proves which corpus the table came from. Two builds from the same inputs must give the same
///     bytes on every platform, so every header field is fixed: member order (<c>NOTICE.txt</c>,
///     <c>cluster-sources.tsv</c>, then <c>bodies/gutenberg-N.txt</c> in ordinal source-id order), modification time 0,
///     mode <c>0644</c>, owner and group id 0 with empty names, no directory entries, no extended headers, and zero
///     padding to the 10,240-byte record size. The headers are written by this class rather than by a tar library so
///     no library default (time stamps, user names, header format) can leak in. Reading uses the standard tar reader and
///     accepts only regular files with safe names. Stateless and thread-safe.
///     </para>
/// </remarks>
internal static class CorpusSnapshot
{
    /// <summary>The name of the generated notice member.</summary>
    public const string NoticeName = "NOTICE.txt";

    /// <summary>The size of one tar block.</summary>
    private const int BlockSize = 512;

    /// <summary>The size of a tar record; the archive is padded to a whole number of records.</summary>
    private const int RecordSize = 20 * BlockSize;

    /// <summary>The longest member name a plain ustar header holds without its prefix field.</summary>
    private const int MaximumNameLength = 100;

    /// <summary>The largest member accepted when reading, in bytes.</summary>
    private const long MaximumMemberBytes = 64L * 1024 * 1024;

    /// <summary>
    ///     Gets the member name of a book's body.
    /// </summary>
    /// <param name="sourceId">The source id.</param>
    /// <returns>The member name <c>bodies/{sourceId}.txt</c>.</returns>
    public static string BodyMemberName(string sourceId) => $"bodies/{sourceId}.txt";

    /// <summary>
    ///     Builds the notice that travels with the corpus.
    /// </summary>
    /// <param name="books">The number of bodies in the archive.</param>
    /// <returns>The notice text.</returns>
    public static string Notice(int books) => string.Join(
        '\n',
        string.Create(CultureInfo.InvariantCulture, $"Normalized public-domain corpus of the Text.En word clusters ({books} books)."),
        string.Empty,
        "Each member bodies/<source_id>.txt is the body of one book published in 1929 or earlier and in the public",
        "domain in the USA, with the distributor's header, footer, license, credit lines and every paragraph naming the",
        "distributor removed. cluster-sources.tsv lists each book's title, author, year, origin URL, license basis and",
        "the SHA-256 of its body. The texts are in the public domain in the USA; check the law of your country before",
        "redistributing them elsewhere.",
        string.Empty,
        "Built by the 'cluster' command of DemaConsulting.Speech.Text.En.Trainer",
        "(https://github.com/demaconsulting/Speech).",
        string.Empty);

    /// <summary>
    ///     Builds the snapshot archive.
    /// </summary>
    /// <param name="path">The archive to write; replaced through a <c>.partial</c> sibling and a rename.</param>
    /// <param name="sourcesFile">The bytes of <c>cluster-sources.tsv</c>.</param>
    /// <param name="bodies">The normalized bodies by source id; written in ordinal source-id order.</param>
    /// <returns>The identity of the archive.</returns>
    /// <exception cref="ArgumentException">A source id would make a member name too long or unsafe.</exception>
    public static CorpusSnapshotInfo Build(string path, byte[] sourcesFile, IReadOnlyList<(string SourceId, byte[] Body)> bodies)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(sourcesFile);
        ArgumentNullException.ThrowIfNull(bodies);

        // Write through a partial sibling and rename, so a reader never sees a truncated archive
        var members = Members(sourcesFile, bodies);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(path))!);
        var partial = path + ".partial";
        long size;
        string sha;
        using (var stream = File.Create(partial))
        {
            (size, sha) = WriteArchive(stream, members);
        }

        File.Move(partial, path, true);
        return new CorpusSnapshotInfo(System.IO.Path.GetFullPath(path), sha, size, members.Count);
    }

    /// <summary>
    ///     Computes the identity of the archive <see cref="Build"/> would write, without writing any file.
    /// </summary>
    /// <remarks>
    ///     A check must compare an existing archive with the one the current inputs give without replacing it; the
    ///     bytes are produced exactly as <see cref="Build"/> produces them and only hashed and counted. No side effects;
    ///     thread-safe.
    /// </remarks>
    /// <param name="sourcesFile">The bytes of <c>cluster-sources.tsv</c>.</param>
    /// <param name="bodies">The normalized bodies by source id; in any order.</param>
    /// <returns>The identity of the archive, with an empty <see cref="CorpusSnapshotInfo.Path"/>.</returns>
    /// <exception cref="ArgumentException">A source id would make a member name too long or unsafe.</exception>
    public static CorpusSnapshotInfo Measure(byte[] sourcesFile, IReadOnlyList<(string SourceId, byte[] Body)> bodies)
    {
        ArgumentNullException.ThrowIfNull(sourcesFile);
        ArgumentNullException.ThrowIfNull(bodies);

        var members = Members(sourcesFile, bodies);
        var (size, sha) = WriteArchive(Stream.Null, members);
        return new CorpusSnapshotInfo(string.Empty, sha, size, members.Count);
    }

    /// <summary>
    ///     Reads every member of a snapshot archive.
    /// </summary>
    /// <param name="path">The archive.</param>
    /// <returns>The member bytes by member name.</returns>
    /// <exception cref="InvalidDataException">The archive is missing or malformed, or holds anything but regular files with safe, unique names.</exception>
    public static IReadOnlyDictionary<string, byte[]> Read(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (!File.Exists(path))
        {
            throw new InvalidDataException($"the snapshot {path} was not found");
        }

        var result = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        try
        {
            using var stream = File.OpenRead(path);
            using var reader = new TarReader(stream);
            while (reader.GetNextEntry() is { } entry)
            {
                if (entry.EntryType is not (TarEntryType.RegularFile or TarEntryType.V7RegularFile) || !IsSafeName(entry.Name)
                    || entry.Length > MaximumMemberBytes || result.ContainsKey(entry.Name))
                {
                    throw new InvalidDataException($"the snapshot member '{entry.Name}' is not a unique regular file with a safe name");
                }

                using var data = new MemoryStream();
                entry.DataStream?.CopyTo(data);
                result[entry.Name] = data.ToArray();
            }
        }
        catch (Exception exception) when (exception is FormatException or EndOfStreamException)
        {
            throw new InvalidDataException($"the snapshot {path} is not a valid tar archive: {exception.Message}");
        }

        return result;
    }

    /// <summary>
    ///     Computes the SHA-256 of a file.
    /// </summary>
    /// <param name="path">The file.</param>
    /// <returns>The lowercase hexadecimal hash.</returns>
    public static string FileSha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    /// <summary>
    ///     Lists the members of the archive in their fixed order.
    /// </summary>
    /// <param name="sourcesFile">The bytes of <c>cluster-sources.tsv</c>.</param>
    /// <param name="bodies">The normalized bodies by source id.</param>
    /// <returns>The notice, the list, then the bodies in ordinal source-id order.</returns>
    private static List<(string Name, byte[] Data)> Members(byte[] sourcesFile, IReadOnlyList<(string SourceId, byte[] Body)> bodies)
    {
        var members = new List<(string Name, byte[] Data)>
        {
            (NoticeName, Encoding.UTF8.GetBytes(Notice(bodies.Count))),
            (ClusterSourceList.FileName, sourcesFile),
        };
        members.AddRange(bodies.OrderBy(static body => body.SourceId, StringComparer.Ordinal).Select(static body => (BodyMemberName(body.SourceId), body.Body)));
        return members;
    }

    /// <summary>
    ///     Writes the archive bytes of the members to a stream while hashing them.
    /// </summary>
    /// <param name="stream">The destination; <see cref="Stream.Null"/> only hashes and counts.</param>
    /// <param name="members">The members in archive order.</param>
    /// <returns>The archive size in bytes and its lowercase hexadecimal SHA-256.</returns>
    /// <exception cref="ArgumentException">A member name is too long or unsafe.</exception>
    private static (long Size, string Sha256) WriteArchive(Stream stream, IReadOnlyList<(string Name, byte[] Data)> members)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        long size = 0;
        foreach (var (name, data) in members)
        {
            size += Write(stream, hash, Header(name, data.LongLength));
            size += Write(stream, hash, data);
            size += Write(stream, hash, new byte[Pad(data.LongLength, BlockSize)]);
        }

        // Two zero blocks end the archive; zeros then fill the last record
        size += Write(stream, hash, new byte[2 * BlockSize]);
        size += Write(stream, hash, new byte[Pad(size, RecordSize)]);
        return (size, Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant());
    }

    /// <summary>
    ///     Builds one fixed ustar header block.
    /// </summary>
    /// <param name="name">The member name (ASCII, at most 100 characters).</param>
    /// <param name="size">The member size in bytes.</param>
    /// <returns>The 512-byte header.</returns>
    private static byte[] Header(string name, long size)
    {
        if (!IsSafeName(name) || name.Length > MaximumNameLength || name.Any(static c => c > '\u007e'))
        {
            throw new ArgumentException($"the member name '{name}' is not a safe ASCII name of at most {MaximumNameLength} characters", nameof(name));
        }

        var header = new byte[BlockSize];
        Field(header, 0, 100, name);
        Field(header, 100, 8, "0000644\0");
        Field(header, 108, 8, "0000000\0");
        Field(header, 116, 8, "0000000\0");
        Field(header, 124, 12, Convert.ToString(size, 8).PadLeft(11, '0') + "\0");
        Field(header, 136, 12, "00000000000\0");
        Field(header, 148, 8, "        ");
        header[156] = (byte)'0';
        Field(header, 257, 6, "ustar\0");
        Field(header, 263, 2, "00");
        Field(header, 329, 8, "0000000\0");
        Field(header, 337, 8, "0000000\0");

        // The checksum is the byte sum with the checksum field read as spaces
        var sum = header.Sum(static b => (int)b);
        Field(header, 148, 8, Convert.ToString(sum, 8).PadLeft(6, '0') + "\0 ");
        return header;
    }

    /// <summary>
    ///     Writes ASCII text into a header field.
    /// </summary>
    /// <param name="header">The header.</param>
    /// <param name="offset">The field offset.</param>
    /// <param name="length">The field length; the text must fit.</param>
    /// <param name="text">The ASCII text.</param>
    private static void Field(byte[] header, int offset, int length, string text) =>
        Encoding.ASCII.GetBytes(text.AsSpan(0, Math.Min(text.Length, length)), header.AsSpan(offset, length));

    /// <summary>
    ///     Determines whether a member name is a safe relative path.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <returns><see langword="true"/> for a non-empty relative <c>/</c>-separated path without empty, <c>.</c> or <c>..</c> segments.</returns>
    private static bool IsSafeName(string name) =>
        name.Length > 0 && !name.Contains('\\') && !name.StartsWith('/')
        && name.Split('/').All(static segment => segment.Length > 0 && segment is not ("." or ".."));

    /// <summary>
    ///     Computes the zero padding after a length up to a multiple of a unit.
    /// </summary>
    /// <param name="length">The length.</param>
    /// <param name="unit">The unit.</param>
    /// <returns>The number of padding bytes, 0 to <paramref name="unit"/> minus 1.</returns>
    private static int Pad(long length, int unit) => (int)((unit - (length % unit)) % unit);

    /// <summary>
    ///     Writes bytes to the archive and the running hash.
    /// </summary>
    /// <param name="stream">The archive stream.</param>
    /// <param name="hash">The running hash.</param>
    /// <param name="bytes">The bytes.</param>
    /// <returns>The number of bytes written.</returns>
    private static long Write(Stream stream, IncrementalHash hash, byte[] bytes)
    {
        stream.Write(bytes);
        hash.AppendData(bytes);
        return bytes.LongLength;
    }
}
