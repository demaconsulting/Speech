using System.Globalization;
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
    [InlineData("en.upos", "noun")]
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
    [InlineData(" slash ")]
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

    /// <summary>Proves every reader rejects a null annotation.</summary>
    [Fact]
    public void TextEnAnnotations_TryReadNormalized_NullAnnotation_ThrowsArgumentNullException()
    {
        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => TextEnAnnotations.TryReadNormalized(null!, out _));
        Assert.Throws<ArgumentNullException>(() => TextEnAnnotations.TryReadPartOfSpeech(null!, out _));
        Assert.Throws<ArgumentNullException>(() => TextEnAnnotations.TryReadSpellOut(null!, out _));
        Assert.Throws<ArgumentNullException>(() => TextEnAnnotations.TryReadPronunciation(null!, out _, out _));
        Assert.Throws<ArgumentNullException>(() => TextEnAnnotations.TryReadReplacement(null!, out _, out _));
        Assert.Throws<ArgumentNullException>(() => TextEnAnnotations.TryReadReading(null!, out _));
        Assert.Throws<ArgumentNullException>(() => TextEnAnnotations.TryReadReadingConfidence(null!, out _));
    }

    /// <summary>Proves a spell annotation with a well-formed letter sequence is read back exactly as stored, padding included.</summary>
    /// <param name="value">The annotation value.</param>
    [Theory]
    [InlineData("F B I")]
    [InlineData(" A B ")]
    public void TextEnAnnotations_TryReadSpellOut_WellFormedLetters_ReturnsValueExactly(string value)
    {
        // Arrange
        var annotation = new SegmentAnnotation(0, 3, TextEnAnnotations.SpellOut, value);

        // Act
        var read = TextEnAnnotations.TryReadSpellOut(annotation, out var letters);

        // Assert
        Assert.True(read);
        Assert.Equal(value, letters);
    }

    /// <summary>Proves a spell annotation of another kind, or with a malformed letter sequence, is not read.</summary>
    /// <param name="kind">The annotation kind.</param>
    /// <param name="value">The annotation value.</param>
    [Theory]
    [InlineData("en.norm", "F B I")]
    [InlineData("en.spell", "")]
    [InlineData("en.spell", "f b i")]
    [InlineData("en.spell", "FBI")]
    [InlineData("en.spell", "F-B")]
    public void TextEnAnnotations_TryReadSpellOut_OtherKindOrMalformedValue_ReturnsFalse(string kind, string value)
    {
        // Arrange
        var annotation = new SegmentAnnotation(0, 3, kind, value);

        // Act
        var read = TextEnAnnotations.TryReadSpellOut(annotation, out var letters);

        // Assert
        Assert.False(read);
        Assert.Equal(string.Empty, letters);
    }

    /// <summary>Proves a pronunciation annotation is split at the first colon into its scheme and phonemes.</summary>
    /// <param name="value">The annotation value.</param>
    /// <param name="scheme">The expected scheme.</param>
    /// <param name="phonemes">The expected phonemes.</param>
    [Theory]
    [InlineData("ipa:p\u0251\u02D0:l", PronunciationScheme.Ipa, "p\u0251\u02D0:l")]
    [InlineData("arpabet:P OW1 L IH0 SH", PronunciationScheme.Arpabet, "P OW1 L IH0 SH")]
    public void TextEnAnnotations_TryReadPronunciation_WellFormedValue_ReturnsSchemeAndPhonemes(string value, PronunciationScheme scheme, string phonemes)
    {
        // Arrange
        var annotation = new SegmentAnnotation(0, 6, TextEnAnnotations.Pronunciation, value);

        // Act
        var read = TextEnAnnotations.TryReadPronunciation(annotation, out var readScheme, out var readPhonemes);

        // Assert
        Assert.True(read);
        Assert.Equal(scheme, readScheme);
        Assert.Equal(phonemes, readPhonemes);
    }

    /// <summary>Proves a pronunciation annotation of another kind, with an unknown scheme or with malformed phonemes is not read.</summary>
    /// <param name="kind">The annotation kind.</param>
    /// <param name="value">The annotation value.</param>
    [Theory]
    [InlineData("en.norm", "ipa:abc")]
    [InlineData("en.pron", "")]
    [InlineData("en.pron", "ipa:")]
    [InlineData("en.pron", "IPA:abc")]
    [InlineData("en.pron", "ipa:a\u0007b")]
    [InlineData("en.pron", "arpabet:P OW3")]
    public void TextEnAnnotations_TryReadPronunciation_OtherKindOrMalformedValue_ReturnsFalse(string kind, string value)
    {
        // Arrange
        var annotation = new SegmentAnnotation(0, 6, kind, value);

        // Act
        var read = TextEnAnnotations.TryReadPronunciation(annotation, out var scheme, out var phonemes);

        // Assert
        Assert.False(read);
        Assert.Equal(default, scheme);
        Assert.Equal(string.Empty, phonemes);
    }

    /// <summary>Proves the combined replacement reader accepts both replacement kinds and flags only the spelled one.</summary>
    [Fact]
    public void TextEnAnnotations_TryReadReplacement_NormAndSpell_ReturnValueAndSpelledFlag()
    {
        // Arrange
        var norm = new SegmentAnnotation(0, 3, TextEnAnnotations.Normalized, "Doctor");
        var empty = new SegmentAnnotation(0, 1, TextEnAnnotations.Normalized, string.Empty);
        var spell = new SegmentAnnotation(0, 3, TextEnAnnotations.SpellOut, " F B I");

        // Act / Assert
        Assert.True(TextEnAnnotations.TryReadReplacement(norm, out var normText, out var normSpelled));
        Assert.Equal("Doctor", normText);
        Assert.False(normSpelled);
        Assert.True(TextEnAnnotations.TryReadReplacement(empty, out var emptyText, out var emptySpelled));
        Assert.Equal(string.Empty, emptyText);
        Assert.False(emptySpelled);
        Assert.True(TextEnAnnotations.TryReadReplacement(spell, out var spellText, out var spellSpelled));
        Assert.Equal(" F B I", spellText);
        Assert.True(spellSpelled);
    }

    /// <summary>Proves the combined replacement reader rejects other kinds and a malformed spelled value.</summary>
    [Fact]
    public void TextEnAnnotations_TryReadReplacement_OtherKindMalformedOrNull_ReturnsFalseOrThrows()
    {
        // Arrange
        var upos = new SegmentAnnotation(0, 3, TextEnAnnotations.PartOfSpeech, "NOUN");
        var pron = new SegmentAnnotation(0, 3, TextEnAnnotations.Pronunciation, "ipa:abc");
        var badSpell = new SegmentAnnotation(0, 3, TextEnAnnotations.SpellOut, "fbi");

        // Act / Assert
        foreach (var annotation in new[] { upos, pron, badSpell })
        {
            Assert.False(TextEnAnnotations.TryReadReplacement(annotation, out var text, out var spelled));
            Assert.Equal(string.Empty, text);
            Assert.False(spelled);
        }

    }

    /// <summary>Proves a reading annotation with a well-formed id is read back, and the id is not checked against the inventory.</summary>
    /// <param name="id">The reading id.</param>
    [Theory]
    [InlineData("record.noun")]
    [InlineData("unknown.reading")]
    public void TextEnAnnotations_TryReadReading_WellFormedId_ReturnsId(string id)
    {
        // Arrange
        var annotation = new SegmentAnnotation(0, 6, TextEnAnnotations.Reading, id);

        // Act
        var read = TextEnAnnotations.TryReadReading(annotation, out var readingId);

        // Assert
        Assert.True(read);
        Assert.Equal(id, readingId);
    }

    /// <summary>Proves another kind or an ill-formed id (upper case, a missing segment, a separator character, an empty value) is not a reading, and nothing throws for a bad value.</summary>
    /// <param name="kind">The annotation kind.</param>
    /// <param name="value">The annotation value.</param>
    [Theory]
    [InlineData("en.upos", "record.noun")]
    [InlineData("en.reading", "")]
    [InlineData("en.reading", "record")]
    [InlineData("en.reading", "Record.noun")]
    [InlineData("en.reading", "record|noun")]
    [InlineData("en.reading", "record.-noun")]
    public void TextEnAnnotations_TryReadReading_OtherKindOrIllFormedId_ReturnsFalse(string kind, string value)
    {
        // Arrange
        var annotation = new SegmentAnnotation(0, 6, kind, value);

        // Act
        var read = TextEnAnnotations.TryReadReading(annotation, out var readingId);

        // Assert
        Assert.False(read);
        Assert.Equal(string.Empty, readingId);
    }

    /// <summary>Proves the confidence reader accepts the documented grammar and returns the invariant value, under every culture.</summary>
    /// <param name="value">The annotation value.</param>
    /// <param name="expected">The expected confidence.</param>
    [Theory]
    [InlineData("0", 0.0)]
    [InlineData("1.000", 1.0)]
    [InlineData("0.123456", 0.123456)]
    public void TextEnAnnotations_TryReadReadingConfidence_ValidValue_ReturnsInvariantNumberUnderEveryCulture(string value, double expected)
    {
        // Arrange
        var annotation = new SegmentAnnotation(0, 6, TextEnAnnotations.ReadingConfidence, value);
        var previous = CultureInfo.CurrentCulture;
        try
        {
            foreach (var culture in new[] { "", "tr-TR", "de-DE", "ar-SA", "fr-FR" })
            {
                CultureInfo.CurrentCulture = new CultureInfo(culture);

                // Act
                var read = TextEnAnnotations.TryReadReadingConfidence(annotation, out var confidence);

                // Assert
                Assert.True(read, culture);
                Assert.Equal(expected, confidence);
            }
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    /// <summary>Proves a confidence above 1, negative, not a number, in exponent form, with whitespace, a comma separator, a sign, too many decimals or non-ASCII digits is rejected without throwing.</summary>
    /// <param name="kind">The annotation kind.</param>
    /// <param name="value">The annotation value.</param>
    [Theory]
    [InlineData("en.reading-confidence", "1.001")]
    [InlineData("en.reading-confidence", "-0.1")]
    [InlineData("en.reading-confidence", "1e-1")]
    [InlineData("en.reading-confidence", "0,5")]
    [InlineData("en.reading-confidence", "0.1234567")]
    [InlineData("en.reading-confidence", "0.5 ")]
    [InlineData("en.reading-confidence", "0.")]
    [InlineData("en.reading-confidence", "")]
    [InlineData("en.reading-confidence", "\u0660.\u0665")]
    [InlineData("en.reading", "0.5")]
    public void TextEnAnnotations_TryReadReadingConfidence_InvalidValueOrKind_ReturnsFalse(string kind, string value)
    {
        // Arrange
        var annotation = new SegmentAnnotation(0, 6, kind, value);

        // Act
        var read = TextEnAnnotations.TryReadReadingConfidence(annotation, out var confidence);

        // Assert
        Assert.False(read);
        Assert.Equal(0.0, confidence);
    }
}
