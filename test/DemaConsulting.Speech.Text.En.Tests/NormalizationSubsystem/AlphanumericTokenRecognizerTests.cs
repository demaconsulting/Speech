using DemaConsulting.Speech.Text.En.NormalizationSubsystem;

namespace DemaConsulting.Speech.Text.En.Tests.NormalizationSubsystem;

/// <summary>
///     Unit tests for <see cref="AlphanumericTokenRecognizer"/>: identifiers of capital letters and digits (letters
///     spelled, digits read) and names joined to a number by a hyphen (only the number read).
/// </summary>
public sealed class AlphanumericTokenRecognizerTests
{
    /// <summary>Joins the spoken texts of the spans with a vertical bar, so a whole reading is one comparable string.</summary>
    /// <param name="spans">The spans, or null.</param>
    /// <returns>The joined spoken texts, or null when there are no spans.</returns>
    private static string? Spoken(IReadOnlyList<NumericSpan>? spans) =>
        spans is null ? null : string.Join('|', spans.Select(span => span.Spoken));

    /// <summary>Proves an identifier is read as one span per run: letters spelled, digits as a cardinal (up to three digits, no leading zero) or digit by digit.</summary>
    /// <param name="token">The token.</param>
    /// <param name="spoken">The expected spoken texts of the spans, joined by a bar.</param>
    [Theory]
    [InlineData("A4", "A|four")]
    [InlineData("B12", "B|twelve")]
    [InlineData("H2O", "H|two|O")]
    [InlineData("R2D2", "R|two|D|two")]
    [InlineData("MP3", "M P|three")]
    [InlineData("A100", "A|one hundred")]
    [InlineData("A007", "A|zero zero seven")]
    [InlineData("RTX4090", "R T X|four zero nine zero")]
    public void AlphanumericTokenRecognizer_RecognizeIdentifier_SpellsLettersAndReadsDigits(string token, string spoken)
    {
        // Act
        var spans = AlphanumericTokenRecognizer.RecognizeIdentifier(token, 0);

        // Assert
        Assert.Equal(spoken, Spoken(spans));
        Assert.NotNull(spans);
        Assert.All(spans, span => Assert.Equal(span.Kind == NormalizationKind.Initialism, char.IsAsciiLetterUpper(token[span.Start])));
        Assert.Equal(token.Length, spans.Sum(span => span.Length));
    }

    /// <summary>Proves the spans of an identifier are adjacent, cover the whole token and index the text through the token offset.</summary>
    [Fact]
    public void AlphanumericTokenRecognizer_RecognizeIdentifier_SpansCoverTheTokenAtItsOffset()
    {
        // Act
        var spans = AlphanumericTokenRecognizer.RecognizeIdentifier("MP3", 7);

        // Assert
        Assert.NotNull(spans);
        Assert.Equal(2, spans.Count);
        Assert.Equal((7, 2, NormalizationKind.Initialism), (spans[0].Start, spans[0].Length, spans[0].Kind));
        Assert.Equal((9, 1, NormalizationKind.Number), (spans[1].Start, spans[1].Length, spans[1].Kind));
    }

    /// <summary>Proves tokens that are not identifiers of the documented shape are left alone: a long capital word before digits, too many digits, lower case, no digits, a single letter, a leading digit, and over-long tokens.</summary>
    /// <param name="token">The token.</param>
    [Theory]
    [InlineData("COVID19")]
    [InlineData("A12345")]
    [InlineData("a4")]
    [InlineData("A4b")]
    [InlineData("4A")]
    [InlineData("AB")]
    [InlineData("A")]
    [InlineData("A4-B")]
    [InlineData("A1B2C3D4E5F6G7")]
    [InlineData("")]
    public void AlphanumericTokenRecognizer_RecognizeIdentifier_OtherShapes_ReturnNull(string token)
    {
        // Act
        var spans = AlphanumericTokenRecognizer.RecognizeIdentifier(token, 0);

        // Assert
        Assert.Null(spans);
    }

    /// <summary>Proves a name joined to a number by a hyphen gets one span over the digits only, read as a cardinal, so the hyphenated word stays one word.</summary>
    /// <param name="token">The token.</param>
    /// <param name="spoken">The expected words of the digits.</param>
    /// <param name="digitStart">The offset of the digits in the token.</param>
    [Theory]
    [InlineData("COVID-19", "nineteen", 6)]
    [InlineData("Catch-22", "twenty two", 6)]
    [InlineData("Boeing-737", "seven hundred thirty seven", 7)]
    [InlineData("F-16", "sixteen", 2)]
    public void AlphanumericTokenRecognizer_RecognizeHyphenatedNumber_ReadsOnlyTheDigits(string token, string spoken, int digitStart)
    {
        // Act
        var spans = AlphanumericTokenRecognizer.RecognizeHyphenatedNumber(token, 5);

        // Assert
        Assert.NotNull(spans);
        var span = Assert.Single(spans);
        Assert.Equal(spoken, span.Spoken);
        Assert.Equal(NormalizationKind.Number, span.Kind);
        Assert.Equal(5 + digitStart, span.Start);
        Assert.Equal(token.Length - digitStart, span.Length);
    }

    /// <summary>Proves other hyphenated shapes are left alone: four digits, no digits, letters mixed into the digits, a leading hyphen, digits before the hyphen and ordinary compounds.</summary>
    /// <param name="token">The token.</param>
    [Theory]
    [InlineData("COVID-1234")]
    [InlineData("COVID-")]
    [InlineData("COVID-1a")]
    [InlineData("-19")]
    [InlineData("co2-19")]
    [InlineData("well-known")]
    [InlineData("a-b-12")]
    [InlineData("mid-1990s")]
    public void AlphanumericTokenRecognizer_RecognizeHyphenatedNumber_OtherShapes_ReturnNull(string token)
    {
        // Act
        var spans = AlphanumericTokenRecognizer.RecognizeHyphenatedNumber(token, 0);

        // Assert
        Assert.Null(spans);
    }
}
