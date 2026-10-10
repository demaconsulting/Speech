using DemaConsulting.Speech.Onnx.Kokoro.ModelManagementSubsystem;

namespace DemaConsulting.Speech.Onnx.Kokoro.Tests.ModelManagementSubsystem;

/// <summary>Tests for <see cref="KokoroTextNormalizer"/>, exercised through <see cref="KokoroLexiconPhonemizer"/>.</summary>
public class KokoroTextNormalizerTests
{
    /// <summary>Text that has a spoken equivalent normalizes to the same phonemes as that equivalent.</summary>
    [Theory]
    [InlineData("42", "forty two")]
    [InlineData("-5", "minus five")]
    [InlineData("3.5", "three point five")]
    [InlineData("1,000", "one thousand")]
    [InlineData("50%", "fifty percent")]
    [InlineData("$19.99", "nineteen dollars and ninety nine cents")]
    [InlineData("3:30 PM", "three thirty p m")]
    [InlineData("2005", "two thousand five")]
    [InlineData("1984", "nineteen eighty four")]
    [InlineData("1st 2nd 3rd", "first second third")]
    [InlineData("555-1234", "five five five, one two three four")]
    [InlineData("Dr. Smith", "doctor smith")]
    [InlineData("Mr. Smith", "mister smith")]
    [InlineData("Caf\u00E9", "cafe")]
    [InlineData("**bold** and `code`", "bold and code")]
    [InlineData("[the docs](https://example.com)", "the docs")]
    [InlineData("<3 love", "love")]
    [InlineData("a_b", "a b")]
    [InlineData("Tom & Jerry", "Tom and Jerry")]
    [InlineData("1 + 1 = 2", "one plus one equals two")]
    public void Normalize_SpokenForm_MatchesEquivalentText(string input, string equivalent)
    {
        // Arrange
        var phonemizer = new KokoroLexiconPhonemizer();

        // Act
        var (actual, _) = phonemizer.Phonemize(input);
        var (expected, _) = phonemizer.Phonemize(equivalent);

        // Assert
        Assert.NotEmpty(expected);
        Assert.Equal(expected, actual);
    }

    /// <summary>Integers convert to English words.</summary>
    [Theory]
    [InlineData(0, "zero")]
    [InlineData(13, "thirteen")]
    [InlineData(90, "ninety")]
    [InlineData(101, "one hundred one")]
    [InlineData(2024, "two thousand twenty four")]
    [InlineData(1_000_000, "one million")]
    public void IntegerToWords_Value_ReturnsWords(long value, string expected)
    {
        // Act / Assert
        Assert.Equal(expected, KokoroTextNormalizer.IntegerToWords(value));
    }

    /// <summary>Very large numbers are spoken (digit by digit) rather than crashing normalization.</summary>
    [Theory]
    [InlineData("$1000000000000000")]
    [InlineData("$99999999999999999999")]
    [InlineData("1,000,000,000,000,000")]
    [InlineData("99,999,999,999,999,999,999,999")]
    public void Normalize_HugeNumber_DoesNotThrow(string input)
    {
        // Act
        var result = KokoroTextNormalizer.Normalize(input);

        // Assert
        Assert.NotEmpty(result.Trim());
    }

    /// <summary>A null argument is rejected.</summary>
    [Fact]
    public void Normalize_NullText_ThrowsArgumentNullException()
    {
        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => KokoroTextNormalizer.Normalize(null!));
    }
}
