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
    ///     The spans in text order: the spelled letters when the name is one to three capitals, then the hyphen and the
    ///     digits read as a number; or <see langword="null"/> when the token is not of the form letters, hyphen, one to three
    ///     digits (one to four after spelled capitals).
    /// </returns>
    internal static IReadOnlyList<NumericSpan>? RecognizeHyphenatedNumber(ReadOnlySpan<char> token, int tokenStart)
    {
        var hyphen = token.IndexOf('-');
        if (hyphen < 1 || token.Length > MaxTokenLength + MaxDigitRun)
        {
            return null;
        }

        // Letters before the hyphen, digits after it, nothing else
        var letters = token[..hyphen];
        var capitals = letters.Length <= MaxLetterRun;
        foreach (var character in letters)
        {
            if (!(character is >= 'a' and <= 'z' or >= 'A' and <= 'Z'))
            {
                return null;
            }

            capitals &= character is >= 'A' and <= 'Z';
        }

        var digits = token[(hyphen + 1)..];
        if (digits.Length < 1 || digits.Length > (capitals ? MaxDigitRun : MaxCardinalRun))
        {
            return null;
        }

        foreach (var character in digits)
        {
            if (character is not (>= '0' and <= '9'))
            {
                return null;
            }
        }

        // The spelled letters of an identifier, then the number in place of the hyphen and the digits: two words, no joint
        var number = new NumericSpan(tokenStart + hyphen, digits.Length + 1, ReadDigits(digits), NormalizationKind.Number);
        return capitals
            ? [new NumericSpan(tokenStart, hyphen, SpelledLetters(letters), NormalizationKind.Initialism), number]
            : [number];
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
