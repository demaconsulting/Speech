using System.Reflection;
using System.Text;

namespace DemaConsulting.Speech.Text.En.NormalizationSubsystem;

/// <summary>
///     The outcome of recognizing one acronym or initialism.
/// </summary>
/// <param name="Spoken">The letters to speak, upper-case and separated by single spaces (and <c>and</c> for an ampersand).</param>
/// <param name="TokenEndsWithPeriod">
///     Whether the recognized token itself ends with the period of a dotted form (<c>U.S.</c>);
///     a dotted form that does not (<c>U.S</c>) is only an initialism when a separate period
///     token follows it.
/// </param>
internal readonly record struct AcronymMatch(string Spoken, bool TokenEndsWithPeriod);

/// <summary>
///     Recognizes acronyms and initialisms that are spoken letter by letter: listed ones, dotted
///     ones, ampersand ones and vowel-less all-capital tokens, case-sensitively.
/// </summary>
/// <remarks>
///     Case matters on purpose: <c>US</c> is the country but <c>us</c> and <c>Us</c> are the
///     pronoun, <c>IT</c> the department but <c>It</c> the pronoun. A token that is also an English
///     word in capitals is left alone inside an all-capital sentence (shouting). Acronyms read as
///     words (<c>NASA</c>) are not listed and, because they contain a vowel, are left as written.
///     The lexicon is the embedded <c>acronyms.tsv</c>. Instances are immutable after loading and
///     thread-safe.
/// </remarks>
internal sealed class AcronymRecognizer
{
    /// <summary>The manifest name of the embedded lexicon.</summary>
    private const string ResourceName = "DemaConsulting.Speech.Text.En.Resources.acronyms.tsv";

    /// <summary>The number of columns of the lexicon file.</summary>
    private const int ColumnCount = 3;

    /// <summary>The shortest upper-case token that can be an initialism.</summary>
    private const int MinimumLetters = 2;

    /// <summary>The longest upper-case token considered at all (the longest listed form is four).</summary>
    private const int MaximumLetters = 6;

    /// <summary>The longest rule-spelled (unlisted) token.</summary>
    private const int MaximumRuleLetters = 5;

    /// <summary>The longest group of letters on either side of an ampersand.</summary>
    private const int MaximumAmpersandGroupLetters = 4;

    /// <summary>The lazily loaded default recognizer; loading is deterministic, so a failure is cached.</summary>
    private static readonly Lazy<AcronymRecognizer> DefaultRecognizer =
        new(LoadDefault, LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>The listed forms with their spoken text and ambiguity flag.</summary>
    private readonly Dictionary<string, (string Spoken, bool Ambiguous)> _entries;

    /// <summary>
    ///     Initializes a new instance over parsed entries.
    /// </summary>
    /// <param name="entries">The listed forms.</param>
    private AcronymRecognizer(Dictionary<string, (string Spoken, bool Ambiguous)> entries)
    {
        _entries = entries;
    }

    /// <summary>
    ///     Gets the recognizer over the embedded lexicon, loaded once on first use.
    /// </summary>
    /// <exception cref="InvalidDataException">The embedded lexicon is malformed (a defective build).</exception>
    internal static AcronymRecognizer Default => DefaultRecognizer.Value;

    /// <summary>
    ///     Parses a lexicon in the documented tab-separated format.
    /// </summary>
    /// <param name="reader">The lexicon text. Must not be null; not disposed by this method.</param>
    /// <returns>The recognizer.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="reader"/> is null.</exception>
    /// <exception cref="InvalidDataException">
    ///     A row is malformed: wrong column count, empty form or spoken text, a form that is not
    ///     all upper-case letters, an ambiguity flag other than yes or no, or a duplicate form.
    ///     The message names the one-based line number.
    /// </exception>
    internal static AcronymRecognizer FromTsv(TextReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);

        var entries = new Dictionary<string, (string Spoken, bool Ambiguous)>(StringComparer.Ordinal);
        var headerSeen = false;
        var lineNumber = 0;
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

            var columns = line.Split('\t');
            if (columns.Length != ColumnCount)
            {
                throw new InvalidDataException($"Line {lineNumber}: expected {ColumnCount} columns but found {columns.Length}.");
            }

            var form = columns[0];
            if (form.Length == 0 || !form.All(character => character is >= 'A' and <= 'Z'))
            {
                throw new InvalidDataException($"Line {lineNumber}: the form must be one or more upper-case ASCII letters.");
            }

            if (columns[1].Length == 0 || columns[1].Trim() != columns[1])
            {
                throw new InvalidDataException($"Line {lineNumber}: the spoken text is empty or has surrounding whitespace.");
            }

            var ambiguous = columns[2] switch
            {
                "yes" => true,
                "no" => false,
                _ => throw new InvalidDataException($"Line {lineNumber}: the ambiguous column must be 'yes' or 'no'."),
            };

            if (!entries.TryAdd(form, (columns[1], ambiguous)))
            {
                throw new InvalidDataException($"Line {lineNumber}: duplicate form '{form}'.");
            }
        }

        return new AcronymRecognizer(entries);
    }

    /// <summary>
    ///     Decides whether a token is an acronym or initialism spoken letter by letter.
    /// </summary>
    /// <param name="token">The token's characters.</param>
    /// <param name="sentenceIsAllCapitals">
    ///     Whether the token's sentence is written entirely in capitals, in which case an
    ///     ambiguous or unlisted capital word is more likely shouting than an initialism.
    /// </param>
    /// <param name="match">The recognition when this method returns <see langword="true"/>.</param>
    /// <returns><see langword="true"/> when the token is to be spoken letter by letter.</returns>
    internal bool TryRecognize(ReadOnlySpan<char> token, bool sentenceIsAllCapitals, out AcronymMatch match)
    {
        match = default;

        // Dotted letters (U.S., U.S.A., p.m.): letters and periods alternating.
        if (TryRecognizeDotted(token, out match))
        {
            return true;
        }

        // Ampersand initialisms (AT&T, R&D, Q&A), optionally with a possessive tail.
        if (TryRecognizeAmpersand(token, out match))
        {
            return true;
        }

        // Plain upper-case tokens: listed first, then the vowel-less rule.
        if (token.Length < MinimumLetters || token.Length > MaximumLetters || !IsAllUpperAsciiLetters(token))
        {
            return false;
        }

        if (_entries.TryGetValue(token.ToString(), out var entry))
        {
            if (entry.Ambiguous && sentenceIsAllCapitals)
            {
                return false;
            }

            match = new AcronymMatch(entry.Spoken, false);
            return true;
        }

        if (sentenceIsAllCapitals || token.Length > MaximumRuleLetters || ContainsVowel(token) || IsRomanNumeralShaped(token))
        {
            return false;
        }

        match = new AcronymMatch(SpellLetters(token), false);
        return true;
    }

    /// <summary>
    ///     Recognizes letters alternating with periods.
    /// </summary>
    /// <param name="token">The token's characters.</param>
    /// <param name="match">The recognition when found.</param>
    /// <returns><see langword="true"/> for <c>U.S.</c> style (ending in a period) or <c>U.S</c> style (not).</returns>
    private static bool TryRecognizeDotted(ReadOnlySpan<char> token, out AcronymMatch match)
    {
        match = default;

        // The shortest form is "a.b" (three characters); letters sit at even offsets, periods at odd ones.
        if (token.Length < 3)
        {
            return false;
        }

        var endsWithPeriod = token[^1] == '.';
        var letterCount = (token.Length + (endsWithPeriod ? 0 : 1)) / 2;
        if (letterCount < MinimumLetters || (endsWithPeriod ? token.Length != letterCount * 2 : token.Length != (letterCount * 2) - 1))
        {
            return false;
        }

        for (var i = 0; i < token.Length; i++)
        {
            var expectLetter = i % 2 == 0;
            if (expectLetter ? !IsAsciiLetter(token[i]) : token[i] != '.')
            {
                return false;
            }
        }

        // Spoken form: every letter upper-cased, separated by single spaces.
        var builder = new StringBuilder(letterCount * 2);
        for (var i = 0; i < token.Length; i += 2)
        {
            if (builder.Length > 0)
            {
                builder.Append(' ');
            }

            builder.Append(char.ToUpperInvariant(token[i]));
        }

        match = new AcronymMatch(builder.ToString(), endsWithPeriod);
        return true;
    }

    /// <summary>
    ///     Recognizes <c>AT&amp;T</c>-style tokens: groups of one to four capitals joined by
    ///     ampersands, optionally followed by a possessive <c>'s</c>.
    /// </summary>
    /// <param name="token">The token's characters.</param>
    /// <param name="match">The recognition when found.</param>
    /// <returns><see langword="true"/> when the token has that shape.</returns>
    private static bool TryRecognizeAmpersand(ReadOnlySpan<char> token, out AcronymMatch match)
    {
        match = default;
        if (token.IndexOf('&') < 0)
        {
            return false;
        }

        // Peel a possessive tail (ASCII or typographic apostrophe) before checking the groups.
        var possessive = false;
        var body = token;
        if (body.Length > 2 && body[^1] is 's' && IsApostropheCharacter(body[^2]))
        {
            possessive = true;
            body = body[..^2];
        }

        var builder = new StringBuilder(body.Length * 2);
        var groups = 0;
        var position = 0;
        while (position <= body.Length)
        {
            // One group: one to four capitals up to the next ampersand or the end.
            var length = 0;
            while (position + length < body.Length && body[position + length] != '&')
            {
                if (body[position + length] is < 'A' or > 'Z')
                {
                    return false;
                }

                length++;
            }

            if (length is 0 or > MaximumAmpersandGroupLetters)
            {
                return false;
            }

            if (groups > 0)
            {
                builder.Append(" and ");
            }

            builder.Append(SpellLetters(body.Slice(position, length)));
            groups++;
            position += length + 1;
        }

        // At least two groups, i.e. at least one ampersand, between capitals.
        if (groups < 2)
        {
            return false;
        }

        if (possessive)
        {
            builder.Append("'s");
        }

        match = new AcronymMatch(builder.ToString(), false);
        return true;
    }

    /// <summary>
    ///     Joins letters with single spaces.
    /// </summary>
    /// <param name="letters">The letters.</param>
    /// <returns>The letters separated by single spaces.</returns>
    private static string SpellLetters(ReadOnlySpan<char> letters)
    {
        var builder = new StringBuilder((letters.Length * 2) - 1);
        for (var i = 0; i < letters.Length; i++)
        {
            if (i > 0)
            {
                builder.Append(' ');
            }

            builder.Append(letters[i]);
        }

        return builder.ToString();
    }

    /// <summary>
    ///     Tells whether every character is an upper-case ASCII letter.
    /// </summary>
    /// <param name="token">The token's characters.</param>
    /// <returns><see langword="true"/> when all characters are A to Z.</returns>
    private static bool IsAllUpperAsciiLetters(ReadOnlySpan<char> token)
    {
        foreach (var character in token)
        {
            if (character is < 'A' or > 'Z')
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    ///     Tells whether any character is an upper-case vowel.
    /// </summary>
    /// <param name="token">The upper-case token's characters.</param>
    /// <returns><see langword="true"/> when the token contains A, E, I, O or U.</returns>
    private static bool ContainsVowel(ReadOnlySpan<char> token)
    {
        foreach (var character in token)
        {
            if (character is 'A' or 'E' or 'I' or 'O' or 'U')
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Tells whether the token consists only of Roman-numeral letters.
    /// </summary>
    /// <param name="token">The upper-case token's characters.</param>
    /// <returns><see langword="true"/> when every letter is one of I, V, X, L, C, D, M.</returns>
    private static bool IsRomanNumeralShaped(ReadOnlySpan<char> token)
    {
        foreach (var character in token)
        {
            if (character is not ('I' or 'V' or 'X' or 'L' or 'C' or 'D' or 'M'))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    ///     Tells whether a character is an ASCII letter.
    /// </summary>
    /// <param name="character">The character.</param>
    /// <returns><see langword="true"/> for A to Z and a to z.</returns>
    private static bool IsAsciiLetter(char character) => character is >= 'A' and <= 'Z' or >= 'a' and <= 'z';

    /// <summary>
    ///     Tells whether a character is an ASCII or typographic apostrophe.
    /// </summary>
    /// <param name="character">The character.</param>
    /// <returns><see langword="true"/> for U+0027, U+2018, U+2019, U+201B and U+02BC.</returns>
    private static bool IsApostropheCharacter(char character) =>
        character is '\'' or '\u2018' or '\u2019' or '\u201B' or '\u02BC';

    /// <summary>
    ///     Loads the embedded lexicon.
    /// </summary>
    /// <returns>The default recognizer.</returns>
    /// <exception cref="InvalidDataException">The resource is missing or malformed.</exception>
    private static AcronymRecognizer LoadDefault()
    {
        using var stream = typeof(AcronymRecognizer).GetTypeInfo().Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidDataException($"The embedded resource '{ResourceName}' is missing.");
        using var reader = new StreamReader(stream);
        return FromTsv(reader);
    }
}
