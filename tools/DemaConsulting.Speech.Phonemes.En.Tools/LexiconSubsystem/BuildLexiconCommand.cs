using System.IO.Compression;
using System.Security.Cryptography;
using DemaConsulting.Speech.Phonemes.En.LexiconSubsystem;

namespace DemaConsulting.Speech.Phonemes.En.Tools.LexiconSubsystem;

/// <summary>
///     The <c>build-lexicon</c> command: derives the embedded lexicon resource from the pinned CMUdict, and with
///     <c>--check</c> verifies that the committed resource is what the build produces.
/// </summary>
/// <remarks>
///     <para>
///     <b>Build.</b> Reads <c>data/phonemes-en/external/cmudict/cmudict.dict</c>, requires its SHA-256 to be the
///     one pinned by <c>data/phonemes-en/MANIFEST.tsv</c>, parses and filters it (see <see cref="CmuDictSource"/>),
///     writes the lexicon image, round-trips it (every entry must read back identical through
///     <see cref="CmuLexicon"/> and be found by <see cref="CmuLexicon.Lookup"/>), compresses it with gzip into the
///     embedded resource and records the image's hash in the manifest.
///     </para>
///     <para>
///     <b>Check.</b> Does the same without writing and compares everything that could make the resource stale: the
///     source hash, the image hash, size and word count recorded in the manifest, the committed resource as an
///     uncompressed image (the compressed bytes depend on the runtime's deflate implementation and are never
///     compared), and the size budget of <see cref="SizeBudgetBytes"/> for the committed resource.
///     </para>
///     <para>
///     Exit codes: <c>0</c> success or a current resource, <c>1</c> a stale or missing resource or a failed
///     round trip, <c>2</c> a usage, I/O or data error. The command is a pure function of its arguments, writers and
///     directories, so it can be run in memory.
///     </para>
/// </remarks>
internal static class BuildLexiconCommand
{
    /// <summary>The name of the command as typed on the command line.</summary>
    public const string Name = "build-lexicon";

    /// <summary>The size budget of the embedded resource in bytes (1 MB).</summary>
    public const long SizeBudgetBytes = 1024 * 1024;

    /// <summary>The name of the pinned input in the manifest.</summary>
    private const string InputName = "cmudict.dict";

    /// <summary>The name of the lexicon image in the manifest.</summary>
    private const string ImageName = "cmudict-en.bin";

    /// <summary>The usage text.</summary>
    public const string Usage = """
        Usage: build-lexicon [options]
               build-lexicon --check [options]

        Builds src/DemaConsulting.Speech.Phonemes.En/Resources/cmudict-en.bin.gz from the pinned CMUdict in
        data/phonemes-en/external/cmudict/cmudict.dict (fetch it first, see data/phonemes-en/README.md), or with
        --check verifies that the committed resource, and the image hash in data/phonemes-en/MANIFEST.tsv, are what
        the build produces.

        Options:
          --root <dir>   The repository root (default: searched upward from the current directory for Speech.slnx).
          --check        Verify instead of write; exit 1 when the resource is stale.
          --help         Shows this text.

        Exit codes: 0 = built or current, 1 = stale or round trip failed, 2 = usage, I/O or data error.
        """;

    /// <summary>
    ///     Runs the command.
    /// </summary>
    /// <param name="args">The arguments after the command name.</param>
    /// <param name="output">Receives progress and results.</param>
    /// <param name="error">Receives usage and I/O errors.</param>
    /// <param name="currentDirectory">The directory relative paths are resolved against and the repository root is searched from.</param>
    /// <param name="baseDirectory">The tool's own folder, the second place the repository root is searched from.</param>
    /// <returns>The exit code: 0, 1 or 2.</returns>
    public static int Run(IReadOnlyList<string> args, TextWriter output, TextWriter error, string currentDirectory, string baseDirectory)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        // Parse the options
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
    ///     Builds or checks the resource.
    /// </summary>
    /// <param name="root">The repository root.</param>
    /// <param name="check">Whether to verify instead of write.</param>
    /// <param name="output">Receives the report.</param>
    /// <param name="error">Receives errors.</param>
    /// <returns>The exit code.</returns>
    private static int Execute(string root, bool check, TextWriter output, TextWriter error)
    {
        var dataDirectory = ToolPaths.DataDirectory(root);
        var sourcePath = Path.Combine(dataDirectory, "external", "cmudict", "cmudict.dict");
        var manifestPath = Path.Combine(dataDirectory, "MANIFEST.tsv");
        var resourcePath = ToolPaths.LexiconResourcePath(root);

        // The source must be the pinned file
        var manifest = DataManifest.Read(manifestPath).ToList();
        var pinned = manifest.FirstOrDefault(r => r.Name == InputName && r.Kind == "input")
            ?? throw new InvalidDataException("MANIFEST.tsv has no input row for " + InputName + ".");
        if (!File.Exists(sourcePath))
        {
            error.WriteLine($"{sourcePath} was not found; fetch CMUdict first (see data/phonemes-en/README.md).");
            return 2;
        }

        var sourceBytes = File.ReadAllBytes(sourcePath);
        var sourceHash = Convert.ToHexStringLower(SHA256.HashData(sourceBytes));
        if (sourceHash != pinned.Sha256 || sourceBytes.Length != pinned.Bytes)
        {
            error.WriteLine($"{InputName} does not match the pin in MANIFEST.tsv (SHA-256 {sourceHash}, expected {pinned.Sha256}).");
            return 2;
        }

        // Derive the image and prove that it reads back identical
        var parsed = CmuDictSource.Parse(File.ReadLines(sourcePath, System.Text.Encoding.UTF8));
        var image = LexiconImageWriter.Write(parsed.Entries);
        var imageHash = Convert.ToHexStringLower(SHA256.HashData(image));
        var problems = new List<string>();
        problems.AddRange(RoundTrip(parsed.Entries, CmuLexicon.FromImage(image)));
        var compressed = Compress(image);

        var variants = parsed.Entries.Sum(e => e.Pronunciations.Count);
        output.WriteLine($"CMUdict lines: {parsed.SourceLines}; kept (a-z and apostrophe words): {parsed.KeptLines}; identical repeats dropped: {parsed.DroppedRepeats}");
        output.WriteLine($"Words: {parsed.Entries.Count}; pronunciations: {variants}");
        output.WriteLine($"Image: {image.Length} bytes, SHA-256 {imageHash}; gzip resource: {compressed.Length} bytes (budget {SizeBudgetBytes})");
        output.WriteLine(problems.Count == 0
            ? $"Round trip: all {parsed.Entries.Count} entries read back identical and are found by Lookup."
            : $"Round trip FAILED for {problems.Count} entries.");
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
            manifest.Add(new ManifestRow(ImageName, "image", imageHash, image.Length, parsed.Entries.Count));
            DataManifest.Write(manifestPath, manifest);
            output.WriteLine($"Wrote {resourcePath} and updated MANIFEST.tsv.");
            return 0;
        }

        // Check: the manifest's image row and the committed resource must be what the build produces
        var recorded = manifest.FirstOrDefault(r => r.Name == ImageName && r.Kind == "image");
        if (recorded is null || recorded.Sha256 != imageHash || recorded.Bytes != image.Length || recorded.Rows != parsed.Entries.Count)
        {
            problems.Add("the image row of MANIFEST.tsv is missing or stale");
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

            if (!Decompress(committed).AsSpan().SequenceEqual(image))
            {
                problems.Add("the committed resource does not decompress to the image the build produces");
            }
        }

        if (problems.Count == 0)
        {
            output.WriteLine("Check passed: the committed resource and MANIFEST.tsv are current.");
            return 0;
        }

        return Report(problems, error, "Stale: ");
    }

    /// <summary>
    ///     Compares every parsed entry with what the lexicon reads back and finds by lookup.
    /// </summary>
    /// <param name="expected">The parsed entries.</param>
    /// <param name="lexicon">The lexicon loaded from the image.</param>
    /// <returns>A description of every entry that does not round-trip (empty when all do).</returns>
    internal static IReadOnlyList<string> RoundTrip(IReadOnlyList<LexiconEntry> expected, CmuLexicon lexicon)
    {
        var problems = new List<string>();
        if (lexicon.WordCount != expected.Count)
        {
            problems.Add($"word count {lexicon.WordCount} instead of {expected.Count}");
        }

        var index = 0;
        foreach (var entry in lexicon.Entries())
        {
            if (index >= expected.Count)
            {
                break;
            }

            var want = expected[index++];
            if (entry.Word != want.Word || !entry.Pronunciations.SequenceEqual(want.Pronunciations))
            {
                problems.Add($"enumeration differs at '{want.Word}'");
            }

            // The binary-search lookup must find every word with the same variants
            if (!lexicon.Lookup(want.Word).SequenceEqual(want.Pronunciations))
            {
                problems.Add($"lookup differs for '{want.Word}'");
            }
        }

        return problems;
    }

    /// <summary>
    ///     Compresses an image with gzip at the smallest size.
    /// </summary>
    /// <param name="image">The image.</param>
    /// <returns>The gzip bytes.</returns>
    private static byte[] Compress(byte[] image)
    {
        using var stream = new MemoryStream();
        using (var gzip = new GZipStream(stream, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            gzip.Write(image);
        }

        return stream.ToArray();
    }

    /// <summary>
    ///     Decompresses gzip bytes.
    /// </summary>
    /// <param name="compressed">The gzip bytes.</param>
    /// <returns>The decompressed bytes.</returns>
    private static byte[] Decompress(byte[] compressed)
    {
        using var gzip = new GZipStream(new MemoryStream(compressed), CompressionMode.Decompress);
        using var result = new MemoryStream();
        gzip.CopyTo(result);
        return result.ToArray();
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
        foreach (var problem in problems.Take(20))
        {
            error.WriteLine(prefix + problem);
        }

        return 1;
    }
}
