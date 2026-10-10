using DemaConsulting.Speech.Onnx.Kokoro.ModelManagementSubsystem;

namespace DemaConsulting.Speech.Onnx.Kokoro.Tests.ModelManagementSubsystem;

/// <summary>
///     Unit tests for <see cref="KokoroLexiconPhonemizer"/>, the embedded lexicon-backed
///     English text -&gt; IPA phoneme string converter used by
///     <c>OnnxKokoroSynthesisEngine</c>.
/// </summary>
/// <remarks>
///     Expected phoneme strings below (for <c>"cat"</c>, <c>"dog"</c>) were read directly from
///     this package's own embedded <c>Resources/kokoro-en-lexicon.tsv.gz</c> resource, not
///     invented, so these tests fail loudly if that embedded resource ever changes. See this
///     class's own remarks for the documented Stage-1 out-of-vocabulary-word and homograph
///     limitations these tests exercise.
/// </remarks>
public sealed class KokoroLexiconPhonemizerTests
{
    /// <summary>
    ///     Proves that a single known word phonemizes to its embedded lexicon entry with no
    ///     unknown words reported.
    /// </summary>
    [Fact]
    public void Phonemize_KnownWord_ReturnsLexiconPhonemesWithNoUnknownWords()
    {
        // Arrange
        var phonemizer = new KokoroLexiconPhonemizer();

        // Act
        var (phonemes, unknownWords) = phonemizer.Phonemize("cat");

        // Assert
        Assert.Equal("k\u02C8\u00E6t", phonemes);
        Assert.Empty(unknownWords);
    }

    /// <summary>
    ///     Proves that multiple known words are joined by a single collapsed-whitespace space
    ///     character, matching misaki's own sentence normalization.
    /// </summary>
    [Fact]
    public void Phonemize_MultipleKnownWords_JoinsWithSingleSpace()
    {
        // Arrange
        var phonemizer = new KokoroLexiconPhonemizer();

        // Act
        var (phonemes, unknownWords) = phonemizer.Phonemize("cat   dog");

        // Assert: any run of whitespace collapses to exactly one vocabulary space character.
        Assert.Equal("k\u02C8\u00E6t d\u02C8\u0254\u0261", phonemes);
        Assert.Empty(unknownWords);
    }

    /// <summary>
    ///     Proves that word lookup is case-insensitive: an upper-cased known word still resolves
    ///     to the same lexicon entry as its lower-case spelling.
    /// </summary>
    [Fact]
    public void Phonemize_UpperCaseKnownWord_ResolvesSameAsLowerCase()
    {
        // Arrange
        var phonemizer = new KokoroLexiconPhonemizer();

        // Act
        var (phonemes, unknownWords) = phonemizer.Phonemize("CAT");

        // Assert
        Assert.Equal("k\u02C8\u00E6t", phonemes);
        Assert.Empty(unknownWords);
    }

    /// <summary>
    ///     Proves the documented Stage-1 out-of-vocabulary-word limitation: a word absent from
    ///     the embedded lexicon is silently dropped from the phoneme string and reported, in
    ///     lower-case, as an unknown word, rather than guessed at.
    /// </summary>
    [Fact]
    public void Phonemize_OutOfVocabularyWord_DroppedAndReportedAsUnknown()
    {
        // Arrange
        var phonemizer = new KokoroLexiconPhonemizer();

        // Act
        var (phonemes, unknownWords) = phonemizer.Phonemize("Notarealenglishwordxyz");

        // Assert
        Assert.Equal(string.Empty, phonemes);
        Assert.Equal(["notarealenglishwordxyz"], unknownWords);
    }

    /// <summary>
    ///     Proves that a vocabulary-declared punctuation character (a period, here) passes
    ///     through the phoneme string unchanged.
    /// </summary>
    [Fact]
    public void Phonemize_VocabularyPunctuation_PassesThrough()
    {
        // Arrange
        var phonemizer = new KokoroLexiconPhonemizer();

        // Act
        var (phonemes, unknownWords) = phonemizer.Phonemize("cat.");

        // Assert
        Assert.Equal("k\u02C8\u00E6t.", phonemes);
        Assert.Empty(unknownWords);
    }

    /// <summary>
    ///     Proves that a punctuation character not declared in Kokoro's own vocabulary (an
    ///     at-sign, here) is dropped from the phoneme string rather than passed through to a
    ///     token-id lookup that would drop it anyway.
    /// </summary>
    [Fact]
    public void Phonemize_NonVocabularyPunctuation_IsDropped()
    {
        // Arrange
        var phonemizer = new KokoroLexiconPhonemizer();

        // Act
        var (phonemes, unknownWords) = phonemizer.Phonemize("cat@dog");

        // Assert: the unsupported '@' character contributes nothing to the phoneme string.
        Assert.Equal("k\u02C8\u00E6td\u02C8\u0254\u0261", phonemes);
        Assert.Empty(unknownWords);
    }

    /// <summary>
    ///     Proves that an empty input produces an empty phoneme string and no unknown words.
    /// </summary>
    [Fact]
    public void Phonemize_EmptyText_ReturnsEmptyPhonemesAndNoUnknownWords()
    {
        // Arrange
        var phonemizer = new KokoroLexiconPhonemizer();

        // Act
        var (phonemes, unknownWords) = phonemizer.Phonemize(string.Empty);

        // Assert
        Assert.Equal(string.Empty, phonemes);
        Assert.Empty(unknownWords);
    }

    /// <summary>
    ///     Proves that a null input throws <see cref="ArgumentNullException"/> rather than
    ///     reaching the tokenizing regex with an unusable value.
    /// </summary>
    [Fact]
    public void Phonemize_NullText_ThrowsArgumentNullException()
    {
        // Arrange
        var phonemizer = new KokoroLexiconPhonemizer();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => phonemizer.Phonemize(null!));
    }
}
