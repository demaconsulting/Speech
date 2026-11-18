namespace DemaConsulting.Speech.Text.En.NormalizationSubsystem;

/// <summary>
///     One replacement a numeric match asks for: the exact source characters it covers and how
///     they are spoken.
/// </summary>
/// <param name="Start">The UTF-16 offset of the first covered character.</param>
/// <param name="Length">The number of covered UTF-16 code units; positive.</param>
/// <param name="Spoken">The spoken words: lowercase letters and single spaces (capitals only in month names, the separate spelled meridiem span <c>P M</c> or <c>A M</c>, <c>Celsius</c> and <c>Fahrenheit</c>), an apostrophe only in <c>o'clock</c>, never a digit.</param>
/// <param name="Kind">One of <see cref="NormalizationKind.Number"/>, <see cref="NormalizationKind.Currency"/>, <see cref="NormalizationKind.DateTime"/> or <see cref="NormalizationKind.Unit"/>; the one spelled capital letter glued to a number (<c>10K</c>) is <see cref="NormalizationKind.Initialism"/>. A <c>DateTime</c> span whose spoken text is a letter sequence (the meridiem letters) is the spelled span; a glued meridiem is always its own span, never part of the words of the number.</param>
internal readonly record struct NumericSpan(int Start, int Length, string Spoken, NormalizationKind Kind);

/// <summary>
///     The outcome of recognizing a numeric expression at one token: up to two replacement spans,
///     the number of tokens the expression consumed, and the counts it asks the pass to record.
/// </summary>
/// <remarks>
///     A plain value (no heap data besides the spoken strings): the recognizer never allocates a
///     list per token. A match with no spans and <see cref="Fallback"/> set means the token looks
///     numeric but is left as written because it is outside the documented limits or shapes.
/// </remarks>
internal readonly struct NumericMatch
{
    /// <summary>The first span.</summary>
    private readonly NumericSpan _first;

    /// <summary>The second span, when <see cref="SpanCount"/> is 2.</summary>
    private readonly NumericSpan _second;

    /// <summary>
    ///     Initializes a new match.
    /// </summary>
    /// <param name="first">The first span (ignored when <paramref name="spanCount"/> is 0).</param>
    /// <param name="second">The second span (ignored unless <paramref name="spanCount"/> is 2).</param>
    /// <param name="spanCount">The number of spans, 0 to 2.</param>
    /// <param name="tokensConsumed">The number of tokens the expression covers from the starting token, at least 1.</param>
    /// <param name="ambiguous">Whether the reading chose a documented default among competing readings.</param>
    /// <param name="fallback">Whether the expression hit a documented limit or an unsupported numeric shape.</param>
    /// <param name="periodUnit">Whether the last span is a unit token that may carry an abbreviation period.</param>
    internal NumericMatch(NumericSpan first, NumericSpan second, int spanCount, int tokensConsumed, bool ambiguous, bool fallback, bool periodUnit)
    {
        _first = first;
        _second = second;
        SpanCount = spanCount;
        TokensConsumed = tokensConsumed;
        Ambiguous = ambiguous;
        Fallback = fallback;
        PeriodUnit = periodUnit;
    }

    /// <summary>Gets the number of spans: 0 (only a fallback), 1 or 2.</summary>
    internal int SpanCount { get; }

    /// <summary>Gets the number of tokens the expression consumed, counted from the starting token; at least 1.</summary>
    internal int TokensConsumed { get; }

    /// <summary>Gets a value that indicates whether the reading is to be counted as ambiguous (at most one count per match).</summary>
    internal bool Ambiguous { get; }

    /// <summary>Gets a value that indicates whether the expression is to be counted as a numeric fallback.</summary>
    internal bool Fallback { get; }

    /// <summary>
    ///     Gets a value that indicates whether the last span covers a unit token that may be
    ///     followed by, or may itself end with, an abbreviation period (<c>ft.</c>, <c>lbs</c> and
    ///     <c>.</c>), so the normalizer applies its period and sentence rules to it.
    /// </summary>
    internal bool PeriodUnit { get; }

    /// <summary>
    ///     Gets one span.
    /// </summary>
    /// <param name="index">The span's index, below <see cref="SpanCount"/>.</param>
    /// <returns>The span.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is not below <see cref="SpanCount"/>.</exception>
    internal NumericSpan this[int index]
    {
        get
        {
            if ((uint)index >= (uint)SpanCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return index == 0 ? _first : _second;
        }
    }
}

/// <summary>
///     Recognizes the numeric expressions of English text - numbers, ordinals, decimals,
///     fractions, years and decades, currency, percent, units, times, dates, digit groups - at a
///     token, and says how each token of the expression is spoken.
/// </summary>
/// <remarks>
///     <para>
///     Every scanner is hand written, reads each token's characters at most a constant number
///     of times, and never backtracks; there is no regular expression anywhere. A token longer
///     than <see cref="MaxTokenLength"/> characters is never scanned (a numeric-looking one is a
///     fallback), a number has at most <see cref="MaxGroups"/> separator-delimited digit groups,
///     and the digit-count limits of <see cref="NumberVerbalizer"/> apply (a cardinal up to 15
///     digits, 16 to 64 digits read digit by digit and counted as a fallback, more left as
///     written). Look-behind and look-ahead are bounded to three tokens, and a date context
///     never crosses a sentence boundary the normalizer keeps (only the period of a month
///     abbreviation such as <c>Jan.</c> continues a date; the period after a full month name
///     or <c>May</c> ends the sentence).
///     </para>
///     <para>
///     The recognizer follows the per-token decomposition rule: it never produces a span that
///     contains whitespace, never a <c>;</c>, and a <c>,</c> or <c>:</c> only between two ASCII
///     digits. A construct of several tokens becomes several spans in source order - one per
///     token, or one for a currency symbol (or an apostrophe) with the adjacent number token -
///     never reordered and never merged across a gap. Only the ASCII digits <c>0-9</c> make a
///     number. All rules are en-US (<c>,</c> thousands, <c>.</c> decimal, month before day).
///     The class is stateless and thread-safe; all tables are read-only.
///     </para>
/// </remarks>
internal static class NumericExpressionRecognizer
{
    /// <summary>The longest token that is scanned; a longer numeric-looking token is a fallback.</summary>
    internal const int MaxTokenLength = 128;

    /// <summary>The largest number of digit groups (digits separated by one punctuation character) in one number.</summary>
    internal const int MaxGroups = 8;

    /// <summary>The longest neighbor token that is examined as a word (unit, month, cue word).</summary>
    private const int MaxWordLength = 16;

    /// <summary>The full month names, January first.</summary>
    private static readonly string[] MonthNameTable =
    [
        "January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December",
    ];

    /// <summary>The month abbreviations, written without a period.</summary>
    private static readonly string[] MonthAbbreviationTable =
    [
        "Jan", "Feb", "Mar", "Apr", "Jun", "Jul", "Aug", "Sep", "Sept", "Oct", "Nov", "Dec",
    ];

    /// <summary>The words directly before a four-digit number that make it a year.</summary>
    private static readonly string[] YearCues =
    [
        "in", "since", "until", "till", "by", "during", "circa", "before", "after", "year",
    ];

    /// <summary>The words directly before a four-digit number that make it a quantity.</summary>
    private static readonly string[] QuantityCues =
    [
        "about", "around", "over", "under", "nearly", "approximately", "roughly", "exactly", "only", "just",
        "almost", "than", "all", "some", "another", "these", "those", "many", "few", "total",
    ];

    /// <summary>The nouns directly after a four-digit number that make it a quantity.</summary>
    private static readonly string[] QuantityNouns =
    [
        "people", "persons", "users", "items", "words", "steps", "times", "days", "hours", "minutes", "seconds",
        "dollars", "euros", "pounds", "miles", "feet", "meters", "kilometers", "units", "pieces", "copies",
        "cases", "points", "votes", "members", "students", "employees", "customers", "participants",
        "visitors", "calories", "years",
    ];

    /// <summary>The eras that, directly after a number, make it a year.</summary>
    private static readonly string[] EraWords = ["AD", "A.D.", "BC", "B.C.", "BCE", "CE"];

    /// <summary>The magnitude words that follow a number (<c>5 million</c>).</summary>
    private static readonly string[] MagnitudeWords = ["thousand", "million", "billion", "trillion"];

    /// <summary>The units that may follow a number; matching is case-sensitive.</summary>
    private static readonly UnitEntry[] UnitTable =
    [
        new("mm", "millimeter", "millimeters", false),
        new("cm", "centimeter", "centimeters", false),
        new("km", "kilometer", "kilometers", false),
        new("ft", "foot", "feet", true),
        new("Ft", "foot", "feet", true),
        new("yd", "yard", "yards", true),
        new("mi", "mile", "miles", true),
        new("mph", "mile per hour", "miles per hour", false),
        new("km/h", "kilometer per hour", "kilometers per hour", false),
        new("mg", "milligram", "milligrams", false),
        new("kg", "kilogram", "kilograms", false),
        new("lb", "pound", "pounds", true),
        new("lbs", "pound", "pounds", true),
        new("oz", "ounce", "ounces", true),
        new("ml", "milliliter", "milliliters", false),
        new("KB", "kilobyte", "kilobytes", false),
        new("MB", "megabyte", "megabytes", false),
        new("GB", "gigabyte", "gigabytes", false),
        new("TB", "terabyte", "terabytes", false),
        new("Hz", "hertz", "hertz", false),
        new("kHz", "kilohertz", "kilohertz", false),
        new("MHz", "megahertz", "megahertz", false),
        new("GHz", "gigahertz", "gigahertz", false),
        new("ms", "millisecond", "milliseconds", false),
        new("hr", "hour", "hours", true),
        new("hrs", "hour", "hours", true),
        new("min", "minute", "minutes", true),
        new("\u00B0", "degree", "degrees", false),
        new("\u00B0C", "degree Celsius", "degrees Celsius", false),
        new("\u00B0F", "degree Fahrenheit", "degrees Fahrenheit", false),
    ];

    /// <summary>The currencies, by symbol and code.</summary>
    private static readonly CurrencyEntry[] CurrencyTable =
    [
        new('$', "USD", "dollar", "dollars", "cent", "cents"),
        new('\u20AC', "EUR", "euro", "euros", "cent", "cents"),
        new('\u00A3', "GBP", "pound", "pounds", "penny", "pence"),
        new('\u00A5', "JPY", "yen", "yen", null, null),
    ];

    /// <summary>The denominators a slash form may have to be read as a fraction.</summary>
    private static readonly int[] FractionDenominators = [2, 3, 4, 5, 6, 7, 8, 9, 10, 16, 32, 64, 100];

    /// <summary>The kinds of separator-delimited digit-group shapes of a numeric token.</summary>
    private enum Shape
    {
        /// <summary>An integer, a decimal, or a number with en-US thousands separators.</summary>
        Plain,

        /// <summary>Three or more groups separated by periods (<c>192.168.0.1</c>).</summary>
        Dotted,

        /// <summary>Two groups separated by a colon (<c>5:30</c>).</summary>
        Time,

        /// <summary>Groups separated by slashes (a fraction or a date).</summary>
        Slash,

        /// <summary>Groups separated by hyphens (a range, a phone number, an identifier or a date).</summary>
        Hyphen,

        /// <summary>Only commas and periods, but not a valid en-US number (<c>1,23</c>, <c>1.234,5</c>).</summary>
        Bad,

        /// <summary>Any other mixture of separators; left alone.</summary>
        Other,
    }

    /// <summary>One row of the unit table.</summary>
    /// <param name="Written">The written form, case-sensitive.</param>
    /// <param name="Singular">The spoken singular, used for exactly one.</param>
    /// <param name="Plural">The spoken plural.</param>
    /// <param name="AllowsPeriod">Whether the written form may carry an abbreviation period.</param>
    private readonly record struct UnitEntry(string Written, string Singular, string Plural, bool AllowsPeriod);

    /// <summary>One row of the currency table.</summary>
    /// <param name="Symbol">The currency symbol.</param>
    /// <param name="Code">The three-letter code.</param>
    /// <param name="Singular">The spoken singular of the major unit.</param>
    /// <param name="Plural">The spoken plural of the major unit.</param>
    /// <param name="MinorSingular">The spoken singular of the minor unit, or <see langword="null"/> when the currency has none in use.</param>
    /// <param name="MinorPlural">The spoken plural of the minor unit, or <see langword="null"/>.</param>
    private readonly record struct CurrencyEntry(char Symbol, string Code, string Singular, string Plural, string? MinorSingular, string? MinorPlural);

    /// <summary>
    ///     The scanned state of one numeric token: its optional prefix, its digit groups and the
    ///     text after the digits.
    /// </summary>
    private ref struct Ctx
    {
        /// <summary>The text.</summary>
        internal string Text;

        /// <summary>The tokens of the text.</summary>
        internal ReadOnlySpan<NormalizationToken> Tokens;

        /// <summary>The index of the scanned token.</summary>
        internal int Index;

        /// <summary>The scanned token.</summary>
        internal NormalizationToken Token;

        /// <summary>The token's characters.</summary>
        internal ReadOnlySpan<char> Span;

        /// <summary>The start and length of each digit group, two integers per group, as offsets into <see cref="Span"/>.</summary>
        internal Span<int> Groups;

        /// <summary>Working memory for the digits of an integer part with separators removed.</summary>
        internal Span<char> Scratch;

        /// <summary>The sign: <c>'-'</c>, <c>'+'</c> or <c>'\0'</c> for none (the minus sign U+2212 is normalized to <c>'-'</c>).</summary>
        internal char Sign;

        /// <summary>The currency symbol written before the digits, or <c>'\0'</c>.</summary>
        internal char Symbol;

        /// <summary>Whether a <c>#</c> precedes the digits.</summary>
        internal bool Hash;

        /// <summary>The number of digit groups (an empty group before a leading period counts).</summary>
        internal int GroupCount;

        /// <summary>The offset in <see cref="Span"/> where the text after the digits starts.</summary>
        internal int RestStart;

        /// <summary>Whether the token has more than <see cref="MaxGroups"/> groups.</summary>
        internal bool TooMany;

        /// <summary>Gets the text after the digits.</summary>
        internal readonly ReadOnlySpan<char> Rest => Span[RestStart..];

        /// <summary>Gets one digit group.</summary>
        /// <param name="group">The group's index, below <see cref="GroupCount"/>.</param>
        /// <returns>The group's digits.</returns>
        internal readonly ReadOnlySpan<char> Group(int group) => Span.Slice(Groups[2 * group], Groups[(2 * group) + 1]);

        /// <summary>Gets the separator before a group.</summary>
        /// <param name="group">The group's index, at least 1.</param>
        /// <returns>The separator character.</returns>
        internal readonly char Separator(int group) => Span[Groups[2 * group] - 1];
    }

    /// <summary>The collector of the spans of one match.</summary>
    private struct Builder
    {
        /// <summary>The first span.</summary>
        private NumericSpan _first;

        /// <summary>The second span.</summary>
        private NumericSpan _second;

        /// <summary>The number of spans added.</summary>
        private int _count;

        /// <summary>The number of tokens consumed; set by the caller, at least 1.</summary>
        internal int Consumed;

        /// <summary>Whether the match is ambiguous.</summary>
        internal bool Ambiguous;

        /// <summary>Whether the match is a fallback.</summary>
        internal bool Fallback;

        /// <summary>Whether the last span is a unit token that may carry a period.</summary>
        internal bool PeriodUnit;

        /// <summary>
        ///     Adds a span.
        /// </summary>
        /// <param name="start">The span's start.</param>
        /// <param name="length">The span's length.</param>
        /// <param name="spoken">The spoken words.</param>
        /// <param name="kind">The span's kind.</param>
        internal void Add(int start, int length, string spoken, NormalizationKind kind)
        {
            var span = new NumericSpan(start, length, spoken, kind);
            if (_count == 0)
            {
                _first = span;
            }
            else
            {
                _second = span;
            }

            _count++;
        }

        /// <summary>
        ///     Produces the finished match.
        /// </summary>
        /// <returns>The match.</returns>
        internal readonly NumericMatch ToMatch() => new(_first, _second, _count, Consumed < 1 ? 1 : Consumed, Ambiguous, Fallback, PeriodUnit);
    }

    /// <summary>
    ///     Tells whether a token is a month: a Title Case full name, or a Title Case abbreviation
    ///     with or without one trailing period.
    /// </summary>
    /// <param name="token">The token's characters.</param>
    /// <returns><see langword="true"/> for <c>January</c>, <c>Jan</c> and <c>Jan.</c>; <see langword="false"/> for <c>jan</c>, <c>MAY</c> and any other word.</returns>
    internal static bool IsMonthToken(ReadOnlySpan<char> token)
    {
        // Reject long tokens first so the table comparison is constant time.
        if (token.Length is 0 or > 10)
        {
            return false;
        }

        var word = token[^1] == '.' ? token[..^1] : token;
        return IsOneOf(word, MonthNameTable) || IsOneOf(word, MonthAbbreviationTable);
    }

    /// <summary>
    ///     Decides whether the token at <paramref name="index"/> starts a numeric expression and,
    ///     if so, how each token of it is spoken.
    /// </summary>
    /// <param name="text">The text the tokens index. Must not be null.</param>
    /// <param name="tokens">The tokens of the text in order.</param>
    /// <param name="index">The index of the candidate token.</param>
    /// <param name="match">The expression when this method returns <see langword="true"/>.</param>
    /// <returns>
    ///     <see langword="true"/> when the token starts a numeric expression (spans, a fallback, or
    ///     both); <see langword="false"/> for any token that is not numeric, in which case no
    ///     fallback is counted.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is not an index of <paramref name="tokens"/>.</exception>
    /// <exception cref="ArgumentException">A token that is read is empty or outside the text.</exception>
    internal static bool TryRecognize(string text, ReadOnlySpan<NormalizationToken> tokens, int index, out NumericMatch match)
    {
        ArgumentNullException.ThrowIfNull(text);
        if ((uint)index >= (uint)tokens.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        match = default;
        var token = ValidToken(text, tokens, index);
        var span = text.AsSpan(token.Start, token.Length);
        var first = span[0];

        // Single-character tokens that start an expression of their own.
        if (token.Length == 1)
        {
            if (IsCurrencySymbol(first))
            {
                return TryCurrencyPair(text, tokens, index, out match);
            }

            if (first is '\'' or '\u2019' or '\u2018')
            {
                return TryApostropheDecade(text, tokens, index, out match);
            }

            if (first == '\u2013')
            {
                return TryRangeDash(text, tokens, index, out match);
            }

            if (TryVulgarFraction(first, out var vulgarNumerator, out var vulgarDenominator))
            {
                var builder = new Builder { Consumed = 1 };
                builder.Add(token.Start, 1, NumberVerbalizer.Fraction(vulgarNumerator, vulgarDenominator), NormalizationKind.Number);
                match = builder.ToMatch();
                return true;
            }
        }

        // The cheap first-character test: only tokens that can start a number are scanned.
        if (ParsePrefix(span, true, out _, out _, out _) < 0)
        {
            return false;
        }

        // A numeric-looking token beyond the cap is not scanned at all.
        if (token.Length > MaxTokenLength)
        {
            match = FallbackMatch();
            return true;
        }

        Span<char> scratch = stackalloc char[MaxTokenLength];
        Span<int> groups = stackalloc int[2 * MaxGroups];
        var context = new Ctx { Text = text, Tokens = tokens, Index = index, Token = token, Span = span, Groups = groups, Scratch = scratch };
        Scan(ref context, true);
        return Dispatch(ref context, out match);
    }

    /// <summary>
    ///     Routes a scanned token to the reader of its shape.
    /// </summary>
    /// <param name="c">The scanned token.</param>
    /// <param name="match">The match when this method returns <see langword="true"/>.</param>
    /// <returns><see langword="true"/> when the token is a numeric expression or a fallback.</returns>
    private static bool Dispatch(ref Ctx c, out NumericMatch match)
    {
        match = default;
        if (c.TooMany)
        {
            match = FallbackMatch();
            return true;
        }

        switch (Classify(ref c))
        {
            case Shape.Plain:
                return TryPlain(ref c, out match);
            case Shape.Bad:
                // Pure digits with commas and periods that are not en-US: left as written and counted.
                if (c.Sign == '\0' && c.Symbol == '\0' && !c.Hash && c.Rest.Length == 0)
                {
                    match = FallbackMatch();
                    return true;
                }

                return false;
            case Shape.Dotted:
                return TryDotted(ref c, out match);
            case Shape.Time:
                return TryTime(ref c, out match);
            case Shape.Slash:
                return TrySlash(ref c, out match);
            case Shape.Hyphen:
                return TryHyphen(ref c, out match);
            default:
                return false;
        }
    }

    /// <summary>
    ///     Creates the match of a numeric-looking token that is left as written.
    /// </summary>
    /// <returns>A match with no span and the fallback flag set.</returns>
    private static NumericMatch FallbackMatch() => new(default, default, 0, 1, false, true, false);

    /// <summary>
    ///     Reads an optional sign, currency symbol or hash and finds where the digits start.
    /// </summary>
    /// <param name="token">The token's characters.</param>
    /// <param name="allowSymbolAndHash">Whether a currency symbol or <c>#</c> may precede the digits.</param>
    /// <param name="sign">The sign, or <c>'\0'</c>.</param>
    /// <param name="symbol">The currency symbol, or <c>'\0'</c>.</param>
    /// <param name="hash">Whether a <c>#</c> was read.</param>
    /// <returns>The offset of the first digit (or of the period before a leading-period decimal), or -1 when the token does not start like a number.</returns>
    private static int ParsePrefix(ReadOnlySpan<char> token, bool allowSymbolAndHash, out char sign, out char symbol, out bool hash)
    {
        sign = '\0';
        symbol = '\0';
        hash = false;
        var position = 0;
        if (token.Length > 1 && token[0] is '-' or '+' or '\u2212')
        {
            sign = token[0] == '\u2212' ? '-' : token[0];
            position = 1;
        }

        if (allowSymbolAndHash)
        {
            if (position < token.Length && IsCurrencySymbol(token[position]))
            {
                symbol = token[position];
                position++;

                // "$-5": a sign may follow the symbol when none came before it.
                if (sign == '\0' && position + 1 < token.Length && token[position] is '-' or '+' or '\u2212')
                {
                    sign = token[position] == '\u2212' ? '-' : token[position];
                    position++;
                }
            }
            else if (sign == '\0' && position < token.Length && token[position] == '#')
            {
                hash = true;
                position++;
            }
        }

        if (position >= token.Length)
        {
            return -1;
        }

        if (IsDigit(token[position]) || (token[position] == '.' && position + 1 < token.Length && IsDigit(token[position + 1])))
        {
            return position;
        }

        return -1;
    }

    /// <summary>
    ///     Scans the prefix, the digit groups and the start of the rest of a token, once, left to
    ///     right.
    /// </summary>
    /// <param name="c">The context to fill; its token must already start like a number.</param>
    /// <param name="allowSymbolAndHash">Whether a currency symbol or <c>#</c> may precede the digits.</param>
    private static void Scan(ref Ctx c, bool allowSymbolAndHash)
    {
        var token = c.Span;
        var position = ParsePrefix(token, allowSymbolAndHash, out var sign, out var symbol, out var hash);
        c.Sign = sign;
        c.Symbol = symbol;
        c.Hash = hash;
        c.GroupCount = 0;
        c.TooMany = false;

        // A leading period starts an empty integer group (".5").
        var groupCount = 0;
        if (token[position] == '.')
        {
            c.Groups[0] = position;
            c.Groups[1] = 0;
            groupCount = 1;
            position++;
        }

        // One digit group per iteration; a separator continues the number only when a digit
        // follows it, so a trailing "," or "." stays in the rest and makes the token non-numeric.
        while (true)
        {
            var start = position;
            while (position < token.Length && IsDigit(token[position]))
            {
                position++;
            }

            if (groupCount >= MaxGroups)
            {
                c.TooMany = true;
                break;
            }

            c.Groups[2 * groupCount] = start;
            c.Groups[(2 * groupCount) + 1] = position - start;
            groupCount++;
            if (position + 1 < token.Length && token[position] is ',' or '.' or ':' or '/' or '-' && IsDigit(token[position + 1]))
            {
                position++;
                continue;
            }

            break;
        }

        c.GroupCount = groupCount;
        c.RestStart = position;
    }

    /// <summary>
    ///     Classifies the separators between the digit groups of a scanned token.
    /// </summary>
    /// <param name="c">The scanned token.</param>
    /// <returns>The shape.</returns>
    private static Shape Classify(ref Ctx c)
    {
        var count = c.GroupCount;
        if (count == 1)
        {
            return Shape.Plain;
        }

        int commas = 0, dots = 0, colons = 0, slashes = 0, hyphens = 0;
        for (var group = 1; group < count; group++)
        {
            switch (c.Separator(group))
            {
                case ',':
                    commas++;
                    break;
                case '.':
                    dots++;
                    break;
                case ':':
                    colons++;
                    break;
                case '/':
                    slashes++;
                    break;
                default:
                    hyphens++;
                    break;
            }
        }

        // A colon, slash or hyphen shape must use that one separator only.
        var others = (colons > 0 ? 1 : 0) + (slashes > 0 ? 1 : 0) + (hyphens > 0 ? 1 : 0);
        if (others > 0)
        {
            if (others > 1 || commas + dots > 0)
            {
                return Shape.Other;
            }

            if (colons > 0)
            {
                return Shape.Time;
            }

            return slashes > 0 ? Shape.Slash : Shape.Hyphen;
        }

        // Periods only: a decimal, or three or more dotted groups.
        if (commas == 0)
        {
            return count == 2 ? Shape.Plain : Shape.Dotted;
        }

        return IsValidThousands(ref c) ? Shape.Plain : Shape.Bad;
    }

    /// <summary>
    ///     Checks the en-US thousands grouping of a token that has commas: one to three digits
    ///     (not starting with zero), then groups of exactly three, then optionally one period and
    ///     the fraction.
    /// </summary>
    /// <param name="c">The scanned token.</param>
    /// <returns><see langword="true"/> for a valid grouping.</returns>
    private static bool IsValidThousands(ref Ctx c)
    {
        var count = c.GroupCount;
        var hasDecimal = c.Separator(count - 1) == '.';
        var integerGroups = hasDecimal ? count - 1 : count;
        for (var group = 1; group < count; group++)
        {
            var expected = hasDecimal && group == count - 1 ? '.' : ',';
            if (c.Separator(group) != expected)
            {
                return false;
            }
        }

        var firstGroup = c.Group(0);
        if (firstGroup.Length is < 1 or > 3 || firstGroup[0] == '0')
        {
            return false;
        }

        for (var group = 1; group < integerGroups; group++)
        {
            if (c.Group(group).Length != 3)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    ///     Copies the integer digits of a plain number (thousands separators removed) into the
    ///     scratch memory and locates its fraction.
    /// </summary>
    /// <param name="c">The scanned token, of shape <see cref="Shape.Plain"/>.</param>
    /// <param name="fractionStart">The offset of the fraction digits in the token.</param>
    /// <param name="fractionLength">The number of fraction digits; 0 for an integer.</param>
    /// <param name="hasThousands">Whether the integer part had separators.</param>
    /// <returns>The number of integer digits copied to the scratch memory (0 for <c>.5</c>).</returns>
    private static int ExtractPlain(ref Ctx c, out int fractionStart, out int fractionLength, out bool hasThousands)
    {
        var count = c.GroupCount;
        var hasDecimal = count >= 2 && c.Separator(count - 1) == '.';
        var integerGroups = hasDecimal ? count - 1 : count;
        fractionStart = hasDecimal ? c.Groups[2 * (count - 1)] : 0;
        fractionLength = hasDecimal ? c.Groups[(2 * (count - 1)) + 1] : 0;
        hasThousands = integerGroups > 1;
        var length = 0;
        for (var group = 0; group < integerGroups; group++)
        {
            var digits = c.Group(group);
            digits.CopyTo(c.Scratch[length..]);
            length += digits.Length;
        }

        return length;
    }

    /// <summary>
    ///     Reads a plain number: a hash number, a currency amount written in one token, a number
    ///     with a suffix (ordinal, decade, meridiem, unit, currency), or a bare number with its
    ///     follow-up tokens.
    /// </summary>
    /// <param name="c">The scanned token, of shape <see cref="Shape.Plain"/>.</param>
    /// <param name="match">The match when this method returns <see langword="true"/>.</param>
    /// <returns><see langword="true"/> when the token is a recognized expression or a fallback.</returns>
    private static bool TryPlain(ref Ctx c, out NumericMatch match)
    {
        match = default;
        if (c.Hash)
        {
            return TryHashNumber(ref c, out match);
        }

        if (c.Symbol != '\0')
        {
            return TryCurrencyAmount(ref c, c.Symbol, c.Token.Start, 1, out match);
        }

        return c.Rest.Length > 0 ? TrySuffixedNumber(ref c, out match) : TryBareNumber(ref c, out match);
    }

    /// <summary>
    ///     Reads <c>#5</c> as <c>number five</c>.
    /// </summary>
    /// <param name="c">The scanned token with a hash prefix.</param>
    /// <param name="match">The match when this method returns <see langword="true"/>.</param>
    /// <returns><see langword="true"/> when the token is a hash number.</returns>
    private static bool TryHashNumber(ref Ctx c, out NumericMatch match)
    {
        match = default;
        if (c.GroupCount != 1 || c.Rest.Length > 0)
        {
            return false;
        }

        var writer = new SpokenWriter(stackalloc char[NumberVerbalizer.MaxSpokenLength]);
        writer.Append("number");
        var fallback = false;
        if (!TryAppendInteger(ref writer, c.Group(0), ref fallback))
        {
            match = FallbackMatch();
            return true;
        }

        var builder = new Builder { Consumed = 1, Fallback = fallback };
        builder.Add(c.Token.Start, c.Token.Length, writer.ToString(), NormalizationKind.Number);
        match = builder.ToMatch();
        return true;
    }

    /// <summary>
    ///     Reads a number that has text after its digits: an ordinal (<c>1st</c>), a decade
    ///     (<c>1990s</c>), a meridiem time (<c>5pm</c>), a number with a vulgar fraction
    ///     (<c>5&#189;</c>), a number glued to a unit (<c>5kg</c>) or to a currency (<c>5USD</c>).
    /// </summary>
    /// <param name="c">The scanned token with a non-empty rest and no currency prefix.</param>
    /// <param name="match">The match when this method returns <see langword="true"/>.</param>
    /// <returns><see langword="true"/> when the rest is one of the documented suffixes.</returns>
    private static bool TrySuffixedNumber(ref Ctx c, out NumericMatch match)
    {
        match = default;
        var length = ExtractPlain(ref c, out var fractionStart, out var fractionLength, out var hasThousands);
        var integerDigits = c.Scratch[..length];
        var fraction = fractionLength > 0 ? c.Span.Slice(fractionStart, fractionLength) : default;
        var rest = c.Rest;
        var unsignedInteger = fractionLength == 0 && c.Sign == '\0';

        // Ordinal: 1st, 22nd, 1,000th; the suffix must be the right one for the number.
        if (unsignedInteger && rest.Length == 2 && IsLetter(rest[0]) && IsLetter(rest[1]) && IsOrdinalSuffixWord(rest))
        {
            return TryOrdinal(ref c, integerDigits, rest, out match);
        }

        // Decade: 1990s, 90s, 1990's.
        if (unsignedInteger && !hasThousands && IsDecadeSuffix(rest) && IsDecadeDigits(integerDigits))
        {
            var builder = new Builder { Consumed = 1 };
            builder.Add(c.Token.Start, c.Token.Length, NumberVerbalizer.Decade(integerDigits), NormalizationKind.Number);
            match = builder.ToMatch();
            return true;
        }

        // Meridiem glued to the hour: 5pm, 5PM. Two adjacent spans inside the one token: the hour
        // (words) and the meridiem letters (spelled), so no consumer infers spelling from a value.
        if (unsignedInteger && !hasThousands && c.GroupCount == 1 && IsMeridiem(rest, uppercaseAllowed: true) && IsHour12(integerDigits))
        {
            var builder = new Builder { Consumed = 1 };
            builder.Add(c.Token.Start, c.RestStart, NumberVerbalizer.Cardinal(integerDigits), NormalizationKind.DateTime);
            builder.Add(c.Token.Start + c.RestStart, rest.Length, MeridiemWords(rest), NormalizationKind.DateTime);
            match = builder.ToMatch();
            return true;
        }

        // A mixed number written in one token: 5 and a vulgar fraction.
        if (unsignedInteger && c.GroupCount == 1 && rest.Length == 1 && TryVulgarFraction(rest[0], out var numerator, out var denominator))
        {
            var writer = new SpokenWriter(stackalloc char[NumberVerbalizer.MaxSpokenLength]);
            var fallback = false;
            if (!TryAppendInteger(ref writer, integerDigits, ref fallback))
            {
                match = FallbackMatch();
                return true;
            }

            writer.Append("and");
            NumberVerbalizer.AppendFraction(ref writer, numerator, denominator);
            var builder = new Builder { Consumed = 1, Fallback = fallback };
            builder.Add(c.Token.Start, c.Token.Length, writer.ToString(), NormalizationKind.Number);
            match = builder.ToMatch();
            return true;
        }

        // A unit or a currency glued to the number: 5kg, 72°F, 5USD, 5$.
        if (TryGetUnit(rest, out var unit, out _))
        {
            return TryGluedWord(ref c, integerDigits, fraction, unit.Singular, unit.Plural, unit.AllowsPeriod, NormalizationKind.Unit, out match);
        }

        if (TryGetCurrencySuffix(rest, out var currency))
        {
            return TryGluedWord(ref c, integerDigits, fraction, currency.Singular, currency.Plural, false, NormalizationKind.Currency, out match);
        }

        // A number joined to a word by a hyphen (12-year-old, 24-hour, 2.5-inch, 3-D): the number is read and the
        // hyphenated word is left as it is, so the phonemizer sees "twelve-year-old", one compound.
        if (c.Sign == '\0' && rest.Length > 1 && rest[0] == '-' && IsLetter(rest[1]))
        {
            return TryHyphenatedPrefix(ref c, integerDigits, fraction, hasThousands, out match);
        }

        // A number with one capital letter glued to it (10K, 5G, 3D, 12B): the number is read and the letter is spelled,
        // which is how the written form is read aloud whatever the letter stands for (thousand, generation, dimensions).
        if (unsignedInteger && !hasThousands && c.GroupCount == 1 && rest.Length == 1 && (rest[0] is >= 'A' and <= 'Z' or 'k'))
        {
            var writer = new SpokenWriter(stackalloc char[NumberVerbalizer.MaxSpokenLength]);
            var fallback = false;
            if (!TryAppendInteger(ref writer, integerDigits, ref fallback))
            {
                match = FallbackMatch();
                return true;
            }

            var builder = new Builder { Consumed = 1, Fallback = fallback };
            builder.Add(c.Token.Start, c.RestStart, writer.ToString(), NormalizationKind.Number);
            builder.Add(c.Token.Start + c.RestStart, 1, char.ToUpperInvariant(rest[0]).ToString(), NormalizationKind.Initialism);
            match = builder.ToMatch();
            return true;
        }

        return false;
    }

    /// <summary>
    ///     Reads the number at the start of a hyphenated compound such as <c>12-year-old</c>: one span over the
    ///     digits, nothing over the hyphen and the word after it.
    /// </summary>
    /// <param name="c">The scanned token, whose rest starts with a hyphen and a letter.</param>
    /// <param name="integerDigits">The integer digits.</param>
    /// <param name="fraction">The fraction digits, or empty.</param>
    /// <param name="hasThousands">Whether the integer part had separators.</param>
    /// <param name="match">The match when this method returns <see langword="true"/>.</param>
    /// <returns><see langword="true"/> when the number is read or is a fallback.</returns>
    /// <remarks>
    ///     A four-digit integer in the year range is read like a year (<c>1500-word</c> is <i>fifteen hundred</i>,
    ///     <c>2000-seat</c> is <i>two thousand</i>), which is also how such a quantity is usually said; any other number
    ///     is read as a cardinal or decimal.
    /// </remarks>
    private static bool TryHyphenatedPrefix(ref Ctx c, ReadOnlySpan<char> integerDigits, ReadOnlySpan<char> fraction, bool hasThousands, out NumericMatch match)
    {
        match = default;
        var writer = new SpokenWriter(stackalloc char[NumberVerbalizer.MaxSpokenLength]);
        var fallback = false;
        if (fraction.IsEmpty && !hasThousands && integerDigits.Length == 4 && integerDigits[0] != '0' && IsYearRange(integerDigits))
        {
            NumberVerbalizer.AppendYear(ref writer, integerDigits);
        }
        else if (!TryAppendNumber(ref writer, c.Sign, integerDigits, fraction, ref fallback))
        {
            match = FallbackMatch();
            return true;
        }

        var builder = new Builder { Consumed = 1, Fallback = fallback };
        builder.Add(c.Token.Start, c.RestStart, writer.ToString(), NormalizationKind.Number);
        match = builder.ToMatch();
        return true;
    }

    /// <summary>
    ///     Reads a number written in one token with a unit or currency word glued to it.
    /// </summary>
    /// <param name="c">The scanned token.</param>
    /// <param name="integerDigits">The integer digits.</param>
    /// <param name="fraction">The fraction digits, or empty.</param>
    /// <param name="singular">The singular word, used for exactly one.</param>
    /// <param name="plural">The plural word.</param>
    /// <param name="periodUnit">Whether the word may carry an abbreviation period.</param>
    /// <param name="kind">The kind of the span.</param>
    /// <param name="match">The match when this method returns <see langword="true"/>.</param>
    /// <returns><see langword="true"/> when the number can be read.</returns>
    private static bool TryGluedWord(ref Ctx c, ReadOnlySpan<char> integerDigits, ReadOnlySpan<char> fraction, string singular, string plural, bool periodUnit, NormalizationKind kind, out NumericMatch match)
    {
        match = default;
        var writer = new SpokenWriter(stackalloc char[NumberVerbalizer.MaxSpokenLength]);
        var fallback = false;
        if (!TryAppendNumber(ref writer, c.Sign, integerDigits, fraction, ref fallback))
        {
            match = FallbackMatch();
            return true;
        }

        writer.Append(IsExactlyOne(integerDigits, fraction) ? singular : plural);
        var builder = new Builder { Consumed = 1, Fallback = fallback, PeriodUnit = periodUnit };
        builder.Add(c.Token.Start, c.Token.Length, writer.ToString(), kind);
        match = builder.ToMatch();
        return true;
    }

    /// <summary>
    ///     Reads an ordinal number such as <c>21st</c> or <c>1,000th</c>.
    /// </summary>
    /// <param name="c">The scanned token.</param>
    /// <param name="integerDigits">The integer digits.</param>
    /// <param name="suffix">The two suffix letters.</param>
    /// <param name="match">The match when this method returns <see langword="true"/>.</param>
    /// <returns><see langword="true"/> when the suffix matches the number (or the number is beyond the limit, a fallback).</returns>
    private static bool TryOrdinal(ref Ctx c, ReadOnlySpan<char> integerDigits, ReadOnlySpan<char> suffix, out NumericMatch match)
    {
        match = default;

        // A leading zero makes the number an identifier, never an ordinal.
        if (integerDigits.Length > 1 && integerDigits[0] == '0')
        {
            return false;
        }

        var last = integerDigits[^1] - '0';
        var lastTwo = integerDigits.Length > 1 ? ((integerDigits[^2] - '0') * 10) + last : last;
        var expected = lastTwo is >= 11 and <= 13 ? "th" : last switch { 1 => "st", 2 => "nd", 3 => "rd", _ => "th" };
        if (!suffix.Equals(expected, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (integerDigits.Length > NumberVerbalizer.MaxCardinalDigits)
        {
            match = FallbackMatch();
            return true;
        }

        // A day of the month (1 to 31, plain digits) next to a month name is part of a date, like a bare day.
        var isDay = c.GroupCount == 1 && integerDigits.Length is 1 or 2 && IsDayValue(integerDigits)
            && ((EndsWithMonth(ref c, c.Index - 1) && !IsRetainedBreakBefore(ref c, c.Index)) || NextIsMonth(ref c));
        var builder = new Builder { Consumed = 1 };
        builder.Add(c.Token.Start, c.Token.Length, NumberVerbalizer.Ordinal(integerDigits), isDay ? NormalizationKind.DateTime : NormalizationKind.Number);
        match = builder.ToMatch();
        return true;
    }

    /// <summary>
    ///     Reads a number that stands alone in its token: a day or a year in a date, a year or a
    ///     cardinal, a decimal; then looks at the next token for a percent sign, a mixed-number
    ///     fraction, a meridiem, a unit or a currency. A day is a number right after a month (or
    ///     the period of a month abbreviation) without a retained sentence boundary between them,
    ///     or right before a month.
    /// </summary>
    /// <param name="c">The scanned token with an empty rest.</param>
    /// <param name="match">The match when this method returns <see langword="true"/>.</param>
    /// <returns><see langword="true"/> when the number is recognized or is a fallback.</returns>
    private static bool TryBareNumber(ref Ctx c, out NumericMatch match)
    {
        match = default;
        var length = ExtractPlain(ref c, out var fractionStart, out var fractionLength, out _);
        var integerDigits = c.Scratch[..length];
        var fraction = fractionLength > 0 ? c.Span.Slice(fractionStart, fractionLength) : default;
        var token = c.Token;
        var isPlainInteger = c.GroupCount == 1 && c.Sign == '\0';
        var builder = new Builder { Consumed = 1 };

        // An area code in parentheses, followed by a seven-digit local number, is read digit by digit.
        if (isPlainInteger && length == 3 && IsAreaCode(ref c))
        {
            builder.Add(token.Start, token.Length, NumberVerbalizer.Digits(integerDigits), NormalizationKind.Number);
            match = builder.ToMatch();
            return true;
        }

        // A day of the month: next to a month name.
        if (isPlainInteger && length is 1 or 2 && integerDigits[0] != '0' && IsDayValue(integerDigits) && ((EndsWithMonth(ref c, c.Index - 1) && !IsRetainedBreakBefore(ref c, c.Index)) || NextIsMonth(ref c)))
        {
            builder.Add(token.Start, token.Length, NumberVerbalizer.Ordinal(integerDigits), NormalizationKind.DateTime);
            match = builder.ToMatch();
            return true;
        }

        // The number itself: a four-digit integer is a year or a cardinal by the documented policy.
        var writer = new SpokenWriter(stackalloc char[NumberVerbalizer.MaxSpokenLength]);
        var fallback = false;
        var kind = NormalizationKind.Number;
        if (isPlainInteger && length == 4 && integerDigits[0] != '0' && IsYearRange(integerDigits))
        {
            var yearValue = (integerDigits[0] - '0') * 1000 + ((integerDigits[1] - '0') * 100) + ((integerDigits[2] - '0') * 10) + (integerDigits[3] - '0');
            var decision = DecideYear(ref c, yearValue);
            if (decision.AsYear)
            {
                NumberVerbalizer.AppendYear(ref writer, integerDigits);
                kind = decision.FromDate ? NormalizationKind.DateTime : NormalizationKind.Number;
            }
            else
            {
                NumberVerbalizer.AppendCardinal(ref writer, integerDigits);
            }

            builder.Ambiguous = decision.Ambiguous && NumberVerbalizer.IsYearReadingDifferent(yearValue);
        }
        else if (!TryAppendNumber(ref writer, c.Sign, integerDigits, fraction, ref fallback))
        {
            match = FallbackMatch();
            return true;
        }

        builder.Fallback = fallback;
        builder.Add(token.Start, token.Length, writer.ToString(), kind);
        AddFollowUp(ref c, ref builder, integerDigits, fraction, isPlainInteger);
        match = builder.ToMatch();
        return true;
    }

    /// <summary>
    ///     Looks at the tokens after a bare number and adds the span of the one that completes it.
    /// </summary>
    /// <param name="c">The scanned number token.</param>
    /// <param name="builder">The builder holding the number's own span.</param>
    /// <param name="integerDigits">The integer digits of the number.</param>
    /// <param name="fraction">The fraction digits, or empty.</param>
    /// <param name="isPlainInteger">Whether the number is an unsigned integer without separators.</param>
    private static void AddFollowUp(ref Ctx c, ref Builder builder, ReadOnlySpan<char> integerDigits, ReadOnlySpan<char> fraction, bool isPlainInteger)
    {
        var nextIndex = NextJoined(c.Text, c.Tokens, c.Index);
        if (nextIndex < 0)
        {
            return;
        }

        var next = ValidToken(c.Text, c.Tokens, nextIndex);
        if (next.Length > MaxWordLength)
        {
            return;
        }

        var word = c.Text.AsSpan(next.Start, next.Length);
        var one = IsExactlyOne(integerDigits, fraction);

        // Percent: 50 % and the like.
        if (word.Length == 1 && word[0] == '%')
        {
            builder.Add(next.Start, next.Length, "percent", NormalizationKind.Number);
            builder.Consumed = 2;
            return;
        }

        // A fraction after a whole number: 3 1/2 and 3 half.
        if (isPlainInteger)
        {
            if (TrySimpleFraction(word, out var numerator, out var denominator) || (word.Length == 1 && TryVulgarFraction(word[0], out numerator, out denominator)))
            {
                var writer = new SpokenWriter(stackalloc char[NumberVerbalizer.MaxSpokenLength]);
                writer.Append("and");
                NumberVerbalizer.AppendFraction(ref writer, numerator, denominator);
                builder.Add(next.Start, next.Length, writer.ToString(), NormalizationKind.Number);
                builder.Consumed = 2;
                return;
            }

            // A lowercase meridiem after an hour: 5 pm.
            if (integerDigits.Length is 1 or 2 && IsHour12(integerDigits) && IsMeridiem(word, uppercaseAllowed: false))
            {
                builder.Add(next.Start, next.Length, MeridiemWords(word), NormalizationKind.DateTime);
                builder.Consumed = 2;
                return;
            }
        }

        // A unit: 5 kg, 6 ft., 72 °F.
        if (TryGetUnit(word, out var unit, out _))
        {
            builder.Add(next.Start, next.Length, one ? unit.Singular : unit.Plural, NormalizationKind.Unit);
            builder.Consumed = 2;
            builder.PeriodUnit = unit.AllowsPeriod;
            return;
        }

        // A currency code or symbol after the amount: 5 USD, 5.50 EUR, 5 €.
        if (TryGetCurrencySuffix(word, out var currency))
        {
            builder.Add(next.Start, next.Length, one ? currency.Singular : currency.Plural, NormalizationKind.Currency);
            builder.Consumed = 2;
            return;
        }

        // A magnitude word may sit between the amount and the code: 5 million USD.
        if (IsOneOf(word, MagnitudeWords))
        {
            var afterIndex = NextJoined(c.Text, c.Tokens, nextIndex);
            if (afterIndex >= 0)
            {
                var after = ValidToken(c.Text, c.Tokens, afterIndex);
                if (after.Length <= MaxWordLength && TryGetCurrencySuffix(c.Text.AsSpan(after.Start, after.Length), out var afterCurrency))
                {
                    builder.Add(after.Start, after.Length, afterCurrency.Plural, NormalizationKind.Currency);
                    builder.Consumed = 3;
                }
            }
        }
    }

    /// <summary>
    ///     The outcome of the year-or-cardinal decision for a four-digit integer.
    /// </summary>
    /// <param name="AsYear">Whether the number is read as a year.</param>
    /// <param name="Ambiguous">Whether the decision fell back to the default (no cue either way).</param>
    /// <param name="FromDate">Whether a date context supplied the reading.</param>
    private readonly record struct YearDecision(bool AsYear, bool Ambiguous, bool FromDate);

    /// <summary>
    ///     Decides whether a four-digit integer in 1000 to 2099 is a year or a quantity, from the
    ///     tokens before and after it.
    /// </summary>
    /// <param name="c">The scanned number token.</param>
    /// <param name="value">The value, 1000 to 2099.</param>
    /// <returns>The decision.</returns>
    private static YearDecision DecideYear(ref Ctx c, int value)
    {
        // Year cues win: a date context, a year-introducing word before, an era word after.
        var previousIndex = c.Index - 1;
        if (IsDateYearContext(ref c))
        {
            return new YearDecision(true, false, true);
        }

        var next = NextWord(ref c, out var nextIsPresent);
        if (previousIndex >= 0)
        {
            var previous = ValidToken(c.Text, c.Tokens, previousIndex);
            if (previous.Length <= MaxWordLength && IsOneOf(c.Text.AsSpan(previous.Start, previous.Length), YearCues, ignoreCase: true))
            {
                return new YearDecision(true, false, false);
            }
        }

        if (nextIsPresent && IsOneOf(next, EraWords))
        {
            return new YearDecision(true, false, false);
        }

        // Quantity cues: a quantifier before, or a unit, a percent sign, a currency, a magnitude
        // word or a counted noun after.
        if (previousIndex >= 0)
        {
            var previous = ValidToken(c.Text, c.Tokens, previousIndex);
            if (previous.Length <= MaxWordLength && IsOneOf(c.Text.AsSpan(previous.Start, previous.Length), QuantityCues, ignoreCase: true))
            {
                return new YearDecision(false, false, false);
            }
        }

        if (nextIsPresent
            && ((next.Length == 1 && next[0] == '%')
                || TryGetUnit(next, out _, out _)
                || TryGetCurrencySuffix(next, out _)
                || IsOneOf(next, MagnitudeWords)
                || IsOneOf(next, QuantityNouns, ignoreCase: true)))
        {
            return new YearDecision(false, false, false);
        }

        // No cue: 1100 to 2099 is a year, 1000 to 1099 a cardinal, and the guess is counted.
        return new YearDecision(value >= 1100, true, false);
    }

    /// <summary>
    ///     Gets the characters of the token after a number when it is joined to it.
    /// </summary>
    /// <param name="c">The scanned number token.</param>
    /// <param name="present">Whether a joined, short next token exists.</param>
    /// <returns>The next token's characters, or empty.</returns>
    private static ReadOnlySpan<char> NextWord(ref Ctx c, out bool present)
    {
        present = false;
        var nextIndex = NextJoined(c.Text, c.Tokens, c.Index);
        if (nextIndex < 0)
        {
            return default;
        }

        var next = ValidToken(c.Text, c.Tokens, nextIndex);
        if (next.Length > MaxWordLength)
        {
            return default;
        }

        present = true;
        return c.Text.AsSpan(next.Start, next.Length);
    }

    /// <summary>
    ///     Tells whether the token before a number puts it in a date as a year: right after a
    ///     month, after a day that follows a month, or after the comma that follows such a day.
    ///     The chain never crosses a retained sentence boundary (see
    ///     <see cref="IsRetainedBreakBefore"/>): every link is checked, so a break anywhere
    ///     between the month and the year ends the date.
    /// </summary>
    /// <param name="c">The scanned number token.</param>
    /// <returns><see langword="true"/> for a year inside a date.</returns>
    private static bool IsDateYearContext(ref Ctx c)
    {
        var previous = c.Index - 1;

        // EndsWithMonth runs first, so index 0 (no previous token) never reaches the break test.
        if (EndsWithMonth(ref c, previous))
        {
            return !IsRetainedBreakBefore(ref c, c.Index);
        }

        if (previous >= 0 && IsDayToken(ref c, previous) && EndsWithMonth(ref c, previous - 1))
        {
            return !IsRetainedBreakBefore(ref c, c.Index) && !IsRetainedBreakBefore(ref c, previous);
        }

        return previous >= 1
            && TokenIs(ref c, previous, ",")
            && IsDayToken(ref c, previous - 1)
            && EndsWithMonth(ref c, previous - 2)
            && !IsRetainedBreakBefore(ref c, c.Index)
            && !IsRetainedBreakBefore(ref c, previous)
            && !IsRetainedBreakBefore(ref c, previous - 1);
    }

    /// <summary>
    ///     Tells whether the token at an index is a month that can continue a date: a Title Case
    ///     full name or abbreviation without a period, or the period of a month abbreviation
    ///     (the only period the abbreviation lexicon consumes, so the only one that does not end
    ///     the sentence). A period after a full month name or <c>May</c> is a sentence end.
    /// </summary>
    /// <param name="c">The scanned token, for the text and the tokens.</param>
    /// <param name="index">The token index to test; may be out of range (then <see langword="false"/>).</param>
    /// <returns><see langword="true"/> for a month token or the period of a month abbreviation.</returns>
    private static bool EndsWithMonth(ref Ctx c, int index)
    {
        if (index < 0 || index >= c.Tokens.Length)
        {
            return false;
        }

        var token = ValidToken(c.Text, c.Tokens, index);
        if (token.Length > 10)
        {
            return false;
        }

        var word = c.Text.AsSpan(token.Start, token.Length);
        return word[^1] == '.' ? IsMonthAbbreviationPeriod(ref c, index) : IsMonthToken(word);
    }

    /// <summary>
    ///     Tells whether the token at an index is a period that belongs to a month abbreviation:
    ///     a <c>.</c> token directly after a Title Case abbreviation without a period, or one
    ///     token that is such an abbreviation with its period (hand-built tokens).
    /// </summary>
    /// <param name="c">The scanned token, for the text and the tokens.</param>
    /// <param name="index">The token index to test; may be out of range (then <see langword="false"/>).</param>
    /// <returns><see langword="true"/> for the period of <c>Jan</c>, <c>Sept</c> and the like; <see langword="false"/> after a full month name or <c>May</c>.</returns>
    private static bool IsMonthAbbreviationPeriod(ref Ctx c, int index)
    {
        if (index < 0 || index >= c.Tokens.Length)
        {
            return false;
        }

        var token = ValidToken(c.Text, c.Tokens, index);
        if (token.Length > 10)
        {
            return false;
        }

        var word = c.Text.AsSpan(token.Start, token.Length);
        if (word.Length > 1 && word[^1] == '.')
        {
            return IsOneOf(word[..^1], MonthAbbreviationTable);
        }

        if (word.Length == 1 && word[0] == '.' && index >= 1)
        {
            var before = ValidToken(c.Text, c.Tokens, index - 1);
            return before.End == token.Start && before.Length <= 10 && IsOneOf(c.Text.AsSpan(before.Start, before.Length), MonthAbbreviationTable);
        }

        return false;
    }

    /// <summary>
    ///     Tells whether a sentence boundary that the normalizer keeps sits before a token: the
    ///     token starts a sentence and the token before it is not the period of a month
    ///     abbreviation (the one break the lexicon suppresses).
    /// </summary>
    /// <param name="c">The scanned token, for the text and the tokens.</param>
    /// <param name="index">The token index; at least 1 and below the token count.</param>
    /// <returns><see langword="true"/> when the boundary stays, so a date does not continue across it.</returns>
    private static bool IsRetainedBreakBefore(ref Ctx c, int index) =>
        c.Tokens[index].StartsSentence && !IsMonthAbbreviationPeriod(ref c, index - 1);

    /// <summary>
    ///     Tells whether the token that follows a number (joined to it) is a month.
    /// </summary>
    /// <param name="c">The scanned number token.</param>
    /// <returns><see langword="true"/> when a month name or abbreviation follows.</returns>
    private static bool NextIsMonth(ref Ctx c)
    {
        var word = NextWord(ref c, out var present);
        return present && IsMonthToken(word);
    }

    /// <summary>
    ///     Tells whether the token at an index is a day of the month: one or two digits (1 to 31),
    ///     optionally with an ordinal suffix.
    /// </summary>
    /// <param name="c">The scanned token, for the text and the tokens.</param>
    /// <param name="index">The token index; may be out of range.</param>
    /// <returns><see langword="true"/> for a day token.</returns>
    private static bool IsDayToken(ref Ctx c, int index)
    {
        if (index < 0 || index >= c.Tokens.Length)
        {
            return false;
        }

        var token = ValidToken(c.Text, c.Tokens, index);
        if (token.Length > 4)
        {
            return false;
        }

        var word = c.Text.AsSpan(token.Start, token.Length);
        var digits = 0;
        while (digits < word.Length && IsDigit(word[digits]))
        {
            digits++;
        }

        if (digits is < 1 or > 2 || !IsDayValue(word[..digits]))
        {
            return false;
        }

        return digits == word.Length || (word.Length - digits == 2 && IsOrdinalSuffixWord(word[digits..]));
    }

    /// <summary>
    ///     Tells whether the token at an index is exactly the given text.
    /// </summary>
    /// <param name="c">The scanned token, for the text and the tokens.</param>
    /// <param name="index">The token index; may be out of range.</param>
    /// <param name="value">The text to compare with.</param>
    /// <returns><see langword="true"/> on an exact (ordinal) match.</returns>
    private static bool TokenIs(ref Ctx c, int index, string value)
    {
        if (index < 0 || index >= c.Tokens.Length)
        {
            return false;
        }

        var token = ValidToken(c.Text, c.Tokens, index);
        return token.Length == value.Length && c.Text.AsSpan(token.Start, token.Length).SequenceEqual(value);
    }

    /// <summary>
    ///     Tells whether a three-digit number sits in parentheses directly before a seven-digit
    ///     <c>ddd-dddd</c> local number: <c>(555) 123-4567</c>.
    /// </summary>
    /// <param name="c">The scanned number token.</param>
    /// <returns><see langword="true"/> for an area code.</returns>
    private static bool IsAreaCode(ref Ctx c)
    {
        var index = c.Index;
        if (index < 1 || index + 2 >= c.Tokens.Length)
        {
            return false;
        }

        var open = ValidToken(c.Text, c.Tokens, index - 1);
        var close = ValidToken(c.Text, c.Tokens, index + 1);
        var local = ValidToken(c.Text, c.Tokens, index + 2);
        if (open.Length != 1 || c.Text[open.Start] != '(' || open.End != c.Token.Start
            || close.Length != 1 || c.Text[close.Start] != ')' || close.Start != c.Token.End
            || local.Length != 8 || (local.Start != close.End && !(local.Start == close.End + 1 && IsGapCharacter(c.Text[close.End]))))
        {
            return false;
        }

        var text = c.Text.AsSpan(local.Start, 8);
        return IsDigit(text[0]) && IsDigit(text[1]) && IsDigit(text[2]) && text[3] == '-'
            && IsDigit(text[4]) && IsDigit(text[5]) && IsDigit(text[6]) && IsDigit(text[7]);
    }

    /// <summary>
    ///     Reads a currency symbol followed by an adjacent amount token (<c>$</c> and <c>5.50</c>).
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="tokens">The tokens.</param>
    /// <param name="index">The index of the symbol token.</param>
    /// <param name="match">The match when this method returns <see langword="true"/>.</param>
    /// <returns><see langword="true"/> when the next token, directly adjacent, is an amount.</returns>
    private static bool TryCurrencyPair(string text, ReadOnlySpan<NormalizationToken> tokens, int index, out NumericMatch match)
    {
        match = default;
        if (index + 1 >= tokens.Length)
        {
            return false;
        }

        var symbolToken = ValidToken(text, tokens, index);
        var amountToken = ValidToken(text, tokens, index + 1);
        if (amountToken.Start != symbolToken.End || amountToken.Length > MaxTokenLength)
        {
            return false;
        }

        var amount = text.AsSpan(amountToken.Start, amountToken.Length);
        if (ParsePrefix(amount, false, out _, out _, out _) < 0)
        {
            return false;
        }

        Span<char> scratch = stackalloc char[MaxTokenLength];
        Span<int> groups = stackalloc int[2 * MaxGroups];
        var context = new Ctx { Text = text, Tokens = tokens, Index = index + 1, Token = amountToken, Span = amount, Groups = groups, Scratch = scratch };
        Scan(ref context, false);
        if (context.TooMany || Classify(ref context) != Shape.Plain)
        {
            return false;
        }

        return TryCurrencyAmount(ref context, text[symbolToken.Start], symbolToken.Start, 2, out match);
    }

    /// <summary>
    ///     Reads a currency amount (the scanned token) for a given symbol: a plain amount, a
    ///     letter magnitude (<c>5M</c>), or an amount followed by a magnitude word.
    /// </summary>
    /// <param name="c">The scanned amount token (no currency prefix of its own when the symbol is a separate token).</param>
    /// <param name="symbol">The currency symbol.</param>
    /// <param name="spanStart">The offset where the first span starts (the symbol).</param>
    /// <param name="baseConsumed">The number of tokens consumed by the symbol and the amount (1 or 2).</param>
    /// <param name="match">The match when this method returns <see langword="true"/>.</param>
    /// <returns><see langword="true"/> when the amount is recognized or is a fallback.</returns>
    private static bool TryCurrencyAmount(ref Ctx c, char symbol, int spanStart, int baseConsumed, out NumericMatch match)
    {
        match = default;
        var rest = c.Rest;
        var magnitudeLetter = '\0';
        if (rest.Length == 1 && rest[0] is 'K' or 'k' or 'M' or 'm' or 'B' or 'b')
        {
            magnitudeLetter = rest[0];
        }
        else if (rest.Length != 0)
        {
            return false;
        }

        var currency = CurrencyBySymbol(symbol);
        var length = ExtractPlain(ref c, out var fractionStart, out var fractionLength, out _);
        var integerDigits = c.Scratch[..length];
        var fraction = fractionLength > 0 ? c.Span.Slice(fractionStart, fractionLength) : default;
        var builder = new Builder { Consumed = baseConsumed };
        var writer = new SpokenWriter(stackalloc char[NumberVerbalizer.MaxSpokenLength]);
        var fallback = false;

        // A magnitude word after the amount splits the reading: "$5" is "five" and "million"
        // becomes "million dollars".
        var magnitudeIndex = -1;
        if (magnitudeLetter == '\0')
        {
            var nextIndex = NextJoined(c.Text, c.Tokens, c.Index);
            if (nextIndex >= 0)
            {
                var next = ValidToken(c.Text, c.Tokens, nextIndex);
                if (next.Length <= MaxWordLength && IsOneOf(c.Text.AsSpan(next.Start, next.Length), MagnitudeWords))
                {
                    magnitudeIndex = nextIndex;
                }
            }
        }

        bool ok;
        if (magnitudeLetter != '\0')
        {
            ok = TryAppendNumber(ref writer, c.Sign, integerDigits, fraction, ref fallback);
            if (ok)
            {
                writer.Append(MagnitudeLetterWord(magnitudeLetter));
                writer.Append(currency.Plural);
            }
        }
        else if (magnitudeIndex >= 0)
        {
            ok = TryAppendNumber(ref writer, c.Sign, integerDigits, fraction, ref fallback);
        }
        else
        {
            ok = TryAppendMoney(ref writer, currency, c.Sign, integerDigits, fraction, ref fallback);
        }

        if (!ok)
        {
            match = FallbackMatch();
            return true;
        }

        builder.Fallback = fallback;
        builder.Add(spanStart, c.Token.End - spanStart, writer.ToString(), NormalizationKind.Currency);
        if (magnitudeIndex >= 0)
        {
            var word = ValidToken(c.Text, c.Tokens, magnitudeIndex);
            builder.Add(word.Start, word.Length, c.Text.AsSpan(word.Start, word.Length).ToString() + " " + currency.Plural, NormalizationKind.Currency);
            builder.Consumed = baseConsumed + 1;
        }

        match = builder.ToMatch();
        return true;
    }

    /// <summary>
    ///     Appends the words of a monetary amount: whole units and minor units for a two-digit
    ///     fraction (<c>five dollars and fifty cents</c>), otherwise the decimal number and the
    ///     plural unit.
    /// </summary>
    /// <param name="writer">The writer.</param>
    /// <param name="currency">The currency.</param>
    /// <param name="sign">The sign, or <c>'\0'</c>.</param>
    /// <param name="integerDigits">The integer digits; may be empty.</param>
    /// <param name="fraction">The fraction digits; may be empty.</param>
    /// <param name="fallback">Set to <see langword="true"/> when the integer part was read digit by digit because of its size.</param>
    /// <returns><see langword="false"/> when the amount is beyond the limits and must be left as written.</returns>
    private static bool TryAppendMoney(ref SpokenWriter writer, CurrencyEntry currency, char sign, ReadOnlySpan<char> integerDigits, ReadOnlySpan<char> fraction, ref bool fallback)
    {
        // Minor units only for exactly two fraction digits, and only where the currency has them.
        if (fraction.Length != 2 || currency.MinorPlural is null || currency.MinorSingular is null)
        {
            if (!TryAppendNumber(ref writer, sign, integerDigits, fraction, ref fallback))
            {
                return false;
            }

            writer.Append(IsExactlyOne(integerDigits, fraction) ? currency.Singular : currency.Plural);
            return true;
        }

        AppendSign(ref writer, sign);
        var minor = ((fraction[0] - '0') * 10) + (fraction[1] - '0');
        var wholeIsZero = integerDigits.TrimStart('0').Length == 0 && integerDigits.Length <= 1;

        // Only minor units: "$.99" and "$0.99".
        if (wholeIsZero && minor != 0)
        {
            NumberVerbalizer.AppendCardinal(ref writer, fraction);
            writer.Append(minor == 1 ? currency.MinorSingular : currency.MinorPlural);
            return true;
        }

        if (integerDigits.Length == 0)
        {
            writer.Append("zero");
        }
        else if (!TryAppendInteger(ref writer, integerDigits, ref fallback))
        {
            return false;
        }

        var whole = integerDigits.Length == 1 && integerDigits[0] == '1';
        writer.Append(whole ? currency.Singular : currency.Plural);
        if (minor != 0)
        {
            writer.Append("and");
            NumberVerbalizer.AppendCardinal(ref writer, fraction);
            writer.Append(minor == 1 ? currency.MinorSingular : currency.MinorPlural);
        }

        return true;
    }

    /// <summary>
    ///     Reads an apostrophe directly before a two-digit decade token (<c>'90s</c>) as one span.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="tokens">The tokens.</param>
    /// <param name="index">The index of the apostrophe token.</param>
    /// <param name="match">The match when this method returns <see langword="true"/>.</param>
    /// <returns><see langword="true"/> when the apostrophe opens an abbreviated decade.</returns>
    private static bool TryApostropheDecade(string text, ReadOnlySpan<NormalizationToken> tokens, int index, out NumericMatch match)
    {
        match = default;
        if (index + 1 >= tokens.Length)
        {
            return false;
        }

        var apostrophe = ValidToken(text, tokens, index);
        var decade = ValidToken(text, tokens, index + 1);
        if (decade.Start != apostrophe.End || decade.Length != 3)
        {
            return false;
        }

        // The apostrophe must open a word: a letter or digit before it (rock'90s) is not a decade.
        if (apostrophe.Start > 0 && System.Text.Rune.IsLetterOrDigit(LastRuneBefore(text, apostrophe.Start)))
        {
            return false;
        }

        var digits = text.AsSpan(decade.Start, 2);
        if (!IsDigit(digits[0]) || digits[0] == '0' || digits[1] != '0' || text[decade.Start + 2] != 's')
        {
            return false;
        }

        var builder = new Builder { Consumed = 2 };
        builder.Add(apostrophe.Start, decade.End - apostrophe.Start, NumberVerbalizer.Decade(digits), NormalizationKind.Number);
        match = builder.ToMatch();
        return true;
    }

    /// <summary>
    ///     Reads an en dash directly between two number tokens as <c>to</c> (<c>5&#8211;10</c>).
    ///     Both neighbors must themselves be read as numbers (see <see cref="IsRangeEndpoint"/>);
    ///     otherwise the dash stays as written and each neighbor follows its own rule
    ///     (<c>1&#8211;2D</c>, <c>1,23&#8211;5</c>, <c>5&#8211;25:99</c> and <c>5&#8211;10x</c> keep the dash).
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="tokens">The tokens.</param>
    /// <param name="index">The index of the dash token.</param>
    /// <param name="match">The match when this method returns <see langword="true"/>.</param>
    /// <returns><see langword="true"/> when the dash touches a number on both sides and both numbers are read.</returns>
    private static bool TryRangeDash(string text, ReadOnlySpan<NormalizationToken> tokens, int index, out NumericMatch match)
    {
        match = default;
        if (index < 1 || index + 1 >= tokens.Length)
        {
            return false;
        }

        var dash = ValidToken(text, tokens, index);
        var before = ValidToken(text, tokens, index - 1);
        var after = ValidToken(text, tokens, index + 1);
        if (before.End != dash.Start || after.Start != dash.End || before.Length > MaxTokenLength || after.Length > MaxTokenLength)
        {
            return false;
        }

        if (!IsDigit(text[before.Start]) || !IsDigit(text[before.End - 1]) || !IsDigit(text[after.Start]))
        {
            return false;
        }

        // Both endpoints must be complete numbers. A neighbor starts and ends with a digit, so it
        // is never a dash token: the recognition below is exactly one level deep, and this method
        // returned from the dash branch before TryRecognize allocated any buffer of its own.
        if (!IsRangeEndpoint(text, tokens, index - 1) || !IsRangeEndpoint(text, tokens, index + 1))
        {
            return false;
        }

        var builder = new Builder { Consumed = 1 };
        builder.Add(dash.Start, dash.Length, "to", NormalizationKind.Number);
        match = builder.ToMatch();
        return true;
    }

    /// <summary>
    ///     Tells whether the token at an index is read as a number by itself: it is recognized and
    ///     produces at least one spoken span (a counted fallback that leaves the token as written,
    ///     such as <c>1,23</c>, and an unrecognized token such as <c>25:99</c> are not). A number with
    ///     a capital letter glued to it (<c>2D</c>) is read, but it is not a complete number, so it is
    ///     not an endpoint of a range.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="tokens">The tokens.</param>
    /// <param name="index">The token index, already validated by the caller.</param>
    /// <returns><see langword="true"/> when the token is read as a number.</returns>
    private static bool IsRangeEndpoint(string text, ReadOnlySpan<NormalizationToken> tokens, int index) =>
        TryRecognize(text, tokens, index, out var endpoint) && endpoint.SpanCount > 0 && !(endpoint.SpanCount == 2 && endpoint[1].Kind == NormalizationKind.Initialism);

    /// <summary>
    ///     Reads a time written <c>h:mm</c>, with an optional glued meridiem or a following
    ///     lowercase <c>am</c> or <c>pm</c> token.
    /// </summary>
    /// <param name="c">The scanned token, of shape <see cref="Shape.Time"/>.</param>
    /// <param name="match">The match when this method returns <see langword="true"/>.</param>
    /// <returns><see langword="true"/> for a valid time.</returns>
    private static bool TryTime(ref Ctx c, out NumericMatch match)
    {
        match = default;
        if (c.GroupCount != 2 || c.Sign != '\0' || c.Symbol != '\0' || c.Hash)
        {
            return false;
        }

        var hour = c.Group(0);
        var minute = c.Group(1);
        if (hour.Length is < 1 or > 2 || minute.Length != 2)
        {
            return false;
        }

        var hourValue = ParseTwo(hour);
        if (hourValue > 23 || ParseTwo(minute) > 59)
        {
            return false;
        }

        var rest = c.Rest;
        var builder = new Builder { Consumed = 1 };
        var writer = new SpokenWriter(stackalloc char[NumberVerbalizer.MaxSpokenLength]);
        if (rest.Length > 0)
        {
            // A glued meridiem needs a 12-hour clock.
            if (!IsMeridiem(rest, uppercaseAllowed: true) || hourValue is < 1 or > 12)
            {
                return false;
            }

            // Two adjacent spans inside the one token: the time (words) and the meridiem letters
            // (spelled), so no consumer infers spelling from a value.
            NumberVerbalizer.AppendClock(ref writer, hour, minute, omitZeroMinute: true);
            builder.Add(c.Token.Start, c.RestStart, writer.ToString(), NormalizationKind.DateTime);
            builder.Add(c.Token.Start + c.RestStart, rest.Length, MeridiemWords(rest), NormalizationKind.DateTime);
            match = builder.ToMatch();
            return true;
        }

        // A separate lowercase meridiem token after the time.
        var follower = NextWord(ref c, out var present);
        var nextIndex = NextJoined(c.Text, c.Tokens, c.Index);
        var meridiem = present && hourValue is >= 1 and <= 12 && IsMeridiem(follower, uppercaseAllowed: false);
        NumberVerbalizer.AppendClock(ref writer, hour, minute, omitZeroMinute: meridiem);
        builder.Add(c.Token.Start, c.Token.Length, writer.ToString(), NormalizationKind.DateTime);
        if (meridiem)
        {
            var next = ValidToken(c.Text, c.Tokens, nextIndex);
            builder.Add(next.Start, next.Length, MeridiemWords(follower), NormalizationKind.DateTime);
            builder.Consumed = 2;
        }

        match = builder.ToMatch();
        return true;
    }

    /// <summary>
    ///     Reads three or more period-separated digit groups (a version or an address).
    /// </summary>
    /// <param name="c">The scanned token, of shape <see cref="Shape.Dotted"/>.</param>
    /// <param name="match">The match when this method returns <see langword="true"/>.</param>
    /// <returns><see langword="true"/> for a valid dotted number.</returns>
    private static bool TryDotted(ref Ctx c, out NumericMatch match)
    {
        match = default;
        if (c.Sign != '\0' || c.Symbol != '\0' || c.Hash || c.Rest.Length > 0)
        {
            return false;
        }

        var writer = new SpokenWriter(stackalloc char[NumberVerbalizer.MaxSpokenLength]);
        var total = 0;
        for (var group = 0; group < c.GroupCount; group++)
        {
            var digits = c.Group(group);
            if (digits.Length == 0)
            {
                return false;
            }

            total += digits.Length;
            if (total > NumberVerbalizer.MaxSpelledDigits)
            {
                match = FallbackMatch();
                return true;
            }

            if (group > 0)
            {
                writer.Append("point");
            }

            // A short group without a leading zero is a cardinal; any other group is read digit by digit.
            if (digits.Length <= 3 && (digits.Length == 1 || digits[0] != '0'))
            {
                NumberVerbalizer.AppendCardinal(ref writer, digits);
            }
            else
            {
                NumberVerbalizer.AppendDigits(ref writer, digits);
            }
        }

        var builder = new Builder { Consumed = 1 };
        builder.Add(c.Token.Start, c.Token.Length, writer.ToString(), NormalizationKind.Number);
        match = builder.ToMatch();
        return true;
    }

    /// <summary>
    ///     Reads a slash form: a simple fraction (two groups) or a date (three groups).
    /// </summary>
    /// <param name="c">The scanned token, of shape <see cref="Shape.Slash"/>.</param>
    /// <param name="match">The match when this method returns <see langword="true"/>.</param>
    /// <returns><see langword="true"/> for a fraction or a valid date.</returns>
    private static bool TrySlash(ref Ctx c, out NumericMatch match)
    {
        match = default;
        if (c.Sign != '\0' || c.Symbol != '\0' || c.Hash || c.Rest.Length > 0)
        {
            return false;
        }

        var builder = new Builder { Consumed = 1 };
        if (c.GroupCount == 2)
        {
            if (!TrySimpleFraction(c.Span, out var numerator, out var denominator))
            {
                return false;
            }

            // A fraction such as 3/4 could be a month and day, so the choice is counted.
            builder.Ambiguous = numerator <= 12 && denominator <= 31;
            builder.Add(c.Token.Start, c.Token.Length, NumberVerbalizer.Fraction(numerator, denominator), NormalizationKind.Number);
            match = builder.ToMatch();
            return true;
        }

        if (c.GroupCount != 3 || !TryDate(ref c, allowTwoDigitYear: true, out var spoken, out var ambiguous))
        {
            return false;
        }

        builder.Ambiguous = ambiguous;
        builder.Add(c.Token.Start, c.Token.Length, spoken, NormalizationKind.DateTime);
        match = builder.ToMatch();
        return true;
    }

    /// <summary>
    ///     Reads a hyphen form: a range, a phone number or identifier (digit by digit), or a date.
    /// </summary>
    /// <param name="c">The scanned token, of shape <see cref="Shape.Hyphen"/>.</param>
    /// <param name="match">The match when this method returns <see langword="true"/>.</param>
    /// <returns><see langword="true"/> for a recognized form or a fallback.</returns>
    private static bool TryHyphen(ref Ctx c, out NumericMatch match)
    {
        match = default;
        if (c.Sign != '\0' || c.Symbol != '\0' || c.Hash || c.Rest.Length > 0)
        {
            return false;
        }

        // Every group must hold digits ("1--2" never reaches here, but a leading period would).
        var total = 0;
        for (var group = 0; group < c.GroupCount; group++)
        {
            var length = c.Group(group).Length;
            if (length == 0)
            {
                return false;
            }

            total += length;
        }

        var builder = new Builder { Consumed = 1 };
        var kind = NormalizationKind.Number;
        var ambiguous = false;
        string spoken;
        if (c.GroupCount == 3 && TryDate(ref c, allowTwoDigitYear: false, out var dateWords, out ambiguous))
        {
            spoken = dateWords;
            kind = NormalizationKind.DateTime;
        }
        else if (c.GroupCount == 2 && TryRange(ref c, out var rangeWords))
        {
            spoken = rangeWords;
        }
        else
        {
            // A phone number or identifier: every digit on its own.
            if (total > NumberVerbalizer.MaxSpelledDigits)
            {
                match = FallbackMatch();
                return true;
            }

            var writer = new SpokenWriter(stackalloc char[NumberVerbalizer.MaxSpokenLength]);
            for (var group = 0; group < c.GroupCount; group++)
            {
                NumberVerbalizer.AppendDigits(ref writer, c.Group(group));
            }

            spoken = writer.ToString();
        }

        builder.Ambiguous = ambiguous;
        builder.Add(c.Token.Start, c.Token.Length, spoken, kind);
        match = builder.ToMatch();
        return true;
    }

    /// <summary>
    ///     Reads two hyphen-separated groups as a range (<c>5-10</c>, <c>1990-2000</c>) unless they
    ///     have the length of a local phone number (<c>555-1234</c>).
    /// </summary>
    /// <param name="c">The scanned token with two groups.</param>
    /// <param name="spoken">The spoken range when this method returns <see langword="true"/>.</param>
    /// <returns><see langword="true"/> when the token is a range.</returns>
    private static bool TryRange(ref Ctx c, out string spoken)
    {
        spoken = string.Empty;
        var low = c.Group(0);
        var high = c.Group(1);
        if ((low.Length == 3 && high.Length == 4)
            || low.Length > 9 || high.Length > 9
            || (low.Length > 1 && low[0] == '0') || (high.Length > 1 && high[0] == '0'))
        {
            return false;
        }

        var writer = new SpokenWriter(stackalloc char[NumberVerbalizer.MaxSpokenLength]);

        // Two four-digit years in order are read as years.
        if (low.Length == 4 && high.Length == 4 && IsYearRange(low) && IsYearRange(high) && low.SequenceCompareTo(high) <= 0)
        {
            NumberVerbalizer.AppendYear(ref writer, low);
            writer.Append("to");
            NumberVerbalizer.AppendYear(ref writer, high);
        }
        else
        {
            NumberVerbalizer.AppendCardinal(ref writer, low);
            writer.Append("to");
            NumberVerbalizer.AppendCardinal(ref writer, high);
        }

        spoken = writer.ToString();
        return true;
    }

    /// <summary>
    ///     Reads three digit groups as a calendar date, in year-first or month-first order.
    /// </summary>
    /// <param name="c">The scanned token with three groups.</param>
    /// <param name="allowTwoDigitYear">Whether the year of a month-first date may have two digits (slash dates only).</param>
    /// <param name="spoken">The spoken date when this method returns <see langword="true"/>.</param>
    /// <param name="ambiguous">Whether month and day could be read either way.</param>
    /// <returns><see langword="true"/> for a valid date.</returns>
    private static bool TryDate(ref Ctx c, bool allowTwoDigitYear, out string spoken, out bool ambiguous)
    {
        spoken = string.Empty;
        ambiguous = false;
        var first = c.Group(0);
        var second = c.Group(1);
        var third = c.Group(2);
        int month, day;
        ReadOnlySpan<char> year;

        if (first.Length == 4)
        {
            // Year first (ISO): 2024-12-25, 2024/12/25.
            if (second.Length is < 1 or > 2 || third.Length is < 1 or > 2 || first[0] == '0')
            {
                return false;
            }

            year = first;
            month = ParseTwo(second);
            day = ParseTwo(third);
        }
        else
        {
            // Month first: 12/25/2024; day first only when the first part cannot be a month.
            if (first.Length is < 1 or > 2 || second.Length is < 1 or > 2 || (third.Length != 4 && !(allowTwoDigitYear && third.Length == 2)))
            {
                return false;
            }

            if (third.Length == 4 && third[0] == '0')
            {
                return false;
            }

            if (third.Length == 2 && third[0] == '0' && third[1] == '0')
            {
                return false;
            }

            year = third;
            var a = ParseTwo(first);
            var b = ParseTwo(second);
            if (a is >= 1 and <= 12 && b is >= 1 and <= 31)
            {
                month = a;
                day = b;
                ambiguous = b <= 12 && a != b;
            }
            else if (a is >= 13 and <= 31 && b is >= 1 and <= 12)
            {
                month = b;
                day = a;
            }
            else
            {
                return false;
            }
        }

        if (month is < 1 or > 12 || day is < 1 or > 31)
        {
            return false;
        }

        var writer = new SpokenWriter(stackalloc char[NumberVerbalizer.MaxSpokenLength]);
        NumberVerbalizer.AppendDate(ref writer, month, day, year);
        spoken = writer.ToString();
        return true;
    }

    /// <summary>
    ///     Reads a slash form <c>a/b</c> as a simple fraction.
    /// </summary>
    /// <param name="token">The token's characters.</param>
    /// <param name="numerator">The numerator when this method returns <see langword="true"/>.</param>
    /// <param name="denominator">The denominator when this method returns <see langword="true"/>.</param>
    /// <returns><see langword="true"/> when both parts are digits without leading zeros, the numerator is at least 1 and below the denominator, and the denominator is one of 2 to 10, 16, 32, 64 or 100.</returns>
    private static bool TrySimpleFraction(ReadOnlySpan<char> token, out int numerator, out int denominator)
    {
        numerator = 0;
        denominator = 0;
        if (token.Length is < 3 or > 7)
        {
            return false;
        }

        var position = token.IndexOf('/');
        if (position < 1 || position > 3 || token.Length - position - 1 is < 1 or > 3)
        {
            return false;
        }

        var top = token[..position];
        var bottom = token[(position + 1)..];
        if (!AllDigits(top) || !AllDigits(bottom) || top[0] == '0' || bottom[0] == '0')
        {
            return false;
        }

        numerator = ParseTwo(top);
        denominator = ParseTwo(bottom);
        return numerator >= 1 && numerator < denominator && Array.IndexOf(FractionDenominators, denominator) >= 0;
    }

    /// <summary>
    ///     Gets the value of a vulgar fraction character.
    /// </summary>
    /// <param name="character">The character.</param>
    /// <param name="numerator">The numerator when this method returns <see langword="true"/>.</param>
    /// <param name="denominator">The denominator when this method returns <see langword="true"/>.</param>
    /// <returns><see langword="true"/> for the characters one half to seven eighths that are handled.</returns>
    private static bool TryVulgarFraction(char character, out int numerator, out int denominator)
    {
        (numerator, denominator) = character switch
        {
            '\u00BD' => (1, 2),
            '\u2153' => (1, 3),
            '\u2154' => (2, 3),
            '\u00BC' => (1, 4),
            '\u00BE' => (3, 4),
            '\u215B' => (1, 8),
            '\u215C' => (3, 8),
            '\u215D' => (5, 8),
            '\u215E' => (7, 8),
            _ => (0, 0),
        };
        return numerator != 0;
    }

    /// <summary>
    ///     Appends the words of an optionally signed number: an integer part, and a fraction read
    ///     digit by digit.
    /// </summary>
    /// <param name="writer">The writer.</param>
    /// <param name="sign">The sign, or <c>'\0'</c>.</param>
    /// <param name="integerDigits">The integer digits; may be empty.</param>
    /// <param name="fraction">The fraction digits; may be empty.</param>
    /// <param name="fallback">Set to <see langword="true"/> when the integer part was read digit by digit because of its size.</param>
    /// <returns><see langword="false"/> when the number is beyond the limits and must be left as written.</returns>
    private static bool TryAppendNumber(ref SpokenWriter writer, char sign, ReadOnlySpan<char> integerDigits, ReadOnlySpan<char> fraction, ref bool fallback)
    {
        if (fraction.Length > NumberVerbalizer.MaxFractionDigits)
        {
            return false;
        }

        AppendSign(ref writer, sign);
        if (integerDigits.Length > 0 && !TryAppendInteger(ref writer, integerDigits, ref fallback))
        {
            return false;
        }

        if (fraction.Length > 0)
        {
            writer.Append("point");
            NumberVerbalizer.AppendDigits(ref writer, fraction);
        }

        return true;
    }

    /// <summary>
    ///     Appends an integer by the size policy: a cardinal up to 15 digits, digit by digit up to
    ///     64 digits (counted as a fallback) or when it starts with a zero, otherwise refused.
    /// </summary>
    /// <param name="writer">The writer.</param>
    /// <param name="digits">The integer digits; not empty.</param>
    /// <param name="fallback">Set to <see langword="true"/> when the digits were spelled because of their size.</param>
    /// <returns><see langword="false"/> when the integer must be left as written.</returns>
    private static bool TryAppendInteger(ref SpokenWriter writer, ReadOnlySpan<char> digits, ref bool fallback)
    {
        // A leading zero (with more than one digit) makes an identifier: digit by digit, not a fallback.
        if (digits.Length > 1 && digits[0] == '0')
        {
            if (digits.Length > NumberVerbalizer.MaxSpelledDigits)
            {
                return false;
            }

            NumberVerbalizer.AppendDigits(ref writer, digits);
            return true;
        }

        if (digits.Length <= NumberVerbalizer.MaxCardinalDigits)
        {
            NumberVerbalizer.AppendCardinal(ref writer, digits);
            return true;
        }

        if (digits.Length > NumberVerbalizer.MaxSpelledDigits)
        {
            return false;
        }

        NumberVerbalizer.AppendDigits(ref writer, digits);
        fallback = true;
        return true;
    }

    /// <summary>
    ///     Appends <c>minus</c> or <c>plus</c> for a sign.
    /// </summary>
    /// <param name="writer">The writer.</param>
    /// <param name="sign">The sign, or <c>'\0'</c> for none.</param>
    private static void AppendSign(ref SpokenWriter writer, char sign)
    {
        if (sign == '-')
        {
            writer.Append("minus");
        }
        else if (sign == '+')
        {
            writer.Append("plus");
        }
    }

    /// <summary>
    ///     Gets the word of a magnitude letter written after a currency amount.
    /// </summary>
    /// <param name="letter"><c>K</c>, <c>M</c> or <c>B</c> in either case.</param>
    /// <returns><c>thousand</c>, <c>million</c> or <c>billion</c>.</returns>
    private static string MagnitudeLetterWord(char letter)
    {
        if (letter is 'K' or 'k')
        {
            return "thousand";
        }

        return letter is 'M' or 'm' ? "million" : "billion";
    }

    /// <summary>
    ///     Tells whether a number is exactly one (so a unit is singular): the digit 1 alone, no
    ///     leading zero and no fraction.
    /// </summary>
    /// <param name="integerDigits">The integer digits.</param>
    /// <param name="fraction">The fraction digits.</param>
    /// <returns><see langword="true"/> for exactly 1.</returns>
    private static bool IsExactlyOne(ReadOnlySpan<char> integerDigits, ReadOnlySpan<char> fraction) =>
        fraction.Length == 0 && integerDigits.Length == 1 && integerDigits[0] == '1';

    /// <summary>
    ///     Finds the token after <paramref name="index"/> when it is joined to it: adjacent, or
    ///     separated by exactly one space (U+0020, U+00A0 or U+202F).
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="tokens">The tokens.</param>
    /// <param name="index">The token's index.</param>
    /// <returns>The index of the next token, or -1.</returns>
    private static int NextJoined(string text, ReadOnlySpan<NormalizationToken> tokens, int index)
    {
        var next = index + 1;
        if (next >= tokens.Length)
        {
            return -1;
        }

        var current = ValidToken(text, tokens, index);
        var following = ValidToken(text, tokens, next);
        var gap = following.Start - current.End;
        if (gap == 0 || (gap == 1 && IsGapCharacter(text[current.End])))
        {
            return next;
        }

        return -1;
    }

    /// <summary>
    ///     Tells whether a character may be the single space between two joined tokens.
    /// </summary>
    /// <param name="character">The character.</param>
    /// <returns><see langword="true"/> for U+0020, U+00A0 and U+202F.</returns>
    private static bool IsGapCharacter(char character) => character is ' ' or '\u00A0' or '\u202F';

    /// <summary>
    ///     Returns a token after checking that it lies inside the text, without computing
    ///     <c>Start + Length</c> before the check (which could overflow for a hostile token).
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="tokens">The tokens.</param>
    /// <param name="index">The token's index.</param>
    /// <returns>The token.</returns>
    /// <exception cref="ArgumentException">The token is empty or outside the text.</exception>
    private static NormalizationToken ValidToken(string text, ReadOnlySpan<NormalizationToken> tokens, int index)
    {
        var token = tokens[index];
        if (token.Length <= 0 || token.Start < 0 || token.Start > text.Length || token.Length > text.Length - token.Start)
        {
            throw new ArgumentException("A token is empty or outside the text.", nameof(tokens));
        }

        return token;
    }

    /// <summary>
    ///     Reads the Unicode scalar that ends right before an index.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="index">The index; at least 1 and at most the text length.</param>
    /// <returns>The scalar, or the replacement character for a malformed sequence.</returns>
    private static System.Text.Rune LastRuneBefore(string text, int index)
    {
        System.Text.Rune.DecodeLastFromUtf16(text.AsSpan(0, index), out var rune, out _);
        return rune;
    }

    /// <summary>
    ///     Finds the unit a written form names.
    /// </summary>
    /// <param name="word">The written form, with an optional trailing period for the units that allow one.</param>
    /// <param name="unit">The unit when this method returns <see langword="true"/>.</param>
    /// <param name="hasPeriod">Whether the written form ended with the period.</param>
    /// <returns><see langword="true"/> for a unit in the (case-sensitive) table.</returns>
    private static bool TryGetUnit(ReadOnlySpan<char> word, out UnitEntry unit, out bool hasPeriod)
    {
        unit = default;
        hasPeriod = false;

        // No unit is longer than four characters (five with a period).
        if (word.Length is 0 or > 5)
        {
            return false;
        }

        var core = word;
        if (word.Length > 1 && word[^1] == '.')
        {
            core = word[..^1];
            hasPeriod = true;
        }

        foreach (var entry in UnitTable)
        {
            if (core.SequenceEqual(entry.Written) && (!hasPeriod || entry.AllowsPeriod))
            {
                unit = entry;
                return true;
            }
        }

        hasPeriod = false;
        return false;
    }

    /// <summary>
    ///     Finds the currency a code or a symbol after a number names.
    /// </summary>
    /// <param name="word">A three-letter code or a single symbol.</param>
    /// <param name="currency">The currency when this method returns <see langword="true"/>.</param>
    /// <returns><see langword="true"/> for <c>USD</c>, <c>EUR</c>, <c>GBP</c>, <c>JPY</c> or a currency symbol.</returns>
    private static bool TryGetCurrencySuffix(ReadOnlySpan<char> word, out CurrencyEntry currency)
    {
        currency = default;
        if (word.Length == 1 && IsCurrencySymbol(word[0]))
        {
            currency = CurrencyBySymbol(word[0]);
            return true;
        }

        if (word.Length != 3)
        {
            return false;
        }

        foreach (var entry in CurrencyTable)
        {
            if (word.SequenceEqual(entry.Code))
            {
                currency = entry;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Gets the currency of a symbol.
    /// </summary>
    /// <param name="symbol">A symbol for which <see cref="IsCurrencySymbol"/> is true.</param>
    /// <returns>The currency.</returns>
    private static CurrencyEntry CurrencyBySymbol(char symbol)
    {
        foreach (var entry in CurrencyTable)
        {
            if (entry.Symbol == symbol)
            {
                return entry;
            }
        }

        return CurrencyTable[0];
    }

    /// <summary>
    ///     Tells whether a character is one of the handled currency symbols.
    /// </summary>
    /// <param name="character">The character.</param>
    /// <returns><see langword="true"/> for the dollar, euro, pound and yen signs.</returns>
    private static bool IsCurrencySymbol(char character) => character is '$' or '\u20AC' or '\u00A3' or '\u00A5';

    /// <summary>
    ///     Tells whether a word is one of the listed words, comparing without regard to ASCII case
    ///     (invariant, never culture sensitive) unless asked otherwise.
    /// </summary>
    /// <param name="word">The word.</param>
    /// <param name="candidates">The listed words.</param>
    /// <param name="ignoreCase">Whether ASCII case is ignored (invariant); otherwise the comparison is exact.</param>
    /// <returns><see langword="true"/> when the word equals one of the candidates.</returns>
    private static bool IsOneOf(ReadOnlySpan<char> word, string[] candidates, bool ignoreCase = false)
    {
        var comparison = ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        foreach (var candidate in candidates)
        {
            if (word.Equals(candidate, comparison))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Tells whether a character is an ASCII digit (never <c>char.IsDigit</c>, which accepts other scripts).
    /// </summary>
    /// <param name="character">The character.</param>
    /// <returns><see langword="true"/> for <c>0</c> to <c>9</c>.</returns>
    private static bool IsDigit(char character) => character is >= '0' and <= '9';

    /// <summary>
    ///     Tells whether a character is an ASCII letter.
    /// </summary>
    /// <param name="character">The character.</param>
    /// <returns><see langword="true"/> for <c>A</c> to <c>Z</c> and <c>a</c> to <c>z</c>.</returns>
    private static bool IsLetter(char character) => character is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z');

    /// <summary>
    ///     Tells whether every character is an ASCII digit.
    /// </summary>
    /// <param name="span">The characters; not empty.</param>
    /// <returns><see langword="true"/> when all characters are digits.</returns>
    private static bool AllDigits(ReadOnlySpan<char> span)
    {
        foreach (var character in span)
        {
            if (!IsDigit(character))
            {
                return false;
            }
        }

        return span.Length > 0;
    }

    /// <summary>
    ///     Parses at most three ASCII digits that were already validated.
    /// </summary>
    /// <param name="digits">The digits.</param>
    /// <returns>The value.</returns>
    private static int ParseTwo(ReadOnlySpan<char> digits)
    {
        var value = 0;
        foreach (var digit in digits)
        {
            value = (value * 10) + (digit - '0');
        }

        return value;
    }

    /// <summary>
    ///     Tells whether two letters spell an ordinal suffix (<c>st</c>, <c>nd</c>, <c>rd</c>, <c>th</c>), in any case.
    /// </summary>
    /// <param name="suffix">The two characters.</param>
    /// <returns><see langword="true"/> for an ordinal suffix of either case.</returns>
    private static bool IsOrdinalSuffixWord(ReadOnlySpan<char> suffix) =>
        suffix.Length == 2
        && (suffix.Equals("st", StringComparison.OrdinalIgnoreCase)
            || suffix.Equals("nd", StringComparison.OrdinalIgnoreCase)
            || suffix.Equals("rd", StringComparison.OrdinalIgnoreCase)
            || suffix.Equals("th", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    ///     Tells whether the text after digits is a decade suffix: <c>s</c>, <c>'s</c> or the
    ///     typographic <c>&#8217;s</c>.
    /// </summary>
    /// <param name="rest">The text after the digits.</param>
    /// <returns><see langword="true"/> for a decade suffix.</returns>
    private static bool IsDecadeSuffix(ReadOnlySpan<char> rest) =>
        (rest.Length == 1 && rest[0] == 's') || (rest.Length == 2 && rest[0] is '\'' or '\u2019' && rest[1] == 's');

    /// <summary>
    ///     Tells whether digits form a decade: two digits (first not zero) or four digits (1000 to
    ///     2099), ending in zero.
    /// </summary>
    /// <param name="digits">The integer digits.</param>
    /// <returns><see langword="true"/> for a decade.</returns>
    private static bool IsDecadeDigits(ReadOnlySpan<char> digits)
    {
        if (digits.Length == 2)
        {
            return digits[0] != '0' && digits[1] == '0';
        }

        return digits.Length == 4 && digits[3] == '0' && digits[0] != '0' && IsYearRange(digits);
    }

    /// <summary>
    ///     Tells whether four digits are a value from 1000 to 2099.
    /// </summary>
    /// <param name="digits">The digits; exactly four.</param>
    /// <returns><see langword="true"/> for 1000 to 2099.</returns>
    private static bool IsYearRange(ReadOnlySpan<char> digits) =>
        digits.Length == 4 && (digits[0] == '1' || (digits[0] == '2' && digits[1] == '0'));

    /// <summary>
    ///     Tells whether one or two digits are a day of the month (1 to 31).
    /// </summary>
    /// <param name="digits">The digits.</param>
    /// <returns><see langword="true"/> for 1 to 31.</returns>
    private static bool IsDayValue(ReadOnlySpan<char> digits)
    {
        if (digits.Length is 0 or > 2)
        {
            return false;
        }

        var value = ParseTwo(digits);
        return value is >= 1 and <= 31;
    }

    /// <summary>
    ///     Tells whether one or two digits are an hour of the 12-hour clock (1 to 12).
    /// </summary>
    /// <param name="digits">The digits.</param>
    /// <returns><see langword="true"/> for 1 to 12.</returns>
    private static bool IsHour12(ReadOnlySpan<char> digits)
    {
        if (digits.Length is 0 or > 2)
        {
            return false;
        }

        var value = ParseTwo(digits);
        return value is >= 1 and <= 12;
    }

    /// <summary>
    ///     Tells whether a word is <c>am</c> or <c>pm</c>.
    /// </summary>
    /// <param name="word">The word.</param>
    /// <param name="uppercaseAllowed">Whether <c>AM</c> and <c>PM</c> count too.</param>
    /// <returns><see langword="true"/> for a meridiem.</returns>
    private static bool IsMeridiem(ReadOnlySpan<char> word, bool uppercaseAllowed) =>
        word.Length == 2
        && (word.SequenceEqual("am") || word.SequenceEqual("pm")
            || (uppercaseAllowed && (word.SequenceEqual("AM") || word.SequenceEqual("PM"))));

    /// <summary>
    ///     Gets the words of a meridiem.
    /// </summary>
    /// <param name="word">A word for which <see cref="IsMeridiem"/> is true.</param>
    /// <returns><c>A M</c> or <c>P M</c>.</returns>
    private static string MeridiemWords(ReadOnlySpan<char> word) => word[0] is 'a' or 'A' ? "A M" : "P M";
}
