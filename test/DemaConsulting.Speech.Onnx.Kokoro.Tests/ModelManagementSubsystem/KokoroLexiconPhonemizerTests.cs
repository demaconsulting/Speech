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
    ///     Proves <see cref="KokoroLexiconPhonemizer.Phonemize"/>'s documented distinctness
    ///     guarantee: the same out-of-vocabulary word, even differing only by case, is reported
    ///     exactly once in <c>unknownWords</c>, in first-seen order, rather than once per
    ///     occurrence in the input text.
    /// </summary>
    [Fact]
    public void Phonemize_RepeatedOutOfVocabularyWord_ReportedOnceInFirstSeenOrder()
    {
        // Arrange
        var phonemizer = new KokoroLexiconPhonemizer();

        // Act
        var (_, unknownWords) = phonemizer.Phonemize(
            "Notarealenglishwordxyz NOTAREALENGLISHWORDXYZ anothernotarealword Notarealenglishwordxyz");

        // Assert: each distinct out-of-vocabulary word (compared case-insensitively, since both
        // are reported lower-cased) appears exactly once, in the order first encountered.
        Assert.Equal(["notarealenglishwordxyz", "anothernotarealword"], unknownWords);
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

        // Assert: the unsupported '@' character is dropped but still separates the two words.
        Assert.Equal("k\u02C8\u00E6t d\u02C8\u0254\u0261", phonemes);
        Assert.Empty(unknownWords);
    }

    /// <summary>Misspellings, missing apostrophes, acronyms and numbers are all spoken.</summary>
    [Fact]
    public void Phonemize_MisspellingsAcronymsAndNumbers_AreSpoken()
    {
        // Arrange
        var phonemizer = new KokoroLexiconPhonemizer();

        // Act
        var (typo, typoUnknown) = phonemizer.Phonemize("teh recieve dont");
        var (fixedText, _) = phonemizer.Phonemize("the receive don't");
        var (acronym, acronymUnknown) = phonemizer.Phonemize("FBI");
        var (letters, _) = phonemizer.Phonemize("F B I");
        var (number, numberUnknown) = phonemizer.Phonemize("$5");
        var (words, _) = phonemizer.Phonemize("five dollars");
        var (markdown, _) = phonemizer.Phonemize("**bold** `code`");
        var (plain, _) = phonemizer.Phonemize("bold code");

        // Assert
        Assert.Equal(fixedText, typo);
        Assert.Empty(typoUnknown);
        Assert.NotEmpty(acronym);
        Assert.Empty(acronymUnknown);
        Assert.Equal(letters.Replace(" ", string.Empty, StringComparison.Ordinal), acronym.Replace(" ", string.Empty, StringComparison.Ordinal));
        Assert.Empty(numberUnknown);
        Assert.Equal(words, number);
        Assert.Equal(plain, markdown);
    }

    /// <summary>Every letter-name phoneme is made of characters the Kokoro vocabulary knows.</summary>
    [Fact]
    public void Phonemize_AcronymLetters_UseOnlyVocabularyCharacters()
    {
        // Arrange
        var phonemizer = new KokoroLexiconPhonemizer();
        var alphabet = string.Concat(Enumerable.Range('A', 26).Select(c => (char)c));

        // Act
        var (phonemes, _) = phonemizer.Phonemize(alphabet);

        // Assert
        Assert.Equal(phonemes.Length, new KokoroPhonemeVocabulary().ToTokenIds(phonemes).Count);
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

    /// <summary>
    ///     Proves that three consecutive periods are emitted as the single ellipsis character
    ///     Kokoro was trained on, rather than three separate period tokens.
    /// </summary>
    [Fact]
    public void Phonemize_ThreePeriods_BecomeSingleEllipsisToken()
    {
        // Arrange
        var phonemizer = new KokoroLexiconPhonemizer();

        // Act
        var (phonemes, unknown) = phonemizer.Phonemize("I know...");

        // Assert
        Assert.Empty(unknown);
        Assert.EndsWith("\u2026", phonemes, StringComparison.Ordinal);
        Assert.DoesNotContain(".", phonemes, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves that common contractions (with straight or typographic apostrophes) are found in
    ///     the embedded lexicon rather than reported as unknown words.
    /// </summary>
    [Theory]
    [InlineData("I don't know")]
    [InlineData("it\u2019s fine, we can't go")]
    [InlineData("I'm sure they won't")]
    public void Phonemize_CommonContractions_AreRecognized(string text)
    {
        // Arrange
        var phonemizer = new KokoroLexiconPhonemizer();

        // Act
        var (phonemes, unknown) = phonemizer.Phonemize(text);

        // Assert
        Assert.Empty(unknown);
        Assert.NotEmpty(phonemes);
    }

    /// <summary>
    ///     Proves that text made only of unknown words yields an empty phoneme string rather than
    ///     leftover whitespace.
    /// </summary>
    [Fact]
    public void Phonemize_OnlyUnknownWords_ReturnsEmptyPhonemes()
    {
        // Arrange
        var phonemizer = new KokoroLexiconPhonemizer();

        // Act
        var (phonemes, unknown) = phonemizer.Phonemize("notarealenglishwordxyz anothernotarealword");

        // Assert
        Assert.Equal(string.Empty, phonemes);
        Assert.Equal(2, unknown.Count);
    }
}
