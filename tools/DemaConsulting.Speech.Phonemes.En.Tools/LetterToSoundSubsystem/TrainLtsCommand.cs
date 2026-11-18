using System.IO.Compression;
using System.Security.Cryptography;
using DemaConsulting.Speech.Phonemes.En.Tools.LexiconSubsystem;

namespace DemaConsulting.Speech.Phonemes.En.Tools.LetterToSoundSubsystem;

/// <summary>
///     The <c>train-lts</c> command: trains the shipped letter-to-sound model from the whole pinned CMUdict and writes
///     the embedded resource, or with <c>--check</c> verifies that the committed resource is what the training produces.
/// </summary>
/// <remarks>
///     <para>
///     <b>Which model ships.</b> The benchmark (<see cref="BenchmarkLtsCommand"/>) measures a model trained on the
///     training part of the stem-family split, so that its numbers are held-out numbers. The shipped model is trained
///     with exactly the same settings (<see cref="LtsTrainingSettings.Shipped"/>) on all the entries, because the held-out
///     words are ordinary dictionary words that make the shipped model slightly better, and nothing about the shipped
///     model is ever evaluated on the words it was trained on. Its image is pinned by SHA-256 in
///     <c>data/phonemes-en/MANIFEST.tsv</c>.
///     </para>
///     <para>
///     <b>Check.</b> Retrains (about ten minutes) and compares the image hash and size with the manifest and the
///     decompressed committed resource with the retrained image (the gzip bytes depend on the runtime and are never
///     compared), and enforces the one-megabyte budget. The training is deterministic (see <see cref="FloatNetwork"/>).
///     </para>
///     <para>Exit codes: <c>0</c> written or current, <c>1</c> stale, <c>2</c> usage, I/O or data error.</para>
/// </remarks>
internal static class TrainLtsCommand
{
    /// <summary>The name of the command as typed on the command line.</summary>
    public const string Name = "train-lts";

    /// <summary>The size budget of the embedded resource in bytes (1 MB).</summary>
    public const long SizeBudgetBytes = 1024 * 1024;

    /// <summary>The name of the model image in the manifest.</summary>
    private const string ImageName = "lts-en.bin";

    /// <summary>The usage text.</summary>
    public const string Usage = """
        Usage: train-lts [options]
               train-lts --check [options]

        Trains the letter-to-sound model on the whole pinned CMUdict (data/phonemes-en/external/cmudict) and writes
        src/DemaConsulting.Speech.Phonemes.En/Resources/lts-en.bin.gz and its pin in data/phonemes-en/MANIFEST.tsv, or
        with --check retrains and verifies both. Training takes about ten minutes and is deterministic.

        Options:
          --root <dir>   The repository root (default: searched upward from the current directory for Speech.slnx).
          --check        Verify instead of write; exit 1 when the resource is stale.
          --help         Shows this text.

        Exit codes: 0 = written or current, 1 = stale, 2 = usage, I/O or data error.
        """;

    /// <summary>
    ///     Runs the command.
    /// </summary>
    /// <param name="args">The arguments after the command name.</param>
    /// <param name="output">Receives progress and results.</param>
    /// <param name="error">Receives usage and I/O errors.</param>
    /// <param name="currentDirectory">The directory relative paths are resolved against and the root is searched from.</param>
    /// <param name="baseDirectory">The tool's own folder, the second place the root is searched from.</param>
    /// <returns>The exit code: 0, 1 or 2.</returns>
    public static int Run(IReadOnlyList<string> args, TextWriter output, TextWriter error, string currentDirectory, string baseDirectory)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        var check = false;
        string? root = null;
        var pending = new Queue<string>(args);
        while (pending.TryDequeue(out var option))
        {
            switch (option)
            {
                case "--help" or "-h":
                    output.WriteLine(Usage);
                    return 0;
                case "--check":
                    check = true;
                    break;
                case "--root" when pending.Count > 0:
                    root = Path.GetFullPath(pending.Dequeue(), currentDirectory);
                    break;
                default:
                    error.WriteLine($"Unknown or incomplete option '{option}'.");
                    error.WriteLine(Usage);
                    return 2;
            }
        }

        try
        {
            root ??= ToolPaths.FindRepositoryRoot(currentDirectory) ?? ToolPaths.FindRepositoryRoot(baseDirectory);
            if (root is null)
            {
                error.WriteLine("The repository root (Speech.slnx) was not found; use --root <dir>.");
                return 2;
            }

            return Execute(root, check, output, error);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or FormatException)
        {
            error.WriteLine(exception.Message);
            return 2;
        }
    }

    /// <summary>
    ///     Trains and writes or checks.
    /// </summary>
    /// <param name="root">The repository root.</param>
    /// <param name="check">Whether to verify instead of write.</param>
    /// <param name="output">Receives the report.</param>
    /// <param name="error">Receives errors.</param>
    /// <returns>The exit code.</returns>
    private static int Execute(string root, bool check, TextWriter output, TextWriter error)
    {
        var entries = LtsCorpus.Load(root);
        var image = LtsTrainer.Train(entries, [], LtsTrainingSettings.Shipped, output.WriteLine);
        var hash = Convert.ToHexStringLower(SHA256.HashData(image));
        var compressed = BenchmarkLtsCommand.Compress(image);
        output.WriteLine($"Image {image.Length} bytes, SHA-256 {hash}; gzip resource {compressed.Length} bytes (budget {SizeBudgetBytes}).");

        var resourcePath = Path.Combine(root, "src", "DemaConsulting.Speech.Phonemes.En", "Resources", "lts-en.bin.gz");
        var manifestPath = Path.Combine(ToolPaths.DataDirectory(root), "MANIFEST.tsv");
        var manifest = DataManifest.Read(manifestPath).ToList();
        var problems = new List<string>();
        if (compressed.Length > SizeBudgetBytes)
        {
            problems.Add($"the resource is {compressed.Length} bytes, over the budget of {SizeBudgetBytes}");
        }

        if (!check)
        {
            if (problems.Count > 0)
            {
                return Report(problems, error, "Not written: ");
            }

            File.WriteAllBytes(resourcePath, compressed);
            manifest.RemoveAll(r => r.Name == ImageName);
            manifest.Add(new ManifestRow(ImageName, "image", hash, image.Length, entries.Count));
            DataManifest.Write(manifestPath, manifest);
            output.WriteLine($"Wrote {resourcePath} and updated MANIFEST.tsv.");
            return 0;
        }

        var recorded = manifest.FirstOrDefault(r => r.Name == ImageName && r.Kind == "image");
        if (recorded is null || recorded.Sha256 != hash || recorded.Bytes != image.Length || recorded.Rows != entries.Count)
        {
            problems.Add("the lts-en.bin row of MANIFEST.tsv is missing or stale");
        }

        if (!File.Exists(resourcePath))
        {
            problems.Add("the committed resource is missing");
        }
        else
        {
            var committed = File.ReadAllBytes(resourcePath);
            if (committed.Length > SizeBudgetBytes)
            {
                problems.Add($"the committed resource is {committed.Length} bytes, over the budget of {SizeBudgetBytes}");
            }

            using var gzip = new GZipStream(new MemoryStream(committed), CompressionMode.Decompress);
            using var decompressed = new MemoryStream();
            gzip.CopyTo(decompressed);
            if (!decompressed.ToArray().AsSpan().SequenceEqual(image))
            {
                problems.Add("the committed resource does not decompress to the image the training produces");
            }
        }

        if (problems.Count == 0)
        {
            output.WriteLine("Check passed: the committed model and MANIFEST.tsv are current.");
            return 0;
        }

        return Report(problems, error, "Stale: ");
    }

    /// <summary>
    ///     Writes the problems and returns the stale exit code.
    /// </summary>
    /// <param name="problems">The problems.</param>
    /// <param name="error">Receives the text.</param>
    /// <param name="prefix">The line prefix.</param>
    /// <returns><c>1</c>.</returns>
    private static int Report(IReadOnlyList<string> problems, TextWriter error, string prefix)
    {
        foreach (var problem in problems)
        {
            error.WriteLine(prefix + problem);
        }

        return 1;
    }
}
