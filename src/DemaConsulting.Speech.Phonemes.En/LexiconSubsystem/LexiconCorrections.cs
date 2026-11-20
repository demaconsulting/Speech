using System.Text;

namespace DemaConsulting.Speech.Phonemes.En.LexiconSubsystem;

/// <summary>
///     The reviewed tables that amend the pronunciation lexicon: the corrections of the few words whose CMU Pronouncing
///     Dictionary entry is a transcription error, and the additions of the few words the dictionary lacks and the
///     letter-to-sound model gets wrong.
/// </summary>
/// <remarks>
///     <para>
///     The lexicon is the dictionary's own content, so a wrong entry would otherwise be spoken wrong for good: CMUdict
///     lists <i>nobly</i> as <c>N AA1 B L IY0</c> (<i>knobbly</i>), although the adverb of <i>noble</i> has the vowel of
///     its stem. The table is written and reviewed by DEMA Consulting from the dictionary's own entry of the stem and
///     phonetic knowledge (<c>data/phonemes-en/lexicon-corrections.tsv</c>, embedded by link); a correction replaces every
///     variant of the word the dictionary lists. The table is deliberately tiny, and each row states why it exists.
///     </para>
///     <para>
///     The additions (<c>data/phonemes-en/lexicon-additions.tsv</c>, <see cref="Additions"/>) have the same format and
///     the same authorship: words the dictionary does not list whose letter-to-sound guess a listener flagged as wrong
///     (<i>agave</i>, <i>aquiver</i>), written from phonetic knowledge and, where there is one, the dictionary's entry of
///     the stem. An addition never shadows a dictionary word: the word pronouncer consults it only after the lexicon.
///     </para>
///     <para>Instances are immutable and thread-safe; <see cref="Default"/> and <see cref="Additions"/> load their embedded table once on first use.</para>
/// </remarks>
internal sealed class LexiconCorrections
{
    /// <summary>The manifest resource name of the corrections table.</summary>
    public const string ResourceName = "DemaConsulting.Speech.Phonemes.En.Resources.lexicon-corrections.tsv";

    /// <summary>The manifest resource name of the additions table.</summary>
    public const string AdditionsResourceName = "DemaConsulting.Speech.Phonemes.En.Resources.lexicon-additions.tsv";

    /// <summary>The lazily loaded embedded corrections table; thread-safe publication.</summary>
    private static readonly Lazy<LexiconCorrections> DefaultTable = new(() => LoadEmbedded(ResourceName, "lexicon-corrections.tsv"), LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>The lazily loaded embedded additions table; thread-safe publication.</summary>
    private static readonly Lazy<LexiconCorrections> AdditionsTable = new(() => LoadEmbedded(AdditionsResourceName, "lexicon-additions.tsv"), LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>The corrected pronunciation of each word.</summary>
    private readonly Dictionary<string, Pronunciation> _corrections;

    /// <summary>
    ///     Initializes the table.
    /// </summary>
    /// <param name="corrections">The corrected pronunciations by word.</param>
    private LexiconCorrections(Dictionary<string, Pronunciation> corrections) => _corrections = corrections;

    /// <summary>Gets the table embedded in this assembly, loaded on first use.</summary>
    /// <exception cref="InvalidOperationException">Thrown when the embedded table is missing or malformed (a build defect).</exception>
    public static LexiconCorrections Default => DefaultTable.Value;

    /// <summary>Gets the additions table embedded in this assembly (words the dictionary lacks), loaded on first use.</summary>
    /// <exception cref="InvalidOperationException">Thrown when the embedded table is missing or malformed (a build defect).</exception>
    public static LexiconCorrections Additions => AdditionsTable.Value;

    /// <summary>Gets the corrected words and their pronunciations, in no particular order.</summary>
    public IReadOnlyDictionary<string, Pronunciation> Entries => _corrections;

    /// <summary>
    ///     Parses the table from text.
    /// </summary>
    /// <param name="table">The table: a header line, then <c>word</c>, <c>arpabet</c>, <c>note</c> per line.</param>
    /// <returns>The table.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="table"/> is null.</exception>
    /// <param name="tableName">The file name the error messages name; the corrections table when omitted.</param>
    /// <exception cref="FormatException">Thrown when a line is malformed, a word is not lower-case letters and apostrophes, a pronunciation is not ARPAbet or a word repeats.</exception>
    public static LexiconCorrections Parse(TextReader table, string tableName = "lexicon-corrections.tsv")
    {
        ArgumentNullException.ThrowIfNull(table);
        var corrections = new Dictionary<string, Pronunciation>(StringComparer.Ordinal);
        var lineNumber = 0;
        while (table.ReadLine() is { } line)
        {
            lineNumber++;

            // The header is skipped; blank lines carry nothing
            if (lineNumber == 1 || line.Length == 0)
            {
                continue;
            }

            var fields = line.Split('\t');
            if (fields.Length != 3)
            {
                throw new FormatException($"{tableName} line {lineNumber}: expected 3 tab-separated columns, found {fields.Length}.");
            }

            var word = fields[0];
            if (word.Length == 0 || !word.All(c => c is (>= 'a' and <= 'z') or '\''))
            {
                throw new FormatException($"{tableName} line {lineNumber}: '{word}' is not a lower-case word.");
            }

            Pronunciation pronunciation;
            try
            {
                pronunciation = Pronunciation.Parse(fields[1]);
            }
            catch (FormatException exception)
            {
                throw new FormatException($"{tableName} line {lineNumber}: {exception.Message}", exception);
            }

            if (pronunciation.Count == 0 || !corrections.TryAdd(word, pronunciation))
            {
                throw new FormatException($"{tableName} line {lineNumber}: the pronunciation of '{word}' is empty or the word repeats.");
            }
        }

        return new LexiconCorrections(corrections);
    }

    /// <summary>
    ///     Looks up the corrected pronunciation of a word.
    /// </summary>
    /// <param name="word">The word in lower case (letters and apostrophes).</param>
    /// <param name="pronunciation">Receives the corrected pronunciation.</param>
    /// <returns><see langword="true"/> when the word is corrected.</returns>
    public bool TryGet(string word, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out Pronunciation? pronunciation) =>
        _corrections.TryGetValue(word, out pronunciation);

    /// <summary>
    ///     Loads a table embedded in this assembly.
    /// </summary>
    /// <param name="resourceName">The manifest resource name of the table.</param>
    /// <param name="tableName">The file name the error messages name.</param>
    /// <returns>The table.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the resource is missing (a build defect).</exception>
    private static LexiconCorrections LoadEmbedded(string resourceName, string tableName)
    {
        var assembly = typeof(LexiconCorrections).Assembly;
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"The embedded resource '{resourceName}' is missing.");
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return Parse(reader, tableName);
    }
}
