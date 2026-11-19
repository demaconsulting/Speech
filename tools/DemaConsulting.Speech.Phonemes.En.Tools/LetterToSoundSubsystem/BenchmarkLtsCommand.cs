using System.Globalization;
using System.IO.Compression;
using DemaConsulting.Speech.Phonemes.En.LetterToSoundSubsystem;
using DemaConsulting.Speech.Phonemes.En.LexiconSubsystem;

namespace DemaConsulting.Speech.Phonemes.En.Tools.LetterToSoundSubsystem;

/// <summary>
///     The <c>benchmark-lts</c> command: the decisive, reproducible out-of-vocabulary benchmark of the rules baseline,
///     the trained model variants and (optionally) an external system's predictions, on the stem-family held-out
///     split of the pinned CMUdict.
/// </summary>
/// <remarks>
///     <para>
///     For every requested variant the command trains a model on the training part of the split only (see
///     <see cref="LtsCorpus"/>), serializes it, loads it back through the library's integer runtime
///     (<see cref="LtsModel"/>, exactly what ships) and scores it on the held-out words together with the rules
///     baseline (<see cref="NrlRules"/>). <c>--g2p-en</c> adds the predictions of the g2p_en checkpoint, produced by a
///     separate local script that lives outside the repository, for the same words (<c>--export-test-words</c> writes
///     the list the script needs). <c>--write</c> writes <c>docs/evaluation/phonemes-en/oov.tsv</c>; the numbers are
///     deterministic, the timing printed to the console is not committed.
///     </para>
///     <para>Exit codes: <c>0</c> done, <c>2</c> a usage, I/O or data error.</para>
/// </remarks>
internal static class BenchmarkLtsCommand
{
    /// <summary>The name of the command as typed on the command line.</summary>
    public const string Name = "benchmark-lts";

    /// <summary>The usage text.</summary>
    public const string Usage = """
        Usage: benchmark-lts [--variants small,shipped,large] [--g2p-en <predictions.tsv>] [--g2p-en-bytes <n>] [--write] [--examples <n>] [options]
               benchmark-lts --export-test-words <file>

        Trains the letter-to-sound variants on the training part of the stem-family split of the pinned CMUdict, scores
        them and the rules baseline on the held-out test part, and prints the table. --write also writes
        docs/evaluation/phonemes-en/oov.tsv. --g2p-en scores external predictions (a TSV of word and ARPAbet phones per
        test word). --export-test-words writes the held-out words, one per line, and exits.

        Options:
          --root <dir>   The repository root (default: searched upward from the current directory for Speech.slnx).
          --help         Shows this text.

        Exit codes: 0 = done, 2 = usage, I/O or data error.
        """;

    /// <summary>The name of the rules candidate.</summary>
    private const string RulesName = "rules-nrl";

    /// <summary>
    ///     Runs the command.
    /// </summary>
    /// <param name="args">The arguments after the command name.</param>
    /// <param name="output">Receives the report.</param>
    /// <param name="error">Receives usage and I/O errors.</param>
    /// <param name="currentDirectory">The directory relative paths are resolved against and the root is searched from.</param>
    /// <param name="baseDirectory">The tool's own folder, the second place the root is searched from.</param>
    /// <returns>The exit code: 0 or 2.</returns>
    public static int Run(IReadOnlyList<string> args, TextWriter output, TextWriter error, string currentDirectory, string baseDirectory)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        string? root = null;
        string? predictions = null;
        string? exportPath = null;
        long predictionsBytes = 0;
        var examples = 0;
        var variants = new List<string> { "shipped" };
        var write = false;
        var pending = new Queue<string>(args);
        while (pending.TryDequeue(out var option))
        {
            switch (option)
            {
                case "--help" or "-h":
                    output.WriteLine(Usage);
                    return 0;
                case "--write":
                    write = true;
                    break;
                case "--examples" when pending.Count > 0 && int.TryParse(pending.Peek(), NumberStyles.None, CultureInfo.InvariantCulture, out examples):
                    pending.Dequeue();
                    break;
                case "--root" when pending.Count > 0:
                    root = Path.GetFullPath(pending.Dequeue(), currentDirectory);
                    break;
                case "--variants" when pending.Count > 0:
                    variants = [.. pending.Dequeue().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];
                    break;
                case "--g2p-en" when pending.Count > 0:
                    predictions = Path.GetFullPath(pending.Dequeue(), currentDirectory);
                    break;
                case "--g2p-en-bytes" when pending.Count > 0 && long.TryParse(pending.Peek(), NumberStyles.None, CultureInfo.InvariantCulture, out predictionsBytes):
                    pending.Dequeue();
                    break;
                case "--export-test-words" when pending.Count > 0:
                    exportPath = Path.GetFullPath(pending.Dequeue(), currentDirectory);
                    break;
                default:
                    error.WriteLine($"Unknown or incomplete option '{option}'.");
                    error.WriteLine(Usage);
                    return 2;
            }
        }

        var unknown = variants.FirstOrDefault(v => !LtsTrainingSettings.Variants.ContainsKey(v));
        if (unknown is not null)
        {
            error.WriteLine($"Unknown variant '{unknown}'; known: {string.Join(", ", LtsTrainingSettings.Variants.Keys)}.");
            return 2;
        }

        try
        {
            root ??= ToolPaths.FindRepositoryRoot(currentDirectory) ?? ToolPaths.FindRepositoryRoot(baseDirectory);
            if (root is null)
            {
                error.WriteLine("The repository root (Speech.slnx) was not found; use --root <dir>.");
                return 2;
            }

            var split = LtsCorpus.Split(LtsCorpus.Load(root));
            output.WriteLine($"Split: {split.Families} stem families; {split.Train.Count} training, {split.Validation.Count} validation, {split.Test.Count} test words.");
            if (exportPath is not null)
            {
                File.WriteAllLines(exportPath, split.Test.Select(e => e.Word));
                output.WriteLine($"Wrote {split.Test.Count} test words to {exportPath}.");
                return 0;
            }

            return Benchmark(root, split, variants, predictions, predictionsBytes, write, examples, output);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or FormatException)
        {
            error.WriteLine(exception.Message);
            return 2;
        }
    }

    /// <summary>
    ///     Compresses an image with gzip at the smallest size.
    /// </summary>
    /// <param name="image">The image.</param>
    /// <returns>The gzip bytes.</returns>
    internal static byte[] Compress(byte[] image)
    {
        using var stream = new MemoryStream();
        using (var gzip = new GZipStream(stream, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            gzip.Write(image);
        }

        return stream.ToArray();
    }

    /// <summary>
    ///     Trains, scores and reports.
    /// </summary>
    /// <param name="root">The repository root.</param>
    /// <param name="split">The split.</param>
    /// <param name="variants">The variants to train.</param>
    /// <param name="predictionsPath">The external predictions, or <see langword="null"/>.</param>
    /// <param name="predictionsBytes">The size of the external model, for the table.</param>
    /// <param name="write">Whether to write the table.</param>
    /// <param name="examples">The number of mismatch examples of the last model to print.</param>
    /// <param name="output">Receives the report.</param>
    /// <returns>The exit code.</returns>
    private static int Benchmark(string root, LtsSplit split, List<string> variants, string? predictionsPath, long predictionsBytes, bool write, int examples, TextWriter output)
    {
        var scores = new List<LtsScore>();
        var sizes = new Dictionary<string, long>(StringComparer.Ordinal) { [RulesName] = 0 };
        scores.Add(LtsBenchmark.Score(RulesName, split.Test, NrlRules.Convert));
        output.WriteLine($"Rules baseline: {NrlRules.RuleCount} rules.");

        LtsModel? lastModel = null;
        foreach (var variant in variants)
        {
            output.WriteLine($"Training variant '{variant}' on the training split...");
            var image = LtsTrainer.Train(split.Train, split.Validation, LtsTrainingSettings.Variants[variant], output.WriteLine);
            var model = LtsModel.FromImage(image);
            lastModel = model;
            var name = "model-" + variant;
            scores.Add(LtsBenchmark.Score(name, split.Test, w => model.Convert(w)));
            sizes[name] = Compress(image).Length;
        }

        if (predictionsPath is not null)
        {
            var predicted = File.ReadLines(predictionsPath)
                .Select(l => l.Split('\t'))
                .Where(c => c.Length == 2)
                .ToDictionary(c => c[0], c => Pronunciation.Parse(c[1]), StringComparer.Ordinal);
            scores.Add(LtsBenchmark.Score("g2p_en", split.Test, w => predicted.GetValueOrDefault(w, Pronunciation.Empty)));
            sizes["g2p_en"] = predictionsBytes;
        }

        output.WriteLine("candidate\twords\tPER with stress\tPER without stress\tword accuracy\tword accuracy no stress\tprimary position\tprimary position 2+ syllables\tms per word");
        foreach (var s in scores)
        {
            output.WriteLine(string.Create(CultureInfo.InvariantCulture, $"{s.Candidate}\t{s.Words}\t{s.PerWithStress:F2}\t{s.PerWithoutStress:F2}\t{s.WordAccuracyWithStress:F2}\t{s.WordAccuracyWithoutStress:F2}\t{s.PrimaryStressAll:F2}\t{s.PrimaryStressMulti:F2}\t{s.MillisecondsPerWord:F4}"));
        }

        if (write)
        {
            var path = Path.Combine(root, "docs", "evaluation", "phonemes-en", "oov.tsv");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, LtsBenchmark.ToTsv(scores, sizes), new System.Text.UTF8Encoding(false));
            output.WriteLine($"Wrote {path}.");
        }

        if (examples > 0 && lastModel is not null)
        {
            output.WriteLine("Sample of mismatches of the last model (word, closest dictionary variant, model):");
            var step = Math.Max(1, split.Test.Count / (examples * 3));
            var shown = 0;
            for (var i = 0; i < split.Test.Count && shown < examples; i += step)
            {
                var entry = split.Test[i];
                var hypothesis = lastModel.Convert(entry.Word);
                var best = entry.Pronunciations.MinBy(v => LtsBenchmark.Distance(hypothesis, v, withStress: true))!;
                if (!hypothesis.Equals(best))
                {
                    output.WriteLine($"  {entry.Word}\t{best}\t{hypothesis}");
                    shown++;
                }
            }
        }

        return 0;
    }
}
