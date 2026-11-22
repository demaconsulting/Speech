using System.Globalization;
using System.Security.Cryptography;
using DemaConsulting.Speech.AudioSubsystem;

namespace DemaConsulting.Speech.SttEvaluation;

/// <summary>
///     One audio file of the speech-to-text evaluation data, as pinned by one row of
///     <c>data/stt-en/MANIFEST.tsv</c>.
/// </summary>
/// <param name="File">The file path relative to the data folder, with forward slashes (for example <c>librispeech-test-clean/61-70968-0014.flac</c>).</param>
/// <param name="Set">The set the file belongs to (<see cref="SttDataSet.LibriSpeechSet"/> or <see cref="SttDataSet.CrossingTheBarSet"/>).</param>
/// <param name="Sha256">The pinned SHA-256 of the file, lower-case hexadecimal.</param>
/// <param name="Bytes">The pinned file size in bytes.</param>
/// <param name="Seconds">The duration of the recording in seconds, as recorded in the manifest (informational).</param>
/// <param name="License">The SPDX identifier of the file's license (for example <c>CC-BY-4.0</c>).</param>
/// <param name="Source">The URL the file came from.</param>
/// <param name="Reference">The reference transcript, verbatim from the corpus.</param>
/// <param name="FullPath">The absolute path of the verified file on this machine.</param>
internal sealed record SttDataItem(
    string File,
    string Set,
    string Sha256,
    long Bytes,
    double Seconds,
    string License,
    string Source,
    string Reference,
    string FullPath)
{
    /// <summary>
    ///     Gets the identifier of the item for reports: the file name without folder or extension
    ///     (for example <c>61-70968-0014</c>).
    /// </summary>
    public string Id => Path.GetFileNameWithoutExtension(File);

    /// <summary>
    ///     Gets a value indicating whether the reference is written with casing and punctuation, so
    ///     that a verbatim (punctuated) word error rate is meaningful for this item. The LibriSpeech
    ///     transcripts are upper case without punctuation, so this is <see langword="false"/> for
    ///     them; the poem's published text is punctuated.
    /// </summary>
    public bool HasPunctuatedReference => Reference.Any(char.IsLower);
}

/// <summary>
///     Loads and verifies the speech-to-text evaluation data (<c>data/stt-en/</c>): the manifest,
///     the checksum of every audio file and the decoded audio.
/// </summary>
/// <remarks>
///     The evaluation tool and the real-model tests must measure exactly the committed recordings,
///     so loading refuses any file whose size or SHA-256 differs from the manifest rather than
///     silently scoring different audio. This type is owned by the non-shipped
///     <c>DemaConsulting.Speech.SttEvaluation</c> tool and compiled (linked) into the tests that use
///     the same data, so both read the data one way. It uses only public APIs of
///     <c>DemaConsulting.Speech</c>. Instances are immutable after <see cref="Load"/> and safe for
///     concurrent reads.
/// </remarks>
internal sealed class SttDataSet
{
    /// <summary>The file name of the manifest inside the data folder.</summary>
    public const string ManifestFileName = "MANIFEST.tsv";

    /// <summary>The set name of the LibriSpeech test-clean subset.</summary>
    public const string LibriSpeechSet = "librispeech-test-clean";

    /// <summary>The set name of the "Crossing the Bar" poem recitation.</summary>
    public const string CrossingTheBarSet = "crossing-the-bar";

    /// <summary>The number of tab-separated columns of a manifest row.</summary>
    private const int ColumnCount = 8;

    /// <summary>
    ///     Initializes a new instance of the <see cref="SttDataSet"/> class over already-verified items.
    /// </summary>
    /// <param name="directory">The absolute path of the data folder.</param>
    /// <param name="items">The verified items, in manifest order.</param>
    private SttDataSet(string directory, IReadOnlyList<SttDataItem> items)
    {
        Directory = directory;
        Items = items;
    }

    /// <summary>Gets the absolute path of the data folder.</summary>
    public string Directory { get; }

    /// <summary>Gets every item of the manifest, in manifest order.</summary>
    public IReadOnlyList<SttDataItem> Items { get; }

    /// <summary>
    ///     Reads the manifest of a data folder and verifies every file it lists.
    /// </summary>
    /// <param name="directory">The data folder holding <see cref="ManifestFileName"/>. Must not be null or blank.</param>
    /// <returns>The verified data set.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="directory"/> is null or blank.</exception>
    /// <exception cref="FileNotFoundException">Thrown when the manifest or a listed audio file does not exist.</exception>
    /// <exception cref="InvalidDataException">
    ///     Thrown when a manifest row is malformed (wrong column count, a size or duration that is
    ///     not a number, a path that leaves the folder), a file is listed twice, or a file's size
    ///     or SHA-256 does not match its row. The message names the file; the data must be
    ///     restored from version control, not used.
    /// </exception>
    /// <remarks>
    ///     Reads every listed file once to hash it (about 5.5 MB for the committed data). Lines that
    ///     are empty or start with <c>#</c> are comments.
    /// </remarks>
    public static SttDataSet Load(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);

        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));
        var manifestPath = Path.Join(root, ManifestFileName);
        if (!System.IO.File.Exists(manifestPath))
        {
            throw new FileNotFoundException($"The STT data manifest '{manifestPath}' was not found.", manifestPath);
        }

        // Parse every row first, then verify each file against its pins
        var items = new List<SttDataItem>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var line in System.IO.File.ReadLines(manifestPath))
        {
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            var item = ParseRow(root, line);
            if (!seen.Add(item.File))
            {
                throw new InvalidDataException($"'{manifestPath}' lists '{item.File}' more than once.");
            }

            Verify(item);
            items.Add(item);
        }

        return new SttDataSet(root, items);
    }

    /// <summary>
    ///     Decodes an item's audio to mono samples at the requested sample rate.
    /// </summary>
    /// <param name="item">The item to decode. Must not be <see langword="null"/>.</param>
    /// <param name="sampleRate">The sample rate the caller needs (the recognition model's rate), in Hz. Must be positive.</param>
    /// <returns>The mono samples in <c>[-1, 1]</c> at <paramref name="sampleRate"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="item"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="sampleRate"/> is not positive.</exception>
    /// <exception cref="InvalidDataException">Thrown when the file cannot be decoded.</exception>
    /// <remarks>
    ///     Uses the library's own WAV/FLAC reader and resampler, the same code a host uses to feed a
    ///     recording to a recognizer, so the evaluation measures the shipped audio path. The
    ///     committed files are already 16 kHz mono, so for the current models no resampling happens.
    /// </remarks>
    public static float[] LoadSamples(SttDataItem item, int sampleRate)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sampleRate);

        var (samples, fileRate) = AudioFileReader.ReadMono(item.FullPath);
        return fileRate == sampleRate ? samples : AudioResampler.Resample(samples, fileRate, sampleRate);
    }

    /// <summary>
    ///     Gets the items of one set, in manifest order.
    /// </summary>
    /// <param name="set">The set name, for example <see cref="LibriSpeechSet"/>. Must not be <see langword="null"/>.</param>
    /// <returns>The items of the set; empty when the manifest has none.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="set"/> is <see langword="null"/>.</exception>
    public IReadOnlyList<SttDataItem> GetSet(string set)
    {
        ArgumentNullException.ThrowIfNull(set);
        return Items.Where(item => item.Set == set).ToList();
    }

    /// <summary>
    ///     Parses one manifest row into an item whose full path lies inside the data folder.
    /// </summary>
    /// <param name="root">The absolute path of the data folder.</param>
    /// <param name="line">The non-comment manifest line.</param>
    /// <returns>The parsed (not yet verified) item.</returns>
    /// <exception cref="InvalidDataException">Thrown when the row is malformed.</exception>
    private static SttDataItem ParseRow(string root, string line)
    {
        var columns = line.Split('\t');
        if (columns.Length != ColumnCount)
        {
            throw new InvalidDataException(
                $"A manifest row must have {ColumnCount} tab-separated columns but has {columns.Length}: '{line}'.");
        }

        if (!long.TryParse(columns[3], NumberStyles.None, CultureInfo.InvariantCulture, out var bytes) ||
            !double.TryParse(columns[4], NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var seconds))
        {
            throw new InvalidDataException($"The size or duration of manifest row '{columns[0]}' is not a number.");
        }

        // A manifest path must name a file inside the data folder; anything else is a corrupt row
        var fullPath = Path.GetFullPath(Path.Join(root, columns[0]));
        if (!fullPath.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"The manifest path '{columns[0]}' leaves the data folder.");
        }

        return new SttDataItem(
            columns[0],
            columns[1],
            columns[2],
            bytes,
            seconds,
            columns[5],
            columns[6],
            columns[7],
            fullPath);
    }

    /// <summary>
    ///     Verifies that an item's file exists and matches its pinned size and SHA-256.
    /// </summary>
    /// <param name="item">The item to verify.</param>
    /// <exception cref="FileNotFoundException">Thrown when the file does not exist.</exception>
    /// <exception cref="InvalidDataException">Thrown when the size or the SHA-256 differs from the pins.</exception>
    private static void Verify(SttDataItem item)
    {
        if (!System.IO.File.Exists(item.FullPath))
        {
            throw new FileNotFoundException($"The STT data file '{item.FullPath}' was not found.", item.FullPath);
        }

        // Compare the cheap size first, then the content hash
        var length = new FileInfo(item.FullPath).Length;
        if (length != item.Bytes)
        {
            throw new InvalidDataException(
                $"The STT data file '{item.File}' has {length} bytes but the manifest pins {item.Bytes}.");
        }

        using var stream = System.IO.File.OpenRead(item.FullPath);
        var hash = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
        if (!string.Equals(hash, item.Sha256, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"The STT data file '{item.File}' has SHA-256 {hash} but the manifest pins {item.Sha256}.");
        }
    }
}
