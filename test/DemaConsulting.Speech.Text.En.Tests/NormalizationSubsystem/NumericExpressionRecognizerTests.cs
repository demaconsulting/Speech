using System.Text.RegularExpressions;
using DemaConsulting.Speech.Text.En.NormalizationSubsystem;
using DemaConsulting.Speech.Text.En.Tests.Fakes;

namespace DemaConsulting.Speech.Text.En.Tests.NormalizationSubsystem;

/// <summary>
///     Unit tests for <see cref="NumericExpressionRecognizer"/>: what each token of a numeric
///     expression is spoken as, the shapes of the spans, the limits, and the tokens it must
///     leave alone. The tokens are the real tokenizer's, exactly as the normalizer sees them.
/// </summary>
public sealed class NumericExpressionRecognizerTests
{
    /// <summary>The closed shape of the words a numeric span may speak: letters, single spaces and an apostrophe, never a digit.</summary>
    private static readonly Regex SpokenShape = new("^[A-Za-z' ]+$", RegexOptions.CultureInvariant);

    /// <summary>
    ///     Runs the recognizer at one token of a text, tokenized by the real tokenizer.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="index">The token index.</param>
    /// <param name="match">The match when the result is <see langword="true"/>.</param>
    /// <returns>Whether the token starts a numeric expression.</returns>
    private static bool Try(string text, int index, out NumericMatch match) =>
        NumericExpressionRecognizer.TryRecognize(text, NormalizationHarness.Tokenize(text), index, out match);

    /// <summary>
    ///     Runs the recognizer over a text taken as one single token, bypassing the tokenizer, so a shape such as
    ///     <c>1.2.3.</c> is judged whole instead of being split into a number and a period.
    /// </summary>
    /// <param name="token">The text of the token.</param>
    /// <param name="match">The match when the result is <see langword="true"/>.</param>
    /// <returns>Whether the token starts a numeric expression.</returns>
    private static bool TryToken(string token, out NumericMatch match) =>
        NumericExpressionRecognizer.TryRecognize(token, [new NormalizationToken(0, token.Length, true)], 0, out match);

    /// <summary>
    ///     Describes the spans of a match as their spoken words joined by a bar.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="index">The token index.</param>
    /// <returns>The spoken words of every span joined by <c>|</c>; <see langword="null"/> when the token is not numeric.</returns>
    private static string? SpokenAt(string text, int index)
    {
        if (!Try(text, index, out var match))
        {
            return null;
        }

        return string.Join('|', Enumerable.Range(0, match.SpanCount).Select(i => match[i].Spoken));
    }

    /// <summary>
    ///     Describes the spans of a match over hand-built tokens (no tokenizer) as their spoken words joined by a bar.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="tokens">The hand-built tokens.</param>
    /// <param name="index">The token index.</param>
    /// <returns>The spoken words of every span joined by <c>|</c>; <see langword="null"/> when the token is not numeric.</returns>
    private static string? SpokenAtTokens(string text, NormalizationToken[] tokens, int index)
    {
        if (!NumericExpressionRecognizer.TryRecognize(text, tokens, index, out var match))
        {
            return null;
        }

        return string.Join('|', Enumerable.Range(0, match.SpanCount).Select(i => match[i].Spoken));
    }

    /// <summary>
    ///     Tells whether any token of a text (tokenized by the real tokenizer) is read with the given spoken words.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="spoken">The spoken words to look for.</param>
    /// <returns><see langword="true"/> when some token's reading equals <paramref name="spoken"/>.</returns>
    private static bool AnyTokenSpeaks(string text, string spoken) =>
        Enumerable.Range(0, NormalizationHarness.Tokenize(text).Length).Any(i => SpokenAt(text, i) == spoken);

    /// <summary>Proves plain numbers are read by their shape: integers, signs, decimals, thousands and leading points.</summary>
    /// <param name="text">The token.</param>
    /// <param name="spoken">The expected words.</param>
    [Theory]
    [InlineData("0", "zero")]
    [InlineData("42", "forty two")]
    [InlineData("100", "one hundred")]
    [InlineData("12345", "twelve thousand three hundred forty five")]
    [InlineData("1,234", "one thousand two hundred thirty four")]
    [InlineData("1,234,567", "one million two hundred thirty four thousand five hundred sixty seven")]
    [InlineData("-5", "minus five")]
    [InlineData("+3", "plus three")]
    [InlineData("\u22127", "minus seven")]
    [InlineData("3.5", "three point five")]
    [InlineData(".5", "point five")]
    [InlineData("-3.5", "minus three point five")]
    [InlineData("1,234.5", "one thousand two hundred thirty four point five")]
    [InlineData("0.05", "zero point zero five")]
    public void NumericExpressionRecognizer_PlainNumbers_AreReadByShape(string text, string spoken)
    {
        // Act
        var recognized = Try(text, 0, out var match);

        // Assert
        Assert.True(recognized);
        Assert.Equal(1, match.SpanCount);
        Assert.Equal(spoken, match[0].Spoken);
        Assert.Equal(NormalizationKind.Number, match[0].Kind);
        Assert.Equal(0, match[0].Start);
        Assert.Equal(text.Length, match[0].Length);
        Assert.Equal(1, match.TokensConsumed);
        Assert.False(match.Fallback);
    }

    /// <summary>Proves thousands separators must be exactly en-US: any other grouping is left as written and counted, never guessed.</summary>
    [Fact]
    public void NumericExpressionRecognizer_ThousandsSeparators_MustBeExactlyGrouped()
    {
        // Assert: valid groupings are read
        foreach (var valid in new[] { "1,000", "12,345", "123,456", "1,234,567", "999,999,999", "1,000.5" })
        {
            Assert.True(Try(valid, 0, out var good), valid);
            Assert.Equal(1, good.SpanCount);
            Assert.False(good.Fallback);
        }

        // Assert: every other comma or period grouping is left as written and counted as a fallback
        foreach (var invalid in new[] { "1,23", "12,34,567", "1,2,3", "1.234,5", "1,0000", "0,123", "01,234", "1,234,56", "1,234.5,6" })
        {
            Assert.True(Try(invalid, 0, out var bad), invalid);
            Assert.Equal(0, bad.SpanCount);
            Assert.True(bad.Fallback, invalid);
            Assert.Equal(1, bad.TokensConsumed);
        }
    }

    /// <summary>Proves a number that starts with a zero (and has more than one digit) is an identifier read digit by digit, never a fallback.</summary>
    [Fact]
    public void NumericExpressionRecognizer_LeadingZeros_AreReadDigitByDigit()
    {
        // Act / Assert
        Assert.Equal("zero zero seven", SpokenAt("007", 0));
        Assert.Equal("zero zero", SpokenAt("00", 0));
        Assert.Equal("zero one two three", SpokenAt("0123", 0));
        Assert.Equal("zero", SpokenAt("0", 0));
        Assert.Equal("zero zero point five", SpokenAt("00.5", 0));
        Assert.False(Try("007", 0, out var match) && match.Fallback);
        Assert.True(Try("0" + new string('1', 64), 0, out var tooLong));
        Assert.Equal(0, tooLong.SpanCount);
        Assert.True(tooLong.Fallback);
    }

    /// <summary>Proves an ordinal needs the suffix that belongs to its number, in either case, and a leading zero is no ordinal.</summary>
    [Fact]
    public void NumericExpressionRecognizer_Ordinals_RequireMatchingSuffix()
    {
        // Assert: right suffix
        Assert.Equal("first", SpokenAt("1st", 0));
        Assert.Equal("second", SpokenAt("2nd", 0));
        Assert.Equal("third", SpokenAt("3rd", 0));
        Assert.Equal("eleventh", SpokenAt("11th", 0));
        Assert.Equal("twenty second", SpokenAt("22nd", 0));
        Assert.Equal("one thousandth", SpokenAt("1,000th", 0));
        Assert.Equal("first", SpokenAt("1ST", 0));
        Assert.Equal("one hundred twelfth", SpokenAt("112TH", 0));

        // Assert: wrong suffix, leading zero, signs and decimals are identity without a fallback
        foreach (var identity in new[] { "1th", "2st", "3nd", "11st", "12nd", "13rd", "01st", "-1st", "1.5th", "21th" })
        {
            Assert.False(Try(identity, 0, out _), identity);
        }

        // Assert: beyond 15 digits is a fallback
        Assert.True(Try("1234567890123456th", 0, out var tooLong));
        Assert.True(tooLong.Fallback);
        Assert.Equal(0, tooLong.SpanCount);
    }

    /// <summary>Proves decades are recognized for tens and hundreds only, in both apostrophe styles, and nothing else ending in s is a decade.</summary>
    [Fact]
    public void NumericExpressionRecognizer_Decades_AreRecognizedOnlyForTensAndHundreds()
    {
        // Assert: recognized
        Assert.Equal("nineteen nineties", SpokenAt("1990s", 0));
        Assert.Equal("nineties", SpokenAt("90s", 0));
        Assert.Equal("nineteen hundreds", SpokenAt("1900s", 0));
        Assert.Equal("two thousands", SpokenAt("2000s", 0));
        Assert.Equal("twenty tens", SpokenAt("2010s", 0));
        Assert.Equal("nineteen nineties", SpokenAt("1990's", 0));
        Assert.Equal("nineties", SpokenAt("90\u2019s", 0));
        Assert.Equal("tens", SpokenAt("10s", 0));

        // Assert: identity
        foreach (var identity in new[] { "5s", "05s", "00s", "10000s", "1995s", "195s", "2100s", "999s", "1,990s", "-90s", "1.0s" })
        {
            Assert.False(Try(identity, 0, out _), identity);
        }
    }

    /// <summary>Proves a lone apostrophe and the decade token after it are one span over both adjacent tokens, and only when the apostrophe opens a word.</summary>
    [Fact]
    public void NumericExpressionRecognizer_ApostropheDecade_SpansBothAdjacentTokens()
    {
        // Arrange: the real tokenizer splits the apostrophe from the decade
        foreach (var apostrophe in new[] { "'", "\u2019", "\u2018" })
        {
            var text = $"the {apostrophe}90s";

            // Act
            var recognized = Try(text, 1, out var match);

            // Assert: one span, covering both tokens
            Assert.True(recognized, apostrophe);
            Assert.Equal(1, match.SpanCount);
            Assert.Equal(2, match.TokensConsumed);
            Assert.Equal(4, match[0].Start);
            Assert.Equal(4, match[0].Length);
            Assert.Equal("nineties", match[0].Spoken);
        }

        // Assert: after a letter or digit it is not a decade; hand-built tokens, as the tokenizer would keep rock'90s whole
        NormalizationToken[] tokens = [new(0, 4, true), new(4, 1, false), new(5, 3, false)];
        Assert.False(NumericExpressionRecognizer.TryRecognize("rock'90s", tokens, 1, out _));

        // Assert: not a decade token after it, or no token at all
        Assert.False(Try("the '95s", 1, out _));
        Assert.False(Try("the '", 1, out _));
        Assert.False(Try("the ' 90s", 1, out _));
    }

    /// <summary>Proves each documented cue decides between a year and a cardinal for a four-digit number.</summary>
    [Fact]
    public void NumericExpressionRecognizer_Year_CueTable_DecidesYearOrCardinal()
    {
        // Arrange: (text, index of the number, expected words, expected ambiguity)
        (string Text, int Index, string Spoken)[] cases =
        [
            ("in 1999", 1, "nineteen ninety nine"),
            ("since 1999", 1, "nineteen ninety nine"),
            ("until 1999", 1, "nineteen ninety nine"),
            ("till 1999", 1, "nineteen ninety nine"),
            ("by 1999", 1, "nineteen ninety nine"),
            ("during 1999", 1, "nineteen ninety nine"),
            ("circa 1999", 1, "nineteen ninety nine"),
            ("before 1999", 1, "nineteen ninety nine"),
            ("after 1999", 1, "nineteen ninety nine"),
            ("year 1999", 1, "nineteen ninety nine"),
            ("IN 1999", 1, "nineteen ninety nine"),
            ("in 1066", 1, "ten sixty six"),
            ("1066 AD", 0, "ten sixty six"),
            ("1999 BCE", 0, "nineteen ninety nine"),
            ("about 1999", 1, "one thousand nine hundred ninety nine"),
            ("around 1999", 1, "one thousand nine hundred ninety nine"),
            ("over 1999", 1, "one thousand nine hundred ninety nine"),
            ("nearly 1999", 1, "one thousand nine hundred ninety nine"),
            ("only 1999", 1, "one thousand nine hundred ninety nine"),
            ("than 1999", 1, "one thousand nine hundred ninety nine"),
            ("1999 people", 0, "one thousand nine hundred ninety nine"),
            ("1999 dollars", 0, "one thousand nine hundred ninety nine"),
            ("1999 years", 0, "one thousand nine hundred ninety nine"),
            ("1999 kg", 0, "one thousand nine hundred ninety nine|kilograms"),
            ("1999 %", 0, "one thousand nine hundred ninety nine|percent"),
            ("1999 USD", 0, "one thousand nine hundred ninety nine|dollars"),
            ("1999 million", 0, "one thousand nine hundred ninety nine"),
            ("in 1999 people", 1, "nineteen ninety nine"),
        ];

        // Act / Assert: a cue never counts as ambiguous
        foreach (var (text, index, spoken) in cases)
        {
            Assert.True(Try(text, index, out var match), text);
            Assert.Equal(spoken, string.Join('|', Enumerable.Range(0, match.SpanCount).Select(i => match[i].Spoken)));
            Assert.False(match.Ambiguous, text);
        }
    }

    /// <summary>Proves a four-digit number with no cue reads as a year from 1100 and as a cardinal below, and the guess is counted only where the readings differ.</summary>
    [Fact]
    public void NumericExpressionRecognizer_Year_NoCue_UsesDefaultAndIsCountedAmbiguous()
    {
        // Arrange: (number, expected words, counted)
        (string Text, string Spoken, bool Counted)[] cases =
        [
            ("1500", "fifteen hundred", true),
            ("1999", "nineteen ninety nine", true),
            ("1100", "eleven hundred", true),
            ("2010", "twenty ten", true),
            ("2099", "twenty ninety nine", true),
            ("1066", "one thousand sixty six", true),
            ("1001", "one thousand one", true),
            ("1000", "one thousand", false),
            ("2000", "two thousand", false),
            ("2001", "two thousand one", false),
            ("2009", "two thousand nine", false),
            ("2100", "two thousand one hundred", false),
            ("9999", "nine thousand nine hundred ninety nine", false),
            ("0999", "zero nine nine nine", false),
        ];

        // Act / Assert
        foreach (var (text, spoken, counted) in cases)
        {
            Assert.True(Try(text, 0, out var match), text);
            Assert.Equal(spoken, match[0].Spoken);
            Assert.Equal(counted, match.Ambiguous);
        }
    }

    /// <summary>Proves currency symbols read dollars and cents, singular only for exactly one, with the symbol and its number joined in one span.</summary>
    [Fact]
    public void NumericExpressionRecognizer_Currency_SymbolsCentsAndSingular()
    {
        // Arrange: the tokenizer splits "$" from the number, but keeps "-$5" and "\u00A5500" whole
        (string Text, string Spoken)[] cases =
        [
            ("$5", "five dollars"),
            ("$1", "one dollar"),
            ("$0", "zero dollars"),
            ("$5.50", "five dollars and fifty cents"),
            ("$1.01", "one dollar and one cent"),
            ("$0.99", "ninety nine cents"),
            ("$.99", "ninety nine cents"),
            ("$5.00", "five dollars"),
            ("$1.00", "one dollar"),
            ("$5.5", "five point five dollars"),
            ("$1,234.50", "one thousand two hundred thirty four dollars and fifty cents"),
            ("\u20AC5.50", "five euros and fifty cents"),
            ("\u00A31.01", "one pound and one penny"),
            ("\u00A30.50", "fifty pence"),
            ("\u00A5500", "five hundred yen"),
            ("\u00A55.25", "five point two five yen"),
            ("-$5", "minus five dollars"),
            ("$-5", "minus five dollars"),
        ];

        // Act / Assert
        foreach (var (text, spoken) in cases)
        {
            Assert.True(Try(text, 0, out var match), text);
            Assert.Equal(1, match.SpanCount);
            Assert.Equal(spoken, match[0].Spoken);
            Assert.Equal(NormalizationKind.Currency, match[0].Kind);
            Assert.Equal(0, match[0].Start);
            Assert.Equal(text.Length, match[0].Length);
            Assert.True(match.TokensConsumed <= 2);
        }

        // Assert: the symbol token and the number token are one span with no whitespace
        Assert.True(Try("pay $5.50 now", 1, out var pair));
        Assert.Equal(2, pair.TokensConsumed);
        Assert.Equal(4, pair[0].Start);
        Assert.Equal(5, pair[0].Length);
    }

    /// <summary>Proves a magnitude word or letter after a currency amount decomposes per token: the amount, and the word that carries the unit.</summary>
    [Fact]
    public void NumericExpressionRecognizer_Currency_MagnitudeWordAndLetterSuffix_Decompose()
    {
        // Act: a magnitude word is its own token and takes the unit
        Assert.True(Try("$5 million", 0, out var word));

        // Assert
        Assert.Equal(2, word.SpanCount);
        Assert.Equal("five", word[0].Spoken);
        Assert.Equal(0, word[0].Start);
        Assert.Equal(2, word[0].Length);
        Assert.Equal("million dollars", word[1].Spoken);
        Assert.Equal(3, word[1].Start);
        Assert.Equal(7, word[1].Length);
        Assert.Equal(3, word.TokensConsumed);

        // Assert: a letter glued to the amount is one span
        Assert.Equal("five million dollars", SpokenAt("$5M", 0));
        Assert.Equal("five point five million dollars", SpokenAt("$5.5M", 0));
        Assert.Equal("ten thousand dollars", SpokenAt("$10k", 0));
        Assert.Equal("three billion dollars", SpokenAt("$3B", 0));
        Assert.Equal("one point five million dollars", SpokenAt("$1.5m", 0));
        Assert.Equal("five million euros", SpokenAt("\u20AC5M", 0));

        // Assert: other letters, and a bare number with a letter, are not currency
        Assert.False(Try("$5X", 0, out _));
        Assert.False(Try("10k", 0, out _));
        Assert.Equal("two point five|billion dollars", SpokenAt("$2.5 billion", 0));
        Assert.Equal("one|trillion dollars", SpokenAt("$1 trillion", 0));
    }

    /// <summary>Proves a code or symbol after a plain number is its own span, per token, and a magnitude word between them is consumed without a span.</summary>
    [Fact]
    public void NumericExpressionRecognizer_Currency_SuffixCodeAndSymbol_ArePerToken()
    {
        // Act
        Assert.True(Try("5 million USD", 0, out var code));

        // Assert: "5" and "USD" are spans, "million" is consumed in between
        Assert.Equal(2, code.SpanCount);
        Assert.Equal("five", code[0].Spoken);
        Assert.Equal("dollars", code[1].Spoken);
        Assert.Equal(10, code[1].Start);
        Assert.Equal(3, code.TokensConsumed);

        // Assert: singular, plural, pound, yen, euro symbol; a code never makes cents
        Assert.Equal("one|dollar", SpokenAt("1 USD", 0));
        Assert.Equal("five|dollars", SpokenAt("5 USD", 0));
        Assert.Equal("five point five zero|euros", SpokenAt("5.50 EUR", 0));
        Assert.Equal("three|pounds", SpokenAt("3 GBP", 0));
        Assert.Equal("five hundred|yen", SpokenAt("500 JPY", 0));
        Assert.Equal("five|euros", SpokenAt("5 \u20AC", 0));
        Assert.Equal("five dollars", SpokenAt("5$", 0));
        Assert.Equal("five dollars", SpokenAt("5USD", 0));
        Assert.Equal("five euros", SpokenAt("5\u20AC", 0));
        Assert.Equal("five", SpokenAt("5 million", 0));
    }

    /// <summary>Proves a currency code before a number, and a code glued to a symbol, is never recognized.</summary>
    [Fact]
    public void NumericExpressionRecognizer_Currency_PrefixCode_IsLeftAlone()
    {
        // Assert: "USD 5": the code token is not numeric (the number is read on its own), "US$5" is a non-numeric token
        Assert.False(Try("USD 5", 0, out _));
        Assert.Equal("five", SpokenAt("USD 5", 1));
        Assert.False(Try("US$5", 0, out _));
        Assert.False(Try("EUR5", 0, out _));
        Assert.False(Try("$", 0, out _));
        Assert.False(Try("$ 5", 0, out _));
        Assert.False(Try("$x", 0, out _));
        Assert.False(Try("$1,23", 0, out _));
    }

    /// <summary>Proves a percent sign is read only after a number, adjacent or one space away, and a number before it is a quantity, not a year.</summary>
    [Fact]
    public void NumericExpressionRecognizer_Percent_FollowsANumberOnly()
    {
        // Assert: per token, the number then the sign
        Assert.Equal("fifty|percent", SpokenAt("50%", 0));
        Assert.Equal("fifty|percent", SpokenAt("50 %", 0));
        Assert.Equal("three point five|percent", SpokenAt("3.5%", 0));
        Assert.Equal("minus five|percent", SpokenAt("-5%", 0));
        Assert.Equal("one thousand nine hundred ninety nine|percent", SpokenAt("1999%", 0));

        // Assert: a sign on its own, after two spaces, or after a word is left alone
        Assert.False(Try("%", 0, out _));
        Assert.False(Try("50% off", 1, out _));
        Assert.Equal("fifty", SpokenAt("50  %", 0));
        Assert.False(Try("up %", 1, out _));
    }

    /// <summary>Proves the unit table: singular for exactly one, plural otherwise, and the period flag only for the units that may carry one.</summary>
    [Fact]
    public void NumericExpressionRecognizer_Units_TableSingularPluralAndPeriod()
    {
        // Arrange: (text, spoken, period unit)
        (string Text, string Spoken, bool PeriodUnit)[] cases =
        [
            ("5 kg", "five|kilograms", false),
            ("1 kg", "one|kilogram", false),
            ("1.0 kg", "one point zero|kilograms", false),
            ("5kg", "five kilograms", false),
            ("60 mph", "sixty|miles per hour", false),
            ("1 mph", "one|mile per hour", false),
            ("5 km/h", "five|kilometers per hour", false),
            ("10km/h", "ten kilometers per hour", false),
            ("72\u00B0F", "seventy two degrees Fahrenheit", false),
            ("1\u00B0C", "one degree Celsius", false),
            ("5\u00B0", "five degrees", false),
            ("72 \u00B0F", "seventy two|degrees Fahrenheit", false),
            ("440 Hz", "four hundred forty|hertz", false),
            ("5 MB", "five|megabytes", false),
            ("5 ms", "five|milliseconds", false),
            ("6 ft", "six|feet", true),
            ("1 ft", "one|foot", true),
            ("6 Ft", "six|feet", true),
            ("5ft", "five feet", true),
            ("5 lbs", "five|pounds", true),
            ("5 oz", "five|ounces", true),
            ("5 min", "five|minutes", true),
            ("5 hr", "five|hours", true),
            ("5 yd", "five|yards", true),
            ("5 mi", "five|miles", true),
        ];

        // Act / Assert
        foreach (var (text, spoken, periodUnit) in cases)
        {
            Assert.True(Try(text, 0, out var match), text);
            Assert.Equal(spoken, string.Join('|', Enumerable.Range(0, match.SpanCount).Select(i => match[i].Spoken)));
            Assert.Equal(periodUnit, match.PeriodUnit);
            Assert.Equal(NormalizationKind.Unit, match[match.SpanCount - 1].Kind);
        }

        // Assert: a period token or a period inside the token belongs to the unit token, never to the span
        Assert.True(Try("6 ft. tall", 0, out var dotted));
        Assert.Equal(2, dotted.TokensConsumed);
        Assert.Equal(2, dotted[1].Start);
        Assert.Equal(3, dotted[1].Length);
        Assert.True(Try("180 lbs.", 0, out var separate));
        Assert.Equal(2, separate.TokensConsumed);
        Assert.Equal(4, separate[1].Start);
        Assert.Equal(3, separate[1].Length);
    }

    /// <summary>Proves ambiguous, word-like and single-letter units, wrong case, and a unit with no number are left alone.</summary>
    [Fact]
    public void NumericExpressionRecognizer_Units_ExcludedAmbiguousForms_AreLeftAlone()
    {
        // Assert: the number is read, nothing follows it
        foreach (var excluded in new[] { "5 in", "5 m", "5 g", "5 l", "5 s", "5 Kg", "5 KG", "5 MHZ", "5 Mb", "5 cm2", "5 inch" })
        {
            Assert.True(Try(excluded, 0, out var match), excluded);
            Assert.Equal(1, match.SpanCount);
            Assert.Equal(1, match.TokensConsumed);
            Assert.False(match.PeriodUnit);
        }

        // Assert: glued, a single letter is not a unit at all
        foreach (var glued in new[] { "5m", "5g", "5in", "5Kg", "5KG" })
        {
            Assert.False(Try(glued, 0, out _), glued);
        }

        // Assert: a unit on its own, or after a word, is not numeric
        Assert.False(Try("kg", 0, out _));
        Assert.False(Try("five kg", 1, out _));
    }

    /// <summary>Proves valid times are read in both clocks, and invalid shapes are identity without a fallback.</summary>
    [Fact]
    public void NumericExpressionRecognizer_Times_ValidAndInvalidShapes()
    {
        // Assert: valid
        (string Text, string Spoken)[] valid =
        [
            ("5:30", "five thirty"), ("12:30", "twelve thirty"), ("5:05", "five oh five"), ("5:00", "five o'clock"),
            ("17:45", "seventeen forty five"), ("17:00", "seventeen hundred"), ("07:05", "oh seven oh five"),
            ("07:00", "oh seven hundred"), ("0:30", "zero thirty"), ("00:00", "zero hundred"), ("23:59", "twenty three fifty nine"),
        ];
        foreach (var (text, spoken) in valid)
        {
            Assert.True(Try(text, 0, out var match), text);
            Assert.Equal(spoken, match[0].Spoken);
            Assert.Equal(NormalizationKind.DateTime, match[0].Kind);
            Assert.False(match.Fallback);
        }

        // Assert: invalid shapes are identity, not a fallback
        foreach (var invalid in new[] { "25:99", "24:00", "5:60", "3:2", "5:30:15", "1:1:1", "123:45", "5:300", "-5:30", "$5:30", "5:30x" })
        {
            Assert.False(Try(invalid, 0, out _), invalid);
        }
    }

    /// <summary>Proves a meridiem glued to the time, as a separate lowercase token, or dotted (the existing initialism rule) is read per token, and invalid hours with a meridiem are not.</summary>
    [Fact]
    public void NumericExpressionRecognizer_Meridiem_GluedSeparateAndDotted()
    {
        // Assert: glued is two adjacent spans inside the one token: the hour or time as words and the meridiem letters
        Assert.Equal("five thirty|P M", SpokenAt("5:30pm", 0));
        Assert.Equal("five thirty|P M", SpokenAt("5:30PM", 0));
        Assert.Equal("five|P M", SpokenAt("5pm", 0));
        Assert.Equal("five|P M", SpokenAt("5PM", 0));
        Assert.Equal("twelve|A M", SpokenAt("12:00am", 0));
        Assert.Equal("five|P M", SpokenAt("5:00pm", 0));

        // Assert: the offsets of the two halves of 5pm and 5:30pm abut exactly, cover the whole token and never overlap
        Assert.True(Try("5pm", 0, out var hourMatch));
        Assert.Equal((0, 1, 1, 2), (hourMatch[0].Start, hourMatch[0].Length, hourMatch[1].Start, hourMatch[1].Length));
        Assert.True(Try("5:30pm", 0, out var timeMatch));
        Assert.Equal((0, 4, 4, 2), (timeMatch[0].Start, timeMatch[0].Length, timeMatch[1].Start, timeMatch[1].Length));
        Assert.Equal(1, timeMatch.TokensConsumed);

        // Assert: separate lowercase is a second span; uppercase and dotted forms are left to the initialism rule
        Assert.Equal("five|P M", SpokenAt("5 pm", 0));
        Assert.Equal("five|A M", SpokenAt("5 am", 0));
        Assert.Equal("five thirty|P M", SpokenAt("5:30 pm", 0));
        Assert.Equal("five", SpokenAt("5 PM", 0));
        Assert.Equal("five", SpokenAt("5 p.m.", 0));
        Assert.Equal("five thirty", SpokenAt("5:30 p.m.", 0));

        // Assert: hour 0 and above 12 take no meridiem
        foreach (var invalid in new[] { "0pm", "13pm", "13:30pm", "0:30pm", "5:30Pm" })
        {
            Assert.False(Try(invalid, 0, out _), invalid);
        }

        Assert.Equal("thirteen", SpokenAt("13 pm", 0));
    }

    /// <summary>Proves numeric dates: the three orders, ambiguity counting, validation, and the dates that are not dates.</summary>
    [Fact]
    public void NumericExpressionRecognizer_NumericDates_OrderAndValidation()
    {
        // Arrange: (text, spoken, ambiguous)
        (string Text, string Spoken, bool Ambiguous)[] dates =
        [
            ("12/25/2024", "December twenty fifth twenty twenty four", false),
            ("25/12/2024", "December twenty fifth twenty twenty four", false),
            ("2024-12-25", "December twenty fifth twenty twenty four", false),
            ("2024/12/25", "December twenty fifth twenty twenty four", false),
            ("12-25-2024", "December twenty fifth twenty twenty four", false),
            ("3/4/2024", "March fourth twenty twenty four", true),
            ("1/2/24", "January second twenty four", true),
            ("03-04-2024", "March fourth twenty twenty four", true),
            ("12/12/2024", "December twelfth twenty twenty four", false),
            ("1/1/05", "January first oh five", false),
            ("2/30/2024", "February thirtieth twenty twenty four", false),
            ("31/12/1999", "December thirty first nineteen ninety nine", false),
        ];

        // Act / Assert
        foreach (var (text, spoken, ambiguous) in dates)
        {
            Assert.True(Try(text, 0, out var match), text);
            Assert.Equal(spoken, match[0].Spoken);
            Assert.Equal(ambiguous, match.Ambiguous);
            Assert.Equal(NormalizationKind.DateTime, match[0].Kind);
        }

        // Assert: not dates; slash forms are identity, hyphen forms fall back to the identifier reading
        foreach (var notDate in new[] { "13/13/2024", "0/5/2024", "2/32/2024", "1/1/00", "5/5/5", "1/1/0999", "2024/13/01", "12/25/24x", "1/2/3/4", "-12/25/2024" })
        {
            Assert.False(Try(notDate, 0, out _), notDate);
        }

        Assert.Equal("two zero two four one three zero one", SpokenAt("2024-13-01", 0));
    }

    /// <summary>Proves a date written with a month name is read one token at a time: the day as an ordinal, the year by the year policy, the month and comma untouched.</summary>
    [Fact]
    public void NumericExpressionRecognizer_MonthNameDates_DecomposePerToken()
    {
        // Assert: "January 5, 2024" is [January][5][,][2024]: the month and the comma are not numeric
        Assert.False(Try("January 5, 2024", 0, out _));
        Assert.Equal("fifth", SpokenAt("January 5, 2024", 1));
        Assert.False(Try("January 5, 2024", 2, out _));
        Assert.True(Try("January 5, 2024", 3, out var year));
        Assert.Equal("twenty twenty four", year[0].Spoken);
        Assert.False(year.Ambiguous);
        Assert.Equal(NormalizationKind.DateTime, year[0].Kind);

        // Assert: the other orders and forms
        Assert.Equal("fifth", SpokenAt("5th January 2024", 0));
        Assert.Equal("fifth", SpokenAt("5 January 2024", 0));
        Assert.Equal("twenty twenty four", SpokenAt("5 January 2024", 2));
        Assert.Equal("twenty fifth", SpokenAt("December 25", 1));
        Assert.Equal("twenty twenty four", SpokenAt("May 2024", 1));
        Assert.Equal("twenty twenty four", SpokenAt("in May 2024", 2));
        Assert.Equal("twenty fifth", SpokenAt("Dec 25 2024", 1));
        Assert.Equal("twenty twenty four", SpokenAt("Dec 25 2024", 2));
        Assert.Equal("fifth", SpokenAt("May 5 people", 1));
        Assert.Equal("forty five", SpokenAt("May 45", 1));
        Assert.Equal("fifth", SpokenAt("Jan. 5", 2));
        Assert.Equal("twenty fifth", SpokenAt("Dec. 25, 2024", 2));
        Assert.Equal("twenty twenty four", SpokenAt("Dec. 25, 2024", 4));
        Assert.Equal("two thousand one", SpokenAt("Sept. 11, 2001", 4));
    }

    /// <summary>Proves an ordinal day next to a month name is a date, as a bare day is: <c>January 5th</c>, <c>5th January</c> and the abbreviation forms are <c>DateTime</c>.</summary>
    [Fact]
    public void NumericExpressionRecognizer_OrdinalDay_NextToMonth_IsDateTime()
    {
        // Arrange: the ordinal after a month, before a month, and after an abbreviation with its period
        (string Text, int Index)[] cases = [("January 5th", 1), ("5th January", 0), ("Jan. 5th", 2), ("Dec 25th 2024", 1)];

        // Act / Assert: each reads as the ordinal and is a date part, the same as the bare-day form
        foreach (var (text, index) in cases)
        {
            Assert.True(Try(text, index, out var match), text);
            Assert.Equal(NormalizationKind.DateTime, match[0].Kind);
        }

        Assert.Equal("fifth", SpokenAt("January 5th", 1));
        Assert.Equal("fifth", SpokenAt("5th January", 0));
        Assert.Equal("twenty fifth", SpokenAt("Dec 25th 2024", 1));
        Assert.True(Try("January 5", 1, out var bare));
        Assert.Equal(NormalizationKind.DateTime, bare[0].Kind);
    }

    /// <summary>Proves an ordinal that is not a day next to a month stays a plain number: no month, a value above 31, a grouped number or a lowercase month.</summary>
    [Fact]
    public void NumericExpressionRecognizer_OrdinalDay_WithoutDateContext_IsNumber()
    {
        // Arrange: ordinals with no month, a month two tokens away, a value that is not a day, a grouped number, and a lowercase month
        string[] texts = ["the 5th floor", "5th of January", "January 45th", "January 1,000th", "may 5th"];
        int[] indexes = [1, 0, 1, 1, 1];

        // Act / Assert
        for (var i = 0; i < texts.Length; i++)
        {
            Assert.True(Try(texts[i], indexes[i], out var match), texts[i]);
            Assert.Equal(NormalizationKind.Number, match[0].Kind);
        }
    }

    /// <summary>Proves a sentence boundary the normalizer keeps between a month and an ordinal ends the date: the ordinal is a plain number, while an abbreviation period keeps it.</summary>
    [Fact]
    public void NumericExpressionRecognizer_OrdinalDay_AfterSentenceBoundary_IsNumber()
    {
        // Act
        var afterMay = Try("We finished in May. 5th people stayed.", 5, out var retained);
        var afterAbbreviation = Try("Jan. 5th", 2, out var kept);

        // Assert: the period after May ends the sentence; the period of Jan. does not
        Assert.True(afterMay);
        Assert.Equal(NormalizationKind.Number, retained[0].Kind);
        Assert.True(afterAbbreviation);
        Assert.Equal(NormalizationKind.DateTime, kept[0].Kind);
    }

    /// <summary>Proves only the Title Case month name or abbreviation is a month: <c>may</c>, <c>MAY</c> and <c>jan</c> are not.</summary>
    [Fact]
    public void NumericExpressionRecognizer_MonthContext_RequiresTitleCase()
    {
        // Assert: vocabulary
        Assert.True(NumericExpressionRecognizer.IsMonthToken("May"));
        Assert.True(NumericExpressionRecognizer.IsMonthToken("January"));
        Assert.True(NumericExpressionRecognizer.IsMonthToken("Sept"));
        Assert.True(NumericExpressionRecognizer.IsMonthToken("Sept."));
        Assert.True(NumericExpressionRecognizer.IsMonthToken("Dec."));
        foreach (var notMonth in new[] { "may", "MAY", "jan", "JANUARY", "Jane", "Mon", "Sun.", "Marching", "Mayo", ".", string.Empty, "May.." })
        {
            Assert.False(NumericExpressionRecognizer.IsMonthToken(notMonth), notMonth);
        }

        // Assert: a lowercase month is no date context, so the number is a plain cardinal
        Assert.Equal("five", SpokenAt("may 5", 1));
        Assert.Equal("five", SpokenAt("MAY 5", 1));
        Assert.Equal("five", SpokenAt("jan 5", 1));
        Assert.Equal("fifth", SpokenAt("Jan 5", 1));
        Assert.Equal(12, NumericExpressionRecognizer.MonthNames.Count);
    }

    /// <summary>Proves a month supplies date context only while no sentence boundary the normalizer keeps lies between it and the number: the period after a full month name or <c>May</c> ends the sentence, a month abbreviation's period does not.</summary>
    [Fact]
    public void NumericExpressionRecognizer_MonthContext_StopsAtSentenceBoundaries()
    {
        // Assert: a period after May or a full month name is a sentence end, so the number is no day and no date year
        Assert.Equal("five", SpokenAt("May. 5 people", 2));
        Assert.Equal("five", SpokenAt("January. 5 people", 2));
        Assert.Equal("five", SpokenAt("We finished in May. 5 people stayed.", 5));
        Assert.True(Try("January. 1999 people", 2, out var cardinal));
        Assert.Equal("one thousand nine hundred ninety nine", cardinal[0].Spoken);
        Assert.Equal(NormalizationKind.Number, cardinal[0].Kind);
        Assert.Equal("fifth", SpokenAt("5 May. 5 people", 0));
        Assert.Equal("five", SpokenAt("5 May. 5 people", 3));
        Assert.True(Try("May. 5, 2024", 4, out var notDateYear));
        Assert.Equal("twenty twenty four", notDateYear[0].Spoken);
        Assert.Equal(NormalizationKind.Number, notDateYear[0].Kind);
        Assert.True(notDateYear.Ambiguous);

        // Assert: a month abbreviation's period keeps the date context (the lexicon consumes it)
        Assert.Equal("fifth", SpokenAt("Jan. 5", 2));
        Assert.Equal("nineteen ninety nine", SpokenAt("Sept. 1999 people", 2));
        Assert.Equal("twenty fifth", SpokenAt("Dec. 25 people", 2));
        Assert.True(Try("Mar. 5, 2024", 4, out var dateYear));
        Assert.Equal("twenty twenty four", dateYear[0].Spoken);
        Assert.Equal(NormalizationKind.DateTime, dateYear[0].Kind);
        Assert.False(dateYear.Ambiguous);

        // Assert: a blank line is a boundary without a period, and a boundary between day and year ends the date
        Assert.Equal("five", SpokenAt("Month: May\n\n5 people", 3));
        Assert.Equal("one thousand nine hundred ninety nine", SpokenAt("Month: May\n\n1999 people", 3));
        Assert.Equal("twenty fifth", SpokenAt("Dec 25\n\n2024 people", 1));
        Assert.Equal("two thousand twenty four", SpokenAt("Dec 25\n\n2024 people", 2));
        Assert.Equal("fifth", SpokenAt("Jan. 5\n\n2024 people", 2));
        Assert.Equal("two thousand twenty four", SpokenAt("Jan. 5\n\n2024 people", 3));

        // Assert: hand-built tokens decide the same way, with no tokenizer involved
        Assert.Equal("five", SpokenAtTokens("May. 5", [new(0, 3, true), new(3, 1, false), new(5, 1, false)], 2));
        Assert.Equal("fifth", SpokenAtTokens("Jan. 5", [new(0, 3, true), new(3, 1, false), new(5, 1, false)], 2));
        Assert.Equal("five", SpokenAtTokens("January. 5", [new(0, 7, true), new(7, 1, false), new(9, 1, false)], 2));
        Assert.Equal("five", SpokenAtTokens("May 5", [new(0, 3, true), new(4, 1, true)], 1));
        Assert.Equal("fifth", SpokenAtTokens("May 5", [new(0, 3, true), new(4, 1, false)], 1));
        Assert.Equal("fifth", SpokenAtTokens("Jan. 5", [new(0, 4, true), new(5, 1, true)], 1));
        Assert.Equal("five", SpokenAtTokens("May. 5", [new(0, 4, true), new(5, 1, false)], 1));

        // Assert: Jan. 5, 2024 is a date unless a boundary is kept inside it
        NormalizationToken[] whole = [new(0, 3, true), new(3, 1, false), new(5, 1, true), new(6, 1, false), new(8, 4, false)];
        Assert.True(NumericExpressionRecognizer.TryRecognize("Jan. 5, 2024", whole, 4, out var wholeYear));
        Assert.Equal(NormalizationKind.DateTime, wholeYear[0].Kind);
        Assert.Equal("twenty twenty four", wholeYear[0].Spoken);
        NormalizationToken[] split = [new(0, 3, true), new(3, 1, false), new(5, 1, true), new(6, 1, false), new(8, 4, true)];
        Assert.True(NumericExpressionRecognizer.TryRecognize("Jan. 5, 2024", split, 4, out var splitYear));
        Assert.Equal(NormalizationKind.Number, splitYear[0].Kind);
        Assert.True(splitYear.Ambiguous);
    }

    /// <summary>Proves an en dash is read as <c>to</c> only when both numbers around it are themselves read as numbers; otherwise the dash stays and each token follows its own rule.</summary>
    [Fact]
    public void NumericExpressionRecognizer_EnDashRange_RequiresBothEndpointsRead()
    {
        // Assert: the dash (token 1) is read when both endpoints are numbers by themselves
        var fiftyDigits = string.Concat(Enumerable.Repeat("1234567890", 5));
        foreach (var read in new[]
        {
            "5\u201310", "1990\u20132000", "1,234\u20135,678", "5.5\u20136.5", "0.5\u20131", "5:30\u20136:30", "5\u201310kg", "5\u201310pm",
            "5\u201310%", "5\u20131/2", "5\u20131,000", "01\u201302", fiftyDigits + "\u20135",
        })
        {
            Assert.True(Try(read, 1, out var dash), read);
            Assert.Equal("to", dash[0].Spoken);
            Assert.Equal(1, dash.SpanCount);
            Assert.Equal(1, dash.TokensConsumed);
            Assert.Equal(NormalizationKind.Number, dash[0].Kind);
        }

        // Assert: the dash is left as written when an endpoint is not read as a number
        foreach (var notRead in new[] { "1\u20132D", "1,23\u20135", "5\u201325:99", "5\u201310x", "1,23\u20135,67", "5\u201310D", "5\u2013.5", "D5\u201310", "a\u2013b", "5\u2013", "\u20135", "5 \u201310", "5\u2013 10" })
        {
            Assert.False(AnyTokenSpeaks(notRead, "to"), notRead);
        }

        // Assert: the endpoints keep their own readings
        Assert.Equal("one", SpokenAt("1\u20132D", 0));
        Assert.False(Try("1\u20132D", 2, out _));
        Assert.True(Try("1,23\u20135", 0, out var counted));
        Assert.Equal(0, counted.SpanCount);
        Assert.True(counted.Fallback);
        Assert.Equal("five", SpokenAt("1,23\u20135", 2));
        Assert.False(Try("5\u201325:99", 2, out _));
        Assert.False(Try("5\u201310x", 2, out _));

        // Assert: a chain reads every dash between two read numbers and none next to an unread one
        Assert.Equal("to", SpokenAt("1\u20132\u20133", 1));
        Assert.Equal("to", SpokenAt("1\u20132\u20133", 3));
        Assert.False(Try("1\u20132D\u20133", 1, out _));
        Assert.False(Try("1\u20132D\u20133", 3, out _));

        // Assert: hand-built tokens at the edges and oversized neighbors are false, a malformed neighbor still throws
        const string text = "5\u201310";
        Assert.False(NumericExpressionRecognizer.TryRecognize(text, [new(1, 1, true), new(2, 2, false)], 0, out _));
        Assert.False(NumericExpressionRecognizer.TryRecognize(text, [new(0, 1, true), new(1, 1, false)], 1, out _));
        Assert.Throws<ArgumentException>(() => NumericExpressionRecognizer.TryRecognize(text, [new(0, 1, true), new(1, 1, false), new(int.MaxValue, 2, false)], 1, out _));
        var wide = new string('1', 129) + "\u20135";
        Assert.False(NumericExpressionRecognizer.TryRecognize(wide, [new(0, 129, true), new(129, 1, false), new(130, 1, false)], 1, out _));
    }

    /// <summary>Proves the vocabulary properties expose read-only, cached views: no cast reaches the private tables.</summary>
    [Fact]
    public void NumericExpressionRecognizer_VocabularyViews_AreReadOnly()
    {
        // Arrange: (view, same property read again, expected count)
        (IReadOnlyList<string> View, IReadOnlyList<string> Again, int Count)[] views =
        [
            (NumericExpressionRecognizer.MonthNames, NumericExpressionRecognizer.MonthNames, 12),
            (NumericExpressionRecognizer.MonthAbbreviations, NumericExpressionRecognizer.MonthAbbreviations, 12),
            (NumericExpressionRecognizer.WeekdayAbbreviations, NumericExpressionRecognizer.WeekdayAbbreviations, 10),
        ];

        // Act / Assert
        foreach (var (view, again, count) in views)
        {
            var before = view.ToArray();
            Assert.Equal(count, view.Count);
            Assert.IsNotType<string[]>(view);
            var list = Assert.IsAssignableFrom<IList<string>>(view);
            Assert.Throws<NotSupportedException>(() => list[0] = "x");
            Assert.Throws<NotSupportedException>(() => list.Add("x"));
            Assert.Equal(before, view);
            Assert.Same(view, again);
        }

        Assert.Equal("May", NumericExpressionRecognizer.MonthNames[4]);
        Assert.DoesNotContain("May", NumericExpressionRecognizer.MonthAbbreviations);
    }

    /// <summary>Proves hyphen groups: ranges, years in order, local and long phone numbers, identifiers, and the leading-zero and size rules.</summary>
    [Fact]
    public void NumericExpressionRecognizer_HyphenGroups_RangePhoneIdAndDate()
    {
        // Arrange: (text, spoken)
        (string Text, string Spoken)[] cases =
        [
            ("5-10", "five to ten"),
            ("100-200", "one hundred to two hundred"),
            ("5-3", "five to three"),
            ("1990-2000", "nineteen ninety to two thousand"),
            ("2020-2024", "twenty twenty to twenty twenty four"),
            ("2024-2020", "two thousand twenty four to two thousand twenty"),
            ("555-1234", "five five five one two three four"),
            ("555-123-4567", "five five five one two three four five six seven"),
            ("1-800-555-1234", "one eight zero zero five five five one two three four"),
            ("123-45-6789", "one two three four five six seven eight nine"),
            ("01-02", "zero one zero two"),
            ("1234567890-5", "one two three four five six seven eight nine zero five"),
            ("2024-12-25", "December twenty fifth twenty twenty four"),
        ];

        // Act / Assert
        foreach (var (text, spoken) in cases)
        {
            Assert.True(Try(text, 0, out var match), text);
            Assert.Equal(spoken, match[0].Spoken);
            Assert.Equal(1, match.SpanCount);
            Assert.False(match.Fallback);
        }

        // Assert: seven groups (63 digits) are read digit by digit; eight groups (72 digits) are a fallback; signs, symbols and letters make the token identity
        Assert.True(Try(string.Join('-', Enumerable.Repeat(new string('1', 9), 7)), 0, out var spelled));
        Assert.Equal(1, spelled.SpanCount);
        Assert.False(spelled.Fallback);
        Assert.True(Try(string.Join('-', Enumerable.Repeat(new string('1', 9), 8)), 0, out var tooMany));
        Assert.True(tooMany.Fallback);
        Assert.Equal(0, tooMany.SpanCount);
        foreach (var identity in new[] { "-5-10", "5-10x", "$5-10", "5-D", "5--10", "5-", "-" })
        {
            Assert.False(TryToken(identity, out _), identity);
        }

        // Assert: the area code in parentheses is per token
        Assert.Equal("five five five", SpokenAt("(555) 123-4567", 1));
        Assert.Equal("one two three four five six seven", SpokenAt("(555) 123-4567", 3));
        Assert.Equal("five hundred fifty five", SpokenAt("(555) only", 1));
    }

    /// <summary>Proves dotted groups: versions and addresses, the cardinal and digit-by-digit group rules, and the eight-group cap.</summary>
    [Fact]
    public void NumericExpressionRecognizer_DottedGroups_VersionAndAddress()
    {
        // Assert
        Assert.Equal("three point two point one", SpokenAt("3.2.1", 0));
        Assert.Equal("two point zero point one", SpokenAt("2.0.1", 0));
        Assert.Equal("one hundred ninety two point one hundred sixty eight point zero point one", SpokenAt("192.168.0.1", 0));
        Assert.Equal("one point zero two point three", SpokenAt("1.02.3", 0));
        Assert.Equal("twelve point twenty five point two zero two four", SpokenAt("12.25.2024", 0));
        Assert.Equal("one point two point three point four point five point six point seven point eight", SpokenAt("1.2.3.4.5.6.7.8", 0));

        // Assert: nine groups is a fallback; a leading point, a sign or a suffix is identity
        Assert.True(Try("1.2.3.4.5.6.7.8.9", 0, out var tooMany));
        Assert.True(tooMany.Fallback);
        Assert.Equal(0, tooMany.SpanCount);
        foreach (var identity in new[] { ".1.2.3", "-1.2.3", "1.2.3x", "1.2.3.", "1.2..3" })
        {
            Assert.False(TryToken(identity, out _), identity);
        }
    }

    /// <summary>Proves slash forms: simple fractions with their ambiguity rule, dates, and the forms that are neither.</summary>
    [Fact]
    public void NumericExpressionRecognizer_SlashForms_FractionsDatesAndIdentity()
    {
        // Arrange: (text, spoken, ambiguous)
        (string Text, string Spoken, bool Ambiguous)[] fractions =
        [
            ("1/2", "one half", true), ("2/3", "two thirds", true), ("3/4", "three quarters", true), ("7/16", "seven sixteenths", true),
            ("9/10", "nine tenths", true), ("1/100", "one hundredth", false), ("1/32", "one thirty second", false),
            ("15/16", "fifteen sixteenths", false), ("63/64", "sixty three sixty fourths", false), ("12/16", "twelve sixteenths", true),
        ];

        // Act / Assert
        foreach (var (text, spoken, ambiguous) in fractions)
        {
            Assert.True(Try(text, 0, out var match), text);
            Assert.Equal(spoken, match[0].Spoken);
            Assert.Equal(ambiguous, match.Ambiguous);
            Assert.Equal(NormalizationKind.Number, match[0].Kind);
        }

        // Assert: identity (no fallback): ratios, rates, equal parts, leading zeros, denominators outside the set, masks
        foreach (var identity in new[] { "24/7", "9/11", "5/5", "1/1", "0/5", "01/2", "1/02", "1/1000", "10.0.0.1/24", "1/2/", "/2", "1//2", "-1/2", "1/2x", "5/3" })
        {
            Assert.False(Try(identity, 0, out _), identity);
        }
    }

    /// <summary>Proves vulgar fractions are read alone, glued to a whole number, and after a whole number as a mixed number, and the written slash form after a whole number too.</summary>
    [Fact]
    public void NumericExpressionRecognizer_VulgarFractionsAndMixedNumbers()
    {
        // Assert: alone
        (string Text, string Spoken)[] vulgar =
        [
            ("\u00BD", "one half"), ("\u2153", "one third"), ("\u2154", "two thirds"), ("\u00BC", "one quarter"),
            ("\u00BE", "three quarters"), ("\u215B", "one eighth"), ("\u215C", "three eighths"), ("\u215D", "five eighths"),
            ("\u215E", "seven eighths"),
        ];
        foreach (var (text, spoken) in vulgar)
        {
            Assert.True(Try(text, 0, out var match), text);
            Assert.Equal(spoken, match[0].Spoken);
            Assert.False(match.Ambiguous);
        }

        // Assert: glued, and as a mixed number per token
        Assert.Equal("five and one half", SpokenAt("5\u00BD", 0));
        Assert.Equal("three|and one half", SpokenAt("3 \u00BD", 0));
        Assert.Equal("three|and one half", SpokenAt("3 1/2", 0));
        Assert.Equal("five|and three quarters", SpokenAt("5 3/4", 0));
        Assert.True(Try("3 1/2", 0, out var mixed));
        Assert.False(mixed.Ambiguous);
        Assert.Equal(2, mixed.TokensConsumed);

        // Assert: a signed number, a decimal or a number with a fraction that is not simple is not a mixed number
        Assert.Equal("minus three", SpokenAt("-3 1/2", 0));
        Assert.Equal("three point five", SpokenAt("3.5 1/2", 0));
        Assert.Equal("three", SpokenAt("3 9/11", 0));
        Assert.False(Try("-5\u00BD", 0, out _));
    }

    /// <summary>
    ///     Proves over generated token streams that every match has only the documented shapes: whitespace-free spans of one
    ///     token or two adjacent tokens, <c>,</c> and <c>:</c> only between digits, no <c>;</c>, spoken words from the closed shape,
    ///     ascending disjoint spans inside the consumed tokens.
    /// </summary>
    [Fact]
    public void NumericExpressionRecognizer_Spans_AreOnlyTheDocumentedShapes_OverGeneratedTokens()
    {
        // Arrange: a deterministic generator over numeric-looking and hostile pieces
        string[] pieces =
        [
            "5", "50", "1,234", "1,234.50", "3.5", ".5", "-5", "+3", "$", "\u20AC", "\u00A3", "\u00A5", "$5", "$5.50", "5$", "5\u20AC", "5USD", "USD", "EUR",
            "5:30", "5:30pm", "5pm", "5:30PM", "pm", "am", "p.m.", "12/25/2024", "2024-12-25", "5-10", "555-1234", "3.2.1", "1/2", "\u00BD", "5\u00BD", "3 1/2",
            "1st", "22nd", "1990s", "90s", "'", "\u2019", "90s'", "'90s", "\u2013", "%", "50%", "kg", "5kg", "ft.", "ft", "lbs", "lbs.", "mph", "\u00B0F", "72\u00B0F",
            "million", "billion", "thousand", "$5M", "5M", "January", "Jan.", "Jan", "May", "May.", "Dec", "2024", "1999", "1066", "#5", "#", "(", ")", ",", ".", ";", ":", "-", "/",
            "007", "1e5", "0x1F", "5\u00B2", "\uFF15", "\u200B", "\uD835\uDFD9", "\uD83D", "e\u0301", "AD", "in", "about", new string('1', 17), new string('1', 70), new string('1', 130), "1.2.3.4.5.6.7.8.9",
        ];
        string[] gaps = ["", " ", " ", "\u00A0", "\u202F", "  ", "\t", "\n"];
        var random = new Random(20260802);
        var shapes = 0;

        for (var iteration = 0; iteration < 2_000; iteration++)
        {
            var builder = new System.Text.StringBuilder();
            var count = random.Next(1, 8);
            for (var i = 0; i < count; i++)
            {
                builder.Append(pieces[random.Next(pieces.Length)]).Append(gaps[random.Next(gaps.Length)]);
            }

            var text = builder.ToString().TrimEnd();
            if (text.Length == 0)
            {
                continue;
            }

            var tokens = NormalizationHarness.Tokenize(text);
            for (var index = 0; index < tokens.Length; index++)
            {
                if (!NumericExpressionRecognizer.TryRecognize(text, tokens, index, out var match))
                {
                    continue;
                }

                // Assert: the consumed tokens exist and the match is non-empty
                var context = $"'{text.Replace("\n", "\\n").Replace("\t", "\\t")}' token {index}";
                Assert.InRange(match.TokensConsumed, 1, 3);
                Assert.True(index + match.TokensConsumed <= tokens.Length, context);
                Assert.True(match.SpanCount > 0 || match.Fallback, context);
                var lastEnd = tokens[index].Start;
                for (var s = 0; s < match.SpanCount; s++)
                {
                    var span = match[s];
                    shapes++;

                    // Assert: inside the consumed tokens, ascending and disjoint
                    var consumedEnd = tokens[index + match.TokensConsumed - 1].Start + tokens[index + match.TokensConsumed - 1].Length;
                    Assert.True(span.Start >= lastEnd && span.Length > 0 && span.Start + span.Length <= consumedEnd, context);
                    lastEnd = span.Start + span.Length;

                    // Assert: no whitespace, no semicolon, a comma or colon only between two ASCII digits
                    var covered = text.Substring(span.Start, span.Length);
                    Assert.DoesNotContain(covered, character => char.IsWhiteSpace(character) || character == ';');
                    for (var c = 0; c < covered.Length; c++)
                    {
                        if (covered[c] is ',' or ':')
                        {
                            Assert.True(c > 0 && c < covered.Length - 1 && covered[c - 1] is >= '0' and <= '9' && covered[c + 1] is >= '0' and <= '9', $"{context}: separator in '{covered}'");
                        }
                    }

                    // Assert: a span covers one token or two adjacent tokens (symbol or apostrophe and the token after it),
                    // except the two halves of a glued meridiem, which split one token into the hour or time and the meridiem letters
                    var coveredTokens = tokens.Count(token => token.Start >= span.Start && token.Start + token.Length <= span.Start + span.Length);
                    if (coveredTokens == 0)
                    {
                        Assert.Equal(2, match.SpanCount);
                        Assert.Equal(1, match.TokensConsumed);
                        Assert.Equal(NormalizationKind.DateTime, span.Kind);
                        var own = tokens[index];
                        var isTimePart = s == 0 && span.Start == own.Start && match[1].Start == span.Start + span.Length;
                        var isMeridiemPart = s == 1 && span.Start + span.Length == own.Start + own.Length && span.Spoken is "P M" or "A M";
                        Assert.True(isTimePart || isMeridiemPart, $"{context}: sub-token span '{covered}'");
                    }
                    else
                    {
                        Assert.InRange(coveredTokens, 1, 2);
                    }

                    if (coveredTokens == 2)
                    {
                        Assert.True(covered[0] is '$' or '\u20AC' or '\u00A3' or '\u00A5' or '\'' or '\u2019' or '\u2018', $"{context}: two-token span '{covered}'");
                    }

                    // Assert: closed spoken shape, no digit, bounded size, numeric kind
                    Assert.Matches(SpokenShape, span.Spoken);
                    Assert.True(span.Spoken.Length <= NumberVerbalizer.MaxSpokenLength, context);
                    Assert.True(span.Kind is NormalizationKind.Number or NormalizationKind.Currency or NormalizationKind.DateTime or NormalizationKind.Unit, context);
                }
            }
        }

        Assert.True(shapes > 1_000, "the generator must exercise many matches");
    }

    /// <summary>Proves the caps: a long numeric-looking token is never scanned and is a fallback, a long non-numeric token is identity, and the digit and group limits hold.</summary>
    [Fact]
    public void NumericExpressionRecognizer_Caps_LeaveOversizedTokensAsWrittenAndFlagFallback()
    {
        // Assert: a token of 129 characters that starts like a number is a fallback; one of 128 is scanned
        var longDigits = new string('1', NumericExpressionRecognizer.MaxTokenLength + 1);
        NormalizationToken[] huge = [new(0, longDigits.Length, true)];
        Assert.True(NumericExpressionRecognizer.TryRecognize(longDigits, huge, 0, out var capped));
        Assert.Equal(0, capped.SpanCount);
        Assert.True(capped.Fallback);
        Assert.Equal(1, capped.TokensConsumed);

        // Assert: a long token that does not start like a number is not numeric
        var word = new string('a', 500);
        NormalizationToken[] wordToken = [new(0, word.Length, true)];
        Assert.False(NumericExpressionRecognizer.TryRecognize(word, wordToken, 0, out _));
        var dollars = "$" + new string('1', 500);
        NormalizationToken[] dollarToken = [new(0, dollars.Length, true)];
        Assert.True(NumericExpressionRecognizer.TryRecognize(dollars, dollarToken, 0, out var dollarCap));
        Assert.True(dollarCap.Fallback);

        // Assert: digit limits at the boundaries
        Assert.False(Try(new string('9', 15), 0, out var fifteen) && fifteen.Fallback);
        Assert.True(Try(new string('9', 16), 0, out var sixteen));
        Assert.True(sixteen.Fallback);
        Assert.Equal(1, sixteen.SpanCount);
        Assert.True(Try(new string('9', 64), 0, out var sixtyFour));
        Assert.True(sixtyFour.Fallback);
        Assert.Equal(1, sixtyFour.SpanCount);
        Assert.True(Try(new string('9', 65), 0, out var sixtyFive));
        Assert.Equal(0, sixtyFive.SpanCount);
        Assert.True(sixtyFive.Fallback);
        Assert.True(Try("0." + new string('1', 32), 0, out var thirtyTwo));
        Assert.Equal(1, thirtyTwo.SpanCount);
        Assert.False(thirtyTwo.Fallback);
        Assert.True(Try("0." + new string('1', 33), 0, out var thirtyThree));
        Assert.Equal(0, thirtyThree.SpanCount);
        Assert.True(thirtyThree.Fallback);
    }

    /// <summary>Proves hostile token ranges (negative, empty, beyond the text, overflowing) are rejected with an exception and never read out of bounds.</summary>
    [Fact]
    public void NumericExpressionRecognizer_HostileTokenRanges_AreRejectedWithoutOverflow()
    {
        // Arrange
        const string text = "5 kg and $5 million";
        var good = new NormalizationToken(0, 1, true);
        NormalizationToken[][] hostile =
        [
            [new(0, 0, true)],
            [new(0, -1, true)],
            [new(-1, 2, true)],
            [new(text.Length, 1, true)],
            [new(text.Length + 1, 1, true)],
            [new(int.MaxValue, 1, true)],
            [new(1, int.MaxValue, true)],
            [new(int.MaxValue - 1, int.MaxValue, true)],
            [good, new(int.MaxValue, 5, false)],
            [good, new(2, int.MaxValue, false)],
            [good, new(2, -5, false)],
        ];

        // Act / Assert: the starting token or a neighbor that is read is rejected without overflowing
        foreach (var tokens in hostile)
        {
            Assert.Throws<ArgumentException>(() =>
            {
                for (var index = 0; index < tokens.Length; index++)
                {
                    NumericExpressionRecognizer.TryRecognize(text, tokens, index, out _);
                }
            });
        }

        Assert.Throws<ArgumentOutOfRangeException>(() => NumericExpressionRecognizer.TryRecognize(text, [good], 1, out _));
        Assert.Throws<ArgumentOutOfRangeException>(() => NumericExpressionRecognizer.TryRecognize(text, [good], -1, out _));
        Assert.Throws<ArgumentNullException>(() => NumericExpressionRecognizer.TryRecognize(null!, [good], 0, out _));
    }

    /// <summary>Proves deferred shapes and non-numeric tokens are identity and never counted as a fallback.</summary>
    [Fact]
    public void NumericExpressionRecognizer_DeferredAndNonNumericShapes_AreIdentityWithoutFallback()
    {
        // Assert: deferred, mixed and non-ASCII shapes
        foreach (var identity in new[]
        {
            "1e5", "0x1F", "1_000", "3D", "3-D", "101A", "7-Eleven", "5-year-old", "mid-1990s", "10k", "1.5M", "5\u00B2", "\uFF15", "\u0663",
            "\uD835\uDFD9", "Q3", "F-16", "COVID-19", "VIII", "word", "the", "...", ",", ".", "-", "+", "#", "#x", "$", "5x", "5.", "5,", ".x",
        })
        {
            Assert.False(TryToken(identity, out _), identity);
        }

        // Assert: plain words with digits in the middle, and the lone symbols that other rules own
        Assert.False(Try("version5", 0, out _));
        Assert.False(Try("a + b", 1, out _));
        Assert.False(Try("a / b", 1, out _));
        Assert.False(Try("x = 5", 1, out _));
        Assert.Equal("five", SpokenAt("x = 5", 2));
    }

    /// <summary>Proves the recognizer keeps no state: concurrent calls over the same tokens return identical matches.</summary>
    [Fact]
    public async Task NumericExpressionRecognizer_ConcurrentCalls_ReturnIdenticalResults()
    {
        // Arrange
        const string text = "Pay $1,234.50 at 5:30pm on January 5, 2024, then 50 % of 3 1/2 kg or 12/25/2024 for 2 ft. in the '90s.";
        var tokens = NormalizationHarness.Tokenize(text);
        string Key()
        {
            var parts = new List<string>();
            for (var index = 0; index < tokens.Length; index++)
            {
                if (NumericExpressionRecognizer.TryRecognize(text, tokens, index, out var match))
                {
                    parts.Add($"{index}:{match.TokensConsumed}:{match.Ambiguous}:{match.Fallback}:" + string.Join('/', Enumerable.Range(0, match.SpanCount).Select(i => $"{match[i].Start},{match[i].Length},{match[i].Spoken},{match[i].Kind}")));
                }
            }

            return string.Join(';', parts);
        }

        var expected = Key();

        // Act
        var tasks = Enumerable.Range(0, 32)
            .Select(_ => Task.Run(() => Enumerable.Range(0, 50).Select(_ => Key()).ToList(), TestContext.Current.CancellationToken))
            .ToArray();
        var results = await Task.WhenAll(tasks);

        // Assert
        Assert.NotEmpty(expected);
        Assert.All(results.SelectMany(keys => keys), key => Assert.Equal(expected, key));
    }
}
