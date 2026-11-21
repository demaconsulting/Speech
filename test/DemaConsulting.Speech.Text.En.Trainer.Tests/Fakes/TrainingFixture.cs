using System.Globalization;
using System.Text;

namespace DemaConsulting.Speech.Text.En.Trainer.Tests.Fakes;

/// <summary>
///     Builds a small but trainable Text.En data folder in a temporary directory, laid out like the repository
///     (<c>data/text-en</c> under a base folder), so the training commands can run on it.
/// </summary>
/// <remarks>
///     <para>
///     The inventory has two readings of <c>lead</c> (<c>lead.verb</c>, listed first, and <c>lead.noun</c>) and a
///     one-candidate form <c>wind</c>. Training rows follow clear phrase patterns (<c>will lead the ...</c> is the
///     verb, <c>the lead pipe ...</c> is the noun), so a trained model classifies unseen sentences of the same
///     patterns. The natural development and holdout rows carry provenance documents; the independent holdout rows
///     are raw sentences. Offsets are computed from the target word, never typed by hand. The folder is created in
///     code so no fixture data files are checked in, and it is deleted on disposal.
///     </para>
///     <para>
///     The part-of-speech hints of the two readings are parameters so a test can make the own-part-of-speech baseline
///     either informative (<c>VERB</c> and <c>NOUN</c>) or never matching (a tag the tagger does not give a word
///     in these patterns), in which case the baseline is the first candidate.
///     </para>
/// </remarks>
internal sealed class TrainingFixture : IDisposable
{
    /// <summary>The UTF-8 encoding without byte order mark used for every file the fixture writes.</summary>
    private static readonly UTF8Encoding Utf8 = new(false);

    /// <summary>The header of the labeled data files.</summary>
    private static readonly string[] DataHeader = ["id", "lemma", "reading_id", "sentence", "start", "end", "source", "license", "split"];

    /// <summary>The objects of the verb patterns.</summary>
    private static readonly string[] VerbObjects = ["team", "group", "class", "march", "parade", "tour", "hike", "project"];

    /// <summary>The things of the noun patterns.</summary>
    private static readonly string[] NounThings = ["pipe", "roof", "weight", "paint", "plate", "box", "ball", "sheet"];

    /// <summary>
    ///     Creates the fixture.
    /// </summary>
    /// <param name="verbHint">The part-of-speech hint of <c>lead.verb</c>.</param>
    /// <param name="nounHint">The part-of-speech hint of <c>lead.noun</c>.</param>
    public TrainingFixture(string verbHint = "VERB", string nounHint = "NOUN")
    {
        BaseDirectory = Path.Combine(Path.GetTempPath(), "texten-training-" + Guid.NewGuid().ToString("N"));
        Root = Path.Combine(BaseDirectory, "data", "text-en");
        Directory.CreateDirectory(Root);
        ModelDirectory = Path.Combine(BaseDirectory, "model");

        Table("readings.tsv", ["reading_id", "lemma", "pos_hint", "description", "status"], [
            ["lead.verb", "lead", verbHint, "Verb use of lead, to guide.", "active"],
            ["lead.noun", "lead", nounHint, "Noun use of lead, the metal.", "active"],
            ["wind.noun", "wind", "NOUN", "Noun use of wind, moving air.", "active"],
        ]);
        Table("forms.tsv", ["form", "reading_id", "kind"], [
            ["lead", "lead.verb", "base"],
            ["lead", "lead.noun", "base"],
            ["wind", "wind.noun", "base"],
        ]);

        // Training: 18 verb rows and 16 noun rows (the verb is the majority), and a few one-candidate rows.
        var train = new List<string[]>();
        var provenance = new List<string[]>();
        foreach (var obj in VerbObjects)
        {
            train.Add(DataRow($"syn-v{train.Count:000}", "lead.verb", $"They will lead the {obj} today.", "lead", "synthetic", "train"));
            train.Add(DataRow($"syn-v{train.Count:000}", "lead.verb", $"Please lead the {obj} now.", "lead", "synthetic", "train"));
        }

        train.Add(DataRow("syn-v-extra1", "lead.verb", "You must lead the way home.", "lead", "synthetic", "train"));
        train.Add(DataRow("syn-v-extra2", "lead.verb", "I can lead the way tonight.", "lead", "synthetic", "train"));
        foreach (var thing in NounThings)
        {
            train.Add(DataRow($"syn-n{train.Count:000}", "lead.noun", $"The lead {thing} was very heavy.", "lead", "synthetic", "train"));
            train.Add(DataRow($"syn-n{train.Count:000}", "lead.noun", $"A lead {thing} fell down.", "lead", "synthetic", "train"));
        }

        train.Add(DataRow("syn-w1", "wind.noun", "A cold wind blew hard.", "wind", "synthetic", "train"));
        train.Add(DataRow("syn-w2", "wind.noun", "The wind howled all night.", "wind", "synthetic", "train"));
        Table("train.tsv", DataHeader, train);

        // Development: natural rows of unseen templates, one document each.
        var dev = new List<string[]>();
        for (var i = 0; i < 4; i++)
        {
            AddNatural(dev, provenance, $"harvest-d{dev.Count:00}", "lead.verb", $"We lead the {VerbObjects[i]} again.", "lead", "dev", $"dev-doc-{dev.Count}");
            AddNatural(dev, provenance, $"harvest-d{dev.Count:00}", "lead.noun", $"That lead {NounThings[i]} is old.", "lead", "dev", $"dev-doc-{dev.Count}");
        }

        Table("dev.tsv", DataHeader, dev);

        // Natural holdout: 20 verb patterns, 19 noun patterns and one noun pattern labeled as the verb (the model is wrong there by design).
        var natural = new List<string[]>();
        for (var i = 0; i < 20; i++)
        {
            AddNatural(natural, provenance, $"harvest-h{natural.Count:00}", "lead.verb", $"Will you lead the {VerbObjects[i % 8]} soon?", "lead", "holdout-natural", $"nat-doc-{natural.Count}");
        }

        for (var i = 0; i < 19; i++)
        {
            AddNatural(natural, provenance, $"harvest-h{natural.Count:00}", "lead.noun", $"Is the lead {NounThings[i % 8]} still here?", "lead", "holdout-natural", $"nat-doc-{natural.Count}");
        }

        AddNatural(natural, provenance, "harvest-h-wrong", "lead.verb", "Is the lead lamp still here?", "lead", "holdout-natural", "nat-doc-wrong");
        Table("holdout-natural.tsv", DataHeader, natural);

        // Independent holdout: 10 verb patterns, 10 noun patterns and one row whose target is inside a longer word.
        var independent = new List<string[]>();
        for (var i = 0; i < 10; i++)
        {
            independent.Add(DataRow($"hind-v{i}", "lead.verb", $"Someone must lead the {VerbObjects[i % 8]} soon.", "lead", "independent-holdout", "test"));
            independent.Add(DataRow($"hind-n{i}", "lead.noun", $"Look at the lead {NounThings[i % 8]} there.", "lead", "independent-holdout", "test"));
        }

        independent.Add(DataRow("hind-unaligned", "lead.noun", "The leader spoke softly.", "lead", "independent-holdout", "test"));
        Table("holdout-independent.tsv", DataHeader, independent);

        Table("harvest-provenance.tsv", ["id", "source_id", "corpus", "sent_id", "label_method"], provenance);

        // Files the trainer must never read: a README (listed in a real manifest but not a training input).
        Write("README.md", "# Fixture data\n");
    }

    /// <summary>Gets the data root (<c>data/text-en</c> under the base folder).</summary>
    public string Root { get; }

    /// <summary>Gets the base folder, laid out like a repository root.</summary>
    public string BaseDirectory { get; }

    /// <summary>Gets a model folder inside the base folder.</summary>
    public string ModelDirectory { get; }

    /// <summary>
    ///     Gets the full path of a file of the data root.
    /// </summary>
    /// <param name="relativePath">The path relative to the data root.</param>
    /// <returns>The full path.</returns>
    public string PathOf(string relativePath) => Path.Combine(Root, relativePath);

    /// <summary>
    ///     Reads a file of the data root as text.
    /// </summary>
    /// <param name="relativePath">The path relative to the data root.</param>
    /// <returns>The text.</returns>
    public string Read(string relativePath) => File.ReadAllText(PathOf(relativePath), Utf8);

    /// <summary>
    ///     Writes a text file in the data root.
    /// </summary>
    /// <param name="relativePath">The path relative to the data root.</param>
    /// <param name="content">The content.</param>
    public void Write(string relativePath, string content) => File.WriteAllText(PathOf(relativePath), content, Utf8);

    /// <summary>
    ///     Replaces a text in a file; the text must occur exactly once so a test cannot silently edit nothing.
    /// </summary>
    /// <param name="relativePath">The path relative to the data root.</param>
    /// <param name="oldText">The text to replace.</param>
    /// <param name="newText">The replacement.</param>
    public void Replace(string relativePath, string oldText, string newText)
    {
        var text = Read(relativePath);
        var first = text.IndexOf(oldText, StringComparison.Ordinal);
        if (first < 0 || text.IndexOf(oldText, first + 1, StringComparison.Ordinal) >= 0)
        {
            throw new InvalidOperationException($"'{oldText}' must occur exactly once in {relativePath}.");
        }

        Write(relativePath, text.Replace(oldText, newText, StringComparison.Ordinal));
    }

    /// <summary>
    ///     Appends a labeled row whose offsets are computed from the target word.
    /// </summary>
    /// <param name="relativePath">The data file.</param>
    /// <param name="id">The row id.</param>
    /// <param name="readingId">The reading id.</param>
    /// <param name="sentence">The sentence.</param>
    /// <param name="word">The target word, which must occur in the sentence.</param>
    /// <param name="source">The source column.</param>
    public void AppendRow(string relativePath, string id, string readingId, string sentence, string word, string source)
    {
        var split = Path.GetFileNameWithoutExtension(relativePath);
        Write(relativePath, Read(relativePath) + string.Join('\t', DataRow(id, readingId, sentence, word, source, split)) + "\n");
    }

    /// <summary>
    ///     Writes a regression overlay whose rows target the word <c>lead</c>.
    /// </summary>
    /// <param name="rows">The rows: id, reading, sentence, origin and seen-in-training mark.</param>
    public void WriteRegression(params (string Id, string ReadingId, string Sentence, string Origin, string Seen)[] rows) =>
        Table(
            "regression.tsv",
            ["id", "lemma", "reading_id", "sentence", "start", "end", "origin", "ref", "seen_in_training", "license"],
            rows.Select(row => OverlayCells(row.Id, row.ReadingId, row.Sentence, [row.Origin, "RUN-LOG.md:1", row.Seen, "CC0-1.0"])));

    /// <summary>
    ///     Writes a generalization set whose rows target the word <c>lead</c>.
    /// </summary>
    /// <param name="rows">The rows: id, reading, sentence, kind, family and cue-unsolvable mark.</param>
    public void WriteGeneralization(params (string Id, string ReadingId, string Sentence, string Kind, string Family, string CueUnsolvable)[] rows) =>
        Table(
            "generalization.tsv",
            ["id", "lemma", "reading_id", "sentence", "start", "end", "kind", "family", "cue_forms", "cue_unsolvable", "source", "license"],
            rows.Select(row => OverlayCells(row.Id, row.ReadingId, row.Sentence, [row.Kind, row.Family, "-", row.CueUnsolvable, "generalization", "CC0-1.0"])));

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Directory.Exists(BaseDirectory))
        {
            Directory.Delete(BaseDirectory, true);
        }
    }

    /// <summary>
    ///     Renders an overlay row whose target is the word <c>lead</c>, with offsets computed from the sentence.
    /// </summary>
    /// <param name="id">The row id.</param>
    /// <param name="readingId">The reading id.</param>
    /// <param name="sentence">The sentence, which must contain <c>lead</c>.</param>
    /// <param name="tail">The cells after the offsets.</param>
    /// <returns>The cells.</returns>
    private static string[] OverlayCells(string id, string readingId, string sentence, string[] tail)
    {
        var cells = DataRow(id, readingId, sentence, "lead", "synthetic", "train");
        return [.. cells[..6], .. tail];
    }

    /// <summary>
    ///     Renders a labeled row as cells.
    /// </summary>
    /// <param name="id">The row id.</param>
    /// <param name="readingId">The reading id.</param>
    /// <param name="sentence">The sentence.</param>
    /// <param name="word">The target word.</param>
    /// <param name="source">The source.</param>
    /// <param name="split">The split column value.</param>
    /// <returns>The cells.</returns>
    private static string[] DataRow(string id, string readingId, string sentence, string word, string source, string split)
    {
        var start = sentence.IndexOf(word, StringComparison.Ordinal);
        if (start < 0)
        {
            throw new InvalidOperationException($"'{word}' is not in '{sentence}'.");
        }

        var lemma = readingId.Split('.')[0];
        var license = source.StartsWith("harvest-", StringComparison.Ordinal) ? "CC-BY-3.0-US" : "CC0-1.0";
        return [id, lemma, readingId, sentence, start.ToString(CultureInfo.InvariantCulture), (start + word.Length).ToString(CultureInfo.InvariantCulture), source, license, split];
    }

    /// <summary>
    ///     Adds a natural row and its provenance record.
    /// </summary>
    /// <param name="rows">The rows being built.</param>
    /// <param name="provenance">The provenance rows being built.</param>
    /// <param name="id">The row id.</param>
    /// <param name="readingId">The reading id.</param>
    /// <param name="sentence">The sentence.</param>
    /// <param name="word">The target word.</param>
    /// <param name="split">The split.</param>
    /// <param name="document">The source document.</param>
    private static void AddNatural(List<string[]> rows, List<string[]> provenance, string id, string readingId, string sentence, string word, string split, string document)
    {
        rows.Add(DataRow(id, readingId, sentence, word, "harvest-masc", split));
        provenance.Add([id, "masc:" + document, "masc", "masc:" + document + "#1", "upos"]);
    }

    /// <summary>
    ///     Writes a table from a header and rows.
    /// </summary>
    /// <param name="relativePath">The file.</param>
    /// <param name="header">The header cells.</param>
    /// <param name="rows">The rows.</param>
    private void Table(string relativePath, string[] header, IEnumerable<string[]> rows) =>
        Write(relativePath, string.Join('\n', new[] { header }.Concat(rows).Select(cells => string.Join('\t', cells))) + "\n");
}
