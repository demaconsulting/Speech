using SharpCompress.Readers;

namespace DemaConsulting.Speech.Onnx.Piper.ModelManagementSubsystem;

/// <summary>
///     Extracts only the ONNX graph and the voice configuration from a Piper release <c>.tar.bz2</c> archive and deletes
///     the archive.
/// </summary>
/// <remarks>
///     The BCL has no BZip2 decoder, so this type is why this package references SharpCompress. The archive also bundles
///     the espeak-ng data (GPL-3.0) and a token table, which this package never uses; every entry whose name does not
///     end in <c>.onnx</c> or <c>.onnx.json</c> is skipped, so that data is never written to disk. A wanted entry is
///     written directly into the destination under its bare file name: the archive's own folder structure plays no part
///     in where a file lands, so a hostile entry path cannot escape the destination directory.
/// </remarks>
internal static class PiperArchiveExtractor
{
    /// <summary>The suffix of the graph file.</summary>
    private const string ModelSuffix = ".onnx";

    /// <summary>The suffix of the voice configuration file.</summary>
    private const string ConfigSuffix = ".onnx.json";

    /// <summary>
    ///     Extracts the <c>.onnx</c> and <c>.onnx.json</c> entries of a <c>.tar.bz2</c> archive into a directory, then
    ///     deletes the archive.
    /// </summary>
    /// <param name="archivePath">The archive file path. Must not be null or empty.</param>
    /// <param name="destinationDirectory">The directory to extract into. Must not be null or empty.</param>
    /// <param name="cancellationToken">A token to observe while extracting.</param>
    /// <returns>A task that completes once the two files are extracted and the archive deleted.</returns>
    /// <exception cref="ArgumentException">Thrown when a path argument is null or empty.</exception>
    /// <exception cref="InvalidDataException">
    ///     Thrown when the archive does not contain exactly one graph and one configuration file. The archive is left in
    ///     place; the caller discards the whole staging directory.
    /// </exception>
    internal static async Task ExtractAndDeleteAsync(
        string archivePath,
        string destinationDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(archivePath);
        ArgumentException.ThrowIfNullOrEmpty(destinationDirectory);

        var extracted = new List<string>();
        using (var archiveStream = File.OpenRead(archivePath))
        using (var reader = ReaderFactory.Open(archiveStream))
        {
            while (await reader.MoveToNextEntryAsync(cancellationToken).ConfigureAwait(false))
            {
                var name = reader.Entry.Key is { } key ? Path.GetFileName(key.TrimEnd('/')) : string.Empty;
                if (reader.Entry.IsDirectory || !IsWantedFile(name))
                {
                    continue;
                }

                if (extracted.Contains(name, StringComparer.Ordinal))
                {
                    throw new InvalidDataException($"The archive contains more than one '{name}'.");
                }

                var target = Path.Combine(destinationDirectory, name);
                await using var output = File.Create(target);
                await reader.WriteEntryToAsync(output, cancellationToken).ConfigureAwait(false);
                extracted.Add(name);
            }
        }

        if (extracted.Count(name => name.EndsWith(ConfigSuffix, StringComparison.OrdinalIgnoreCase)) != 1 ||
            extracted.Count(name => name.EndsWith(ModelSuffix, StringComparison.OrdinalIgnoreCase)) != 1)
        {
            throw new InvalidDataException(
                $"The archive must contain exactly one '{ModelSuffix}' and one '{ConfigSuffix}' file, but contained: " +
                $"{(extracted.Count == 0 ? "neither" : string.Join(", ", extracted))}.");
        }

        File.Delete(archivePath);
    }

    /// <summary>Reports whether a file name is the graph or the voice configuration.</summary>
    private static bool IsWantedFile(string name) =>
        name.EndsWith(ModelSuffix, StringComparison.OrdinalIgnoreCase) ||
        name.EndsWith(ConfigSuffix, StringComparison.OrdinalIgnoreCase);
}
