using System.Globalization;
using System.Reflection;
using System.Text;

namespace DemaConsulting.Speech.Phonemes.En.LexiconSubsystem;

/// <summary>
///     The pronunciation of every heteronym reading id of the English text analysis, and the rule that turns the
///     pronunciation of a reading into the pronunciation of one inflected spelling of it.
/// </summary>
/// <remarks>
///     <para>
///     <b>Why a table.</b> The CMU Pronouncing Dictionary lists the variants of a heteronym but not which variant
///     belongs to which sense, and for about a quarter of the heteronyms it lacks a variant altogether (it has no
///     separate noun and verb entry for <c>export</c>). The reading ids (<c>record.noun</c>, <c>record.verb</c>) are
///     therefore mapped to a pronunciation by a frozen, reviewed table (<c>reading-pronunciations.tsv</c>) whose
///     rows say where each pronunciation came from: <c>cmu#N</c> (variant <c>N</c> of the lemma in the
///     lexicon), <c>rule:&lt;name&gt;</c> (a named derivation from a lexicon entry) or <c>authored</c>.
///     </para>
///     <para>
///     <b>Inflected forms.</b> A row gives the pronunciation of the reading's own headword; the pronunciation of
///     an inflected spelling the text analysis can label with that reading (<c>records</c>, <c>recorded</c>,
///     <c>abusing</c>) is the headword's pronunciation plus the sound of the ending, by the morphology rules. The few
///     spellings those rules cannot give (<c>does</c> of <c>do</c>, <c>lives</c> of <c>life</c>, <c>dove</c> of
///     <c>dive</c>) are listed in <c>form-overrides.tsv</c>. A pair that neither route resolves is a gap; there is
///     no silent default, and the list of gaps is committed and tested (<c>reading-gaps.tsv</c>, expected empty).
///     </para>
///     <para>
///     Instances are immutable and thread-safe; <see cref="Default"/> loads the embedded tables once on first use.
///     </para>
/// </remarks>
internal sealed class ReadingPronunciations
{
    /// <summary>The manifest resource name of the reading table.</summary>
    public const string ReadingsResourceName = "DemaConsulting.Speech.Phonemes.En.Resources.reading-pronunciations.tsv";

    /// <summary>The manifest resource name of the form overrides.</summary>
    public const string OverridesResourceName = "DemaConsulting.Speech.Phonemes.En.Resources.form-overrides.tsv";

    /// <summary>The lazily loaded embedded tables; thread-safe publication.</summary>
    private static readonly Lazy<ReadingPronunciations> DefaultTables = new(LoadEmbedded, LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>The headword pronunciation of every reading id.</summary>
    private readonly Dictionary<string, ReadingEntry> _entries;

    /// <summary>The pronunciation of the spellings listed as overrides, keyed by form and reading id.</summary>
    private readonly Dictionary<(string Form, string ReadingId), Pronunciation> _overrides;

    /// <summary>
    ///     Initializes the tables.
    /// </summary>
    /// <param name="entries">The reading rows, keyed by reading id.</param>
    /// <param name="overrides">The form overrides.</param>
    private ReadingPronunciations(Dictionary<string, ReadingEntry> entries, Dictionary<(string Form, string ReadingId), Pronunciation> overrides)
    {
        _entries = entries;
        _overrides = overrides;
    }

    /// <summary>Gets the tables embedded in this assembly, loaded on first use.</summary>
    /// <exception cref="InvalidOperationException">Thrown when an embedded table is missing or malformed (a build defect).</exception>
    public static ReadingPronunciations Default => DefaultTables.Value;

    /// <summary>Gets every row of the reading table, in no particular order.</summary>
    public IReadOnlyCollection<ReadingEntry> Entries => _entries.Values;

    /// <summary>Gets the number of form overrides.</summary>
    public int OverrideCount => _overrides.Count;

    /// <summary>
    ///     Parses the two tables from text.
    /// </summary>
    /// <param name="readings">The reading table: a header line, then <c>reading_id</c>, <c>arpabet</c>, <c>source</c> per line.</param>
    /// <param name="formOverrides">The override table: a header line, then <c>form</c>, <c>reading_id</c>, <c>arpabet</c>, <c>note</c> per line.</param>
    /// <returns>The tables.</returns>
    /// <exception cref="FormatException">Thrown when a line is malformed, a pronunciation is not ARPAbet, a source is not one of the three kinds or a key repeats.</exception>
    public static ReadingPronunciations Parse(TextReader readings, TextReader formOverrides)
    {
        ArgumentNullException.ThrowIfNull(readings);
        ArgumentNullException.ThrowIfNull(formOverrides);

        // Reading rows: reading_id, arpabet, source
        var entries = new Dictionary<string, ReadingEntry>(StringComparer.Ordinal);
        foreach (var (lineNumber, fields) in DataRows(readings, 3, "reading-pronunciations.tsv"))
        {
            var source = fields[2];
            if (!IsValidSource(source))
            {
                throw new FormatException($"reading-pronunciations.tsv line {lineNumber}: '{source}' is not cmu#N, rule:<name> or authored.");
            }

            var entry = new ReadingEntry(fields[0], ParsePronunciation(fields[1], "reading-pronunciations.tsv", lineNumber), source);
            if (!entries.TryAdd(entry.ReadingId, entry))
            {
                throw new FormatException($"reading-pronunciations.tsv line {lineNumber}: reading id '{entry.ReadingId}' repeats.");
            }
        }

        // Override rows: form, reading_id, arpabet, note
        var overrides = new Dictionary<(string Form, string ReadingId), Pronunciation>();
        foreach (var (lineNumber, fields) in DataRows(formOverrides, 4, "form-overrides.tsv"))
        {
            var key = (fields[0], fields[1]);
            if (!entries.ContainsKey(key.Item2))
            {
                throw new FormatException($"form-overrides.tsv line {lineNumber}: reading id '{key.Item2}' has no row in the reading table.");
            }

            if (!overrides.TryAdd(key, ParsePronunciation(fields[2], "form-overrides.tsv", lineNumber)))
            {
                throw new FormatException($"form-overrides.tsv line {lineNumber}: ({key.Item1}, {key.Item2}) repeats.");
            }
        }

        return new ReadingPronunciations(entries, overrides);
    }

    /// <summary>
    ///     Gets the headword pronunciation row of a reading id.
    /// </summary>
    /// <param name="readingId">The reading id, such as <c>record.noun</c>.</param>
    /// <param name="entry">Receives the row, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the id has a row.</returns>
    public bool TryGetEntry(string readingId, out ReadingEntry? entry) => _entries.TryGetValue(readingId, out entry);

    /// <summary>
    ///     Resolves the pronunciation of one spelling under one reading.
    /// </summary>
    /// <remarks>
    ///     Order: an override for exactly this (form, reading id); the headword pronunciation when the form is the
    ///     reading's lemma; the headword pronunciation plus the ending when the form is a regular inflection of the
    ///     lemma (<see cref="TryInflect"/>). Anything else is a gap and returns <see langword="false"/>.
    /// </remarks>
    /// <param name="form">The lower-case spelling of the word.</param>
    /// <param name="readingId">The reading id.</param>
    /// <param name="pronunciation">Receives the pronunciation, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the pair resolves.</returns>
    public bool TryResolve(string form, string readingId, out Pronunciation? pronunciation)
    {
        ArgumentNullException.ThrowIfNull(form);
        ArgumentNullException.ThrowIfNull(readingId);

        // An explicit override beats everything: it exists because the rules would be wrong
        if (_overrides.TryGetValue((form, readingId), out pronunciation))
        {
            return true;
        }

        pronunciation = null;
        if (!_entries.TryGetValue(readingId, out var entry))
        {
            return false;
        }

        // The reading's own headword, or a regular inflection of it
        var lemma = LemmaOf(readingId);
        if (string.Equals(form, lemma, StringComparison.Ordinal))
        {
            pronunciation = entry.Pronunciation;
            return true;
        }

        pronunciation = TryInflect(lemma, form, entry.Pronunciation);
        return pronunciation is not null;
    }

    /// <summary>
    ///     Gets the lemma of a reading id: the part before the first period.
    /// </summary>
    /// <param name="readingId">The reading id, such as <c>record.noun</c>.</param>
    /// <returns>The lemma, such as <c>record</c>.</returns>
    public static string LemmaOf(string readingId)
    {
        ArgumentNullException.ThrowIfNull(readingId);
        var dot = readingId.IndexOf('.');
        return dot < 0 ? readingId : readingId[..dot];
    }

    /// <summary>
    ///     Builds the pronunciation of a regular inflection of a lemma from the lemma's pronunciation.
    /// </summary>
    /// <remarks>
    ///     Recognized spellings: <c>-s</c>/<c>-es</c>/<c>-ies</c> (plural or third person), <c>-ed</c>/<c>-d</c>/<c>-ied</c>
    ///     (with a doubled final consonant), <c>-ing</c> (silent <c>e</c> dropped, final consonant doubled,
    ///     <c>-ie</c> to <c>-ying</c>), <c>-er</c>/<c>-r</c>, <c>-est</c>/<c>-st</c> and <c>-'s</c>.
    /// </remarks>
    /// <param name="lemma">The lemma spelling.</param>
    /// <param name="form">The inflected spelling.</param>
    /// <param name="lemmaPronunciation">The lemma's pronunciation; must not be empty.</param>
    /// <returns>The pronunciation, or <see langword="null"/> when the form is not a regular inflection of the lemma.</returns>
    public static Pronunciation? TryInflect(string lemma, string form, Pronunciation lemmaPronunciation)
    {
        ArgumentNullException.ThrowIfNull(lemma);
        ArgumentNullException.ThrowIfNull(form);
        ArgumentNullException.ThrowIfNull(lemmaPronunciation);
        if (lemmaPronunciation.Count == 0 || lemma.Length == 0)
        {
            return null;
        }

        var endsInE = lemma.EndsWith('e');
        var endsInY = lemma.EndsWith('y');
        var doubled = lemma + lemma[^1];

        SuffixKind? kind = null;
        if (form == lemma + "s" || form == lemma + "es" || (endsInY && form == lemma[..^1] + "ies"))
        {
            kind = SuffixKind.PluralS;
        }
        else if (form == lemma + "ed" || (endsInE && form == lemma + "d") || (endsInY && form == lemma[..^1] + "ied") || form == doubled + "ed")
        {
            kind = SuffixKind.PastEd;
        }
        else if (form == lemma + "ing" || (endsInE && form == lemma[..^1] + "ing") || form == doubled + "ing" || (lemma.EndsWith("ie", StringComparison.Ordinal) && form == lemma[..^2] + "ying"))
        {
            kind = SuffixKind.Ing;
        }
        else if (form == lemma + "er" || (endsInE && form == lemma + "r") || form == doubled + "er")
        {
            kind = SuffixKind.ComparativeEr;
        }
        else if (form == lemma + "est" || (endsInE && form == lemma + "st") || form == doubled + "est")
        {
            kind = SuffixKind.SuperlativeEst;
        }
        else if (form == lemma + "'s")
        {
            kind = SuffixKind.PossessiveS;
        }

        return kind is null ? null : Morphology.Apply(lemmaPronunciation, kind.Value);
    }

    /// <summary>
    ///     Loads the tables embedded in this assembly.
    /// </summary>
    /// <returns>The tables.</returns>
    private static ReadingPronunciations LoadEmbedded()
    {
        var assembly = typeof(ReadingPronunciations).Assembly;
        using var readings = OpenResource(assembly, ReadingsResourceName);
        using var overrides = OpenResource(assembly, OverridesResourceName);
        return Parse(readings, overrides);
    }

    /// <summary>
    ///     Opens an embedded text resource.
    /// </summary>
    /// <param name="assembly">The assembly that embeds it.</param>
    /// <param name="name">The manifest resource name.</param>
    /// <returns>A reader over the resource.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the resource is missing.</exception>
    private static StreamReader OpenResource(Assembly assembly, string name)
    {
        var stream = assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"The embedded resource '{name}' is missing.");
        return new StreamReader(stream, Encoding.UTF8);
    }

    /// <summary>
    ///     Enumerates the data lines of a tab-separated table after skipping its header.
    /// </summary>
    /// <param name="reader">The table.</param>
    /// <param name="columns">The required column count.</param>
    /// <param name="name">The table name for error messages.</param>
    /// <returns>The one-based line number and the fields of each non-blank line.</returns>
    /// <exception cref="FormatException">Thrown when a line has the wrong column count.</exception>
    private static IEnumerable<(int LineNumber, string[] Fields)> DataRows(TextReader reader, int columns, string name)
    {
        var lineNumber = 0;
        while (reader.ReadLine() is { } line)
        {
            lineNumber++;

            // The header is skipped; blank lines carry nothing
            if (lineNumber == 1 || line.Length == 0)
            {
                continue;
            }

            var fields = line.Split('\t');
            if (fields.Length != columns)
            {
                throw new FormatException($"{name} line {lineNumber}: expected {columns} tab-separated columns, found {fields.Length}.");
            }

            yield return (lineNumber, fields);
        }
    }

    /// <summary>
    ///     Parses an ARPAbet pronunciation field.
    /// </summary>
    /// <param name="text">The field.</param>
    /// <param name="name">The table name for error messages.</param>
    /// <param name="lineNumber">The line number for error messages.</param>
    /// <returns>The non-empty pronunciation.</returns>
    /// <exception cref="FormatException">Thrown when the field is empty or not ARPAbet.</exception>
    private static Pronunciation ParsePronunciation(string text, string name, int lineNumber)
    {
        Pronunciation pronunciation;
        try
        {
            pronunciation = Pronunciation.Parse(text);
        }
        catch (FormatException exception)
        {
            throw new FormatException($"{name} line {lineNumber}: {exception.Message}", exception);
        }

        if (pronunciation.Count == 0)
        {
            throw new FormatException($"{name} line {lineNumber}: the pronunciation is empty.");
        }

        return pronunciation;
    }

    /// <summary>
    ///     Checks the provenance field of a reading row.
    /// </summary>
    /// <param name="source">The field.</param>
    /// <returns><see langword="true"/> for <c>cmu#N</c>, <c>rule:&lt;name&gt;</c> or <c>authored</c>.</returns>
    private static bool IsValidSource(string source) =>
        source == "authored"
        || (source.StartsWith("rule:", StringComparison.Ordinal) && source.Length > "rule:".Length)
        || (source.StartsWith("cmu#", StringComparison.Ordinal)
            && int.TryParse(source.AsSpan(4), NumberStyles.None, CultureInfo.InvariantCulture, out var n)
            && n >= 1);
}

/// <summary>
///     One row of the reading table: the pronunciation of a reading's headword and where it came from.
/// </summary>
/// <param name="ReadingId">The reading id, such as <c>record.noun</c>.</param>
/// <param name="Pronunciation">The ARPAbet pronunciation of the reading's lemma.</param>
/// <param name="Source">The provenance: <c>cmu#N</c>, <c>rule:&lt;name&gt;</c> or <c>authored</c>.</param>
internal sealed record ReadingEntry(string ReadingId, Pronunciation Pronunciation, string Source);
