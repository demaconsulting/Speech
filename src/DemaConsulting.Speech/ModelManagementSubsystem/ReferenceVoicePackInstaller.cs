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
///     <b>Safety</b>: an entry whose name is rooted, contains a backslash, a drive colon, a NUL or a <c>..</c>/<c>.</c>
///     segment fails the whole install (a legitimate, hash-pinned archive never has one), even when the entry would
///     otherwise be ignored by the allow-list; an entry (extracted or ignored) with a segment that is not valid on
///     every platform
///     (invalid or control characters, reserved device names, a trailing dot or space) fails it too, two entries that
///     map to one destination fail it, each extracted path is re-checked to
///     lie inside the staging directory, and the number of bytes actually read per entry and in total is capped
///     (the caps are enforced on the bytes read from the entry stream, not on the sizes the archive headers declare).
/// </remarks>
internal static class ReferenceVoicePackInstaller
{
    /// <summary>The name of the metadata file written next to the extracted content.</summary>
    internal const string PackMetadataFileName = "pack.json";

    /// <summary>The name of the extracted folder holding the voice clips.</summary>
    internal const string VoicesDirectoryName = "voices";

    /// <summary>The name of the extracted folder holding the per-speaker emotion (mood) clips.</summary>
    internal const string EmotionsDirectoryName = "voices-emotion";

    /// <summary>The name of the extracted license file at the pack root.</summary>
    internal const string LicenseFileName = "LICENSE.md";

    /// <summary>The name of the extracted readme file (at the pack root and in each clip folder).</summary>
    internal const string ReadmeFileName = "README.md";

    /// <summary>The names the installer itself creates or extracts at the root of an installed pack.</summary>
    internal static readonly string[] ReservedRootNames =
        [PackMetadataFileName, VoicesDirectoryName, EmotionsDirectoryName, LicenseFileName, ReadmeFileName];

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

    /// <summary>The characters rejected in any extracted path segment on every platform (the Windows-invalid set; a colon is already rejected in <see cref="SplitSafely"/>).</summary>
    private static readonly char[] InvalidSegmentChars = [.. Path.GetInvalidFileNameChars(), '*', '?', '<', '>', '|', '"'];

    /// <summary>The Windows reserved device names (matched with or without an extension).</summary>
    private static readonly HashSet<string> ReservedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM0", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT0", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    };

    /// <summary>
    ///     Extracts the pack's allowed entries from the staged archive into the staging directory.
    /// </summary>
    /// <param name="pack">The pack being installed.</param>
    /// <param name="includeEmotions"><see langword="true"/> to also extract the <c>voices-emotion</c> clips.</param>
    /// <param name="stagingDirectory">The staging directory holding the verified archive; also the extraction target.</param>
    /// <param name="cancellationToken">A token that aborts the extraction.</param>
    /// <returns>A task that completes when extraction, archive deletion and the metadata write are done.</returns>
    /// <exception cref="InvalidDataException">Thrown when the archive is not a valid zip, has an unsafe entry, exceeds a size cap or contains no voices.</exception>
    /// <exception cref="IOException">Thrown when the staged archive is missing or cannot be read or an extracted file cannot be written.</exception>
    internal static Task InstallAsync(ReferenceVoicePack pack, bool includeEmotions, string stagingDirectory, CancellationToken cancellationToken) =>
        InstallAsync(pack, includeEmotions, stagingDirectory, MaximumEntryBytes, MaximumTotalBytes, cancellationToken);

    /// <summary>
    ///     Extracts the pack's allowed entries with explicit size caps (a test seam; production uses
    ///     <see cref="MaximumEntryBytes"/> and <see cref="MaximumTotalBytes"/>).
    /// </summary>
    /// <param name="pack">The pack being installed.</param>
    /// <param name="includeEmotions"><see langword="true"/> to also extract the <c>voices-emotion</c> clips.</param>
    /// <param name="stagingDirectory">The staging directory holding the verified archive; also the extraction target.</param>
    /// <param name="maximumEntryBytes">The largest accepted extracted size of one entry (bytes).</param>
    /// <param name="maximumTotalBytes">The largest accepted total extracted size (bytes).</param>
    /// <param name="cancellationToken">A token that aborts the extraction.</param>
    /// <returns>A task that completes when extraction, archive deletion and the metadata write are done.</returns>
    /// <exception cref="InvalidDataException">Thrown when the archive is not a valid zip, has an unsafe entry, exceeds a size cap or contains no voices.</exception>
    internal static async Task InstallAsync(
        ReferenceVoicePack pack,
        bool includeEmotions,
        string stagingDirectory,
        long maximumEntryBytes,
        long maximumTotalBytes,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pack);
        ArgumentException.ThrowIfNullOrEmpty(stagingDirectory);

        // The archive is opened off the caller's thread (the central directory read has no asynchronous API); every
        // entry is then copied with asynchronous file I/O so the install never blocks a thread for the transfer
        await Task.Yield();

        // Cancellation takes precedence over a missing or unreadable archive
        cancellationToken.ThrowIfCancellationRequested();

        var archivePath = Path.Combine(stagingDirectory, pack.ArchiveFileName);
        var stagingRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(stagingDirectory));
        var voiceCount = 0;
        var emotionCount = 0;
        long totalBytes = 0;
        var written = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        using (var archive = OpenArchive(archivePath))
        {
            foreach (var entry in archive.Entries)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Every entry is validated for portability before the allow-list decides whether it is extracted, so an
                // ignored entry with a name that is invalid on some platform still fails the install
                var segments = SplitSafely(entry.FullName);
                ValidateDestinationSegments(entry.FullName, segments);

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
                totalBytes += await CopyEntryAsync(entry, destination, totalBytes, maximumEntryBytes, maximumTotalBytes, cancellationToken).ConfigureAwait(false);
                voiceCount += kind == EntryKind.Voice ? 1 : 0;
                emotionCount += kind == EntryKind.Emotion ? 1 : 0;
            }
        }

        // An archive with no entries never observes cancellation inside the loop, so honor it before the empty-archive failure
        cancellationToken.ThrowIfCancellationRequested();

        if (voiceCount == 0)
        {
            throw new InvalidDataException(
                $"The '{pack.Id}' archive contains no '{pack.ArchiveRootFolder}/{VoicesDirectoryName}/*{VoiceExtension}' voice clips.");
        }

        // Write the metadata first and delete the archive only once the whole extraction succeeded, so any failure or
        // cancellation up to this point keeps the verified archive for a retry
        await WriteMetadataAsync(pack, stagingRoot, voiceCount, includeEmotions, emotionCount, cancellationToken).ConfigureAwait(false);

        // The archive was only an input; remove it so it does not stay in the store
        File.Delete(archivePath);
    }

    /// <summary>
    ///     Opens the staged archive, converting a file that is not a readable zip (<see cref="InvalidDataException"/>,
    ///     <see cref="NotSupportedException"/> or <see cref="ArgumentException"/> from the zip reader) into an
    ///     <see cref="InvalidDataException"/>. A missing or inaccessible archive is not converted and surfaces as the
    ///     <see cref="IOException"/> (for example <see cref="FileNotFoundException"/>) or
    ///     <see cref="UnauthorizedAccessException"/> the file system reports.
    /// </summary>
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
    ///     Rejects an entry whose segments cannot be created on every platform (invalid characters, control
    ///     characters, reserved device names, trailing dots or spaces) with an <see cref="InvalidDataException"/>
    ///     instead of letting the file system throw an unrelated exception type. Applied to every archive entry,
    ///     whether or not the allow-list extracts it.
    /// </summary>
    private static void ValidateDestinationSegments(string entryName, string[] segments)
    {
        foreach (var segment in segments)
        {
            var stem = segment.Split('.')[0].TrimEnd(' ');
            if (segment.IndexOfAny(InvalidSegmentChars) >= 0
                || segment.Any(char.IsControl)
                || segment.EndsWith('.')
                || segment.EndsWith(' ')
                || ReservedNames.Contains(stem))
            {
                throw new InvalidDataException($"Archive entry '{entryName}' has a name that is not valid on every platform.");
            }
        }
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
            if (string.Equals(segments[2], ReadmeFileName, StringComparison.Ordinal))
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
            if (segments.Length == 3 && string.Equals(segments[2], ReadmeFileName, StringComparison.Ordinal))
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
        string.Equals(name, LicenseFileName, StringComparison.Ordinal) || string.Equals(name, ReadmeFileName, StringComparison.Ordinal);

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
    private static async Task<long> CopyEntryAsync(
        ZipArchiveEntry entry,
        string destination,
        long totalSoFar,
        long maximumEntryBytes,
        long maximumTotalBytes,
        CancellationToken cancellationToken)
    {
        long copied = 0;
        var buffer = new byte[BufferSize];
#pragma warning disable S6966 // ZipArchiveEntry.OpenAsync exists only on net10.0; this library also targets net8.0/net9.0
        await using var source = entry.Open();
#pragma warning restore S6966
        await using var target = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None, BufferSize, useAsync: true);
        int read;
        while ((read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
        {
            copied += read;
            if (copied > maximumEntryBytes || totalSoFar + copied > maximumTotalBytes)
            {
                throw new InvalidDataException($"Archive entry '{entry.FullName}' exceeds the allowed extraction size.");
            }

            await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
        }

        return copied;
    }

    /// <summary>Writes <c>pack.json</c> describing the installed pack.</summary>
    private static Task WriteMetadataAsync(
        ReferenceVoicePack pack,
        string stagingRoot,
        int voiceCount,
        bool includeEmotions,
        int emotionCount,
        CancellationToken cancellationToken)
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

        return File.WriteAllTextAsync(Path.Combine(stagingRoot, PackMetadataFileName), JsonSerializer.Serialize(metadata, JsonOptions), cancellationToken);
    }
}
