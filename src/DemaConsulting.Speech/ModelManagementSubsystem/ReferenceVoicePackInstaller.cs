using System.IO.Compression;
using System.Text.Json;

namespace DemaConsulting.Speech.ModelManagementSubsystem;

/// <summary>
///     The install hook for a <see cref="ReferenceVoicePack"/>: safely extracts only the voice clips, optionally the
///     emotion (mood) clips, and the license/readme files from the verified, staged archive, then deletes the
///     archive and writes <c>pack.json</c>.
/// </summary>
/// <remarks>
///     <b>Allow-list</b>: of every archive entry only <c>{root}/voices/{name}.flac</c>, <c>{root}/voices/README.md</c>,
///     <c>{root}/LICENSE.md</c> and <c>{root}/README.md</c> are extracted (the root folder is stripped); when emotions
///     are requested <c>{root}/voices-emotion/{speaker}/{mood}.flac</c> and <c>{root}/voices-emotion/README.md</c> are
///     extracted too. Everything else is ignored. Emotion clips never count as voices: an archive with only emotion
///     clips still fails.
///     <b>Safety</b>: an entry whose name is rooted, contains a backslash, a drive colon or a <c>..</c> segment fails
///     the whole install (a legitimate, hash-pinned archive never has one), each extracted path is re-checked to
///     lie inside the staging directory, and the number of bytes actually read per entry and in total is capped
///     regardless of what the archive's headers claim.
/// </remarks>
internal static class ReferenceVoicePackInstaller
{
    /// <summary>The name of the metadata file written next to the extracted content.</summary>
    internal const string PackMetadataFileName = "pack.json";

    /// <summary>The name of the extracted folder holding the voice clips.</summary>
    internal const string VoicesDirectoryName = "voices";

    /// <summary>The name of the extracted folder holding the per-speaker emotion (mood) clips.</summary>
    internal const string EmotionsDirectoryName = "voices-emotion";

    /// <summary>The largest accepted extracted size of one entry (bytes).</summary>
    internal const long MaximumEntryBytes = 128L * 1024 * 1024;

    /// <summary>The largest accepted total extracted size (bytes).</summary>
    internal const long MaximumTotalBytes = 2L * 1024 * 1024 * 1024;

    /// <summary>The voice clip extension that is extracted.</summary>
    private const string VoiceExtension = ".flac";

    /// <summary>The copy buffer size.</summary>
    private const int BufferSize = 81920;

    /// <summary>JSON options for <c>pack.json</c>.</summary>
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    /// <summary>
    ///     Extracts the pack's allowed entries from the staged archive into the staging directory.
    /// </summary>
    /// <param name="pack">The pack being installed.</param>
    /// <param name="includeEmotions"><see langword="true"/> to also extract the <c>voices-emotion</c> clips.</param>
    /// <param name="stagingDirectory">The staging directory holding the verified archive; also the extraction target.</param>
    /// <param name="cancellationToken">A token that aborts the extraction.</param>
    /// <returns>A task that completes when extraction, archive deletion and the metadata write are done.</returns>
    /// <exception cref="InvalidDataException">Thrown when the archive is not a valid zip, has an unsafe entry, exceeds a size cap or contains no voices.</exception>
    internal static Task InstallAsync(ReferenceVoicePack pack, bool includeEmotions, string stagingDirectory, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pack);
        ArgumentException.ThrowIfNullOrEmpty(stagingDirectory);

        // Extraction is synchronous file work; keep it off the caller's thread
        return Task.Run(() => Install(pack, includeEmotions, stagingDirectory, cancellationToken), cancellationToken);
    }

    /// <summary>Performs the extraction synchronously.</summary>
    private static void Install(ReferenceVoicePack pack, bool includeEmotions, string stagingDirectory, CancellationToken cancellationToken)
    {
        var archivePath = Path.Combine(stagingDirectory, pack.ArchiveFileName);
        var stagingRoot = Path.GetFullPath(stagingDirectory);
        var voiceCount = 0;
        var emotionCount = 0;
        long totalBytes = 0;
        var written = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        using (var archive = OpenArchive(archivePath))
        {
            foreach (var entry in archive.Entries)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var segments = SplitSafely(entry.FullName);
                var relativePath = MapToDestination(pack, includeEmotions, segments, out var kind);
                if (relativePath is null)
                {
                    continue;
                }

                var destination = Path.GetFullPath(Path.Combine(stagingRoot, relativePath));
                if (!destination.StartsWith(stagingRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                {
                    throw new InvalidDataException($"Archive entry '{entry.FullName}' resolves outside the install directory.");
                }

                if (!written.Add(destination))
                {
                    throw new InvalidDataException($"Archive contains the entry '{entry.FullName}' more than once.");
                }

                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                totalBytes += CopyEntry(entry, destination, totalBytes, cancellationToken);
                voiceCount += kind == EntryKind.Voice ? 1 : 0;
                emotionCount += kind == EntryKind.Emotion ? 1 : 0;
            }
        }

        if (voiceCount == 0)
        {
            throw new InvalidDataException(
                $"The '{pack.Id}' archive contains no '{pack.ArchiveRootFolder}/{VoicesDirectoryName}/*{VoiceExtension}' voice clips.");
        }

        // The archive was only an input; remove it so it does not stay in the store
        File.Delete(archivePath);
        WriteMetadata(pack, stagingRoot, voiceCount, includeEmotions, emotionCount);
    }

    /// <summary>Opens the staged archive, converting an unreadable file into an <see cref="InvalidDataException"/>.</summary>
    private static ZipArchive OpenArchive(string archivePath)
    {
        try
        {
            return ZipFile.OpenRead(archivePath);
        }
        catch (InvalidDataException)
        {
            throw;
        }
        catch (Exception ex) when (ex is NotSupportedException or ArgumentException)
        {
            throw new InvalidDataException($"The reference-voice archive '{Path.GetFileName(archivePath)}' is not a valid zip file.", ex);
        }
    }

    /// <summary>
    ///     Splits an entry name into segments, rejecting any name that could escape the extraction directory.
    /// </summary>
    private static string[] SplitSafely(string entryName)
    {
        if (entryName.Contains('\\') || entryName.Contains(':') || entryName.StartsWith('/') || entryName.Contains('\0'))
        {
            throw new InvalidDataException($"Archive entry '{entryName}' has an unsafe path.");
        }

        var segments = entryName.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Any(segment => segment is ".." or "."))
        {
            throw new InvalidDataException($"Archive entry '{entryName}' has an unsafe path.");
        }

        return segments;
    }

    /// <summary>
    ///     Maps an entry's segments to its destination relative to the staging directory, or <see langword="null"/>
    ///     when the entry is not on the allow-list.
    /// </summary>
    private static string? MapToDestination(ReferenceVoicePack pack, bool includeEmotions, string[] segments, out EntryKind kind)
    {
        kind = EntryKind.Other;
        if (segments.Length < 2 || !string.Equals(segments[0], pack.ArchiveRootFolder, StringComparison.Ordinal))
        {
            return null;
        }

        if (segments.Length == 2 && IsTextFile(segments[1]))
        {
            return segments[1];
        }

        if (segments.Length == 3 && string.Equals(segments[1], VoicesDirectoryName, StringComparison.Ordinal))
        {
            if (string.Equals(segments[2], "README.md", StringComparison.Ordinal))
            {
                return Path.Combine(VoicesDirectoryName, segments[2]);
            }

            if (segments[2].EndsWith(VoiceExtension, StringComparison.OrdinalIgnoreCase) && segments[2].Length > VoiceExtension.Length)
            {
                kind = EntryKind.Voice;
                return Path.Combine(VoicesDirectoryName, segments[2]);
            }
        }

        if (includeEmotions && segments.Length >= 3 && string.Equals(segments[1], EmotionsDirectoryName, StringComparison.Ordinal))
        {
            if (segments.Length == 3 && string.Equals(segments[2], "README.md", StringComparison.Ordinal))
            {
                return Path.Combine(EmotionsDirectoryName, segments[2]);
            }

            // {root}/voices-emotion/{speaker}/{mood}.flac - exactly one speaker folder and one clip
            if (segments.Length == 4 && segments[3].EndsWith(VoiceExtension, StringComparison.OrdinalIgnoreCase) && segments[3].Length > VoiceExtension.Length)
            {
                kind = EntryKind.Emotion;
                return Path.Combine(EmotionsDirectoryName, segments[2], segments[3]);
            }
        }

        return null;
    }

    /// <summary>Determines whether a root-level file is one of the license/readme files that are extracted.</summary>
    private static bool IsTextFile(string name) =>
        string.Equals(name, "LICENSE.md", StringComparison.Ordinal) || string.Equals(name, "README.md", StringComparison.Ordinal);

    /// <summary>What an allow-listed archive entry is.</summary>
    private enum EntryKind
    {
        /// <summary>A text file or readme (extracted, not counted).</summary>
        Other,

        /// <summary>A base voice clip.</summary>
        Voice,

        /// <summary>A speaker-and-mood emotion clip.</summary>
        Emotion,
    }

    /// <summary>Copies one entry to disk, enforcing the per-entry and total byte caps on the bytes actually read.</summary>
    private static long CopyEntry(ZipArchiveEntry entry, string destination, long totalSoFar, CancellationToken cancellationToken)
    {
        long copied = 0;
        var buffer = new byte[BufferSize];
        using var source = entry.Open();
        using var target = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        int read;
        while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
        {
            copied += read;
            if (copied > MaximumEntryBytes || totalSoFar + copied > MaximumTotalBytes)
            {
                throw new InvalidDataException($"Archive entry '{entry.FullName}' exceeds the allowed extraction size.");
            }

            target.Write(buffer, 0, read);
            cancellationToken.ThrowIfCancellationRequested();
        }

        return copied;
    }

    /// <summary>Writes <c>pack.json</c> describing the installed pack.</summary>
    private static void WriteMetadata(ReferenceVoicePack pack, string stagingRoot, int voiceCount, bool includeEmotions, int emotionCount)
    {
        var metadata = new
        {
            id = pack.Id,
            name = pack.DisplayName,
            license = pack.LicenseName,
            licenseUrl = pack.LicenseUrl.ToString(),
            attribution = pack.Attribution,
            sourceUrl = pack.SourceUrl.ToString(),
            archiveUrl = pack.Uri.ToString(),
            archiveSha256 = pack.Sha256Checksum,
            voiceCount,
            emotionClipCount = emotionCount,
            variants = includeEmotions ? new[] { VoicesDirectoryName, EmotionsDirectoryName } : new[] { VoicesDirectoryName },
        };

        File.WriteAllText(Path.Combine(stagingRoot, PackMetadataFileName), JsonSerializer.Serialize(metadata, JsonOptions));
    }
}
