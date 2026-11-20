namespace DemaConsulting.Speech.Text.En.NormalizationSubsystem;

/// <summary>
///     Recognizes the tokens that mix letters and digits in the ways of identifiers and names - <c>A4</c>, <c>B12</c>,
///     <c>H2O</c>, <c>R2D2</c>, <c>MP3</c>, <c>COVID-19</c> - and says how each part is spoken, so <c>A4</c> is read
///     <i>A four</i> (not the article and <i>four</i>) and <c>COVID-19</c> is read <i>COVID nineteen</i> (not a pause and
///     <i>one nine</i>).
/// </summary>
/// <remarks>
///     <para>
///     An identifier is a token of ASCII capital letters and digits that starts with letters and alternates runs of one
///     to three letters and one to four digits. Each letter run is spelled; each digit run is read as a cardinal when it
///     has at most three digits and no leading zero (<c>B12</c> is <i>B twelve</i>, <c>A100</c> is <i>A one hundred</i>)
///     and digit by digit otherwise (<c>RTX4090</c> is <i>R T X four zero nine zero</i>). A word of four or more capital
///     letters before digits (<c>COVID19</c>) is not an identifier and is left alone.
///     </para>
///     <para>
///     A name joined to a number by a hyphen (<c>SCP-184</c>, <c>F-16</c>, <c>COVID-19</c>, <c>Catch-22</c>) is a token of
///     letters, a hyphen and digits. The hyphen and the digits are replaced together by the number, so the name and the
///     number are two words with nothing between them (not one hyphenated compound, which ran the letters into the
///     number: <i>S-C-P-one</i>, and not a pause). Letters that are one to three capitals are an identifier's letter run
///     and are spelled, each by its name, like the letter runs of <c>MP3</c> (<c>SCP-184</c> is <i>S C P one hundred eighty
///     four</i>, <c>A-1</c> is <i>A one</i>, not the article); any other name (<c>COVID</c>, <c>Catch</c>, <c>Boeing</c>) is
///     left as written. The digits follow the identifier convention: a cardinal for one to three digits without a leading
///     zero, digit by digit otherwise (<c>SCP-049</c> is <i>zero four nine</i>); four digits are accepted after spelled
///     capitals only (<c>SCP-1234</c>, read digit by digit like <c>RTX4090</c>); four digits after any other name are
///     left to the numeric rules, as before.
///     </para>
///     The class is stateless and thread-safe. Every span has only capital letters or letters, single spaces and no digit,
///     comma, colon, semicolon or whitespace.
/// </remarks>
internal static class AlphanumericTokenRecognizer
{
    /// <summary>The longest token that is examined.</summary>
    internal const int MaxTokenLength = 12;

    /// <summary>The most letters in one run of an identifier.</summary>
    private const int MaxLetterRun = 3;

    /// <summary>The most digits in one run of an identifier or after a hyphen.</summary>
    private const int MaxDigitRun = 4;

    /// <summary>The most digits that are read as a cardinal; longer runs are read digit by digit.</summary>
    private const int MaxCardinalRun = 3;

    /// <summary>
    ///     Finds the spans of a token that is an identifier of capital letters and digits.
    /// </summary>
    /// <param name="token">The token's characters.</param>
    /// <param name="tokenStart">The offset of the token in the text; the spans index the text.</param>
    /// <returns>The spans in text order (letters spelled, digits read), or <see langword="null"/> when the token is not an identifier.</returns>
    internal static IReadOnlyList<NumericSpan>? RecognizeIdentifier(ReadOnlySpan<char> token, int tokenStart)
    {
        // A possessive tail stays as written after the last spoken word (B12's)
        token = WithoutPossessive(token);
        if (token.Length < 2 || token.Length > MaxTokenLength || token[0] is not (>= 'A' and <= 'Z'))
        {
            return null;
        }

        // First pass: the token must be alternating runs of capital letters and digits, within the limits
        var runs = 0;
        var hasDigits = false;
        var position = 0;
        while (position < token.Length)
        {
            var isLetter = token[position] is >= 'A' and <= 'Z';
            var end = position;
            while (end < token.Length && (isLetter ? token[end] is >= 'A' and <= 'Z' : token[end] is >= '0' and <= '9'))
            {
                end++;
            }

            var length = end - position;
            if (length == 0 || length > (isLetter ? MaxLetterRun : MaxDigitRun))
            {
                return null;
            }

            hasDigits |= !isLetter;
            runs++;
            position = end;
        }

        if (!hasDigits || runs < 2)
        {
            return null;
        }

        // Second pass: one span per run
        var spans = new List<NumericSpan>(runs);
        position = 0;
        while (position < token.Length)
        {
            var isLetter = token[position] is >= 'A' and <= 'Z';
            var end = position;
            while (end < token.Length && (isLetter ? token[end] is >= 'A' and <= 'Z' : token[end] is >= '0' and <= '9'))
            {
                end++;
            }

            var run = token[position..end];
            spans.Add(isLetter
                ? new NumericSpan(tokenStart + position, run.Length, SpelledLetters(run), NormalizationKind.Initialism)
                : new NumericSpan(tokenStart + position, run.Length, ReadDigits(run), NormalizationKind.Number));
            position = end;
        }

        return spans;
    }

    /// <summary>
    ///     Finds the spans of a token that is a name joined to a number by a hyphen.
    /// </summary>
    /// <param name="token">The token's characters.</param>
    /// <param name="tokenStart">The offset of the token in the text; the spans index the text.</param>
    /// <returns>
    ///     The spans in text order: the spelled letters when the name is one to three capitals, then each hyphen with the
    ///     group after it (digits read as a number, an identifier such as <c>T2</c> spelled and read); or
    ///     <see langword="null"/> when the token is not of the form letters, hyphen, one to three digits (one to four after
    ///     spelled capitals), optionally more hyphenated groups after spelled capitals, and optionally a possessive
    ///     <c>'s</c>, which is left as written after the last spoken word.
    /// </returns>
    internal static IReadOnlyList<NumericSpan>? RecognizeHyphenatedNumber(ReadOnlySpan<char> token, int tokenStart)
    {
        var body = WithoutPossessive(token);
        var hyphen = body.IndexOf('-');
        if (hyphen < 1 || body.Length > MaxTokenLength + MaxDigitRun)
        {
            return null;
        }

        // Letters before the first hyphen
        var letters = body[..hyphen];
        var capitals = letters.Length <= MaxLetterRun;
        foreach (var character in letters)
        {
            if (!(character is >= 'a' and <= 'z' or >= 'A' and <= 'Z'))
            {
                return null;
            }

            capitals &= character is >= 'A' and <= 'Z';
        }

        // The spelled letters of an identifier, then each group in place of its hyphen: separate words, no joint
        var spans = new List<NumericSpan>(4);
        if (capitals)
        {
            spans.Add(new NumericSpan(tokenStart, hyphen, SpelledLetters(letters), NormalizationKind.Initialism));
        }

        var position = hyphen;
        while (position < body.Length)
        {
            var next = body[(position + 1)..].IndexOf('-');
            var end = next < 0 ? body.Length : position + 1 + next;
            var group = body[(position + 1)..end];
            if (group.IsEmpty)
            {
                return null;
            }

            if (AllDigits(group))
            {
                // The first group is the number of the name; a later one only after spelled capitals (SCP-093-7)
                if (group.Length > (capitals ? MaxDigitRun : MaxCardinalRun) || (position != hyphen && !capitals))
                {
                    return null;
                }

                spans.Add(new NumericSpan(tokenStart + position, end - position, ReadDigits(group), NormalizationKind.Number));
            }
            else if (capitals && position != hyphen && (IsCapitals(group) || RecognizeIdentifier(group, tokenStart + position + 1) is not null))
            {
                // A later identifier group (SCP-093-T2, SCP-093-EX): its letters and digits, the first span taking the hyphen
                var groupSpans = IsCapitals(group)
                    ? [new NumericSpan(tokenStart + position + 1, group.Length, SpelledLetters(group), NormalizationKind.Initialism)]
                    : RecognizeIdentifier(group, tokenStart + position + 1)!;
                var first = groupSpans[0];
                spans.Add(first with { Start = first.Start - 1, Length = first.Length + 1 });
                for (var i = 1; i < groupSpans.Count; i++)
                {
                    spans.Add(groupSpans[i]);
                }
            }
            else
            {
                return null;
            }

            position = end;
        }

        return spans;
    }

    /// <summary>
    ///     Removes a possessive tail (<c>'s</c> or <c>’s</c>) from the end of a token.
    /// </summary>
    /// <param name="token">The token's characters.</param>
    /// <returns>The token without the tail, or the token itself when it has none.</returns>
    private static ReadOnlySpan<char> WithoutPossessive(ReadOnlySpan<char> token) =>
        token.Length > 2 && token[^1] == 's' && token[^2] is '\'' or '\u2019' ? token[..^2] : token;

    /// <summary>
    ///     Tells whether a group is one to three capital letters.
    /// </summary>
    /// <param name="group">The group's characters.</param>
    /// <returns><see langword="true"/> for <c>T</c>, <c>EX</c>, <c>ABC</c>.</returns>
    private static bool IsCapitals(ReadOnlySpan<char> group)
    {
        if (group.Length is 0 or > MaxLetterRun)
        {
            return false;
        }

        foreach (var character in group)
        {
            if (character is not (>= 'A' and <= 'Z'))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    ///     Tells whether a group is only ASCII digits.
    /// </summary>
    /// <param name="group">The group's characters.</param>
    /// <returns><see langword="true"/> when the group is not empty and has only digits.</returns>
    private static bool AllDigits(ReadOnlySpan<char> group)
    {
        foreach (var character in group)
        {
            if (character is not (>= '0' and <= '9'))
            {
                return false;
            }
        }

        return !group.IsEmpty;
    }
    /// <summary>
    ///     Reads a run of digits: as a cardinal when it is short and has no leading zero, digit by digit otherwise.
    /// </summary>
    /// <param name="digits">The ASCII digits.</param>
    /// <returns>The spoken words.</returns>
    private static string ReadDigits(ReadOnlySpan<char> digits) =>
        digits.Length <= MaxCardinalRun && (digits.Length == 1 || digits[0] != '0')
            ? NumberVerbalizer.Cardinal(digits)
            : NumberVerbalizer.Digits(digits);

    /// <summary>
    ///     Writes capital letters as a spelled sequence separated by single spaces.
    /// </summary>
    /// <param name="letters">The capital letters.</param>
    /// <returns>The letter sequence, for example <c>M P</c>.</returns>
    private static string SpelledLetters(ReadOnlySpan<char> letters)
    {
        var spoken = new char[(letters.Length * 2) - 1];
        for (var i = 0; i < letters.Length; i++)
        {
            spoken[i * 2] = letters[i];
            if (i > 0)
            {
                spoken[(i * 2) - 1] = ' ';
            }
        }

        return new string(spoken);
    }
}
