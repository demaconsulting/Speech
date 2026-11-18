using System.Globalization;

namespace DemaConsulting.Speech.Phonemes.En.Tools.LexiconSubsystem;

/// <summary>
///     One row of <c>data/phonemes-en/MANIFEST.tsv</c>: a pinned input or a derived image.
/// </summary>
/// <param name="Name">The artifact name (<c>cmudict.dict</c>, <c>cmudict-en.bin</c>).</param>
/// <param name="Kind"><c>input</c> for a fetched file that is never committed, or <c>image</c> for the uncompressed content of a committed resource.</param>
/// <param name="Sha256">The lower-case hexadecimal SHA-256 of the artifact's bytes.</param>
/// <param name="Bytes">The artifact's size in bytes.</param>
/// <param name="Rows">The number of lines of an input, or of words of an image.</param>
internal sealed record ManifestRow(string Name, string Kind, string Sha256, long Bytes, int Rows);

/// <summary>
///     Reads and writes <c>MANIFEST.tsv</c>, the file that pins the CMUdict input and the lexicon image by hash.
/// </summary>
/// <remarks>
///     The format mirrors the Text.En manifest: <c>#</c> comment lines, then tab-separated rows of
///     <c>name, kind, sha256, bytes, rows</c>, UTF-8, LF line endings and a final newline. The compressed resource
///     is deliberately not pinned: its bytes depend on the runtime's deflate implementation, while the image
///     inside it does not.
/// </remarks>
internal static class DataManifest
{
    /// <summary>The comment lines written at the top of the manifest.</summary>
    private static readonly string[] Header =
    [
        "# Pins of the Phonemes.En data builds, verified by 'build-lexicon --check' and 'train-lts --check' of the Phonemes.En tools; the local caches external/ and work/ and the compressed resources are not listed",
        "# kind = input (fetched, never committed) or image (the uncompressed content of the gzip resource of the same name in src/DemaConsulting.Speech.Phonemes.En/Resources/: cmudict-en.bin.gz or lts-en.bin.gz)",
        "# columns = name, kind, sha256, bytes, rows (lines of an input, words of an image: the dictionary words of the lexicon, the dictionary entries the model was trained on)",
    ];

    /// <summary>
    ///     Reads the manifest.
    /// </summary>
    /// <param name="path">The manifest path.</param>
    /// <returns>The rows in file order.</returns>
    /// <exception cref="InvalidDataException">Thrown when a row is malformed.</exception>
    public static IReadOnlyList<ManifestRow> Read(string path)
    {
        var rows = new List<ManifestRow>();
        foreach (var line in File.ReadAllLines(path))
        {
            if (line.Length == 0 || line[0] == '#')
            {
                continue;
            }

            var cells = line.Split('\t');
            if (cells.Length != 5
                || !long.TryParse(cells[3], NumberStyles.None, CultureInfo.InvariantCulture, out var bytes)
                || !int.TryParse(cells[4], NumberStyles.None, CultureInfo.InvariantCulture, out var count))
            {
                throw new InvalidDataException("Malformed manifest row: '" + line + "'.");
            }

            rows.Add(new ManifestRow(cells[0], cells[1], cells[2], bytes, count));
        }

        return rows;
    }

    /// <summary>
    ///     Writes the manifest.
    /// </summary>
    /// <param name="path">The manifest path.</param>
    /// <param name="rows">The rows.</param>
    public static void Write(string path, IEnumerable<ManifestRow> rows)
    {
        var lines = Header.Concat(rows.Select(r => string.Join('\t', r.Name, r.Kind, r.Sha256, r.Bytes.ToString(CultureInfo.InvariantCulture), r.Rows.ToString(CultureInfo.InvariantCulture))));
        File.WriteAllText(path, string.Join('\n', lines) + "\n", new System.Text.UTF8Encoding(false));
    }
}
