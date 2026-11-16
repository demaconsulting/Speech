using DemaConsulting.Speech.Text.En.NormalizationSubsystem;

namespace DemaConsulting.Speech.Text.En.Tests.NormalizationSubsystem;

/// <summary>
///     Unit tests for <see cref="AcronymRecognizer"/>: case sensitivity, the shouting guard, the
///     vowel-less rule, dotted and ampersand forms, the strict loader and the lazy default.
/// </summary>
public sealed class AcronymRecognizerTests
{
    /// <summary>The header line of a lexicon, used to build small custom tables.</summary>
    private const string Header = "form\tspoken\tambiguous\n";

    /// <summary>Recognizes a token with the default lexicon.</summary>
    /// <param name="token">The token.</param>
    /// <param name="shouting">Whether the sentence is all capitals.</param>
    /// <returns>Whether it was recognized, and the match.</returns>
    private static (bool Found, AcronymMatch Match) Recognize(string token, bool shouting = false)
    {
        var found = AcronymRecognizer.Default.TryRecognize(token, shouting, out var match);
        return (found, match);
    }

    /// <summary>Proves the listed initialisms are recognized only in their exact case.</summary>
    /// <param name="token">The token.</param>
    /// <param name="recognized">Whether it must be recognized.</param>
    [Theory]
    [InlineData("US", true)]
    [InlineData("us", false)]
    [InlineData("Us", false)]
    [InlineData("IT", true)]
    [InlineData("It", false)]
    [InlineData("it", false)]
    [InlineData("AM", true)]
    [InlineData("am", false)]
    [InlineData("Am", false)]
    [InlineData("FBI", true)]
    [InlineData("fbi", false)]
    [InlineData("Fbi", false)]
    [InlineData("PM", true)]
    [InlineData("ID", true)]
    [InlineData("Id", false)]
    public void AcronymRecognizer_TryRecognize_CaseVariants_AreCaseSensitive(string token, bool recognized)
    {
        // Act
        var (found, _) = Recognize(token);

        // Assert
        Assert.Equal(recognized, found);
    }

    /// <summary>Proves a recognized initialism is spoken as its capital letters separated by spaces.</summary>
    /// <param name="token">The token.</param>
    /// <param name="spoken">The expected spoken form.</param>
    [Theory]
    [InlineData("FBI", "F B I")]
    [InlineData("US", "U S")]
    [InlineData("ASAP", "A S A P")]
    [InlineData("NYC", "N Y C")]
    [InlineData("HTTP", "H T T P")]
    [InlineData("PHP", "P H P")]
    public void AcronymRecognizer_TryRecognize_Initialism_IsSpokenLetterByLetter(string token, string spoken)
    {
        // Act
        var (found, match) = Recognize(token);

        // Assert
        Assert.True(found);
        Assert.Equal(spoken, match.Spoken);
        Assert.False(match.TokenEndsWithPeriod);
    }

    /// <summary>Proves in an all-capital sentence an ambiguous form is left alone but an unambiguous listed form and a dotted form are still spoken, and the vowel-less rule is suspended.</summary>
    [Fact]
    public void AcronymRecognizer_TryRecognize_ShoutingSentence_LeavesAmbiguousAndRuleSpelledForms()
    {
        // Act / Assert
        Assert.False(Recognize("US", shouting: true).Found);
        Assert.False(Recognize("IT", shouting: true).Found);
        Assert.False(Recognize("AM", shouting: true).Found);
        Assert.False(Recognize("NYC", shouting: true).Found);
        Assert.True(Recognize("FBI", shouting: true).Found);
        Assert.True(Recognize("U.S.", shouting: true).Found);
        Assert.True(Recognize("US", shouting: false).Found);
    }

    /// <summary>Proves unlisted capitals with a vowel, Roman-numeral-shaped capitals, over-long or one-letter tokens, and malformed dotted or ampersand shapes are not initialisms.</summary>
    /// <param name="token">The token.</param>
    [Theory]
    [InlineData("NASA")]
    [InlineData("NATO")]
    [InlineData("SCUBA")]
    [InlineData("CDXL")]
    [InlineData("MMMM")]
    [InlineData("XLVII")]
    [InlineData("DC")]
    [InlineData("A")]
    [InlineData("BCDFGH")]
    [InlineData("BCDFGHJ")]
    [InlineData("Nyc")]
    [InlineData("N1C")]
    [InlineData("U.")]
    [InlineData("..")]
    [InlineData("...")]
    [InlineData("U..S")]
    [InlineData("US.")]
    [InlineData("a.bc")]
    [InlineData(".U.S")]
    [InlineData("U,S.")]
    [InlineData("1.2.")]
    [InlineData("&")]
    [InlineData("A&")]
    [InlineData("&A")]
    [InlineData("A&&B")]
    [InlineData("a&b")]
    [InlineData("ABCDE&F")]
    [InlineData("Johnson&Johnson")]
    [InlineData("A&B2")]
    public void AcronymRecognizer_TryRecognize_TokensThatAreNotInitialisms_AreNotRecognized(string token)
    {
        // Act
        var (found, _) = Recognize(token);

        // Assert
        Assert.False(found);
    }

    /// <summary>Proves dotted letter forms are spoken upper-cased and spaced, and report whether the token itself carries the final period.</summary>
    /// <param name="token">The token.</param>
    /// <param name="spoken">The expected spoken form.</param>
    /// <param name="endsWithPeriod">Whether the token ends with the period.</param>
    [Theory]
    [InlineData("U.S.", "U S", true)]
    [InlineData("U.S.A.", "U S A", true)]
    [InlineData("p.m.", "P M", true)]
    [InlineData("J.R.R.", "J R R", true)]
    [InlineData("U.S", "U S", false)]
    [InlineData("a.m", "A M", false)]
    public void AcronymRecognizer_TryRecognize_DottedLetters_AreSpelled(string token, string spoken, bool endsWithPeriod)
    {
        // Act
        var (found, match) = Recognize(token);

        // Assert
        Assert.True(found);
        Assert.Equal(spoken, match.Spoken);
        Assert.Equal(endsWithPeriod, match.TokenEndsWithPeriod);
    }

    /// <summary>Proves ampersand initialisms are spoken group by group with <c>and</c>, keeping a possessive tail.</summary>
    /// <param name="token">The token.</param>
    /// <param name="spoken">The expected spoken form.</param>
    [Theory]
    [InlineData("AT&T", "A T and T")]
    [InlineData("R&D", "R and D")]
    [InlineData("Q&A", "Q and A")]
    [InlineData("AT&T's", "A T and T's")]
    [InlineData("AT&T\u2019s", "A T and T's")]
    [InlineData("A&B&C", "A and B and C")]
    public void AcronymRecognizer_TryRecognize_AmpersandForms_AreSpelledWithAnd(string token, string spoken)
    {
        // Act
        var (found, match) = Recognize(token);

        // Assert
        Assert.True(found);
        Assert.Equal(spoken, match.Spoken);
    }

    /// <summary>Proves the loader builds a custom lexicon and the recognizer then uses exactly it.</summary>
    [Fact]
    public void AcronymRecognizer_FromTsv_CustomLexicon_IsUsed()
    {
        // Arrange
        var recognizer = AcronymRecognizer.FromTsv(new StringReader("# c\r\n\r\n" + Header + "NASA\tN A S A\tno\r\nXY\tX Y\tyes\r\n"));

        // Act
        var nasa = recognizer.TryRecognize("NASA", false, out var match);
        var shouted = recognizer.TryRecognize("XY", true, out _);

        // Assert
        Assert.True(nasa);
        Assert.Equal("N A S A", match.Spoken);
        Assert.False(shouted);
    }

    /// <summary>Proves malformed rows are rejected with an error that names the one-based line number.</summary>
    /// <param name="body">The rows after the header.</param>
    [Theory]
    [InlineData("US\tU S\n")]
    [InlineData("us\tU S\tno\n")]
    [InlineData("\tU S\tno\n")]
    [InlineData("US\t\tno\n")]
    [InlineData("US\t U S\tno\n")]
    [InlineData("US\tU S\tmaybe\n")]
    [InlineData("US\tU S\tno\nUS\tU S\tno\n")]
    public void AcronymRecognizer_FromTsv_MalformedRow_ThrowsInvalidDataExceptionNamingTheLine(string body)
    {
        // Arrange: the header is line 1 and the first data row line 2 (the duplicate is reported on line 3)
        var content = Header + body;
        var expected = body.Contains("US\tU S\tno\nUS", StringComparison.Ordinal) ? "Line 3" : "Line 2";

        // Act
        var exception = Assert.Throws<InvalidDataException>(() => AcronymRecognizer.FromTsv(new StringReader(content)));

        // Assert
        Assert.Contains(expected, exception.Message, StringComparison.Ordinal);
    }

    /// <summary>Proves a null reader is rejected and the lazy default is one shared instance under concurrent first use.</summary>
    [Fact]
    public async Task AcronymRecognizer_FromTsvAndDefault_NullReaderAndConcurrentFirstUse_BehaveAsDocumented()
    {
        // Act
        var instances = await Task.WhenAll(Enumerable.Range(0, 32).Select(_ => Task.Run(() => AcronymRecognizer.Default, TestContext.Current.CancellationToken)));

        // Assert
        Assert.Throws<ArgumentNullException>(() => AcronymRecognizer.FromTsv(null!));
        Assert.All(instances, instance => Assert.Same(instances[0], instance));
    }
}
