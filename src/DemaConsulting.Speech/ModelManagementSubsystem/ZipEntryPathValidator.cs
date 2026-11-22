namespace DemaConsulting.Speech.ModelManagementSubsystem;

/// <summary>
///     The single, shared zip-slip and name-portability guard used by every installer that extracts a downloaded
///     zip archive into a model's staging directory.
/// </summary>
/// <remarks>
///     A checksum-verified archive is still untrusted input to an extractor: the checksum proves the bytes are the
///     published bytes, not that the published entry names are safe. This class holds the security-sensitive name
///     checks in one place so <see cref="ReferenceVoicePackInstaller"/> and <see cref="SpeechModelBundleInstaller"/>
///     cannot drift apart. It applies three checks:
///     <list type="number">
///         <item><description>
///             <see cref="SplitSafely"/> rejects a name that could escape the extraction directory: a backslash, a
///             colon (drive qualifier or alternate data stream), a leading slash, a NUL, or a <c>.</c>/<c>..</c>
///             segment.
///         </description></item>
///         <item><description>
///             <see cref="ValidatePortableSegments"/> rejects a segment that cannot be created on every platform:
///             invalid or control characters, Windows reserved device names, or a trailing dot or space.
///         </description></item>
///         <item><description>
///             <see cref="ResolveUnderRoot"/> re-checks that the fully resolved destination lies strictly inside the
///             extraction root, as a final defense independent of the two name checks.
///         </description></item>
///     </list>
///     Every failure is an <see cref="InvalidDataException"/> naming the offending entry, so the downloader
///     classifies it as an install-hook failure and installs nothing. The class is stateless and thread-safe.
/// </remarks>
internal static class ZipEntryPathValidator
{
    /// <summary>
    ///     The characters rejected in any path segment on every platform (the Windows-invalid set; a colon is already
    ///     rejected in <see cref="SplitSafely"/>).
    /// </summary>
    private static readonly char[] InvalidSegmentChars = [.. Path.GetInvalidFileNameChars(), '*', '?', '<', '>', '|', '"'];

    /// <summary>The Windows reserved device names (matched with or without an extension).</summary>
    private static readonly HashSet<string> ReservedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM0", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT0", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    };

    /// <summary>
    ///     Splits a zip entry name into its non-empty <c>/</c>-separated segments, rejecting any name that could
    ///     escape the extraction directory.
    /// </summary>
    /// <remarks>
    ///     Zip entry names always use <c>/</c> as the separator, so a backslash is never a legitimate separator: it is
    ///     rejected rather than interpreted, because Windows would treat it as one. Empty segments (from a doubled or
    ///     trailing slash, as in a directory entry <c>onnx/</c>) are dropped, so a directory entry splits into the
    ///     segments of the directory it names.
    /// </remarks>
    /// <param name="entryName">The raw entry name from the archive. Must not be <see langword="null"/>.</param>
    /// <returns>The entry's non-empty segments, in order; empty only for a name made of slashes alone.</returns>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <paramref name="entryName"/> contains a backslash, a colon, a NUL, starts with a slash, or has a
    ///     <c>.</c> or <c>..</c> segment.
    /// </exception>
    internal static string[] SplitSafely(string entryName)
    {
        // Reject separators and qualifiers that could make the name resolve somewhere other than a plain relative path
        if (entryName.Contains('\\') || entryName.Contains(':') || entryName.StartsWith('/') || entryName.Contains('\0'))
        {
            throw new InvalidDataException($"Archive entry '{entryName}' has an unsafe path.");
        }

        // Reject relative-path segments, which canonicalization would otherwise collapse to traverse upward
        var segments = entryName.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Any(segment => segment is ".." or "."))
        {
            throw new InvalidDataException($"Archive entry '{entryName}' has an unsafe path.");
        }

        return segments;
    }

    /// <summary>
    ///     Rejects an entry whose segments cannot be created on every platform, with an
    ///     <see cref="InvalidDataException"/> instead of letting the file system throw an unrelated exception type.
    /// </summary>
    /// <remarks>
    ///     Applied to every archive entry, whether or not the caller extracts it, so an archive that installs on one
    ///     platform never fails on another. The device-name check uses the part of a segment before its first dot
    ///     with trailing spaces removed, matching how Windows resolves <c>NUL.txt</c> and <c>CON .flac</c>.
    /// </remarks>
    /// <param name="entryName">The raw entry name, used only in the exception message.</param>
    /// <param name="segments">The segments returned by <see cref="SplitSafely"/> for <paramref name="entryName"/>.</param>
    /// <exception cref="InvalidDataException">
    ///     Thrown when a segment contains an invalid or control character, ends with a dot or a space, or is a Windows
    ///     reserved device name.
    /// </exception>
    internal static void ValidatePortableSegments(string entryName, IEnumerable<string> segments)
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
    ///     Applies both name checks to an entry name and returns its segments: the common entry point for callers
    ///     that validate every entry before writing anything.
    /// </summary>
    /// <param name="entryName">The raw entry name from the archive. Must not be <see langword="null"/>.</param>
    /// <returns>The entry's validated, non-empty segments.</returns>
    /// <exception cref="InvalidDataException">Thrown when either <see cref="SplitSafely"/> or <see cref="ValidatePortableSegments"/> rejects the name.</exception>
    internal static string[] Validate(string entryName)
    {
        var segments = SplitSafely(entryName);
        ValidatePortableSegments(entryName, segments);
        return segments;
    }

    /// <summary>
    ///     Resolves a relative destination beneath an extraction root and proves the result lies strictly inside it.
    /// </summary>
    /// <remarks>
    ///     A final, independent defense: even if a name check above were ever weakened, a destination that resolves to
    ///     the root itself or anywhere outside it is still refused. The comparison is ordinal on the canonical full
    ///     paths, with a trailing separator appended to the root so <c>/stage-evil</c> never passes as being under
    ///     <c>/stage</c>.
    /// </remarks>
    /// <param name="rootFullPath">
    ///     The extraction root as an absolute path with no trailing separator (as produced by
    ///     <c>Path.TrimEndingDirectorySeparator(Path.GetFullPath(root))</c>).
    /// </param>
    /// <param name="relativePath">The destination relative to <paramref name="rootFullPath"/>, using platform separators.</param>
    /// <param name="entryName">The raw entry name, used only in the exception message.</param>
    /// <returns>The canonical absolute destination path.</returns>
    /// <exception cref="InvalidDataException">Thrown when the destination does not lie strictly inside <paramref name="rootFullPath"/>.</exception>
    internal static string ResolveUnderRoot(string rootFullPath, string relativePath, string entryName)
    {
        var destination = Path.GetFullPath(Path.Combine(rootFullPath, relativePath));
        if (!destination.StartsWith(rootFullPath + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Archive entry '{entryName}' resolves outside the install directory.");
        }

        return destination;
    }
}
