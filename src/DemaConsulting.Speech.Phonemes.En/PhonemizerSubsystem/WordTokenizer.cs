using DemaConsulting.Speech.Phonemes.En.LexiconSubsystem;

namespace DemaConsulting.Speech.Phonemes.En.PhonemizerSubsystem;

/// <summary>
///     The kind of a token of the spoken text.
/// </summary>
internal enum TokenKind
{
    /// <summary>A word: Latin letters, possibly with inner apostrophes (<c>don't</c>) and hyphens (<c>well-known</c>).</summary>
    Word,

    /// <summary>One letter of a spelled replacement, spoken by its letter name.</summary>
    Letter,

    /// <summary>A run of ASCII digits.</summary>
    Digits,

    /// <summary>A contraction or possessive ending (<c>'s</c>, <c>'ll</c>) that follows a word token directly, split off by a replacement boundary.</summary>
    Clitic,

    /// <summary>A run of the same punctuation mark.</summary>
    Punctuation,

    /// <summary>A run of characters the phonemizer cannot pronounce.</summary>
    Unsupported,
}

/// <summary>
///     One token: a kind and a range of the spoken text.
/// </summary>
/// <param name="Kind">The kind.</param>
/// <param name="Start">The first spoken index.</param>
/// <param name="End">One past the last spoken index.</param>
internal readonly record struct Token(TokenKind Kind, int Start, int End)
{
    /// <summary>Gets the length of the token.</summary>
    public int Length => End - Start;
}

/// <summary>
///     Splits spoken text into word, letter, digit, clitic, punctuation and unsupported tokens.
/// </summary>
/// <remarks>
///     <para>
///     Rules, in the order they are tried at each position: whitespace and the core library's audio-tag placeholder
///     are skipped; a letter of a spelled replacement is a token of its own; a run of digits is one token; a run of
///     Latin letters is a word, which may contain apostrophes between letters and hyphens between letters (and may
///     take a trailing apostrophe after <c>s</c>, the plural possessive), except a hyphen between two replacements,
///     which ends the word and is a punctuation token (the boundary between the digit groups of a phone number);
///     a leading apostrophe belongs to a word only
///     when the lexicon lists it that way (<c>'cause</c>, <c>'tis</c>) and otherwise is a quote mark; a contraction
///     ending directly after a word that a replacement boundary cut off (<c>I</c> of a spelled <c>F B I</c> followed
///     by <c>'s</c>) is a clitic token; the marks in <see cref="PunctuationMarks"/> are punctuation, a run of one
///     mark being a single token; anything else is an unsupported run.
///     </para>
///     <para>Stateless and thread-safe.</para>
/// </remarks>
internal static class WordTokenizer
{
    /// <summary>The punctuation marks the phonemizer passes through; the apostrophes are here for the quote case.</summary>
    private const string PunctuationMarks = ".,;:!?\u2026\u2014\u2013-()[]{}\"\u201C\u201D\u2018\u2019'\u00AB\u00BB";

    /// <summary>The contraction endings (without the apostrophe) recognized as a clitic token.</summary>
    private static readonly string[] CliticEndings = ["s", "d", "ll", "ve", "re", "m"];

    /// <summary>
    ///     Tokenizes spoken text.
    /// </summary>
    /// <param name="spoken">The spoken text.</param>
    /// <param name="lexicon">The lexicon, consulted only to decide whether a leading apostrophe is part of a word.</param>
    /// <returns>The tokens in order.</returns>
    public static List<Token> Tokenize(SpokenText spoken, CmuLexicon lexicon)
    {
        ArgumentNullException.ThrowIfNull(spoken);
        ArgumentNullException.ThrowIfNull(lexicon);

        var tokens = new List<Token>();
        var index = 0;
        while (index < spoken.Length)
        {
            index = Next(spoken, lexicon, index, tokens);
        }

        return tokens;
    }

    /// <summary>
    ///     Tells whether a character is a Latin letter (ASCII or in the Latin-1 and Latin Extended blocks).
    /// </summary>
    /// <param name="c">The character.</param>
    /// <returns><see langword="true"/> for a Latin letter.</returns>
    public static bool IsLatinLetter(char c) =>
        c is (>= 'A' and <= 'Z') or (>= 'a' and <= 'z')
        || (c is >= '\u00C0' and <= '\u024F' && c is not '\u00D7' and not '\u00F7');

    /// <summary>
    ///     Tells whether a character is an apostrophe (straight or typographic).
    /// </summary>
    /// <param name="c">The character.</param>
    /// <returns><see langword="true"/> for an apostrophe.</returns>
    public static bool IsApostrophe(char c) => c is '\'' or '\u2019';

    /// <summary>
    ///     Reads the token that starts at an index and adds it.
    /// </summary>
    /// <param name="spoken">The spoken text.</param>
    /// <param name="lexicon">The lexicon.</param>
    /// <param name="index">The index to read at.</param>
    /// <param name="tokens">Receives the token, if any.</param>
    /// <returns>The index after the token.</returns>
    private static int Next(SpokenText spoken, CmuLexicon lexicon, int index, List<Token> tokens)
    {
        var s = spoken.Text;
        var c = s[index];

        // Whitespace and the audio-tag placeholder carry no sound
        if (char.IsWhiteSpace(c) || c == '\uFFFC')
        {
            return index + 1;
        }

        // A letter of a spelled replacement is spoken by name, one token each
        if (spoken.IsSpelledAt(index) && IsLatinLetter(c))
        {
            tokens.Add(new Token(TokenKind.Letter, index, index + 1));
            return index + 1;
        }

        if (c is >= '0' and <= '9')
        {
            return Add(tokens, TokenKind.Digits, index, ScanWhile(s, index, ch => ch is >= '0' and <= '9'));
        }

        if (IsLatinLetter(c))
        {
            return Add(tokens, TokenKind.Word, index, ScanWord(spoken, index));
        }

        return IsApostrophe(c) ? NextApostrophe(spoken, lexicon, index, tokens) : NextMark(spoken, index, tokens);
    }

    /// <summary>
    ///     Reads a token that starts with an apostrophe: a clitic, a word that the lexicon lists with a leading apostrophe, or a quote mark.
    /// </summary>
    /// <param name="spoken">The spoken text.</param>
    /// <param name="lexicon">The lexicon.</param>
    /// <param name="index">The index of the apostrophe.</param>
    /// <param name="tokens">Receives the token.</param>
    /// <returns>The index after the token.</returns>
    private static int NextApostrophe(SpokenText spoken, CmuLexicon lexicon, int index, List<Token> tokens)
    {
        var s = spoken.Text;
        var lettersEnd = ScanWhile(s, index + 1, IsLatinLetter);
        if (lettersEnd > index + 1)
        {
            var letters = s[(index + 1)..lettersEnd].ToLowerInvariant();

            // A contraction ending cut off from its word by a replacement boundary
            if (index > 0 && IsLatinLetter(s[index - 1]) && Array.IndexOf(CliticEndings, letters) >= 0)
            {
                tokens.Add(new Token(TokenKind.Clitic, index, lettersEnd));
                return lettersEnd;
            }

            // 'cause, 'tis
            if (lexicon.Contains("'" + letters))
            {
                tokens.Add(new Token(TokenKind.Word, index, lettersEnd));
                return lettersEnd;
            }
        }

        return Add(tokens, TokenKind.Punctuation, index, index + 1);
    }

    /// <summary>
    ///     Reads a punctuation run or an unsupported run.
    /// </summary>
    /// <param name="spoken">The spoken text.</param>
    /// <param name="index">The index of the first character.</param>
    /// <param name="tokens">Receives the token.</param>
    /// <returns>The index after the token.</returns>
    private static int NextMark(SpokenText spoken, int index, List<Token> tokens)
    {
        var s = spoken.Text;
        var c = s[index];
        if (PunctuationMarks.Contains(c))
        {
            // A run of one mark ("...", "!!") is one token
            return Add(tokens, TokenKind.Punctuation, index, ScanWhile(s, index, ch => ch == c));
        }

        // Anything else up to the next space, letter, digit or mark is unsupported
        return Add(tokens, TokenKind.Unsupported, index, ScanWhile(s, index, ch => !char.IsWhiteSpace(ch) && ch != '\uFFFC' && !IsLatinLetter(ch) && ch is not (>= '0' and <= '9') && !PunctuationMarks.Contains(ch)));
    }

    /// <summary>
    ///     Scans a word: Latin letters with inner apostrophes and hyphens and a trailing plural-possessive apostrophe.
    /// </summary>
    /// <param name="spoken">The spoken text.</param>
    /// <param name="start">The index of the first letter.</param>
    /// <returns>The index after the word.</returns>
    private static int ScanWord(SpokenText spoken, int start)
    {
        var s = spoken.Text;
        var index = start;
        while (index < s.Length)
        {
            if (IsWordLetter(spoken, index))
            {
                index++;
            }
            else if ((IsApostrophe(s[index]) || s[index] == '-') && index + 1 < s.Length && IsWordLetter(spoken, index + 1) && !spoken.IsBoundaryHyphenAt(index))
            {
                // An apostrophe or hyphen between two letters stays inside the word, unless the hyphen separates two
                // replacements (the groups of a phone number), where it is a boundary of its own
                index++;
            }
            else
            {
                break;
            }
        }

        // dogs' : the plural possessive keeps its apostrophe
        if (index < s.Length && IsApostrophe(s[index]) && (s[index - 1] is 's' or 'S') && (index + 1 >= s.Length || !IsLatinLetter(s[index + 1])))
        {
            index++;
        }

        return index;
    }

    /// <summary>
    ///     Tells whether the character at an index is a letter that belongs to an ordinary word (not a spelled letter).
    /// </summary>
    /// <param name="spoken">The spoken text.</param>
    /// <param name="index">The index.</param>
    /// <returns><see langword="true"/> for a Latin letter outside a spelled replacement.</returns>
    private static bool IsWordLetter(SpokenText spoken, int index) =>
        IsLatinLetter(spoken.Text[index]) && !spoken.IsSpelledAt(index);

    /// <summary>
    ///     Adds a token and returns its end.
    /// </summary>
    /// <param name="tokens">The token list.</param>
    /// <param name="kind">The kind.</param>
    /// <param name="start">The start.</param>
    /// <param name="end">The end.</param>
    /// <returns><paramref name="end"/>.</returns>
    private static int Add(List<Token> tokens, TokenKind kind, int start, int end)
    {
        tokens.Add(new Token(kind, start, end));
        return end;
    }

    /// <summary>
    ///     Finds the end of the run of characters that satisfy a test.
    /// </summary>
    /// <param name="s">The text.</param>
    /// <param name="start">The first index to test.</param>
    /// <param name="test">The test.</param>
    /// <returns>The index of the first character that fails the test, or the text length.</returns>
    private static int ScanWhile(string s, int start, Func<char, bool> test)
    {
        var index = start;
        while (index < s.Length && test(s[index]))
        {
            index++;
        }

        return index;
    }
}
