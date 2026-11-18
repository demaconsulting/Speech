using DemaConsulting.Speech.Text.En.Trainer.TrainingDataSubsystem;

namespace DemaConsulting.Speech.Text.En.Trainer.Tests.TrainingDataSubsystem;

/// <summary>
///     Unit tests for <see cref="SpanValidator"/>: whole-word spans over UTF-16 offsets, scalar-aware neighbors.
/// </summary>
public sealed class SpanValidatorTests
{
    /// <summary>A letter outside the Basic Multilingual Plane (mathematical script capital A), a surrogate pair.</summary>
    private const string AstralLetter = "\U0001D49C";

    /// <summary>A symbol outside the Basic Multilingual Plane (grinning face), a surrogate pair.</summary>
    private const string AstralSymbol = "\U0001F600";

    /// <summary>The forms listed for the reading under test.</summary>
    private static readonly IReadOnlySet<string> Forms = new HashSet<string> { "lead", "leads" };

    /// <summary>Proves an exact listed word between spaces is valid.</summary>
    [Fact]
    public void SpanValidator_Validate_ExactWord_ReturnsNull()
    {
        // Arrange / Act
        var error = SpanValidator.Validate("She will lead the team.", 9, 13, Forms);

        // Assert
        Assert.Null(error);
    }

    /// <summary>Proves a capitalized word matches its lower-case listed form.</summary>
    [Fact]
    public void SpanValidator_Validate_CapitalizedWord_MatchesListedFormCaseInsensitively()
    {
        // Arrange / Act
        var error = SpanValidator.Validate("Lead is heavy.", 0, 4, Forms);

        // Assert
        Assert.Null(error);
    }

    /// <summary>Proves a slice that is not a listed form of the reading is rejected.</summary>
    [Fact]
    public void SpanValidator_Validate_UnlistedForm_ReturnsError()
    {
        // Arrange / Act
        var error = SpanValidator.Validate("She will led the team.", 9, 12, Forms);

        // Assert
        Assert.NotNull(error);
        Assert.Contains("not a listed form", error);
    }

    /// <summary>Proves a span at the start of the sentence, at its end, next to punctuation, or next to a supplementary-plane symbol is a whole word.</summary>
    [Theory]
    [InlineData("follow the lead", 11, 15)]
    [InlineData("\"lead\" them", 1, 5)]
    [InlineData(AstralSymbol + "lead" + AstralSymbol, 2, 6)]
    [InlineData(AstralSymbol + "lead", 2, 6)]
    [InlineData("lead" + AstralSymbol, 0, 4)]
    public void SpanValidator_Validate_EdgesAndPunctuation_AreWholeWords(string sentence, int start, int end)
    {
        // Arrange / Act
        var error = SpanValidator.Validate(sentence, start, end, Forms);

        // Assert
        Assert.Null(error);
    }

    /// <summary>Proves a letter or digit (ASCII, non-ASCII or a superscript number) before the span makes it a partial word.</summary>
    [Theory]
    [InlineData("xlead is", 1, 5)]
    [InlineData("²lead is", 1, 5)]
    [InlineData(AstralLetter + "lead", 2, 6)]
    public void SpanValidator_Validate_LetterOrNumberBefore_ReturnsPrecededError(string sentence, int start, int end)
    {
        // Arrange / Act
        var error = SpanValidator.Validate(sentence, start, end, Forms);

        // Assert
        Assert.NotNull(error);
        Assert.Contains("preceded by a letter or digit", error);
    }

    /// <summary>Proves a letter or digit after the span makes it a partial word.</summary>
    [Theory]
    [InlineData("lead2 is", 0, 4)]
    [InlineData("leadé is", 0, 4)]
    [InlineData("lead" + AstralLetter, 0, 4)]
    public void SpanValidator_Validate_LetterOrNumberAfter_ReturnsFollowedError(string sentence, int start, int end)
    {
        // Arrange / Act
        var error = SpanValidator.Validate(sentence, start, end, Forms);

        // Assert
        Assert.NotNull(error);
        Assert.Contains("followed by a letter or digit", error);
    }

    /// <summary>Proves an off-by-one offset that cuts into a longer word or takes a space is rejected.</summary>
    [Theory]
    [InlineData("She will lead the team.", 10, 13)]
    [InlineData("She will lead the team.", 9, 14)]
    public void SpanValidator_Validate_OffByOne_ReturnsError(string sentence, int start, int end)
    {
        // Arrange / Act
        var error = SpanValidator.Validate(sentence, start, end, Forms);

        // Assert
        Assert.NotNull(error);
    }

    /// <summary>Proves empty, reversed and out-of-range spans are rejected.</summary>
    [Theory]
    [InlineData(-1, 4)]
    [InlineData(4, 0)]
    [InlineData(0, 99)]
    public void SpanValidator_Validate_OutOfRange_ReturnsError(int start, int end)
    {
        // Arrange / Act
        var error = SpanValidator.Validate("lead the way", start, end, Forms);

        // Assert
        Assert.NotNull(error);
        Assert.Contains("out of range", error);
    }

    /// <summary>Proves an offset between the two halves of a surrogate pair is rejected, as the start and as the end of the span.</summary>
    [Fact]
    public void SpanValidator_Validate_StartInsideSurrogatePair_ReturnsError()
    {
        // Arrange: the emoji occupies code units 0 and 1 of the first sentence and 4 and 5 of the second
        var startInside = AstralSymbol + "lead";
        var endInside = "lead" + AstralSymbol;

        // Act
        var startError = SpanValidator.Validate(startInside, 1, 6, Forms);
        var endError = SpanValidator.Validate(endInside, 0, 5, Forms);

        // Assert
        Assert.Contains("surrogate pair", startError);
        Assert.Contains("surrogate pair", endError);
    }

    /// <summary>Proves a lone surrogate neighbor is not a letter and so does not block the span.</summary>
    [Fact]
    public void SpanValidator_Validate_LoneSurrogateNeighbor_IsNotALetter()
    {
        // Arrange
        var sentence = "\uD83Dlead";

        // Act
        var error = SpanValidator.Validate(sentence, 1, 5, Forms);

        // Assert
        Assert.Null(error);
    }
}
