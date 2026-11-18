using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using DemaConsulting.Speech.Phonemes.En.LexiconSubsystem;
using DemaConsulting.Speech.Phonemes.En.PhonemizerSubsystem;
using DemaConsulting.Speech.Phonemes.En.RenderingSubsystem;
using DemaConsulting.Speech.Phonemes.En.Tools.PhonemizerSubsystem;

namespace DemaConsulting.Speech.Phonemes.En.Tools.RenderingSubsystem;

/// <summary>
///     The <c>measure-oracle</c> command: exports our renderings of the measurement corpora so a script kept outside the
///     repository can compare them with the reference engines on this machine.
/// </summary>
/// <remarks>
///     <para>
///     The corpora are the sentences of <c>sentences-golden.tsv</c>, the own-written sentences of
///     <c>data/phonemes-en/renderer-corpus.tsv</c> and a word list: the most frequent dictionary words of the
///     English text-analysis training sentences (a deterministic sample, by count then alphabetically). Every sentence runs
///     through the whole pipeline (real text analysis, then the phonemizer) and each item is written with its Piper IPA
///     (with and without the weak forms) and its Kokoro misaki symbols to <c>ours.jsonl</c>, one sentence or word per line.
///     </para>
///     <para>
///     The command writes nothing but that file, and refuses a folder inside the repository other than the git-ignored
///     <c>data/phonemes-en/work</c>: the reference engines' outputs and the comparison results never enter the
///     repository. It does not run the engines and has no dependency on them.
///     </para>
///     <para>Exit codes: <c>0</c> done, <c>2</c> usage or I/O error.</para>
/// </remarks>
internal static class MeasureOracleCommand
{
    /// <summary>The name of the command as typed on the command line.</summary>
    public const string Name = "measure-oracle";

    /// <summary>The usage text.</summary>
    public const string Usage = """
        Usage: measure-oracle [--out <dir>] [--words <count>] [--root <dir>] [--sentences <file>]

        Exports our Piper IPA and Kokoro misaki renderings of the measurement corpora (the golden sentences, the own-written
        sentences of data/phonemes-en/renderer-corpus.tsv and the most frequent dictionary words of the text-analysis
        training sentences) to <dir>/ours.jsonl for the comparison script that is kept outside the repository.

        Options:
          --sentences <file>
                           Export only the sentences of this tab-separated file (a header line, then an id and a sentence per line)
                           to <dir>/ours-custom.jsonl, for ad hoc comparisons of a set of sentences.
          --out <dir>      Output folder (default: a DemaConsulting.Speech.Phonemes.En.Oracle folder in the temporary directory,
                           outside the repository). Inside the repository only the git-ignored data/phonemes-en/work is allowed.
          --words <count>  Size of the word-list pass (default: 3000).
          --word-offset <n>
                           Skip the n most frequent words (default: 0). With an offset the output is ours-validation.jsonl and holds
                           the held-out passes: the words after the skipped ones and the own-written sentences of
                           data/phonemes-en/renderer-validation.tsv, text nobody tuned the rules on.
          --root <dir>     The repository root (default: searched upward from the current directory for Speech.slnx).
          --help           Shows this text.

        Exit codes: 0 = done, 2 = usage or I/O error.
        """;

    /// <summary>The default number of words of the word-list pass.</summary>
    private const int DefaultWords = 3000;

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
        string? outDirectory = null;
        var words = DefaultWords;
        var offset = 0;
        string? sentencesFile = null;
        var pending = new Queue<string>(args);
        while (pending.TryDequeue(out var option))
        {
            switch (option)
            {
                case "--help" or "-h":
                    output.WriteLine(Usage);
                    return 0;
                case "--root" when pending.Count > 0:
                    root = Path.GetFullPath(pending.Dequeue(), currentDirectory);
                    break;
                case "--out" when pending.Count > 0:
                    outDirectory = Path.GetFullPath(pending.Dequeue(), currentDirectory);
                    break;
                case "--words" when pending.Count > 0 && int.TryParse(pending.Peek(), out var count) && count >= 0:
                    words = count;
                    pending.Dequeue();
                    break;
                case "--sentences" when pending.Count > 0:
                    sentencesFile = Path.GetFullPath(pending.Dequeue(), currentDirectory);
                    break;
                case "--word-offset" when pending.Count > 0 && int.TryParse(pending.Peek(), out var skip) && skip >= 0:
                    offset = skip;
                    pending.Dequeue();
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

            var work = Path.Combine(root, "data", "phonemes-en", "work");
            outDirectory ??= Path.Combine(Path.GetTempPath(), "DemaConsulting.Speech.Phonemes.En.Oracle");
            if (IsInside(outDirectory, root) && !IsInside(outDirectory, work))
            {
                error.WriteLine("The output folder is inside the repository but not in the git-ignored data/phonemes-en/work: refused.");
                return 2;
            }

            Directory.CreateDirectory(outDirectory);
            var outputName = "ours.jsonl";
            if (sentencesFile is not null)
            {
                outputName = "ours-custom.jsonl";
            }
            else if (offset > 0)
            {
                outputName = "ours-validation.jsonl";
            }

            var path = Path.Combine(outDirectory, outputName);
            var lines = Export(root, words, offset, path, sentencesFile);
            output.WriteLine($"{path}: {lines} lines (sentences and words)");
            return 0;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            error.WriteLine("measure-oracle failed: " + exception.Message);
            return 2;
        }
    }

    /// <summary>
    ///     Gets the most frequent dictionary words of the text-analysis training sentences.
    /// </summary>
    /// <param name="trainPath">The training file (tab-separated, sentence in the fourth column, tokens separated by spaces).</param>
    /// <param name="count">The number of words.</param>
    /// <returns>The words, most frequent first.</returns>
    internal static IReadOnlyList<string> FrequentWords(string trainPath, int count)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        var pattern = new Regex("[a-z]+(?:'[a-z]+)?", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        foreach (var line in File.ReadLines(trainPath).Skip(1))
        {
            var fields = line.Split('\t');
            if (fields.Length < 4)
            {
                continue;
            }

            foreach (var match in pattern.Matches(fields[3].ToLowerInvariant()).Select(m => m.Value))
            {
                counts[match] = counts.GetValueOrDefault(match) + 1;
            }
        }

        return [.. counts
            .Where(c => CmuLexicon.Default.Contains(c.Key))
            .OrderByDescending(c => c.Value)
            .ThenBy(c => c.Key, StringComparer.Ordinal)
            .Take(count)
            .Select(c => c.Key)];
    }

    /// <summary>
    ///     Tests whether a path is inside a folder.
    /// </summary>
    /// <param name="path">The path.</param>
    /// <param name="folder">The folder.</param>
    /// <returns><see langword="true"/> when the path is the folder or below it.</returns>
    private static bool IsInside(string path, string folder)
    {
        var relative = Path.GetRelativePath(folder, path);
        return relative != ".." && !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) && !Path.IsPathRooted(relative);
    }

    /// <summary>
    ///     Writes the export.
    /// </summary>
    /// <param name="root">The repository root.</param>
    /// <param name="words">The size of the word-list pass.</param>
    /// <param name="offset">The number of most frequent words to skip; with an offset the held-out passes are exported instead of the tuning corpora.</param>
    /// <param name="path">The output file.</param>
    /// <param name="sentencesFile">A file of sentences to export instead of the corpora (and no words), or <see langword="null"/>.</param>
    /// <returns>The number of lines.</returns>
    private static int Export(string root, int words, int offset, string path, string? sentencesFile)
    {
        var testData = Path.Combine(root, "test", "DemaConsulting.Speech.Phonemes.En.Tests", "TestData");

        // Choose the sentence source: an ad hoc file, the held-out validation sentences, or the tuning corpora
        IEnumerable<SentenceCase> sentenceCases;
        if (sentencesFile is not null)
        {
            sentenceCases = GoldenCorpus.ReadSentences(sentencesFile);
        }
        else if (offset > 0)
        {
            sentenceCases = GoldenCorpus.ReadSentences(Path.Combine(root, "data", "phonemes-en", "renderer-validation.tsv"));
        }
        else
        {
            sentenceCases = GoldenCorpus.ReadSentences(Path.Combine(testData, "sentences-golden.tsv"))
                .Concat(GoldenCorpus.ReadSentences(Path.Combine(root, "data", "phonemes-en", "renderer-corpus.tsv")));
        }

        List<(string Kind, string Id, PhonemizedText Text)> sentences =
            [.. sentenceCases.Select(s => (Kind: "sentence", s.Id, Text: GoldenCorpus.RunEndToEnd(s.Sentence)))];
        var phonemizer = new EnglishPhonemizer();
        List<(string Kind, string Id, PhonemizedText Text)> wordList = [];
        if (sentencesFile is null)
        {
            wordList =
            [.. FrequentWords(Path.Combine(root, "data", "text-en", "train.tsv"), words + offset)
                .Skip(offset)
                .Select((w, i) => (Kind: "word", Id: "w" + (offset + i + 1).ToString("D4", System.Globalization.CultureInfo.InvariantCulture), Text: phonemizer.Phonemize(w)))];
        }

        var piper = new EspeakIpaRenderer();
        var piperFull = new EspeakIpaRenderer(EspeakIpaProfile.EnUs, useWeakForms: false);
        var kokoro = new MisakiRenderer();
        var kokoroWeak = new MisakiRenderer(MisakiProfile.Us, useWeakForms: true);
        using var stream = File.Create(path);
        var lines = 0;
        foreach (var (kind, id, text) in sentences.Concat(wordList))
        {
            var items = text.Items;
            var piperPieces = piper.RenderItems(items);
            var fullPieces = piperFull.RenderItems(items);
            var kokoroPieces = kokoro.RenderItems(items);
            var weakPieces = kokoroWeak.RenderItems(items);
            var grouped = WeakForms.GroupedWithPrevious(items);
            using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
            {
                writer.WriteStartObject();
                writer.WriteString("kind", kind);
                writer.WriteString("id", id);
                writer.WriteString("spoken", Spoken(items));
                writer.WriteStartArray("items");
                for (var i = 0; i < items.Count; i++)
                {
                    writer.WriteStartObject();
                    writer.WriteString("t", items[i].Text);
                    writer.WriteString("k", items[i].Kind.ToString());
                    writer.WriteString("s", items[i].Source.ToString());
                    writer.WriteString("a", items[i].Arpabet);
                    writer.WriteString("p", piperPieces[i]);
                    writer.WriteString("f", fullPieces[i]);
                    writer.WriteString("m", kokoroPieces[i]);
                    writer.WriteString("w", weakPieces[i]);
                    writer.WriteBoolean("j", grouped[i]);
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
                writer.WriteEndObject();
            }

            stream.WriteByte((byte)'\n');
            lines++;
        }

        return lines;
    }

    /// <summary>
    ///     Writes the spoken text of a result: the words and marks in order, a space before every word.
    /// </summary>
    /// <param name="items">The items.</param>
    /// <returns>The spoken text.</returns>
    private static string Spoken(IReadOnlyList<PhonemizedItem> items)
    {
        var builder = new StringBuilder();
        foreach (var item in items.Where(i => i.Kind != PhonemizedItemKind.Unsupported))
        {
            if (item.Kind == PhonemizedItemKind.Word && builder.Length > 0)
            {
                builder.Append(' ');
            }

            builder.Append(item.Text);
        }

        return builder.ToString();
    }
}
