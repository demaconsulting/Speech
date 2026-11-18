using System.Globalization;
using DemaConsulting.Speech.Phonemes.En.LetterToSoundSubsystem;
using DemaConsulting.Speech.Phonemes.En.PhonemizerSubsystem;
using DemaConsulting.Speech.Phonemes.En.RenderingSubsystem;

namespace DemaConsulting.Speech.Phonemes.En.Tools.PhonemizerSubsystem;

/// <summary>
///     The <c>regenerate-goldens</c> command: regenerates the generated columns of the phonemizer golden corpora and
///     reports how well the real text analysis labels the readings corpus.
/// </summary>
/// <remarks>
///     <para>
///     <b>Sentences</b> (<c>test/.../TestData/sentences-golden.tsv</c>): runs every sentence through the whole pipeline
///     (the real text analysis, then the phonemizer) and rewrites the <c>expected</c> column. The result is a
///     regression baseline, not an oracle: a changed line must be reviewed line by line and a wrong pronunciation fixed
///     in the code or data, never accepted into the golden.
///     </para>
///     <para>
///     <b>Readings</b> (<c>readings-golden.tsv</c>): for each reading id the corpus holds one sentence and the target
///     word. The command sets <c>mode</c> to <c>e2e</c> when the real text analysis labels the target with the reading
///     id and to <c>forced</c> when it does not (the golden is never bent to the classifier: a forced case tests the
///     phonemizer with a synthetic annotation instead), reports the classifier's hit rate, and drafts an empty
///     <c>expected</c> from the phonemizer with the reading forced. The draft is then reviewed by hand; a reviewed
///     <c>expected</c> is never overwritten unless <c>--redraft</c> is given.
///     </para>
///     <para>
///     <b>Out-of-vocabulary words</b> (<c>oov-golden.tsv</c>): rewrites the <c>expected</c> column with the shipped
///     letter-to-sound model's pronunciation of each listed word. It pins the model's output to detect weight drift;
///     the listed words are chosen by hand (names, brands, neologisms and compounds that the dictionary lacks) and the
///     output is reviewed, never hand-corrected.
///     </para>
///     <para>Exit codes: <c>0</c> done, <c>2</c> usage or I/O error.</para>
/// </remarks>
internal static class RegenerateGoldensCommand
{
    /// <summary>The name of the command as typed on the command line.</summary>
    public const string Name = "regenerate-goldens";

    /// <summary>The usage text.</summary>
    public const string Usage = """
        Usage: regenerate-goldens [--sentences] [--readings [--redraft]] [--oov] [--renderers] [options]
               regenerate-goldens --try "<sentence>"

        Regenerates the expected column of test/DemaConsulting.Speech.Phonemes.En.Tests/TestData/sentences-golden.tsv
        (--sentences), the mode and drafted expected columns of readings-golden.tsv (--readings), the expected column
        of oov-golden.tsv (--oov) and the Piper and Kokoro renderings piper-ipa-golden.tsv and kokoro-misaki-golden.tsv
        (--renderers), and prints the text analysis' hit rate on the readings corpus. With --try prints the
        analysis and the phonemized result of one sentence. Without --sentences, --readings, --oov and --renderers all
        four corpora are processed.

        Options:
          --root <dir>   The repository root (default: searched upward from the current directory for Speech.slnx).
          --redraft      With --readings: overwrite reviewed expected values with a fresh draft.
          --help         Shows this text.

        Exit codes: 0 = done, 2 = usage or I/O error.
        """;

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
        string? sentence = null;
        var doSentences = false;
        var doReadings = false;
        var doOov = false;
        var doRenderers = false;
        var redraft = false;
        var pending = new Queue<string>(args);
        while (pending.TryDequeue(out var option))
        {
            switch (option)
            {
                case "--help" or "-h":
                    output.WriteLine(Usage);
                    return 0;
                case "--sentences":
                    doSentences = true;
                    break;
                case "--readings":
                    doReadings = true;
                    break;
                case "--oov":
                    doOov = true;
                    break;
                case "--renderers":
                    doRenderers = true;
                    break;
                case "--redraft":
                    redraft = true;
                    break;
                case "--root" when pending.Count > 0:
                    root = Path.GetFullPath(pending.Dequeue(), currentDirectory);
                    break;
                case "--try" when pending.Count > 0:
                    sentence = pending.Dequeue();
                    break;
                default:
                    error.WriteLine($"Unknown or incomplete option '{option}'.");
                    error.WriteLine(Usage);
                    return 2;
            }
        }

        if (sentence is not null)
        {
            Try(sentence, output);
            return 0;
        }

        try
        {
            root ??= ToolPaths.FindRepositoryRoot(currentDirectory) ?? ToolPaths.FindRepositoryRoot(baseDirectory);
            if (root is null)
            {
                error.WriteLine("The repository root (Speech.slnx) was not found; use --root <dir>.");
                return 2;
            }

            var testData = Path.Combine(root, "test", "DemaConsulting.Speech.Phonemes.En.Tests", "TestData");
            var both = !doSentences && !doReadings && !doOov && !doRenderers;
            if (doSentences || both)
            {
                RegenerateSentences(Path.Combine(testData, "sentences-golden.tsv"), output);
            }

            if (doReadings || both)
            {
                RegenerateReadings(Path.Combine(testData, "readings-golden.tsv"), redraft, output);
            }

            if (doOov || both)
            {
                RegenerateOov(Path.Combine(testData, "oov-golden.tsv"), output);
            }

            if (doRenderers || both)
            {
                RegenerateRenderers(root, testData, output);
            }

            return 0;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            error.WriteLine("regenerate-goldens failed: " + exception.Message);
            return 2;
        }
    }

    /// <summary>
    ///     Prints the analysis annotations and the phonemized result of one sentence.
    /// </summary>
    /// <param name="sentence">The sentence.</param>
    /// <param name="output">Receives the report.</param>
    private static void Try(string sentence, TextWriter output)
    {
        output.WriteLine(GoldenCorpus.Format(GoldenCorpus.RunEndToEnd(sentence)));
    }

    /// <summary>
    ///     Rewrites the expected column of the sentences corpus.
    /// </summary>
    /// <param name="path">The corpus file.</param>
    /// <param name="output">Receives the report.</param>
    private static void RegenerateSentences(string path, TextWriter output)
    {
        var cases = GoldenCorpus.ReadSentences(path);
        var updated = cases.Select(c => c with { Expected = GoldenCorpus.Format(GoldenCorpus.RunEndToEnd(c.Sentence)) }).ToList();
        var changed = updated.Where((c, i) => !string.Equals(c.Expected, cases[i].Expected, StringComparison.Ordinal)).Count();
        GoldenCorpus.WriteSentences(path, updated);
        output.WriteLine($"sentences-golden.tsv: {updated.Count} sentences, {changed} expected lines changed or drafted");
    }

    /// <summary>
    ///     Rewrites the expected column of the out-of-vocabulary word corpus from the shipped letter-to-sound model.
    /// </summary>
    /// <param name="path">The corpus file: <c>#</c> comment lines, then <c>word</c> and <c>expected</c> separated by a tab.</param>
    /// <param name="output">Receives the report.</param>
    private static void RegenerateOov(string path, TextWriter output)
    {
        var lines = File.ReadAllLines(path);
        var result = new List<string>(lines.Length);
        var words = 0;
        var changed = 0;
        foreach (var line in lines)
        {
            if (line.Length == 0 || line[0] == '#')
            {
                result.Add(line);
                continue;
            }

            var cells = line.Split('\t');
            LetterToSound.Default.TryConvert(cells[0], out var pronunciation);
            var expected = pronunciation.ToString();
            words++;
            changed += cells.Length < 2 || cells[1] != expected ? 1 : 0;
            result.Add(cells[0] + "\t" + expected);
        }

        File.WriteAllText(path, string.Join('\n', result) + "\n", new System.Text.UTF8Encoding(false));
        output.WriteLine($"oov-golden.tsv: {words} words, {changed} expected values changed or drafted");
    }

    /// <summary>
    ///     Rewrites the renderer golden corpora from the sentences corpus: the Piper IPA and the Kokoro misaki phoneme
    ///     string of every sentence, with the token ids of each.
    /// </summary>
    /// <param name="root">The repository root.</param>
    /// <param name="testData">The test data folder.</param>
    /// <param name="output">Receives the report.</param>
    private static void RegenerateRenderers(string root, string testData, TextWriter output)
    {
        var sentences = GoldenCorpus.ReadSentences(Path.Combine(testData, "sentences-golden.tsv"));
        var piper = SymbolInventory.LoadPiper(Path.Combine(root, "test", "DemaConsulting.Speech.Onnx.Piper.Tests", "TestData", "en_US-libritts_r-medium.onnx.trimmed.json"));
        var kokoro = SymbolInventory.LoadKokoro(Path.Combine(root, "src", "DemaConsulting.Speech.Onnx.Kokoro", "Resources", "kokoro-v1.0-tokenizer.json"));
        var piperRenderer = new EspeakIpaRenderer();
        var kokoroRenderer = new MisakiRenderer();
        WriteRendered(Path.Combine(testData, "piper-ipa-golden.tsv"), sentences, piperRenderer.Render, piper, output);
        WriteRendered(Path.Combine(testData, "kokoro-misaki-golden.tsv"), sentences, kokoroRenderer.Render, kokoro, output);
    }

    /// <summary>
    ///     Writes one renderer golden corpus.
    /// </summary>
    /// <param name="path">The corpus file.</param>
    /// <param name="sentences">The sentences.</param>
    /// <param name="render">The renderer of a phonemized text.</param>
    /// <param name="inventory">The symbol inventory whose ids are recorded.</param>
    /// <param name="output">Receives the report.</param>
    private static void WriteRendered(string path, IReadOnlyList<SentenceCase> sentences, Func<PhonemizedText, string> render, SymbolInventory inventory, TextWriter output)
    {
        var old = File.Exists(path) ? GoldenCorpus.ReadRendered(path).ToDictionary(c => c.Id, c => c.Phonemes, StringComparer.Ordinal) : [];
        var cases = sentences.Select(s =>
        {
            var phonemes = render(GoldenCorpus.RunEndToEnd(s.Sentence));
            return new RenderedCase(s.Id, s.Sentence, phonemes, inventory.ToIds(phonemes));
        }).ToList();
        var changed = cases.Count(c => !old.TryGetValue(c.Id, out var before) || before != c.Phonemes);
        GoldenCorpus.WriteRendered(path, cases);
        output.WriteLine($"{Path.GetFileName(path)}: {cases.Count} sentences, {changed} phoneme lines changed or drafted");
    }

    /// <summary>
    ///     Updates the mode column and drafts the empty expected values of the readings corpus.
    /// </summary>
    /// <param name="path">The corpus file.</param>
    /// <param name="redraft">Whether to overwrite reviewed expected values.</param>
    /// <param name="output">Receives the report.</param>
    private static void RegenerateReadings(string path, bool redraft, TextWriter output)
    {
        var cases = GoldenCorpus.ReadReadings(path);
        var updated = new List<ReadingCase>(cases.Count);
        var hits = 0;
        var misses = new List<string>();
        foreach (var c in cases)
        {
            var classified = GoldenCorpus.ClassifiedReading(c.Sentence, c.Target);
            var hit = string.Equals(classified, c.ReadingId, StringComparison.Ordinal);
            if (hit)
            {
                hits++;
            }
            else
            {
                misses.Add($"{c.ReadingId} (classified as {classified ?? "nothing"})");
            }

            var expected = c.Expected;
            if (redraft || expected.Length == 0)
            {
                expected = GoldenCorpus.FindWord(GoldenCorpus.RunForced(c.Sentence, c.Target, c.ReadingId), c.Target)?.Arpabet ?? string.Empty;
            }

            updated.Add(c with { Mode = hit ? "e2e" : "forced", Expected = expected });
        }

        GoldenCorpus.WriteReadings(path, updated);
        output.WriteLine(string.Create(
            CultureInfo.InvariantCulture,
            $"readings-golden.tsv: {cases.Count} readings; the text analysis labels {hits} correctly ({100.0 * hits / Math.Max(1, cases.Count):F1}%), {misses.Count} are forced"));
        foreach (var miss in misses)
        {
            output.WriteLine("  forced: " + miss);
        }
    }
}
