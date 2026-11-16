using DemaConsulting.Speech.SynthesisSubsystem;
using DemaConsulting.Speech.Text.En.AnalysisSubsystem;

namespace DemaConsulting.Speech.Text.En.Tests.AnalysisSubsystem;

/// <summary>
///     Unit tests for <see cref="TextEnAnnotations"/>: the annotation kind names and the typed
///     part-of-speech reader.
/// </summary>
public sealed class TextEnAnnotationsTests
{
    /// <summary>Proves the six annotation kinds have their documented, distinct names.</summary>
    [Fact]
    public void TextEnAnnotations_Kinds_HaveDocumentedDistinctNames()
    {
        // Arrange
        string[] kinds =
        [
            TextEnAnnotations.Normalized,
            TextEnAnnotations.PartOfSpeech,
            TextEnAnnotations.Reading,
            TextEnAnnotations.ReadingConfidence,
            TextEnAnnotations.Pronunciation,
            TextEnAnnotations.SpellOut,
        ];

        // Assert
        Assert.Equal(["en.norm", "en.upos", "en.reading", "en.reading-confidence", "en.pron", "en.spell"], kinds);
        Assert.Equal(kinds.Length, kinds.Distinct(StringComparer.Ordinal).Count());
    }

    /// <summary>Proves a part-of-speech annotation with a Universal Dependencies value is read back.</summary>
    [Theory]
    [InlineData("NOUN")]
    [InlineData("VERB")]
    [InlineData("ADJ")]
    [InlineData("X")]
    public void TextEnAnnotations_TryReadPartOfSpeech_PartOfSpeechAnnotation_ReturnsTagName(string tag)
    {
        // Arrange
        var annotation = new SegmentAnnotation(0, 3, TextEnAnnotations.PartOfSpeech, tag);

        // Act
        var read = TextEnAnnotations.TryReadPartOfSpeech(annotation, out var partOfSpeech);

        // Assert
        Assert.True(read);
        Assert.Equal(tag, partOfSpeech);
    }

    /// <summary>Proves an annotation of another kind, or with a value outside the tag set, is not a part-of-speech annotation.</summary>
    [Theory]
    [InlineData("en.reading", "NOUN")]
    [InlineData("en.upos", "noun")]
    [InlineData("en.upos", "NOUNS")]
    [InlineData("en.upos", "")]
    [InlineData("EN.UPOS", "NOUN")]
    public void TextEnAnnotations_TryReadPartOfSpeech_OtherKindOrUnknownValue_ReturnsFalse(string kind, string value)
    {
        // Arrange
        var annotation = new SegmentAnnotation(0, 3, kind, value);

        // Act
        var read = TextEnAnnotations.TryReadPartOfSpeech(annotation, out var partOfSpeech);

        // Assert
        Assert.False(read);
        Assert.Equal(string.Empty, partOfSpeech);
    }

    /// <summary>Proves a normalization annotation is read back with its exact spoken text, including the empty value that means "speak nothing".</summary>
    /// <param name="value">The annotation value.</param>
    [Theory]
    [InlineData("Doctor")]
    [InlineData(" slash ")]
    [InlineData("'")]
    [InlineData("")]
    public void TextEnAnnotations_TryReadNormalized_NormalizationAnnotation_ReturnsSpokenText(string value)
    {
        // Arrange
        var annotation = new SegmentAnnotation(0, 3, TextEnAnnotations.Normalized, value);

        // Act
        var read = TextEnAnnotations.TryReadNormalized(annotation, out var spoken);

        // Assert
        Assert.True(read);
        Assert.Equal(value, spoken);
    }

    /// <summary>Proves an annotation of another kind (or a differently cased kind) is not a normalization.</summary>
    /// <param name="kind">The annotation kind.</param>
    [Theory]
    [InlineData("en.upos")]
    [InlineData("EN.NORM")]
    [InlineData("en.norm ")]
    [InlineData("en.spell")]
    public void TextEnAnnotations_TryReadNormalized_OtherKind_ReturnsFalse(string kind)
    {
        // Arrange
        var annotation = new SegmentAnnotation(0, 3, kind, "NOUN");

        // Act
        var read = TextEnAnnotations.TryReadNormalized(annotation, out var spoken);

        // Assert
        Assert.False(read);
        Assert.Equal(string.Empty, spoken);
    }

    /// <summary>Proves a null annotation is rejected by the normalization reader.</summary>
    [Fact]
    public void TextEnAnnotations_TryReadNormalized_NullAnnotation_ThrowsArgumentNullException()
    {
        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => TextEnAnnotations.TryReadNormalized(null!, out _));
    }

    /// <summary>Proves a null annotation is rejected.</summary>
    [Fact]
    public void TextEnAnnotations_TryReadPartOfSpeech_NullAnnotation_ThrowsArgumentNullException()
    {
        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => TextEnAnnotations.TryReadPartOfSpeech(null!, out _));
    }
}
