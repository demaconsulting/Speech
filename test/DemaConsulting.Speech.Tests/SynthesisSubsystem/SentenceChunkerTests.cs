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
    ///     Proves that <see cref="SentenceChunker.Chunk"/> rejects a <c>maxChunkLength</c>
    ///     that is not strictly positive (zero).
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

    /// <summary>
    ///     Proves that a comma inside a short sentence, well under the length budget, still
    ///     becomes its own chunk: secondary clause-boundary splitting is unconditional, not
    ///     gated behind exceeding the length budget.
    /// </summary>
    [Fact]
    public void SentenceChunker_Chunk_ShortSentenceWithComma_SplitsOnCommaEvenUnderBudget()
    {
        // Act
        var chunks = SentenceChunker.Chunk("Hello, world. This is fine.");

        // Assert
        Assert.Equal(["Hello,", "world.", "This is fine."], chunks);
    }

    /// <summary>
    ///     Proves that a semicolon and a colon inside short sentences, well under the length
    ///     budget, also unconditionally split, same as a comma - including a comma or colon that
    ///     sits right next to a numeral but is flanked by a digit on only one side (not part of
    ///     the numeral itself, so it still splits normally).
    /// </summary>
    [Theory]
    [InlineData("Wait; then go.", "Wait;", "then go.")]
    [InlineData("Note: it works.", "Note:", "it works.")]
    [InlineData("I have 5, but need more.", "I have 5,", "but need more.")]
    [InlineData("Section 3: the results.", "Section 3:", "the results.")]
    public void SentenceChunker_Chunk_ShortSentenceWithSemicolonOrColon_SplitsEvenUnderBudget(
        string text, string expectedFirst, string expectedSecond)
    {
        // Act
        var chunks = SentenceChunker.Chunk(text);

        // Assert
        Assert.Equal([expectedFirst, expectedSecond], chunks);
    }

    /// <summary>
    ///     Proves that a genuine sentence-ending period directly followed by a digit with no
    ///     space (e.g. after an abbreviation-like word) still splits normally as a sentence
    ///     boundary, rather than being swallowed by the bare-fraction-decimal exception in
    ///     <see cref="SentenceChunker_Chunk_LeadingDecimalPoint_StaysAttached"/>. This locks in
    ///     the letter-preceded discriminator that makes that exception safe: only a period with
    ///     no letter immediately before it (start-of-text, whitespace, a sign, or a currency
    ///     symbol) is ever treated as part of a numeral.
    /// </summary>
    [Theory]
    [InlineData("Wait.5 more.", "Wait.", "5 more.")]
    [InlineData("See etc.5 for details.", "See etc.", "5 for details.")]
    public void SentenceChunker_Chunk_LetterPrecededPeriodFollowedByDigit_Splits(
        string text, string expectedFirst, string expectedSecond)
    {
        // Act
        var chunks = SentenceChunker.Chunk(text);

        // Assert
        Assert.Equal(2, chunks.Count);
        Assert.Equal(expectedFirst, chunks[0]);
        Assert.Equal(expectedSecond, chunks[1]);
    }

    /// <summary>
    ///     Proves that clause punctuation embedded inside a numeral (a colon in a time, a comma
    ///     as a thousands separator, a decimal point between digits) is never treated as a
    ///     clause boundary, so times and large numbers stay in one chunk rather than being
    ///     split mid-numeral.
    /// </summary>
    [Theory]
    [InlineData("The time is 12:30.", "The time is 12:30.")]
    [InlineData("It costs 1,000 dollars.", "It costs 1,000 dollars.")]
    [InlineData("The code is 12:30:45.", "The code is 12:30:45.")]
    [InlineData("0.5", "0.5")]
    public void SentenceChunker_Chunk_ClausePunctuationEmbeddedInNumeral_StaysAttached(
        string text, string expectedChunk)
    {
        // Act
        var chunks = SentenceChunker.Chunk(text);

        // Assert
        Assert.Equal([expectedChunk], chunks);
    }

    /// <summary>
    ///     Proves that a decimal point with no leading digit to flank it (a bare-fraction
    ///     decimal such as ".5" or "$.99") is never treated as a sentence boundary, so it is not
    ///     dropped as a degenerate, word-less chunk - which would otherwise silence the "point"
    ///     when the number is later spoken (e.g. ".5" reading as "five" instead of
    ///     "point five").
    /// </summary>
    [Theory]
    [InlineData(".5", ".5")]
    [InlineData("It costs $.99.", "It costs $.99.")]
    [InlineData("The value is .5 units.", "The value is .5 units.")]
    public void SentenceChunker_Chunk_LeadingDecimalPoint_StaysAttached(
        string text, string expectedChunk)
    {
        // Act
        var chunks = SentenceChunker.Chunk(text);

        // Assert
        var chunk = Assert.Single(chunks);
        Assert.Equal(expectedChunk, chunk);
    }

    /// <summary>
    ///     Proves that a whitespace-spaced run of degenerate, word-less punctuation pieces (here
    ///     an ellipsis written with spaces between each dot) merges onto the end of the preceding
    ///     chunk rather than each dot becoming its own chunk. This is the exact worked example
    ///     from the reported latency issue.
    /// </summary>
    [Fact]
    public void SentenceChunker_Chunk_SpacedPunctuationRun_MergesIntoPrecedingChunk()
    {
        // Act
        var chunks = SentenceChunker.Chunk("Sentence one . . . . . Sentence two");

        // Assert
        Assert.Equal(["Sentence one . . . . .", "Sentence two"], chunks);
    }

    /// <summary>
    ///     Proves that requirement 2 (merging degenerate, word-less pieces into the preceding
    ///     chunk) applies uniformly to secondary (clause) boundary punctuation, not just primary
    ///     sentence-ending punctuation.
    /// </summary>
    [Fact]
    public void SentenceChunker_Chunk_LoneSecondaryBoundaryWithNoWordContent_MergesIntoPrecedingChunk()
    {
        // Act
        var chunks = SentenceChunker.Chunk("Wait , , stop.");

        // Assert
        Assert.Equal(["Wait , ,", "stop."], chunks);
    }

    /// <summary>
    ///     Proves that a degenerate punctuation run at the very start of the text - with no
    ///     preceding chunk to merge into - is dropped rather than emitted or throwing.
    /// </summary>
    [Fact]
    public void SentenceChunker_Chunk_LeadingDegenerateRun_IsDropped()
    {
        // Act
        var chunks = SentenceChunker.Chunk(". . . Hello.");

        // Assert
        Assert.Equal(["Hello."], chunks);
    }

    /// <summary>
    ///     Proves that a chunk ending in an adjacent ellipsis ("...") is flagged via
    ///     <see cref="SentenceChunker.ChunkWithMetadata"/> as ending in an ellipsis.
    /// </summary>
    [Fact]
    public void SentenceChunker_ChunkWithMetadata_TrailingAdjacentEllipsis_EndsWithEllipsisIsTrue()
    {
        // Act
        var chunks = SentenceChunker.ChunkWithMetadata("And then it happened...");

        // Assert
        var chunk = Assert.Single(chunks);
        Assert.True(chunk.EndsWithEllipsis);
    }

    /// <summary>
    ///     Proves that a chunk ending in a whitespace-spaced ellipsis (". . .") is also flagged
    ///     as ending in an ellipsis, not just the adjacent-dot form.
    /// </summary>
    [Fact]
    public void SentenceChunker_ChunkWithMetadata_TrailingSpacedEllipsis_EndsWithEllipsisIsTrue()
    {
        // Act
        var chunks = SentenceChunker.ChunkWithMetadata("Sentence one . . . . . Sentence two");

        // Assert
        Assert.True(chunks[0].EndsWithEllipsis);
        Assert.False(chunks[1].EndsWithEllipsis);
    }

    /// <summary>
    ///     Proves that an ordinary sentence ending in a single terminator is not flagged as
    ///     ending in an ellipsis, and that exactly two dots (below the three-dot threshold) is
    ///     also not flagged, proving the threshold is exactly <c>&gt;= 3</c>, not <c>&gt;= 2</c>.
    /// </summary>
    [Theory]
    [InlineData("Hello world.")]
    [InlineData("Hello world..")]
    public void SentenceChunker_ChunkWithMetadata_FewerThanThreeDots_EndsWithEllipsisIsFalse(string text)
    {
        // Act
        var chunks = SentenceChunker.ChunkWithMetadata(text);

        // Assert
        var chunk = Assert.Single(chunks);
        Assert.False(chunk.EndsWithEllipsis);
    }

    /// <summary>
    ///     Proves that <see cref="SentenceChunker.Chunk"/> and <see cref="SentenceChunker.ChunkWithMetadata"/>
    ///     always produce the same chunk text in the same order, so <c>Chunk</c> remains a pure
    ///     projection of <c>ChunkWithMetadata</c>.
    /// </summary>
    [Fact]
    public void SentenceChunker_Chunk_AndChunkWithMetadata_ProduceSameChunkText()
    {
        // Arrange
        const string text = "Hello, world! To be or not to be... that is the question. Wait , , stop.";

        // Act
        var chunks = SentenceChunker.Chunk(text);
        var chunksWithMetadata = SentenceChunker.ChunkWithMetadata(text);

        // Assert
        Assert.Equal(chunks, chunksWithMetadata.Select(chunk => chunk.Text));
    }

    /// <summary>
    ///     Proves an annotation inside the second sentence is delivered with an offset relative
    ///     to that chunk, not to the whole text.
    /// </summary>
    [Fact]
    public void SentenceChunker_ChunkAligned_AnnotationWithinSecondSentence_OffsetRelativeToChunk()
    {
        // Arrange: "read" starts at index 18 of the text and index 7 of the second chunk
        const string text = "First one. Second read here.";
        var annotations = new[] { new SegmentAnnotation(18, 4, "reading", "red") };

        // Act
        var chunks = SentenceChunker.ChunkAligned(text, annotations, null);

        // Assert
        Assert.Equal(["First one.", "Second read here."], chunks.Select(chunk => chunk.Text));
        Assert.Empty(chunks[0].Annotations);
        var annotation = Assert.Single(chunks[1].Annotations);
        Assert.Equal(7, annotation.Start);
        Assert.Equal("read", chunks[1].Text.Substring(annotation.Start, annotation.Length));
        Assert.Equal("reading", annotation.Kind);
        Assert.Equal("red", annotation.Value);
    }

    /// <summary>
    ///     Proves mutating the list assigned to <see cref="SentenceChunk.Annotations"/> after
    ///     construction does not change the chunk, and a null element is rejected.
    /// </summary>
    [Fact]
    public void SentenceChunk_Annotations_SourceListMutatedAfterAssignment_ChunkUnchanged()
    {
        // Arrange
        var source = new List<SegmentAnnotation> { new(0, 5, "k", "v") };
        var chunk = new SentenceChunk("Hello world.", false) { Annotations = source };

        // Act
        source.Add(new SegmentAnnotation(6, 5, "k2", "v2"));
        source[0] = new SegmentAnnotation(1, 1, "x", "y");

        // Assert
        var annotation = Assert.Single(chunk.Annotations);
        Assert.Equal(0, annotation.Start);
        Assert.Equal("k", annotation.Kind);
        Assert.False(chunk.Annotations is List<SegmentAnnotation>);
        Assert.Throws<ArgumentException>(() => new SentenceChunk("x", false) { Annotations = [null!] });
        Assert.Throws<ArgumentNullException>(() => new SentenceChunk("x", false) { Annotations = null! });
    }

    /// <summary>
    ///     Proves leading whitespace that chunking trims is accounted for in the offset.
    /// </summary>
    [Fact]
    public void SentenceChunker_ChunkAligned_LeadingWhitespace_OffsetAccountsForTrim()
    {
        // Arrange
        const string text = "   Hello world.";
        var annotations = new[] { new SegmentAnnotation(9, 5, "k", "v") };

        // Act
        var chunk = Assert.Single(SentenceChunker.ChunkAligned(text, annotations, null));

        // Assert
        var annotation = Assert.Single(chunk.Annotations);
        Assert.Equal(6, annotation.Start);
        Assert.Equal("world", chunk.Text.Substring(annotation.Start, annotation.Length));
    }

    /// <summary>
    ///     Proves offsets are mapped through whitespace that a budget split collapses to a single
    ///     space, including an annotation spanning the collapsed whitespace.
    /// </summary>
    [Fact]
    public void SentenceChunker_ChunkAligned_WhitespaceBudgetSplit_AnnotationRebasedThroughCollapsedWhitespace()
    {
        // Arrange: "alpha   beta" (three spaces) rejoins as "alpha beta" in the first chunk
        const string text = "alpha   beta   gamma";
        var annotations = new[]
        {
            new SegmentAnnotation(8, 4, "word", "beta"),
            new SegmentAnnotation(0, 12, "span", "alpha beta"),
            new SegmentAnnotation(15, 5, "word", "gamma"),
        };

        // Act
        var chunks = SentenceChunker.ChunkAligned(text, annotations, null, maxChunkLength: 12);

        // Assert
        Assert.Equal(["alpha beta", "gamma"], chunks.Select(chunk => chunk.Text));
        Assert.Equal(
            ["beta", "alpha beta"],
            chunks[0].Annotations.Select(a => chunks[0].Text.Substring(a.Start, a.Length)));
        var gamma = Assert.Single(chunks[1].Annotations);
        Assert.Equal(0, gamma.Start);
        Assert.Equal(5, gamma.Length);
    }

    /// <summary>
    ///     Proves annotations survive a punctuation-only merge, including one on the merged
    ///     punctuation itself.
    /// </summary>
    [Fact]
    public void SentenceChunker_ChunkAligned_DegenerateMerge_AnnotationBeforeMergedPunctuation_Preserved()
    {
        // Arrange: "Wait , , stop." yields "Wait , ," and "stop."
        const string text = "Wait , , stop.";
        var annotations = new[]
        {
            new SegmentAnnotation(0, 4, "word", "Wait"),
            new SegmentAnnotation(7, 1, "punctuation", "comma"),
            new SegmentAnnotation(9, 4, "word", "stop"),
        };

        // Act
        var chunks = SentenceChunker.ChunkAligned(text, annotations, null);

        // Assert
        Assert.Equal(["Wait , ,", "stop."], chunks.Select(chunk => chunk.Text));
        Assert.Equal(
            ["Wait", ","],
            chunks[0].Annotations.Select(a => chunks[0].Text.Substring(a.Start, a.Length)));
        var stop = Assert.Single(chunks[1].Annotations);
        Assert.Equal("stop", chunks[1].Text.Substring(stop.Start, stop.Length));
    }

    /// <summary>
    ///     Proves an annotation that crosses a chunk boundary is dropped rather than misplaced.
    /// </summary>
    [Fact]
    public void SentenceChunker_ChunkAligned_AnnotationSpanningChunkBoundary_Dropped()
    {
        // Arrange: "one. Sec" crosses the sentence boundary
        const string text = "First one. Second";
        var annotations = new[] { new SegmentAnnotation(6, 8, "k", "v") };

        // Act
        var chunks = SentenceChunker.ChunkAligned(text, annotations, null);

        // Assert
        Assert.Equal(2, chunks.Count);
        Assert.All(chunks, chunk => Assert.Empty(chunk.Annotations));
    }

    /// <summary>
    ///     Proves an annotation that starts on whitespace the chunker trims is dropped.
    /// </summary>
    [Fact]
    public void SentenceChunker_ChunkAligned_AnnotationOnTrimmedWhitespace_Dropped()
    {
        // Arrange: index 6 is the space between the chunks "Hello." and "World."
        const string text = "Hello. World.";
        var annotations = new[]
        {
            new SegmentAnnotation(6, 1, "k", "space"),
            new SegmentAnnotation(6, 3, "k", "startsOnSpace"),
        };

        // Act
        var chunks = SentenceChunker.ChunkAligned(text, annotations, null);

        // Assert
        Assert.All(chunks, chunk => Assert.Empty(chunk.Annotations));
    }

    /// <summary>
    ///     Proves an annotation on a dropped leading degenerate run is dropped, while one on the
    ///     following word is kept.
    /// </summary>
    [Fact]
    public void SentenceChunker_ChunkAligned_LeadingDegenerateRunAnnotation_Dropped()
    {
        // Arrange: ". . . Hello." drops the leading dots; "Hello" starts at index 6
        const string text = ". . . Hello.";
        var annotations = new[]
        {
            new SegmentAnnotation(0, 1, "k", "dot"),
            new SegmentAnnotation(6, 5, "k", "hello"),
        };

        // Act
        var chunk = Assert.Single(SentenceChunker.ChunkAligned(text, annotations, null));

        // Assert
        var annotation = Assert.Single(chunk.Annotations);
        Assert.Equal("hello", annotation.Value);
        Assert.Equal("Hello", chunk.Text.Substring(annotation.Start, annotation.Length));
    }

    /// <summary>
    ///     Proves that on the analyzed path a chunk whose only letter is a supplementary-plane
    ///     character (a surrogate pair) is kept, together with its annotation.
    /// </summary>
    [Fact]
    public void SentenceChunker_ChunkAligned_SurrogatePairLetterChunkWithBreaks_RetainedWithAnnotation()
    {
        // Arrange: U+20000 is a CJK ideograph encoded as two UTF-16 code units
        const string text = "\U00020000 Hello";
        var annotations = new[] { new SegmentAnnotation(0, 2, "k", "v") };

        // Act
        var chunks = SentenceChunker.ChunkAligned(text, annotations, [2]);

        // Assert
        Assert.Equal(["\U00020000", "Hello"], chunks.Select(chunk => chunk.Text));
        var annotation = Assert.Single(chunks[0].Annotations);
        Assert.Equal(0, annotation.Start);
        Assert.Equal(2, annotation.Length);
        Assert.Empty(chunks[1].Annotations);
    }

    /// <summary>
    ///     Proves the annotation-only analysis path (no authoritative breaks) also keeps a
    ///     surrogate-pair letter chunk.
    /// </summary>
    [Fact]
    public void SentenceChunker_ChunkAligned_SurrogatePairLetterChunkWithAnnotationsOnly_Retained()
    {
        // Arrange
        const string text = "\U00020000. Hello";
        var annotations = new[] { new SegmentAnnotation(0, 2, "k", "v") };

        // Act
        var chunks = SentenceChunker.ChunkAligned(text, annotations, null);

        // Assert
        Assert.Equal(["\U00020000.", "Hello"], chunks.Select(chunk => chunk.Text));
        Assert.Single(chunks[0].Annotations);
    }

    /// <summary>
    ///     Pins the deliberately unchanged no-analysis behavior: without annotations or breaks the
    ///     legacy per-code-unit test still treats a surrogate-pair letter as word-less, so the
    ///     output stays identical to the output before analysis existed.
    /// </summary>
    [Fact]
    public void SentenceChunker_Chunk_SurrogatePairLetterChunkNoAnalysis_KeepsLegacyFiltering()
    {
        // Act
        var chunks = SentenceChunker.Chunk("\U00020000. Hello");

        // Assert
        Assert.Equal(["Hello"], chunks);
    }

    /// <summary>
    ///     Guards against over-broadening: an emoji-only chunk is not a letter or digit, so on the
    ///     analyzed path it is still merged into the preceding chunk.
    /// </summary>
    [Fact]
    public void SentenceChunker_ChunkAligned_SurrogatePairEmoji_StillDegenerate()
    {
        // Arrange
        const string text = "Hello \U0001F600";

        // Act
        var chunks = SentenceChunker.ChunkAligned(text, null, [6]);

        // Assert
        Assert.Equal("Hello \U0001F600", Assert.Single(chunks).Text);
    }

    /// <summary>
    ///     Proves an annotation extending past the end of the text is dropped.
    /// </summary>
    [Fact]
    public void SentenceChunker_ChunkAligned_AnnotationOutOfRange_Dropped()
    {
        // Arrange
        var annotations = new[] { new SegmentAnnotation(4, 10, "k", "v") };

        // Act
        var chunk = Assert.Single(SentenceChunker.ChunkAligned("Hello.", annotations, null));

        // Assert
        Assert.Empty(chunk.Annotations);
    }

    /// <summary>
    ///     Proves offsets are UTF-16 code unit indices: an annotation over a surrogate pair keeps
    ///     both code units.
    /// </summary>
    [Fact]
    public void SentenceChunker_ChunkAligned_SurrogatePairAnnotation_UsesCodeUnitOffsets()
    {
        // Arrange: the emoji occupies two code units at index 3 of the second chunk's text
        const string text = "Go. Hi \ud83d\ude00 there.";
        var annotations = new[] { new SegmentAnnotation(7, 2, "emoji", "smile") };

        // Act
        var chunks = SentenceChunker.ChunkAligned(text, annotations, null);

        // Assert
        var annotation = Assert.Single(chunks[1].Annotations);
        Assert.Equal(3, annotation.Start);
        Assert.Equal("\ud83d\ude00", chunks[1].Text.Substring(annotation.Start, annotation.Length));
    }

    /// <summary>
    ///     Proves analyzer sentence breaks replace the punctuation heuristic: "Dr." is not split
    ///     off as a sentence.
    /// </summary>
    [Fact]
    public void SentenceChunker_ChunkAligned_AnalyzerBreaks_OverridePunctuationHeuristic()
    {
        // Arrange
        const string text = "Dr. Smith arrived. He left.";

        // Act
        var chunks = SentenceChunker.ChunkAligned(text, null, [18]);

        // Assert: the heuristic alone gives three chunks, because it splits off the "Dr." abbreviation
        Assert.Equal(["Dr. Smith arrived.", "He left."], chunks.Select(chunk => chunk.Text));
        Assert.Equal(["Dr.", "Smith arrived.", "He left."], SentenceChunker.Chunk(text));
    }

    /// <summary>
    ///     Proves an analyzer can split a sentence where there is no punctuation at all.
    /// </summary>
    [Fact]
    public void SentenceChunker_ChunkAligned_AnalyzerBreaks_SplitWithoutPunctuation()
    {
        // Act
        var chunks = SentenceChunker.ChunkAligned("one two", null, [4]);

        // Assert
        Assert.Equal(["one", "two"], chunks.Select(chunk => chunk.Text));
    }

    /// <summary>
    ///     Proves an empty (but non-null) break list means no sentence splits, while clause
    ///     splitting still applies.
    /// </summary>
    [Fact]
    public void SentenceChunker_ChunkAligned_EmptyBreaksList_NoPrimarySplitButClauseSplitStillApplies()
    {
        // Act
        var noSplit = SentenceChunker.ChunkAligned("A. B.", null, []);
        var clauseSplit = SentenceChunker.ChunkAligned("Hello, world. Again.", null, []);

        // Assert
        Assert.Equal(["A. B."], noSplit.Select(chunk => chunk.Text));
        Assert.Equal(["Hello,", "world. Again."], clauseSplit.Select(chunk => chunk.Text));
    }

    /// <summary>
    ///     Proves breaks at the very start or end of the text, and beyond it, are ignored, as are
    ///     breaks inside trimmed leading whitespace.
    /// </summary>
    [Fact]
    public void SentenceChunker_ChunkAligned_BreaksOutOfRangeIgnored()
    {
        // Act
        var outside = SentenceChunker.ChunkAligned("Hello world.", null, [0, 12, 99, 500]);
        var leading = SentenceChunker.ChunkAligned("  Hi there", null, [1, 2]);

        // Assert
        Assert.Equal(["Hello world."], outside.Select(chunk => chunk.Text));
        Assert.Equal(["Hi there"], leading.Select(chunk => chunk.Text));
    }

    /// <summary>
    ///     Proves the clause pass still splits at a comma inside an analyzer-defined sentence.
    /// </summary>
    [Fact]
    public void SentenceChunker_ChunkAligned_AnalyzerBreaks_ClauseCommaStillSplits()
    {
        // Act
        var chunks = SentenceChunker.ChunkAligned("Hello, world. Bye now", null, [13]);

        // Assert
        Assert.Equal(["Hello,", "world.", "Bye now"], chunks.Select(chunk => chunk.Text));
    }

    /// <summary>
    ///     Proves analyzer breaks and annotations work together: the annotation lands in the
    ///     sentence the analyzer defined, relative to that chunk.
    /// </summary>
    [Fact]
    public void SentenceChunker_ChunkAligned_AnalyzerBreaksAndAnnotations_AnnotationRelativeToAnalyzerSentence()
    {
        // Arrange: "He" starts at index 19
        const string text = "Dr. Smith arrived. He left.";
        var annotations = new[] { new SegmentAnnotation(19, 2, "pos", "PRON") };

        // Act
        var chunks = SentenceChunker.ChunkAligned(text, annotations, [18]);

        // Assert
        var annotation = Assert.Single(chunks[1].Annotations);
        Assert.Equal(0, annotation.Start);
        Assert.Equal("He", chunks[1].Text.Substring(annotation.Start, annotation.Length));
    }

    /// <summary>
    ///     Proves that with no annotations and no breaks the aligned entry point returns exactly
    ///     what <see cref="SentenceChunker.ChunkWithMetadata"/> returns, over the whole corpus.
    /// </summary>
    [Fact]
    public void SentenceChunker_ChunkAligned_NoAnnotationsNoBreaks_EqualsChunkWithMetadata()
    {
        foreach (var (input, max) in GoldenCorpus)
        {
            // Act
            var aligned = SentenceChunker.ChunkAligned(input, [], null, max);
            var plain = SentenceChunker.ChunkWithMetadata(input, max);

            // Assert
            Assert.Equal(plain, aligned);
        }
    }

    /// <summary>
    ///     Proves a null text is rejected by the aligned entry point.
    /// </summary>
    [Fact]
    public void SentenceChunker_ChunkAligned_NullText_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => SentenceChunker.ChunkAligned(null!, null, null));
    }

    /// <summary>
    ///     Proves a non-positive length budget is rejected by the aligned entry point.
    /// </summary>
    [Fact]
    public void SentenceChunker_ChunkAligned_NonPositiveMaxLength_ThrowsArgumentOutOfRangeException()
    {
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => SentenceChunker.ChunkAligned("text", null, null, 0));
    }

    /// <summary>
    ///     Proves a default <see cref="SentenceChunk"/> struct reports no annotations, and that
    ///     setting an empty list is equal to leaving it unset.
    /// </summary>
    [Fact]
    public void SentenceChunk_Annotations_DefaultStruct_IsEmpty()
    {
        // Arrange
        var unset = default(SentenceChunk);
        var explicitEmpty = new SentenceChunk("x", false) { Annotations = [] };
        var plain = new SentenceChunk("x", false);

        // Act & Assert
        Assert.Empty(unset.Annotations);
        Assert.Equal(plain, explicitEmpty);
        Assert.Throws<ArgumentNullException>(() => plain with { Annotations = null! });
    }

    /// <summary>
    ///     The representative corpus used to pin the chunker's unannotated output. Every entry is
    ///     paired, by index, with an entry of <c>GoldenExpected</c>.
    /// </summary>
    internal static readonly (string Input, int MaxChunkLength)[] GoldenCorpus =
    [
        ("Hello world.", SentenceChunker.DefaultMaxChunkLength),
        ("First sentence. Second sentence! Third sentence?", SentenceChunker.DefaultMaxChunkLength),
        ("To be or not to be... that is the question.", SentenceChunker.DefaultMaxChunkLength),
        ("Really?! Are you sure?", SentenceChunker.DefaultMaxChunkLength),
        ("Stop!! Right there.", SentenceChunker.DefaultMaxChunkLength),
        ("Hello. World.", SentenceChunker.DefaultMaxChunkLength),
        ("And then it happened...", SentenceChunker.DefaultMaxChunkLength),
        ("This is a long clause, and this is another long clause.", 30),
        ("one two three four five six seven eight nine ten", 15),
        ("supercalifragilisticexpialidocious", 5),
        ("Hello, world. This is fine.", SentenceChunker.DefaultMaxChunkLength),
        ("Wait; then go.", SentenceChunker.DefaultMaxChunkLength),
        ("Note: it works.", SentenceChunker.DefaultMaxChunkLength),
        ("I have 5, but need more.", SentenceChunker.DefaultMaxChunkLength),
        ("Section 3: the results.", SentenceChunker.DefaultMaxChunkLength),
        ("Wait.5 more.", SentenceChunker.DefaultMaxChunkLength),
        ("See etc.5 for details.", SentenceChunker.DefaultMaxChunkLength),
        ("The time is 12:30.", SentenceChunker.DefaultMaxChunkLength),
        ("It costs 1,000 dollars.", SentenceChunker.DefaultMaxChunkLength),
        ("The code is 12:30:45.", SentenceChunker.DefaultMaxChunkLength),
        ("0.5", SentenceChunker.DefaultMaxChunkLength),
        (".5", SentenceChunker.DefaultMaxChunkLength),
        ("It costs $.99.", SentenceChunker.DefaultMaxChunkLength),
        ("The value is .5 units.", SentenceChunker.DefaultMaxChunkLength),
        ("Sentence one . . . . . Sentence two", SentenceChunker.DefaultMaxChunkLength),
        ("Wait , , stop.", SentenceChunker.DefaultMaxChunkLength),
        (". . . Hello.", SentenceChunker.DefaultMaxChunkLength),
        ("Hello, world! To be or not to be... that is the question. Wait , , stop.", SentenceChunker.DefaultMaxChunkLength),
        ("Hello world..", SentenceChunker.DefaultMaxChunkLength),
        ("  \t leading and trailing whitespace.  \r\n ", SentenceChunker.DefaultMaxChunkLength),
        ("Line one.\nLine two.\r\nLine three.\tTab here.", SentenceChunker.DefaultMaxChunkLength),
        ("alpha   beta    gamma   delta   epsilon   zeta", 14),
        ("alpha   beta, gamma    delta   epsilon   zeta. eta   theta   iota", 12),
        (",leading comma then words", SentenceChunker.DefaultMaxChunkLength),
        ("... then it stopped", SentenceChunker.DefaultMaxChunkLength),
        ("trailing degenerate .", SentenceChunker.DefaultMaxChunkLength),
        ("Dr. Smith arrived. He left.", SentenceChunker.DefaultMaxChunkLength),
        ("Version 3.14 is out. Price: $1,000.50, tax extra; total 12:30:45 today!", SentenceChunker.DefaultMaxChunkLength),
        ("A.5 and B,5 and C:5 and 1.5 and 2,5 and 3:5", SentenceChunker.DefaultMaxChunkLength),
        ("x 1,", SentenceChunker.DefaultMaxChunkLength),
        ("5.", SentenceChunker.DefaultMaxChunkLength),
        ("7, 8, 9; 10: eleven", SentenceChunker.DefaultMaxChunkLength),
        ("Yes? No! Maybe... Perhaps?! Surely!!! Fine.", SentenceChunker.DefaultMaxChunkLength),
        ("a b c d e f g h i j k l m n o p q r s t u v w x y z", 7),
        ("one two three. four five six, seven eight nine; ten eleven twelve", 10),
        ("short , , , words ; ; here", SentenceChunker.DefaultMaxChunkLength),
        ("What is this ... ? ! really", SentenceChunker.DefaultMaxChunkLength),
        ("Caf\u00e9 cr\u00e8me, na\u00ef" + "ve fa\u00e7ade. \ud83d\ude00 smile, \ud83d\ude00 again.", SentenceChunker.DefaultMaxChunkLength),
        ("!!!", SentenceChunker.DefaultMaxChunkLength),
        (",", SentenceChunker.DefaultMaxChunkLength),
        ("word", SentenceChunker.DefaultMaxChunkLength),
        (string.Join(" ", Enumerable.Repeat("word", 600)), SentenceChunker.DefaultMaxChunkLength),
        (string.Join(". ", Enumerable.Range(0, 120).Select(i => $"Sentence number {i}, with a clause")), SentenceChunker.DefaultMaxChunkLength),
        (string.Join("   ", Enumerable.Repeat("spaced", 90)) + ".", SentenceChunker.DefaultMaxChunkLength),
    ];

    /// <summary>
    ///     The chunk texts, ellipsis flags (<c>'1'</c> = ends with ellipsis), optional digest and chunk count
    ///     the implementation produced for each <c>GoldenCorpus</c> entry BEFORE the position-aware
    ///     refactor, captured verbatim from that implementation. A flags value of <c>"*"</c> means the
    ///     expectation is the SHA-256 digest of the canonical rendering (texts joined by newline, then
    ///     <c>|</c> and the flag characters) because the output is too long to list.
    /// </summary>
    private static readonly (string[] Chunks, string Flags, string? Digest, int Count)[] GoldenExpected =
    [
        (["Hello world."], "0", null, 1),
        (["First sentence.", "Second sentence!", "Third sentence?"], "000", null, 3),
        (["To be or not to be...", "that is the question."], "10", null, 2),
        (["Really?!", "Are you sure?"], "00", null, 2),
        (["Stop!!", "Right there."], "00", null, 2),
        (["Hello.", "World."], "00", null, 2),
        (["And then it happened..."], "1", null, 1),
        (["This is a long clause,", "and this is another long", "clause."], "000", null, 3),
        (["one two three", "four five six", "seven eight", "nine ten"], "0000", null, 4),
        (["supercalifragilisticexpialidocious"], "0", null, 1),
        (["Hello,", "world.", "This is fine."], "000", null, 3),
        (["Wait;", "then go."], "00", null, 2),
        (["Note:", "it works."], "00", null, 2),
        (["I have 5,", "but need more."], "00", null, 2),
        (["Section 3:", "the results."], "00", null, 2),
        (["Wait.", "5 more."], "00", null, 2),
        (["See etc.", "5 for details."], "00", null, 2),
        (["The time is 12:30."], "0", null, 1),
        (["It costs 1,000 dollars."], "0", null, 1),
        (["The code is 12:30:45."], "0", null, 1),
        (["0.5"], "0", null, 1),
        ([".5"], "0", null, 1),
        (["It costs $.99."], "0", null, 1),
        (["The value is .5 units."], "0", null, 1),
        (["Sentence one . . . . .", "Sentence two"], "10", null, 2),
        (["Wait , ,", "stop."], "00", null, 2),
        (["Hello."], "0", null, 1),
        (["Hello,", "world!", "To be or not to be...", "that is the question.", "Wait , ,", "stop."], "001000", null, 6),
        (["Hello world.."], "0", null, 1),
        (["leading and trailing whitespace."], "0", null, 1),
        (["Line one.", "Line two.", "Line three.", "Tab here."], "0000", null, 4),
        (["alpha beta", "gamma delta", "epsilon zeta"], "000", null, 3),
        (["alpha beta,", "gamma delta", "epsilon", "zeta.", "eta theta", "iota"], "000000", null, 6),
        (["leading comma then words"], "0", null, 1),
        (["then it stopped"], "0", null, 1),
        (["trailing degenerate ."], "0", null, 1),
        (["Dr.", "Smith arrived.", "He left."], "000", null, 3),
        (["Version 3.14 is out.", "Price:", "$1,000.50,", "tax extra;", "total 12:30:45 today!"], "00000", null, 5),
        (["A.", "5 and B,", "5 and C:", "5 and 1.5 and 2,5 and 3:5"], "0000", null, 4),
        (["x 1,"], "0", null, 1),
        (["5."], "0", null, 1),
        (["7,", "8,", "9;", "10:", "eleven"], "00000", null, 5),
        (["Yes?", "No!", "Maybe...", "Perhaps?!", "Surely!!!", "Fine."], "001000", null, 6),
        (["a b c d", "e f g h", "i j k l", "m n o p", "q r s t", "u v w x", "y z"], "0000000", null, 7),
        (["one two", "three.", "four five", "six,", "seven", "eight", "nine;", "ten eleven", "twelve"], "000000000", null, 9),
        (["short , , ,", "words ; ;", "here"], "000", null, 3),
        (["What is this ... ? !", "really"], "00", null, 2),
        (["Caf\u00e9 cr\u00e8me,", "na\u00ef" + "ve fa\u00e7ade.", "\ud83d\ude00 smile,", "\ud83d\ude00 again."], "0000", null, 4),
        ([], "", null, 0),
        ([], "", null, 0),
        (["word"], "0", null, 1),
        ([], "*", "CB478337CD73C55E05C83CC74381E42165FB4800B51604E290323AD7CE439486", 15),
        ([], "*", "8A59295A04576B45F7AAE03549C9138FF83C2A718FA075E048BA4FE27DB983D2", 240),
        ([], "*", "76CF5EF36F264D64D62D17D26785DEA48A1E3EBB38138F12D354553160672BD6", 4),
    ];

    /// <summary>
    ///     Proves the position-aware chunker refactor is byte-for-byte output-compatible with the
    ///     pre-refactor implementation: every corpus entry yields exactly the chunk texts and
    ///     ellipsis flags recorded from the original code.
    /// </summary>
    [Fact]
    public void SentenceChunker_Chunk_RepresentativeCorpus_MatchesGoldenChunks()
    {
        // Arrange
        Assert.Equal(GoldenCorpus.Length, GoldenExpected.Length);

        for (var i = 0; i < GoldenCorpus.Length; i++)
        {
            var (input, max) = GoldenCorpus[i];
            var expected = GoldenExpected[i];

            // Act
            var actual = SentenceChunker.ChunkWithMetadata(input, max);

            // Assert
            Assert.Equal(expected.Count, actual.Count);
            var actualFlags = new string(actual.Select(c => c.EndsWithEllipsis ? '1' : '0').ToArray());
            if (expected.Digest is null)
            {
                Assert.Equal(expected.Chunks, actual.Select(c => c.Text));
                Assert.Equal(expected.Flags, actualFlags);
            }
            else
            {
                var canonical = string.Join("\n", actual.Select(c => c.Text)) + "|" + actualFlags;
                var digest = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(canonical)));
                Assert.Equal(expected.Digest, digest);
            }

            // The plain-text projection must agree with the metadata form
            Assert.Equal(actual.Select(c => c.Text), SentenceChunker.Chunk(input, max));
        }
    }

}
