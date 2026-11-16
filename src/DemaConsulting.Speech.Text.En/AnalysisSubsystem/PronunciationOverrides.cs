using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using DemaConsulting.Speech.Text.En.NormalizationSubsystem;

namespace DemaConsulting.Speech.Text.En.AnalysisSubsystem;

/// <summary>
///     An immutable dictionary of host-supplied pronunciation overrides for
///     <see cref="EnglishTextAnalyzer"/>: whole words that are spoken as written, replaced by plain
///     words, spelled letter by letter or given a phoneme string, whatever the built-in rules
///     would have done.
/// </summary>
/// <remarks>
///     <para>
///     <b>Building.</b> A dictionary is built from entries with <see cref="Create"/> or from a
///     tab-separated file with <see cref="FromTsv"/>; both validate every entry with the same
///     rules and fail with the position of the first bad entry, never its content. Once built the
///     dictionary never changes: it keeps a private copy of its entries and is safe to share across
///     threads and analyzers.
///     </para>
///     <para>
///     <b>Matching.</b> A key is compared with one whole token of the analyzed text, never with a
///     part of a token or with several tokens (multi-word entries are not supported). The token
///     must also not touch a letter, a combining mark or a decimal digit on either side (so the
///     <c>Do</c> of a split <c>Don't</c> never matches). The text is tokenized exactly as for
///     part-of-speech tagging, so a key must be a word that survives tokenization as one token:
///     <c>NASA</c> matches in <c>NASA's</c> (the tokenizer splits <c>'s</c> off), a hyphenated
///     compound such as <c>Wi-Fi</c> is one token and matches only as a whole, and a word that
///     ends with an abbreviation period (<c>Dr.</c>, <c>U.S.</c>) cannot be a key because the
///     built-in abbreviation rules own it. Case-sensitive entries compare UTF-16 characters
///     exactly; case-insensitive entries use <see cref="StringComparison.OrdinalIgnoreCase"/>
///     (invariant simple case mapping, so Turkish dotted and dotless I never fold into the ASCII
///     letters and no culture is consulted); no Unicode normalization form is applied, so write
///     keys in normalization form C. A case-sensitive entry beats a case-insensitive entry for the
///     same token. A key containing an apostrophe also matches the same word with typographic
///     apostrophes (U+2019); the two spellings are one key, so entries that differ only in the
///     apostrophe style (and, for case-insensitive entries, in case) are duplicates, and no two
///     entries can ever claim the same lookup spelling.
///     </para>
///     <para>
///     <b>Precedence.</b> An override beats every built-in normalization rule for its token; the
///     one exception is a token that an earlier multi-token match (such as the <c>kg</c> of
///     <c>5 kg</c>) has already consumed, which is left to that match and counted as ignored.
///     </para>
///     <para>
///     <b>Limits.</b> At most <see cref="MaxEntries"/> entries; a word is at most
///     <see cref="MaxWordLength"/> UTF-16 code units; a payload at most
///     <see cref="MaxPayloadLength"/>; a spelled word at most <see cref="MaxSpellOutLetters"/>
///     letters; a TSV line at most <see cref="MaxTsvLineLength"/> characters and a TSV file at most
///     <see cref="MaxTsvLines"/> lines. A duplicate (the same word, compared the way the entry's
///     case flag says, with the same case flag) is an error, never "last one wins".
///     </para>
/// </remarks>
public sealed class PronunciationOverrides
{
    /// <summary>The most entries a dictionary holds.</summary>
    public const int MaxEntries = 10_000;

    /// <summary>The most UTF-16 code units of an entry's word.</summary>
    public const int MaxWordLength = 64;

    /// <summary>The most UTF-16 code units of an entry's payload (spoken text or <c>scheme:phonemes</c>).</summary>
    public const int MaxPayloadLength = 256;

    /// <summary>The most letters of a word that is spelled out.</summary>
    public const int MaxSpellOutLetters = 32;

    /// <summary>The most UTF-16 code units of one TSV line, not counting its line terminator.</summary>
    public const int MaxTsvLineLength = 1_024;

    /// <summary>The most lines of a TSV file, counting blank and comment lines.</summary>
    public const int MaxTsvLines = 50_000;

    /// <summary>The number of tokens visited between two cancellation checks.</summary>
    internal const int CancellationCheckInterval = 256;

    /// <summary>The number of tab-separated columns of a TSV row without a payload.</summary>
    private const int MinTsvColumns = 3;

    /// <summary>The number of tab-separated columns of a TSV row with a payload.</summary>
    private const int MaxTsvColumns = 4;

    /// <summary>The lazily created shared empty dictionary.</summary>
    private static readonly PronunciationOverrides EmptyDictionary = new([]);

    /// <summary>The private copy of the entries, in the order supplied.</summary>
    private readonly PronunciationOverrideEntry[] _entries;

    /// <summary>The read-only view handed out by <see cref="Entries"/>.</summary>
    private readonly ReadOnlyCollection<PronunciationOverrideEntry> _entriesView;

    /// <summary>The case-sensitive lookup rows, sorted by ordinal key comparison.</summary>
    private readonly Row[] _sensitive;

    /// <summary>The case-insensitive lookup rows, sorted by ordinal-ignore-case key comparison.</summary>
    private readonly Row[] _insensitive;

    /// <summary>The length of the shortest key; only meaningful when there are entries.</summary>
    private readonly int _minKeyLength;

    /// <summary>The length of the longest key; only meaningful when there are entries.</summary>
    private readonly int _maxKeyLength;

    /// <summary>
    ///     Initializes a new dictionary from already validated, duplicate-free entries.
    /// </summary>
    /// <param name="entries">The entries; the array is owned by the new instance.</param>
    private PronunciationOverrides(PronunciationOverrideEntry[] entries)
    {
        _entries = entries;
        _entriesView = Array.AsReadOnly(entries);

        var sensitive = new List<Row>();
        var insensitive = new List<Row>();
        var minKeyLength = int.MaxValue;
        var maxKeyLength = 0;
        foreach (var entry in entries)
        {
            var key = OverrideSyntax.CanonicalKey(entry.Word);
            var kind = entry.Action switch
            {
                OverrideAction.Skip => TokenDirectiveKind.Skip,
                OverrideAction.Say => TokenDirectiveKind.Replace,
                OverrideAction.SpellOut => TokenDirectiveKind.Spell,
                _ => TokenDirectiveKind.Pronounce,
            };
            var value = entry.Action is OverrideAction.Say or OverrideAction.Pronunciation ? entry.Payload : null;
            var rows = entry.CaseSensitive ? sensitive : insensitive;
            rows.Add(new Row(key, kind, value));

            // The same word written with typographic apostrophes is the same key. Rows within one
            // table never share a key: a canonical key has only plain apostrophes and an alias
            // only typographic ones, so an alias never equals any canonical key; and two aliases
            // are equal only when their canonical keys are equal, which the Accumulator already
            // rejected as a duplicate. The sort below is therefore total and TryFind is
            // deterministic. Across tables the case-sensitive one is searched first and wins.
            if (key.Contains('\''))
            {
                rows.Add(new Row(key.Replace('\'', OverrideSyntax.TypographicApostrophe), kind, value));
            }

            minKeyLength = Math.Min(minKeyLength, key.Length);
            maxKeyLength = Math.Max(maxKeyLength, key.Length);
        }

        _sensitive = [.. sensitive];
        Array.Sort(_sensitive, static (a, b) => a.Key.AsSpan().CompareTo(b.Key, StringComparison.Ordinal));
        _insensitive = [.. insensitive];
        Array.Sort(_insensitive, static (a, b) => a.Key.AsSpan().CompareTo(b.Key, StringComparison.OrdinalIgnoreCase));
        _minKeyLength = minKeyLength;
        _maxKeyLength = maxKeyLength;
    }

    /// <summary>
    ///     Gets the dictionary with no entries; analyzing with it is the same as analyzing without
    ///     overrides.
    /// </summary>
    public static PronunciationOverrides Empty => EmptyDictionary;

    /// <summary>
    ///     Gets the number of entries.
    /// </summary>
    public int Count => _entries.Length;

    /// <summary>
    ///     Gets the entries in the order they were supplied (or appeared in the file). The list is
    ///     a read-only view of the dictionary's private copy.
    /// </summary>
    public IReadOnlyList<PronunciationOverrideEntry> Entries => _entriesView;

    /// <summary>
    ///     Builds a dictionary from entries.
    /// </summary>
    /// <param name="entries">The entries; enumerated once, at most <see cref="MaxEntries"/> + 1 of them. Must not be null.</param>
    /// <returns>The immutable dictionary; <see cref="Empty"/> when there are no entries.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="entries"/> or one of its elements is null.</exception>
    /// <exception cref="ArgumentException">
    ///     An entry is invalid or a duplicate, or there are more than <see cref="MaxEntries"/>
    ///     entries. The message names the zero-based index of the entry and the reason, never the
    ///     word or the payload.
    /// </exception>
    /// <remarks>
    ///     Entries are copied, so later changes to the source collection never affect the
    ///     dictionary. Stateless and thread-safe.
    /// </remarks>
    public static PronunciationOverrides Create(IEnumerable<PronunciationOverrideEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var accumulator = new Accumulator();
        var index = 0;
        foreach (var entry in entries)
        {
            ArgumentNullException.ThrowIfNull(entry);
            var reason = accumulator.TryAdd(entry);
            if (reason is not null)
            {
                throw new ArgumentException($"Entry {index.ToString(CultureInfo.InvariantCulture)}: {reason}.", nameof(entries));
            }

            index++;
        }

        return accumulator.Build();
    }

    /// <summary>
    ///     Reads a dictionary from the tab-separated format: one entry per line,
    ///     <c>word TAB case TAB action [TAB payload]</c>.
    /// </summary>
    /// <param name="reader">The text to read, to its end. Must not be null; neither disposed nor retained.</param>
    /// <returns>The immutable dictionary; <see cref="Empty"/> when the file has no entries.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="reader"/> is null.</exception>
    /// <exception cref="InvalidDataException">
    ///     A line is malformed, invalid or a duplicate, or a limit is exceeded. The message names
    ///     the one-based line number and the reason, never the line's content.
    /// </exception>
    /// <remarks>
    ///     <para>
    ///     Lines end with LF or CRLF (a lone CR is an error); a byte-order mark at the start of
    ///     the first line is skipped. Empty lines and lines starting with <c>#</c> are ignored; a
    ///     line of only whitespace is an error and there is no header row. The <c>case</c> column
    ///     is <c>sensitive</c> or <c>insensitive</c>; the <c>action</c> column is <c>skip</c>,
    ///     <c>say</c>, <c>spell</c> or <c>pron</c>; <c>skip</c> and <c>spell</c> take no payload
    ///     (an empty fourth column is tolerated), <c>say</c> takes the spoken words and
    ///     <c>pron</c> takes <c>ipa:...</c> or <c>arpabet:...</c>. No field may have leading or
    ///     trailing whitespace; there is no quoting or escaping; a control character other than
    ///     the tab separator is an error. Each entry is validated with the same rules as
    ///     <see cref="Create"/>.
    ///     </para>
    ///     <para>
    ///     The reader is consumed through a bounded buffer, so an endless input without line
    ///     breaks is rejected after <see cref="MaxTsvLineLength"/> + 1 characters and an endless
    ///     input of short lines after <see cref="MaxTsvLines"/> + 1 lines. Exceptions of the
    ///     reader itself propagate unchanged. Stateless and thread-safe.
    ///     </para>
    /// </remarks>
    public static PronunciationOverrides FromTsv(TextReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);

        var accumulator = new Accumulator();
        var buffer = new char[MaxTsvLineLength];
        var lineNumber = 0;
        var firstCharacter = true;
        while (true)
        {
            var length = ReadLine(reader, buffer, lineNumber + 1, ref firstCharacter);
            if (length < 0)
            {
                break;
            }

            lineNumber++;
            if (lineNumber > MaxTsvLines)
            {
                throw LineError(lineNumber, "the file has too many lines");
            }

            var line = buffer.AsSpan(0, length);
            if (line.IsEmpty)
            {
                continue;
            }

            foreach (var character in line)
            {
                if (char.IsControl(character) && character != '\t')
                {
                    throw LineError(lineNumber, "the line contains a control character");
                }
            }

            if (line[0] == '#')
            {
                continue;
            }

            var reason = ParseRow(line, out var entry);
            reason ??= accumulator.TryAdd(entry!);
            if (reason is not null)
            {
                throw LineError(lineNumber, reason);
            }
        }

        return accumulator.Build();
    }

    /// <summary>
    ///     Finds which entries apply to the tokens of a text.
    /// </summary>
    /// <param name="text">The text the tokens index (placeholders already neutralized). Must not be null.</param>
    /// <param name="tokens">The tokens in text order.</param>
    /// <param name="cancellationToken">Checked before any work and every <see cref="CancellationCheckInterval"/> tokens.</param>
    /// <param name="tokenVisited">A test seam called with the index of each token as it is visited; production passes null.</param>
    /// <returns>One directive per matching token, in ascending token order; an empty array when nothing matches.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is null.</exception>
    /// <exception cref="ArgumentException">A token is empty or outside the text.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is canceled.</exception>
    /// <remarks>
    ///     Linear in the number of tokens, with a logarithmic lookup per candidate token; a
    ///     token that cannot match (wrong first character, shorter than the shortest key or longer
    ///     than the longest, touching a word character) costs no lookup and no allocation.
    /// </remarks>
    internal TokenDirective[] Resolve(string text, ReadOnlySpan<NormalizationToken> tokens, CancellationToken cancellationToken, Action<int>? tokenVisited = null)
    {
        ArgumentNullException.ThrowIfNull(text);
        cancellationToken.ThrowIfCancellationRequested();

        List<TokenDirective>? found = null;
        for (var i = 0; i < tokens.Length; i++)
        {
            var token = tokens[i];

            // Start is checked against the text length and Length against what remains after it, so
            // the range is validated without computing Start + Length, which can overflow.
            if (token.Length <= 0 || token.Start < 0 || token.Start > text.Length || token.Length > text.Length - token.Start)
            {
                throw new ArgumentException("A token is empty or outside the text.", nameof(tokens));
            }

            // Every visited token counts, matched or not, so the check cadence is a fixed number of tokens.
            if ((i + 1) % CancellationCheckInterval == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            tokenVisited?.Invoke(i);

            if (_entries.Length == 0 || token.Length < _minKeyLength || token.Length > _maxKeyLength)
            {
                continue;
            }

            var span = text.AsSpan(token.Start, token.Length);
            if (!StartsWithLetter(span) || TouchesWordCharacter(text, token.Start, token.Length))
            {
                continue;
            }

            if (TryFind(_sensitive, span, StringComparison.Ordinal, out var row)
                || TryFind(_insensitive, span, StringComparison.OrdinalIgnoreCase, out row))
            {
                found ??= [];
                found.Add(new TokenDirective(i, row.Kind, row.Value));
            }
        }

        return found is null ? [] : [.. found];
    }

    /// <summary>
    ///     Reads one line through a bounded buffer.
    /// </summary>
    /// <param name="reader">The reader.</param>
    /// <param name="buffer">The buffer of exactly <see cref="MaxTsvLineLength"/> characters.</param>
    /// <param name="lineNumber">The one-based number of the line being read, for error messages.</param>
    /// <param name="firstCharacter">Whether no character of the stream has been read yet; a leading byte-order mark is skipped.</param>
    /// <returns>The length of the line without its terminator, or -1 at the end of the stream when no character was read.</returns>
    /// <exception cref="InvalidDataException">The line is longer than the buffer or has a lone carriage return.</exception>
    private static int ReadLine(TextReader reader, char[] buffer, int lineNumber, ref bool firstCharacter)
    {
        var length = 0;
        var anyRead = false;
        while (true)
        {
            var next = reader.Read();
            if (next < 0)
            {
                return anyRead ? length : -1;
            }

            anyRead = true;
            if (firstCharacter)
            {
                firstCharacter = false;
                if (next == '\uFEFF')
                {
                    continue;
                }
            }

            if (next == '\n')
            {
                return length;
            }

            if (next == '\r')
            {
                // Only CRLF is a line terminator.
                if (reader.Read() == '\n')
                {
                    return length;
                }

                throw LineError(lineNumber, "the line contains a control character (a lone carriage return)");
            }

            if (length == buffer.Length)
            {
                throw LineError(lineNumber, "the line is too long");
            }

            buffer[length++] = (char)next;
        }
    }

    /// <summary>
    ///     Parses the columns of one data row into an entry.
    /// </summary>
    /// <param name="line">The row, without terminator; not a comment, no control characters.</param>
    /// <param name="entry">The entry when this method returns <see langword="null"/>.</param>
    /// <returns><see langword="null"/> when the row parsed; otherwise the reason.</returns>
    private static string? ParseRow(ReadOnlySpan<char> line, out PronunciationOverrideEntry? entry)
    {
        entry = null;
        if (IsAllWhiteSpace(line))
        {
            return "the line contains only whitespace";
        }

        // Split at the tabs without allocating a column array (MemoryExtensions.Split is not
        // available on every target framework). A fifth column is detected by counting one more.
        Span<Range> ranges = stackalloc Range[MaxTsvColumns + 1];
        var columns = 0;
        var fieldStart = 0;
        for (var i = 0; i <= line.Length; i++)
        {
            if (i < line.Length && line[i] != '\t')
            {
                continue;
            }

            if (columns == ranges.Length)
            {
                break;
            }

            ranges[columns++] = fieldStart..i;
            fieldStart = i + 1;
        }
        if (columns is < MinTsvColumns or > MaxTsvColumns)
        {
            return "expected three or four tab-separated columns";
        }

        for (var i = 0; i < columns; i++)
        {
            var field = line[ranges[i]];
            if (field.Length > 0 && (char.IsWhiteSpace(field[0]) || char.IsWhiteSpace(field[^1])))
            {
                return "a column has leading or trailing whitespace";
            }
        }

        var caseColumn = line[ranges[1]];
        bool caseSensitive;
        if (caseColumn.SequenceEqual("sensitive"))
        {
            caseSensitive = true;
        }
        else if (caseColumn.SequenceEqual("insensitive"))
        {
            caseSensitive = false;
        }
        else
        {
            return "the case column must be 'sensitive' or 'insensitive'";
        }

        var actionColumn = line[ranges[2]];
        var payload = columns == MaxTsvColumns ? line[ranges[3]] : default;
        OverrideAction action;
        if (actionColumn.SequenceEqual("skip"))
        {
            action = OverrideAction.Skip;
        }
        else if (actionColumn.SequenceEqual("say"))
        {
            action = OverrideAction.Say;
        }
        else if (actionColumn.SequenceEqual("spell"))
        {
            action = OverrideAction.SpellOut;
        }
        else if (actionColumn.SequenceEqual("pron"))
        {
            action = OverrideAction.Pronunciation;
        }
        else
        {
            return "the action column must be 'skip', 'say', 'spell' or 'pron'";
        }

        string? payloadText = null;
        if (action is OverrideAction.Say or OverrideAction.Pronunciation)
        {
            payloadText = payload.ToString();
        }
        else if (!payload.IsEmpty)
        {
            return "a payload is not allowed for this action";
        }

        entry = new PronunciationOverrideEntry(line[ranges[0]].ToString(), caseSensitive, action, payloadText);
        return null;
    }

    /// <summary>
    ///     Creates the exception for a bad TSV line.
    /// </summary>
    /// <param name="lineNumber">The one-based line number.</param>
    /// <param name="reason">The fixed-text reason.</param>
    /// <returns>The exception to throw.</returns>
    private static InvalidDataException LineError(int lineNumber, string reason) =>
        new($"Line {lineNumber.ToString(CultureInfo.InvariantCulture)}: {reason}.");

    /// <summary>
    ///     Tells whether every character is whitespace.
    /// </summary>
    /// <param name="line">The characters.</param>
    /// <returns><see langword="true"/> when there is no non-whitespace character.</returns>
    private static bool IsAllWhiteSpace(ReadOnlySpan<char> line)
    {
        foreach (var character in line)
        {
            if (!char.IsWhiteSpace(character))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    ///     Tells whether a token starts with a letter, counted per Unicode scalar.
    /// </summary>
    /// <param name="token">The token's characters; not empty.</param>
    /// <returns><see langword="true"/> when the first scalar is a letter.</returns>
    private static bool StartsWithLetter(ReadOnlySpan<char> token)
    {
        Rune.DecodeFromUtf16(token, out var rune, out _);
        return Rune.IsLetter(rune);
    }

    /// <summary>
    ///     Tells whether the scalar before or after a token is a letter, a combining mark or a
    ///     decimal digit, in which case the token is only a piece of a longer word.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="start">The token's start.</param>
    /// <param name="length">The token's length.</param>
    /// <returns><see langword="true"/> when either neighbor is a word character.</returns>
    private static bool TouchesWordCharacter(string text, int start, int length)
    {
        // Decoding whole scalars means a supplementary-plane letter next to the token counts as one
        // character; a malformed neighbor decodes as U+FFFD, which is not a word character.
        Rune.DecodeLastFromUtf16(text.AsSpan(0, start), out var before, out _);
        Rune.DecodeFromUtf16(text.AsSpan(start + length), out var after, out _);
        return IsWordCharacter(before) || IsWordCharacter(after);
    }

    /// <summary>
    ///     Tells whether a scalar is a letter, a combining mark or a decimal digit.
    /// </summary>
    /// <param name="rune">The scalar.</param>
    /// <returns><see langword="true"/> for a word character.</returns>
    private static bool IsWordCharacter(Rune rune) =>
        Rune.IsLetter(rune) || OverrideSyntax.IsDecimalDigit(rune) || OverrideSyntax.IsMark(rune);

    /// <summary>
    ///     Binary-searches sorted rows for a token.
    /// </summary>
    /// <param name="rows">The rows, sorted with <paramref name="comparison"/>.</param>
    /// <param name="token">The token's characters.</param>
    /// <param name="comparison">The comparison the rows are sorted with.</param>
    /// <param name="row">The row found.</param>
    /// <returns><see langword="true"/> when a row's key equals the token under the comparison.</returns>
    private static bool TryFind(Row[] rows, ReadOnlySpan<char> token, StringComparison comparison, out Row row)
    {
        int low = 0, high = rows.Length - 1;
        while (low <= high)
        {
            var middle = low + ((high - low) / 2);
            var order = token.CompareTo(rows[middle].Key, comparison);
            if (order == 0)
            {
                row = rows[middle];
                return true;
            }

            if (order > 0)
            {
                low = middle + 1;
            }
            else
            {
                high = middle - 1;
            }
        }

        row = default;
        return false;
    }

    /// <summary>
    ///     One lookup row: a canonical key (or its typographic-apostrophe spelling) and what to do.
    /// </summary>
    /// <param name="Key">The key.</param>
    /// <param name="Kind">The directive kind.</param>
    /// <param name="Value">The directive's value, or null.</param>
    private readonly record struct Row(string Key, TokenDirectiveKind Kind, string? Value);

    /// <summary>
    ///     Collects validated entries and rejects duplicates and overflow; shared by
    ///     <see cref="Create"/> and <see cref="FromTsv"/> so both apply the same rules.
    /// </summary>
    private sealed class Accumulator
    {
        /// <summary>The accepted entries in order.</summary>
        private readonly List<PronunciationOverrideEntry> _accepted = [];

        /// <summary>The canonical keys of the case-sensitive entries.</summary>
        private readonly HashSet<string> _sensitiveKeys = new(StringComparer.Ordinal);

        /// <summary>The canonical keys of the case-insensitive entries.</summary>
        private readonly HashSet<string> _insensitiveKeys = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        ///     Validates an entry and adds it.
        /// </summary>
        /// <param name="entry">The entry.</param>
        /// <returns><see langword="null"/> when the entry was added; otherwise the fixed-text reason.</returns>
        internal string? TryAdd(PronunciationOverrideEntry entry)
        {
            if (_accepted.Count >= MaxEntries)
            {
                return "the dictionary has too many entries";
            }

            var reason = ValidateEntry(entry);
            if (reason is not null)
            {
                return reason;
            }

            var key = OverrideSyntax.CanonicalKey(entry.Word);
            if (!(entry.CaseSensitive ? _sensitiveKeys : _insensitiveKeys).Add(key))
            {
                return "duplicate entry";
            }

            _accepted.Add(entry);
            return null;
        }

        /// <summary>
        ///     Builds the dictionary from the accepted entries.
        /// </summary>
        /// <returns>The dictionary; the shared empty one when nothing was accepted.</returns>
        internal PronunciationOverrides Build() =>
            _accepted.Count == 0 ? EmptyDictionary : new PronunciationOverrides([.. _accepted]);

        /// <summary>
        ///     Validates one entry on its own.
        /// </summary>
        /// <param name="entry">The entry.</param>
        /// <returns><see langword="null"/> when valid; otherwise the reason (fixed text, never the entry's content).</returns>
        private static string? ValidateEntry(PronunciationOverrideEntry entry)
        {
            if (entry.Action is not (OverrideAction.Skip or OverrideAction.Say or OverrideAction.SpellOut or OverrideAction.Pronunciation))
            {
                return "the action is not defined";
            }

            var reason = OverrideSyntax.ValidateKey(entry.Word);
            if (reason is not null)
            {
                return reason;
            }

            switch (entry.Action)
            {
                case OverrideAction.Skip:
                    return entry.Payload is null ? null : "a payload is not allowed for this action";
                case OverrideAction.SpellOut:
                    return entry.Payload is not null
                        ? "a payload is not allowed for this action"
                        : OverrideSyntax.ValidateSpellKey(entry.Word);
                case OverrideAction.Say:
                    return OverrideSyntax.ValidateSayPayload(entry.Payload);
                default:
                    return OverrideSyntax.ParsePronunciation(entry.Payload, out _, out _);
            }
        }
    }
}
