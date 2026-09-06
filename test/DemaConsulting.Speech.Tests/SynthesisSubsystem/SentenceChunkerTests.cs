using DemaConsulting.Speech.SynthesisSubsystem;

namespace DemaConsulting.Speech.Tests.SynthesisSubsystem;

/// <summary>
///     Unit tests for <see cref="SentenceChunker"/>.
/// </summary>
public class SentenceChunkerTests
{
    /// <summary>
    ///     Proves that empty or whitespace-only text produces no chunks.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void SentenceChunker_Chunk_EmptyOrWhitespace_ReturnsEmpty(string text)
    {
        // Act
        var chunks = SentenceChunker.Chunk(text);

        // Assert
        Assert.Empty(chunks);
    }

    /// <summary>
    ///     Proves that short text under the length budget is returned as a single chunk with
    ///     sentence punctuation attached.
    /// </summary>
    [Fact]
    public void SentenceChunker_Chunk_ShortSingleSentence_ReturnsOneChunk()
    {
        // Act
        var chunks = SentenceChunker.Chunk("Hello world.");

        // Assert
        Assert.Equal(["Hello world."], chunks);
    }

    /// <summary>
    ///     Proves that multiple sentences under the length budget each become their own chunk.
    /// </summary>
    [Fact]
    public void SentenceChunker_Chunk_MultipleSentences_SplitsOnPrimaryBoundaries()
    {
        // Act
        var chunks = SentenceChunker.Chunk("First sentence. Second sentence! Third sentence?");

        // Assert
        Assert.Equal(["First sentence.", "Second sentence!", "Third sentence?"], chunks);
    }

    /// <summary>
    ///     Proves that an ellipsis mid-sentence is treated as a single boundary, staying attached
    ///     to the sentence that precedes it, rather than being split into degenerate
    ///     single-punctuation-character chunks (one per '.'). This is the exact sentence reported
    ///     against the Kokoro model.
    /// </summary>
    [Fact]
    public void SentenceChunker_Chunk_MidSentenceEllipsis_StaysAttachedAsSingleChunk()
    {
        // Act
        var chunks = SentenceChunker.Chunk("To be or not to be... that is the question.");

        // Assert
        Assert.Equal(["To be or not to be...", "that is the question."], chunks);
    }

    /// <summary>
    ///     Proves that mixed consecutive terminal punctuation (e.g. "?!") is treated as a single
    ///     boundary, not split into separate single-character chunks.
    /// </summary>
    [Fact]
    public void SentenceChunker_Chunk_MixedConsecutiveTerminators_StaysAttachedAsSingleChunk()
    {
        // Act
        var chunks = SentenceChunker.Chunk("Really?! Are you sure?");

        // Assert
        Assert.Equal(["Really?!", "Are you sure?"], chunks);
    }

    /// <summary>
    ///     Proves that repeated terminal punctuation (e.g. "!!") is treated as a single boundary,
    ///     not split into separate single-character chunks.
    /// </summary>
    [Fact]
    public void SentenceChunker_Chunk_RepeatedExclamations_StaysAttachedAsSingleChunk()
    {
        // Act
        var chunks = SentenceChunker.Chunk("Stop!! Right there.");

        // Assert
        Assert.Equal(["Stop!!", "Right there."], chunks);
    }

    /// <summary>
    ///     Proves that a normal single-terminator sentence is completely unaffected by the
    ///     consecutive-boundary-run handling (no regression).
    /// </summary>
    [Fact]
    public void SentenceChunker_Chunk_SingleTerminators_NoRegression()
    {
        // Act
        var chunks = SentenceChunker.Chunk("Hello. World.");

        // Assert
        Assert.Equal(["Hello.", "World."], chunks);
    }

    /// <summary>
    ///     Proves that an ellipsis at the very end of the text is handled correctly, with no
    ///     trailing empty or degenerate chunk produced.
    /// </summary>
    [Fact]
    public void SentenceChunker_Chunk_TrailingEllipsis_NoDegenerateTrailingChunk()
    {
        // Act
        var chunks = SentenceChunker.Chunk("And then it happened...");

        // Assert
        Assert.Equal(["And then it happened..."], chunks);
    }

    /// <summary>
    ///     Proves that a single sentence longer than the length budget is split further on clause
    ///     punctuation.
    /// </summary>
    [Fact]
    public void SentenceChunker_Chunk_LongSentenceWithClauses_SplitsOnSecondaryBoundaries()
    {
        // Arrange: one long sentence with two clauses, forced over a tiny budget
        const string text = "This is a long clause, and this is another long clause.";

        // Act
        var chunks = SentenceChunker.Chunk(text, maxChunkLength: 30);

        // Assert: the first chunk splits precisely at the comma, and every chunk reconstructs
        // the original text in order with no chunk exceeding the budget by more than one word
        Assert.Equal("This is a long clause,", chunks[0]);
        Assert.Equal(text, string.Join(' ', chunks));
    }

    /// <summary>
    ///     Proves that a single clause still too long after both punctuation passes is split on
    ///     whitespace boundaries, never exceeding the budget by more than one word.
    /// </summary>
    [Fact]
    public void SentenceChunker_Chunk_LongClauseWithNoPunctuation_SplitsOnWhitespaceBudget()
    {
        // Arrange: one long run of words with no sentence or clause punctuation at all
        const string text = "one two three four five six seven eight nine ten";

        // Act
        var chunks = SentenceChunker.Chunk(text, maxChunkLength: 15);

        // Assert: every chunk reconstructs to the original words in order
        Assert.True(chunks.Count > 1);
        Assert.Equal(text, string.Join(' ', chunks));
    }

    /// <summary>
    ///     Proves that a single word longer than the budget is still returned whole rather than
    ///     broken mid-word.
    /// </summary>
    [Fact]
    public void SentenceChunker_Chunk_SingleWordExceedsBudget_ReturnsWholeWord()
    {
        // Arrange
        const string longWord = "supercalifragilisticexpialidocious";

        // Act
        var chunks = SentenceChunker.Chunk(longWord, maxChunkLength: 5);

        // Assert
        Assert.Equal([longWord], chunks);
    }

    /// <summary>
    ///     Proves that <see cref="SentenceChunker.Chunk"/> rejects a null <c>maxChunkLength</c>
    ///     that is not strictly positive.
    /// </summary>
    [Fact]
    public void SentenceChunker_Chunk_NonPositiveMaxLength_ThrowsArgumentOutOfRangeException()
    {
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => SentenceChunker.Chunk("text", maxChunkLength: 0));
    }

    /// <summary>
    ///     Proves that <see cref="SentenceChunker.Chunk"/> rejects a null text argument.
    /// </summary>
    [Fact]
    public void SentenceChunker_Chunk_NullText_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => SentenceChunker.Chunk(null!));
    }
}
