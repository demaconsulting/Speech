using System.Reflection;

namespace DemaConsulting.Speech.Text.En.NormalizationSubsystem;

/// <summary>
///     How an abbreviation relates to the words around it; decides what happens to its period
///     and to the sentence break after it.
/// </summary>
internal enum AbbreviationClass
{
    /// <summary>Precedes a name or number (<c>Dr. Smith</c>); its period never ends a sentence.</summary>
    Prefix,

    /// <summary>Follows a name or street (<c>Main St.</c>); its period may end a sentence.</summary>
    Suffix,

    /// <summary>Used either way (<c>etc.</c>); the tokenizer's sentence opinion stands.</summary>
    Free,

    /// <summary>The ordinary word followed by a real period (<c>No.</c> as in "No. I don't"); no replacement.</summary>
    Word,
}

/// <summary>
///     The outcome of resolving one abbreviation candidate.
/// </summary>
/// <param name="Spoken">The spoken replacement; for <see cref="AbbreviationClass.Word"/> the written word, which is never applied.</param>
/// <param name="Class">The class of the chosen reading.</param>
/// <param name="UsedDefault">
///     Whether the reading was chosen by falling back to the documented default, or by picking
///     one when both the prefix and the suffix cue were present (counted as ambiguous).
/// </param>
/// <param name="IncludesPeriodToken">
///     Whether the candidate is the word token followed by its own adjacent <c>.</c> token (the
///     period is then a separate token the span may include), rather than a single token.
/// </param>
internal readonly record struct AbbreviationMatch(string Spoken, AbbreviationClass Class, bool UsedDefault, bool IncludesPeriodToken);

/// <summary>
///     Recognizes lexicon abbreviations and chooses among the readings of the ambiguous ones
///     (<c>St.</c>, <c>Dr.</c>, <c>Sr.</c>, <c>No.</c>) from the neighboring words only.
/// </summary>
/// <remarks>
///     The lexicon is the embedded <c>abbreviations.tsv</c>. Context cues are lexical and
///     orthographic (capitalization, digits, a short list of sentence-starting words and
///     determiners, and a short list of name suffixes and credentials that continue a name);
///     no part-of-speech tag is used, so the result does not depend on the
///     analyzer's options. Resolution looks at no more than three neighboring tokens, so a pass
///     over a text is linear. Instances are immutable after loading and thread-safe.
/// </remarks>
internal sealed class AbbreviationResolver
{
    /// <summary>The manifest name of the embedded lexicon.</summary>
    private const string ResourceName = "DemaConsulting.Speech.Text.En.Resources.abbreviations.tsv";

    /// <summary>The number of columns of the lexicon file.</summary>
    private const int ColumnCount = 6;

    /// <summary>The words that begin a sentence and are therefore no evidence of a name.</summary>
    private static readonly HashSet<string> SentenceStarters = new(StringComparer.Ordinal)
    {
        "The", "A", "An", "He", "She", "It", "They", "We", "I", "You", "This", "That", "These", "Those",
        "There", "Then", "Here", "His", "Her", "Our", "Their", "My", "Your", "In", "On", "At", "As", "But",
        "And", "Or", "So", "If", "When", "While", "After", "Before", "Because", "However", "Also", "What",
        "Who", "Why", "How", "Where", "Please", "Let", "Do", "Does", "Did", "Is", "Are", "Was", "Were",
        "Will", "Would", "Can", "Could", "Should", "May", "Might", "Must", "Not", "No", "Yes", "Now",
        "Today", "Tomorrow", "Yesterday", "Next",
    };

    /// <summary>
    ///     The name suffixes and credentials (written without a final period) that continue a name
    ///     and so never end a sentence directly before another of them: <c>Jr. Ph.D.</c>.
    /// </summary>
    private static readonly HashSet<string> NameContinuations = new(StringComparer.Ordinal)
    {
        "Jr", "Sr", "Esq", "II", "III", "IV", "Ph.D", "PhD", "M.D", "D.D.S", "D.M.D", "D.V.M", "D.O", "J.D",
        "R.N", "C.P.A", "MBA", "CPA",
    };

    /// <summary>The determiners that, directly before an abbreviation, make a title reading likelier.</summary>
    private static readonly HashSet<string> Determiners = new(StringComparer.OrdinalIgnoreCase)
    {
        "the", "a", "an", "my", "our", "your", "his", "her", "their", "this", "that", "these", "those", "any",
    };

    /// <summary>The lazily loaded default resolver; loading is deterministic, so a failure is cached.</summary>
    private static readonly Lazy<AbbreviationResolver> DefaultResolver =
        new(LoadDefault, LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>The rows by written form; each list holds the alternative readings of that form.</summary>
    private readonly Dictionary<string, Row[]> _rows;

    /// <summary>The length of the longest form, so longer tokens are rejected without a lookup.</summary>
    private readonly int _maxFormLength;

    /// <summary>
    ///     Initializes a new instance over parsed rows.
    /// </summary>
    /// <param name="rows">The rows by form.</param>
    /// <param name="count">The number of rows.</param>
    private AbbreviationResolver(Dictionary<string, Row[]> rows, int count)
    {
        _rows = rows;
        Count = count;
        _maxFormLength = rows.Count == 0 ? 0 : rows.Keys.Max(form => form.Length);
    }

    /// <summary>
    ///     Gets the resolver over the embedded lexicon, loaded once on first use.
    /// </summary>
    /// <exception cref="InvalidDataException">The embedded lexicon is malformed (a defective build).</exception>
    internal static AbbreviationResolver Default => DefaultResolver.Value;

    /// <summary>
    ///     Gets the number of lexicon rows (alternative readings count separately).
    /// </summary>
    internal int Count { get; }

    /// <summary>
    ///     Parses a lexicon in the documented tab-separated format.
    /// </summary>
    /// <param name="reader">The lexicon text. Must not be null; not disposed by this method.</param>
    /// <returns>The resolver.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="reader"/> is null.</exception>
    /// <exception cref="InvalidDataException">
    ///     A row is malformed: wrong column count, empty form or spoken text, an unknown class or
    ///     guard, a duplicate reading, or a form without exactly one default reading. The message
    ///     names the one-based line number.
    /// </exception>
    internal static AbbreviationResolver FromTsv(TextReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);

        var byForm = new Dictionary<string, List<Row>>(StringComparer.Ordinal);
        var firstLineOfForm = new Dictionary<string, int>(StringComparer.Ordinal);
        var headerSeen = false;
        var lineNumber = 0;
        var count = 0;
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            lineNumber++;

            // Blank lines and comments carry no data; the first data line is the header.
            line = line.TrimEnd('\r');
            if (line.Length == 0 || line[0] == '#')
            {
                continue;
            }

            if (!headerSeen)
            {
                headerSeen = true;
                continue;
            }

            var row = ParseRow(line, lineNumber);
            if (!byForm.TryGetValue(row.Form, out var list))
            {
                list = [];
                byForm[row.Form] = list;
                firstLineOfForm[row.Form] = lineNumber;
            }

            if (list.Any(existing => existing.Class == row.Class && existing.Spoken == row.Spoken))
            {
                throw new InvalidDataException($"Line {lineNumber}: duplicate reading for '{row.Form}'.");
            }

            list.Add(row);
            count++;
        }

        // Every form needs exactly one default reading so an inconclusive context has an answer.
        foreach (var (form, list) in byForm)
        {
            if (list.Count(row => row.IsDefault) != 1)
            {
                throw new InvalidDataException($"Line {firstLineOfForm[form]}: '{form}' must have exactly one default reading.");
            }
        }

        return new AbbreviationResolver(byForm.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray(), StringComparer.Ordinal), count);
    }

    /// <summary>
    ///     Decides whether the token at <paramref name="index"/> (or the token and its adjacent
    ///     period token) is a lexicon abbreviation, and which reading applies.
    /// </summary>
    /// <param name="text">The text the tokens index. Must not be null.</param>
    /// <param name="tokens">The tokens of the text, in order.</param>
    /// <param name="index">The index of the candidate token; must be a valid index of <paramref name="tokens"/>.</param>
    /// <param name="match">The resolution when this method returns <see langword="true"/>.</param>
    /// <returns><see langword="true"/> when a lexicon abbreviation (or a single-letter initial) was found.</returns>
    internal bool TryResolve(string text, ReadOnlySpan<NormalizationToken> tokens, int index, out AbbreviationMatch match)
    {
        var token = tokens[index];
        var span = token.In(text);
        match = default;

        // The tokenizer sometimes keeps the period inside the token ("Dr.") and sometimes emits it
        // as a separate adjacent token ("Ave" "."); try the dotted form first, then the plain one.
        var periodToken = index + 1 < tokens.Length
            && tokens[index + 1].Length == 1
            && tokens[index + 1].Start == token.End
            && text[token.End] == '.';

        if (periodToken && TryLookup(text, token.Start, token.Length + 1, tokens, index, includesPeriodToken: true, out match))
        {
            return true;
        }

        if (TryLookup(text, token.Start, token.Length, tokens, index, includesPeriodToken: false, out match))
        {
            return true;
        }

        return TryResolveInitial(text, tokens, index, span, out match);
    }

    /// <summary>
    ///     Looks one written form up and resolves it.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="start">The offset of the form.</param>
    /// <param name="length">The length of the form.</param>
    /// <param name="tokens">The tokens.</param>
    /// <param name="index">The index of the first token of the candidate.</param>
    /// <param name="includesPeriodToken">Whether the form spans the word token and its period token.</param>
    /// <param name="match">The resolution when found.</param>
    /// <returns><see langword="true"/> when the form is in the lexicon and at least one reading passes its guards.</returns>
    private bool TryLookup(string text, int start, int length, ReadOnlySpan<NormalizationToken> tokens, int index, bool includesPeriodToken, out AbbreviationMatch match)
    {
        match = default;

        // Cheap rejections first, so the common word costs no allocation: too long for any form, or
        // lower-case with neither a period nor a slash (every lower-case form has one of them).
        if (length > _maxFormLength)
        {
            return false;
        }

        var characters = text.AsSpan(start, length);
        if (!char.IsLetter(characters[0]))
        {
            return false;
        }

        if (char.IsLower(characters[0]) && !includesPeriodToken && characters.IndexOfAny('.', '/') < 0)
        {
            return false;
        }

        if (!_rows.TryGetValue(characters.ToString(), out var rows))
        {
            return false;
        }

        // Filter the readings by their guards. A passing guarded reading is more specific than an
        // unguarded one of the same form (for example "No." before a digit), so it replaces them.
        var passing = new List<Row>(rows.Length);
        foreach (var row in rows)
        {
            if (row.GuardsPass(text, tokens, index, includesPeriodToken))
            {
                passing.Add(row);
            }
        }

        if (passing.Count > 1 && passing.Any(row => row.HasGuard))
        {
            passing.RemoveAll(row => !row.HasGuard);
        }

        if (passing.Count == 0)
        {
            return false;
        }

        // A single reading needs no context and is never ambiguous.
        if (passing.Count == 1)
        {
            match = new AbbreviationMatch(passing[0].Spoken, passing[0].Class, false, includesPeriodToken);
            return true;
        }

        var (chosen, usedDefault) = ChooseReading(text, tokens, index, includesPeriodToken, passing);
        match = new AbbreviationMatch(chosen.Spoken, chosen.Class, usedDefault, includesPeriodToken);
        return true;
    }

    /// <summary>
    ///     Chooses between the prefix and suffix readings of an ambiguous abbreviation from the
    ///     words around it.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="tokens">The tokens.</param>
    /// <param name="index">The index of the first token of the candidate.</param>
    /// <param name="includesPeriodToken">Whether the candidate includes a separate period token.</param>
    /// <param name="candidates">The readings that passed their guards; at least two.</param>
    /// <returns>The chosen reading and whether the choice is counted as ambiguous.</returns>
    private static (Row Reading, bool Ambiguous) ChooseReading(string text, ReadOnlySpan<NormalizationToken> tokens, int index, bool includesPeriodToken, List<Row> candidates)
    {
        var prefix = candidates.Find(row => row.Class == AbbreviationClass.Prefix);
        var suffix = candidates.Find(row => row.Class == AbbreviationClass.Suffix);
        var defaultRow = candidates.Find(row => row.IsDefault) ?? candidates[0];

        // Alternatives that are not a prefix/suffix pair (a word reading against a prefix reading)
        // are decided by their guards alone, so the default is the answer without any ambiguity.
        if (prefix is null || suffix is null)
        {
            return (defaultRow, false);
        }

        // Locate the neighbors: the previous token (skipping a possessive clitic) and the second
        // previous one, and the next token after the abbreviation, skipping closing tokens.
        var last = includesPeriodToken ? index + 1 : index;
        var previous = PreviousIndex(text, tokens, index);
        var secondPrevious = previous < 0 ? -1 : PreviousIndex(text, tokens, previous);
        var next = NextIndex(text, tokens, last);

        // The cues, all derived from at most three neighbors.
        var prefixCue = next >= 0
            && (IsNameLike(text, tokens[next]) || IsInitial(text, tokens[next]))
            && !IsNameContinuation(text, tokens, next);
        var previousIsNumber = previous >= 0 && StartsWithDigit(text, tokens[previous]);
        var previousIsName = previous >= 0 && IsNameLike(text, tokens[previous]);
        var suffixCue = previousIsNumber
            || (previousIsName && (!tokens[previous].StartsSentence || !prefixCue));
        var strongSuffix = previousIsNumber
            || (previousIsName && secondPrevious >= 0 && StartsWithDigit(text, tokens[secondPrevious]));
        var strongPrefix = previous < 0
            || char.IsLower(text[tokens[previous].Start])
            || IsOpeningMark(text, tokens[previous])
            || Determiners.Contains(tokens[previous].In(text).ToString());

        // The decision order of the design: strong evidence first, then the single cue, then the
        // competing and the missing cues (those two are counted as ambiguous).
        if (strongSuffix)
        {
            return (suffix, false);
        }

        if (strongPrefix && prefixCue)
        {
            return (prefix, false);
        }

        if (prefixCue && !suffixCue)
        {
            return (prefix, false);
        }

        if (suffixCue && !prefixCue)
        {
            return (suffix, false);
        }

        if (prefixCue && suffixCue)
        {
            return (prefix, true);
        }

        // Neither cue: a preceding determiner or lower-case word still points to a title; otherwise
        // the documented default decides, and that guess is counted.
        return strongPrefix ? (prefix, false) : (defaultRow, true);
    }

    /// <summary>
    ///     Applies the single-letter initial rule: <c>J.</c> followed by a name or another initial
    ///     is the letter itself.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="tokens">The tokens.</param>
    /// <param name="index">The candidate's index.</param>
    /// <param name="span">The candidate token's characters.</param>
    /// <param name="match">The resolution when found.</param>
    /// <returns><see langword="true"/> when the token is an initial in a name.</returns>
    private static bool TryResolveInitial(string text, ReadOnlySpan<NormalizationToken> tokens, int index, ReadOnlySpan<char> span, out AbbreviationMatch match)
    {
        match = default;

        // Exactly one upper-case letter and its period, kept in one token by the tokenizer.
        if (span.Length != 2 || !char.IsUpper(span[0]) || span[1] != '.')
        {
            return false;
        }

        // The initial must be followed by a name (not a sentence-starting word) or another initial,
        // so that "Vitamin C. The end" keeps its sentence break.
        var next = NextIndex(text, tokens, index);
        if (next < 0 || !(IsNameLike(text, tokens[next]) || IsInitial(text, tokens[next])))
        {
            return false;
        }

        match = new AbbreviationMatch(span[..1].ToString(), AbbreviationClass.Prefix, false, false);
        return true;
    }

    /// <summary>
    ///     Finds the previous token, skipping a possessive clitic (<c>Patrick's St.</c> looks at
    ///     <c>Patrick</c>).
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="tokens">The tokens.</param>
    /// <param name="index">The index whose predecessor is wanted.</param>
    /// <returns>The index of the previous token, or -1 when there is none.</returns>
    private static int PreviousIndex(string text, ReadOnlySpan<NormalizationToken> tokens, int index)
    {
        var previous = index - 1;
        if (previous >= 1 && IsPossessiveClitic(tokens[previous].In(text)))
        {
            previous--;
        }

        return previous;
    }

    /// <summary>
    ///     Finds the next token after <paramref name="index"/>, skipping closing quotes and
    ///     brackets.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="tokens">The tokens.</param>
    /// <param name="index">The index whose successor is wanted.</param>
    /// <returns>The index of the next token, or -1 when there is none.</returns>
    private static int NextIndex(string text, ReadOnlySpan<NormalizationToken> tokens, int index)
    {
        var next = index + 1;
        while (next < tokens.Length && IsClosingMark(text, tokens[next]))
        {
            next++;
        }

        return next < tokens.Length ? next : -1;
    }

    /// <summary>
    ///     Tells whether a token is a possessive clitic (an apostrophe and an <c>s</c>).
    /// </summary>
    /// <param name="span">The token's characters.</param>
    /// <returns><see langword="true"/> for <c>'s</c> in any apostrophe style.</returns>
    private static bool IsPossessiveClitic(ReadOnlySpan<char> span) =>
        span.Length == 2 && IsApostrophe(span[0]) && (span[1] == 's' || span[1] == 'S');

    /// <summary>
    ///     Tells whether a character is an ASCII or typographic apostrophe.
    /// </summary>
    /// <param name="character">The character.</param>
    /// <returns><see langword="true"/> for U+0027, U+2018, U+2019, U+201B and U+02BC.</returns>
    private static bool IsApostrophe(char character) =>
        character is '\'' or '\u2018' or '\u2019' or '\u201B' or '\u02BC';

    /// <summary>
    ///     Tells whether a token is a closing quote or bracket.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="token">The token.</param>
    /// <returns><see langword="true"/> for a single closing character.</returns>
    private static bool IsClosingMark(string text, NormalizationToken token) =>
        token.Length == 1 && text[token.Start] is ')' or ']' or '}' or '"' or '\'' or '\u201D' or '\u2019' or '\u00BB';

    /// <summary>
    ///     Tells whether a token is an opening quote or bracket.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="token">The token.</param>
    /// <returns><see langword="true"/> for a single opening character.</returns>
    private static bool IsOpeningMark(string text, NormalizationToken token) =>
        token.Length == 1 && text[token.Start] is '(' or '[' or '{' or '"' or '\'' or '\u201C' or '\u2018' or '\u00AB';

    /// <summary>
    ///     Tells whether a token starts with a decimal digit (a number or an ordinal).
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="token">The token.</param>
    /// <returns><see langword="true"/> when the first character is a digit.</returns>
    private static bool StartsWithDigit(string text, NormalizationToken token) => char.IsDigit(text[token.Start]);

    /// <summary>
    ///     Tells whether a token looks like (part of) a name: it starts with an upper-case letter
    ///     and is not one of the words that merely begin sentences.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="token">The token.</param>
    /// <returns><see langword="true"/> for a capitalized, non-sentence-starting word.</returns>
    private static bool IsNameLike(string text, NormalizationToken token) =>
        char.IsUpper(text[token.Start]) && !SentenceStarters.Contains(token.In(text).ToString());

    /// <summary>
    ///     Tells whether a token is a name suffix or credential (<c>Jr.</c>, <c>Ph.D.</c>,
    ///     <c>III</c>) that continues a name, with or without its final period. Only a short,
    ///     capitalized token is looked up, so the check is constant time.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="tokens">The tokens.</param>
    /// <param name="index">The token's index.</param>
    /// <returns><see langword="true"/> for a listed name suffix or credential.</returns>
    internal static bool IsNameContinuation(string text, ReadOnlySpan<NormalizationToken> tokens, int index)
    {
        var token = tokens[index];
        var length = token.Length > 0 && text[token.Start + token.Length - 1] == '.' ? token.Length - 1 : token.Length;
        return length is >= 2 and <= 6
            && char.IsUpper(text[token.Start])
            && NameContinuations.Contains(text.Substring(token.Start, length));
    }

    /// <summary>
    ///     Tells whether a token is a single-letter initial such as <c>J.</c>.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="token">The token.</param>
    /// <returns><see langword="true"/> for an upper-case letter and a period.</returns>
    private static bool IsInitial(string text, NormalizationToken token) =>
        token.Length == 2 && char.IsUpper(text[token.Start]) && text[token.Start + 1] == '.';

    /// <summary>
    ///     Loads the embedded lexicon.
    /// </summary>
    /// <returns>The default resolver.</returns>
    /// <exception cref="InvalidDataException">The resource is missing or malformed.</exception>
    private static AbbreviationResolver LoadDefault()
    {
        using var stream = typeof(AbbreviationResolver).GetTypeInfo().Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidDataException($"The embedded resource '{ResourceName}' is missing.");
        using var reader = new StreamReader(stream);
        return FromTsv(reader);
    }

    /// <summary>
    ///     Parses one data line of the lexicon.
    /// </summary>
    /// <param name="line">The line, without its line terminator.</param>
    /// <param name="lineNumber">The one-based line number for error messages.</param>
    /// <returns>The row.</returns>
    /// <exception cref="InvalidDataException">The line is malformed.</exception>
    private static Row ParseRow(string line, int lineNumber)
    {
        var columns = line.Split('\t');
        if (columns.Length != ColumnCount)
        {
            throw new InvalidDataException($"Line {lineNumber}: expected {ColumnCount} columns but found {columns.Length}.");
        }

        var form = columns[0];
        var spoken = columns[1];
        if (form.Length == 0 || form.Trim() != form)
        {
            throw new InvalidDataException($"Line {lineNumber}: the form is empty or has surrounding whitespace.");
        }

        if (spoken.Length == 0 || spoken.Trim() != spoken)
        {
            throw new InvalidDataException($"Line {lineNumber}: the spoken text is empty or has surrounding whitespace.");
        }

        var abbreviationClass = columns[2] switch
        {
            "prefix" => AbbreviationClass.Prefix,
            "suffix" => AbbreviationClass.Suffix,
            "free" => AbbreviationClass.Free,
            "word" => AbbreviationClass.Word,
            _ => throw new InvalidDataException($"Line {lineNumber}: unknown class '{columns[2]}'."),
        };

        var isDefault = columns[3] switch
        {
            "yes" => true,
            "no" => false,
            _ => throw new InvalidDataException($"Line {lineNumber}: the default column must be 'yes' or 'no'."),
        };

        var guards = GuardKinds.None;
        if (columns[4] != "-")
        {
            foreach (var name in columns[4].Split('+'))
            {
                guards |= name switch
                {
                    "next-cap" => GuardKinds.NextCap,
                    "next-digit" => GuardKinds.NextDigit,
                    "prev-not-number" => GuardKinds.PrevNotNumber,
                    _ => throw new InvalidDataException($"Line {lineNumber}: unknown guard '{name}'."),
                };
            }
        }

        return new Row(form, spoken, abbreviationClass, isDefault, guards);
    }

    /// <summary>The conditions a lexicon row may require of its neighbors.</summary>
    [Flags]
    private enum GuardKinds
    {
        /// <summary>No condition.</summary>
        None = 0,

        /// <summary>The next token starts with an upper-case letter.</summary>
        NextCap = 1,

        /// <summary>The next token starts with a digit.</summary>
        NextDigit = 2,

        /// <summary>The previous token does not start with a digit.</summary>
        PrevNotNumber = 4,
    }

    /// <summary>
    ///     One reading of one written form.
    /// </summary>
    /// <param name="Form">The written form.</param>
    /// <param name="Spoken">The spoken replacement.</param>
    /// <param name="Class">The class of the reading.</param>
    /// <param name="IsDefault">Whether this is the form's default reading.</param>
    /// <param name="Guards">The neighbor conditions the reading requires.</param>
    private sealed record Row(string Form, string Spoken, AbbreviationClass Class, bool IsDefault, GuardKinds Guards)
    {
        /// <summary>Gets a value that indicates whether the reading has any neighbor condition.</summary>
        internal bool HasGuard => Guards != GuardKinds.None;

        /// <summary>
        ///     Tells whether every neighbor condition of this reading holds.
        /// </summary>
        /// <param name="text">The text.</param>
        /// <param name="tokens">The tokens.</param>
        /// <param name="index">The index of the candidate's first token.</param>
        /// <param name="includesPeriodToken">Whether the candidate includes its separate period token.</param>
        /// <returns><see langword="true"/> when all conditions hold.</returns>
        internal bool GuardsPass(string text, ReadOnlySpan<NormalizationToken> tokens, int index, bool includesPeriodToken)
        {
            if (Guards == GuardKinds.None)
            {
                return true;
            }

            var next = NextIndex(text, tokens, includesPeriodToken ? index + 1 : index);
            if (Guards.HasFlag(GuardKinds.NextCap) && (next < 0 || !char.IsUpper(text[tokens[next].Start])))
            {
                return false;
            }

            if (Guards.HasFlag(GuardKinds.NextDigit) && (next < 0 || !char.IsDigit(text[tokens[next].Start])))
            {
                return false;
            }

            return !Guards.HasFlag(GuardKinds.PrevNotNumber) || index == 0 || !char.IsDigit(text[tokens[index - 1].Start]);
        }
    }
}
