using System.Formats.Tar;
using System.Text;
using DemaConsulting.Speech.Text.En.Trainer.ClusteringSubsystem;

namespace DemaConsulting.Speech.Text.En.Trainer.Tests.ClusteringSubsystem;

/// <summary>
///     Unit tests for <see cref="CorpusSnapshot"/>: the archive is byte-reproducible (golden SHA-256, two builds equal,
///     input order irrelevant), its header fields are fixed, and reading refuses unsafe archives.
/// </summary>
public sealed class CorpusSnapshotTests : IDisposable
{
    /// <summary>The golden SHA-256 of the archive of <see cref="Bodies"/> and <see cref="SourcesFile"/>.</summary>
    private const string GoldenSha256 = "4e7e552046835fe22876d575a7b89fa3043867cb8d8af2fa67dd0e7a73a5fedf";

    /// <summary>The fixed bytes of the source list used by the tests.</summary>
    private static readonly byte[] SourcesFile = Encoding.UTF8.GetBytes("source_id\tstatus\ngutenberg-2\tused\ngutenberg-10\tused\n");

    /// <summary>The temporary folder.</summary>
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "texten-snapshot-" + Guid.NewGuid().ToString("N"));

    /// <summary>Gets the fixed bodies used by the tests.</summary>
    private static (string, byte[])[] Bodies =>
    [
        ("gutenberg-2", Encoding.UTF8.GetBytes("The second book.\n")),
        ("gutenberg-10", Encoding.UTF8.GetBytes("The tenth book, with a longer body.\n\nTwo paragraphs.\n")),
    ];

    /// <summary>Proves the archive of fixed inputs has the recorded SHA-256 on every platform and two builds are byte-equal.</summary>
    [Fact]
    public void CorpusSnapshot_Build_FixedInputs_GoldenShaAndByteEqual()
    {
        // Arrange
        var first = Path.Combine(_folder, "one.tar");
        var second = Path.Combine(_folder, "two.tar");

        // Act
        var a = CorpusSnapshot.Build(first, SourcesFile, Bodies);
        var b = CorpusSnapshot.Build(second, SourcesFile, Bodies);

        // Assert
        Assert.Equal(GoldenSha256, a.Sha256);
        Assert.Equal(File.ReadAllBytes(first), File.ReadAllBytes(second));
        Assert.Equal(a.Sha256, CorpusSnapshot.FileSha256(first));
        Assert.Equal(new FileInfo(first).Length, a.Bytes);
        Assert.Equal(0, a.Bytes % 10240);
        Assert.Equal(4, a.Files);
        Assert.Equal(a.Sha256, b.Sha256);
    }

    /// <summary>Proves the order the bodies are given in does not change the archive.</summary>
    [Fact]
    public void CorpusSnapshot_Build_ReversedInput_SameArchive()
    {
        // Act
        var forward = CorpusSnapshot.Build(Path.Combine(_folder, "f.tar"), SourcesFile, Bodies);
        var reversed = CorpusSnapshot.Build(Path.Combine(_folder, "r.tar"), SourcesFile, [.. Bodies.Reverse()]);

        // Assert
        Assert.Equal(forward.Sha256, reversed.Sha256);
    }

    /// <summary>Proves measuring gives the golden hash, size and file count of the built archive without writing any file.</summary>
    [Fact]
    public void CorpusSnapshot_Measure_FixedInputs_SameIdentityWithoutWriting()
    {
        // Act
        var measured = CorpusSnapshot.Measure(SourcesFile, [.. Bodies.Reverse()]);
        var built = CorpusSnapshot.Build(Path.Combine(_folder, "m.tar"), SourcesFile, Bodies);

        // Assert: the same identity, an empty path, and only the built archive in the folder
        Assert.Equal(GoldenSha256, measured.Sha256);
        Assert.Equal((built.Sha256, built.Bytes, built.Files), (measured.Sha256, measured.Bytes, measured.Files));
        Assert.Equal(string.Empty, measured.Path);
        Assert.Equal(["m.tar"], Directory.GetFiles(_folder).Select(Path.GetFileName));
    }

    /// <summary>Proves the members come back in the fixed order with fixed time, mode, owner and group.</summary>
    [Fact]
    public void CorpusSnapshot_Build_HeaderFields_AreFixed()
    {
        // Arrange
        var path = Path.Combine(_folder, "h.tar");
        CorpusSnapshot.Build(path, SourcesFile, Bodies);

        // Act
        var entries = new List<TarEntry>();
        using (var stream = File.OpenRead(path))
        using (var reader = new TarReader(stream))
        {
            while (reader.GetNextEntry(copyData: true) is { } entry)
            {
                entries.Add(entry);
            }
        }

        // Assert
        Assert.Equal(["NOTICE.txt", "cluster-sources.tsv", "bodies/gutenberg-10.txt", "bodies/gutenberg-2.txt"], entries.Select(static entry => entry.Name));
        Assert.All(entries, static entry =>
        {
            Assert.Equal(TarEntryFormat.Ustar, entry.Format);
            Assert.Equal(DateTimeOffset.UnixEpoch, entry.ModificationTime);
            Assert.Equal(0, entry.Uid);
            Assert.Equal(0, entry.Gid);
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead, entry.Mode);
        });
    }

    /// <summary>Proves reading returns every member's bytes, including the notice that names the book count.</summary>
    [Fact]
    public void CorpusSnapshot_Read_BuiltArchive_ReturnsMembers()
    {
        // Arrange
        var path = Path.Combine(_folder, "m.tar");
        CorpusSnapshot.Build(path, SourcesFile, Bodies);

        // Act
        var members = CorpusSnapshot.Read(path);

        // Assert
        Assert.Equal(SourcesFile, members["cluster-sources.tsv"]);
        Assert.Equal(Bodies[0].Item2, members[CorpusSnapshot.BodyMemberName("gutenberg-2")]);
        Assert.Equal(CorpusSnapshot.Notice(2), Encoding.UTF8.GetString(members[CorpusSnapshot.NoticeName]));
    }

    /// <summary>Proves an archive with an unsafe member name, a missing archive and a file that is not an archive are refused.</summary>
    [Fact]
    public void CorpusSnapshot_Read_UnsafeOrMissing_Throws()
    {
        // Arrange: an archive whose member escapes the folder
        Directory.CreateDirectory(_folder);
        var evil = Path.Combine(_folder, "evil.tar");
        using (var stream = File.Create(evil))
        using (var writer = new TarWriter(stream, TarEntryFormat.Ustar))
        {
            writer.WriteEntry(new UstarTarEntry(TarEntryType.RegularFile, "../escape.txt") { DataStream = new MemoryStream([1, 2]) });
        }

        var garbage = Path.Combine(_folder, "garbage.tar");
        File.WriteAllBytes(garbage, Encoding.ASCII.GetBytes(new string('x', 1024)));

        // Act / Assert
        Assert.Throws<InvalidDataException>(() => CorpusSnapshot.Read(evil));
        Assert.Throws<InvalidDataException>(() => CorpusSnapshot.Read(Path.Combine(_folder, "missing.tar")));
        Assert.Throws<InvalidDataException>(() => CorpusSnapshot.Read(garbage));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, true);
        }
    }
}
