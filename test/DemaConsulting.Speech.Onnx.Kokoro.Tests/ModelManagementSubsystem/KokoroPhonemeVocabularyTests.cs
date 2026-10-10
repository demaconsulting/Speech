using DemaConsulting.Speech.Onnx.Kokoro.ModelManagementSubsystem;

namespace DemaConsulting.Speech.Onnx.Kokoro.Tests.ModelManagementSubsystem;

/// <summary>
///     Unit tests for <see cref="KokoroPhonemeVocabulary"/>, the embedded
///     <c>tokenizer.json</c>-backed character-to-token-id vocabulary shared by
///     <see cref="OnnxKokoroEnglishSynthesisModel"/> and
///     <c>OnnxKokoroSynthesisEngine</c>.
/// </summary>
/// <remarks>
///     Token ids asserted below (<c>'a'</c> = 43, <c>'z'</c> = 68) were confirmed directly
///     against this package's own embedded <c>Resources/kokoro-v1.0-tokenizer.json</c> resource,
///     not assumed; <c>'B'</c> was confirmed absent from that same vocabulary, standing in for a
///     phonemizer-produced character the model has no embedding for.
/// </remarks>
public sealed class KokoroPhonemeVocabularyTests
{
    /// <summary>
    ///     Proves that known, in-vocabulary characters convert to their confirmed token ids, in
    ///     order.
    /// </summary>
    [Fact]
    public void ToTokenIds_KnownChars_ReturnsConfirmedIdsInOrder()
    {
        // Arrange
        var vocabulary = new KokoroPhonemeVocabulary();

        // Act
        var ids = vocabulary.ToTokenIds("az");

        // Assert
        Assert.Equal([43, 68], ids);
    }

    /// <summary>
    ///     Proves that a character absent from the embedded vocabulary is silently dropped rather
    ///     than throwing or inserting a placeholder id.
    /// </summary>
    [Fact]
    public void ToTokenIds_UnknownChar_IsSkipped()
    {
        // Arrange
        var vocabulary = new KokoroPhonemeVocabulary();

        // Act
        var ids = vocabulary.ToTokenIds("aBz");

        // Assert: 'B' is not in the vocabulary and must be dropped, not substituted.
        Assert.Equal([43, 68], ids);
    }

    /// <summary>
    ///     Proves that an empty phoneme string returns an empty token id list rather than
    ///     throwing.
    /// </summary>
    [Fact]
    public void ToTokenIds_EmptyString_ReturnsEmptyList()
    {
        // Arrange
        var vocabulary = new KokoroPhonemeVocabulary();

        // Act
        var ids = vocabulary.ToTokenIds(string.Empty);

        // Assert
        Assert.Empty(ids);
    }

    /// <summary>
    ///     Proves that a null phoneme string throws <see cref="ArgumentNullException"/> rather
    ///     than reaching the per-character loop with an unusable value.
    /// </summary>
    [Fact]
    public void ToTokenIds_NullPhonemes_ThrowsArgumentNullException()
    {
        // Arrange
        var vocabulary = new KokoroPhonemeVocabulary();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => vocabulary.ToTokenIds(null!));
    }

    /// <summary>
    ///     Proves that a phoneme string longer than <see cref="KokoroPhonemeVocabulary.MaxPhonemeTokens"/>
    ///     is truncated to exactly that many token ids, never padded with
    ///     <see cref="KokoroPhonemeVocabulary.PadTokenId"/> by this method itself (the caller adds
    ///     the leading/trailing pad token).
    /// </summary>
    [Fact]
    public void ToTokenIds_LongerThanMaxPhonemeTokens_IsTruncated()
    {
        // Arrange
        var vocabulary = new KokoroPhonemeVocabulary();
        var phonemes = new string('a', KokoroPhonemeVocabulary.MaxPhonemeTokens + 50);

        // Act
        var ids = vocabulary.ToTokenIds(phonemes);

        // Assert
        Assert.Equal(KokoroPhonemeVocabulary.MaxPhonemeTokens, ids.Count);
    }

    /// <summary>
    ///     Proves that <see cref="KokoroPhonemeVocabulary.PadTokenId"/> is the constant value
    ///     <see cref="OnnxKokoroEnglishSynthesisModel"/>'s padding logic depends on.
    /// </summary>
    [Fact]
    public void PadTokenId_IsZero()
    {
        // Arrange & Act & Assert
        Assert.Equal(0, KokoroPhonemeVocabulary.PadTokenId);
    }
}
