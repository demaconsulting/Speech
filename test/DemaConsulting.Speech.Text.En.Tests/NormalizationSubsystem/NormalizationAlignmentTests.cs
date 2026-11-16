using DemaConsulting.Speech.Text.En.NormalizationSubsystem;

namespace DemaConsulting.Speech.Text.En.Tests.NormalizationSubsystem;

/// <summary>
///     Unit tests for the alignment model: <see cref="AlignedTextBuilder"/> (which enforces the
///     alignment contract on every span and break edit), <see cref="AlignedText"/> (the immutable
///     result with its partition, spoken-text application and break reconciliation) and the two
///     public vocabularies <see cref="NormalizationKind"/> and <see cref="CliticKind"/>.
/// </summary>
public sealed class NormalizationAlignmentTests
{
    /// <summary>Proves a span that overlaps, precedes the previous span, is empty or leaves the text is rejected.</summary>
    /// <param name="start">The start of the second span.</param>
    /// <param name="length">The length of the second span.</param>
    [Theory]
    [InlineData(2, 3)]
    [InlineData(0, 2)]
    [InlineData(6, 0)]
    [InlineData(6, -1)]
    [InlineData(-1, 2)]
    [InlineData(6, 99)]
    public void AlignedTextBuilder_Add_SpanOutOfOrderOverlappingEmptyOrOutOfBounds_ThrowsArgumentException(int start, int length)
    {
        // Arrange: one accepted span at [1,4) of "abcdefghij"
        var builder = new AlignedTextBuilder("abcdefghij");
        builder.Add(new NormalizedSpan(1, 3, "x", NormalizationKind.Abbreviation));

        // Act / Assert
        Assert.Throws<ArgumentException>(() => builder.Add(new NormalizedSpan(start, length, "y", NormalizationKind.Abbreviation)));
    }

    /// <summary>Proves a span is told to touch the previous one only when it ends exactly at the start and its spoken text ends with a letter.</summary>
    [Fact]
    public void AlignedTextBuilder_TouchesPreviousSpokenLetter_ReportsAdjacencyOnlyAfterLetterEndedSpan()
    {
        // Arrange: no span yet
        var builder = new AlignedTextBuilder("ab\u2122\u00AE cd \u2022\u00A9 ef");
        Assert.False(builder.TouchesPreviousSpokenLetter(0));

        // Act / Assert: a letter-ended replacement ending at index 3 touches a span starting there, but not one after a gap
        builder.Add(new NormalizedSpan(2, 1, " trademark", NormalizationKind.Symbol));
        Assert.True(builder.TouchesPreviousSpokenLetter(3));
        Assert.False(builder.TouchesPreviousSpokenLetter(4));

        // a replacement that ends with a space does not
        builder.Add(new NormalizedSpan(3, 1, " registered ", NormalizationKind.Symbol));
        Assert.False(builder.TouchesPreviousSpokenLetter(4));

        // a dropped span does not
        builder.Add(new NormalizedSpan(8, 1, string.Empty, NormalizationKind.Dropped));
        Assert.False(builder.TouchesPreviousSpokenLetter(9));
    }

    /// <summary>Proves ascending, disjoint spans (including touching ones) are accepted and come back in order.</summary>
    [Fact]
    public void AlignedTextBuilder_Add_AscendingDisjointSpans_AreKeptInOrder()
    {
        // Arrange
        var builder = new AlignedTextBuilder("abcdef ghi");

        // Act
        builder.Add(new NormalizedSpan(0, 3, "x", NormalizationKind.Abbreviation));
        builder.Add(new NormalizedSpan(3, 3, "y", NormalizationKind.Symbol));
        builder.Add(new NormalizedSpan(7, 3, "z", NormalizationKind.Initialism));
        var aligned = builder.Build();

        // Assert
        Assert.Equal([0, 3, 7], aligned.Spans.Select(span => span.Start));
    }

    /// <summary>Proves a span that starts or ends on whitespace is rejected.</summary>
    /// <param name="start">The span's start.</param>
    /// <param name="length">The span's length.</param>
    [Theory]
    [InlineData(3, 2)]
    [InlineData(0, 4)]
    [InlineData(3, 1)]
    public void AlignedTextBuilder_Add_SpanWithWhitespaceEdge_ThrowsArgumentException(int start, int length)
    {
        // Arrange: the space is at index 3
        var builder = new AlignedTextBuilder("abc def");

        // Act / Assert
        Assert.Throws<ArgumentException>(() => builder.Add(new NormalizedSpan(start, length, "x", NormalizationKind.Abbreviation)));
    }

    /// <summary>Proves a span that covers a tag placeholder is rejected.</summary>
    [Fact]
    public void AlignedTextBuilder_Add_SpanCoveringPlaceholder_ThrowsArgumentException()
    {
        // Arrange
        var builder = new AlignedTextBuilder("ab\uFFFCcd");

        // Act / Assert
        Assert.Throws<ArgumentException>(() => builder.Add(new NormalizedSpan(0, 5, "x", NormalizationKind.Abbreviation)));
    }

    /// <summary>Proves a span that cuts a surrogate pair at either edge is rejected, and one that keeps the pair whole is accepted.</summary>
    [Fact]
    public void AlignedTextBuilder_Add_SpanCuttingSurrogatePair_ThrowsArgumentException()
    {
        // Arrange: "a" + emoji (two code units) + "b"
        var builder = new AlignedTextBuilder("a\uD83D\uDE00b");

        // Act / Assert: ends inside the pair; starts inside the pair; covers it whole
        Assert.Throws<ArgumentException>(() => builder.Add(new NormalizedSpan(0, 2, "x", NormalizationKind.Abbreviation)));
        Assert.Throws<ArgumentException>(() => builder.Add(new NormalizedSpan(2, 2, "x", NormalizationKind.Abbreviation)));
        builder.Add(new NormalizedSpan(0, 4, "x", NormalizationKind.Abbreviation));
        Assert.Single(builder.Build().Spans);
    }

    /// <summary>Proves the replacement must be non-null, placeholder-free, and empty exactly for a dropped span.</summary>
    [Fact]
    public void AlignedTextBuilder_Add_InvalidSpokenText_ThrowsArgumentException()
    {
        // Arrange
        var builder = new AlignedTextBuilder("abc");

        // Act / Assert
        Assert.Throws<ArgumentException>(() => builder.Add(new NormalizedSpan(0, 1, null!, NormalizationKind.Abbreviation)));
        Assert.Throws<ArgumentException>(() => builder.Add(new NormalizedSpan(0, 1, "a\uFFFCb", NormalizationKind.Abbreviation)));
        Assert.Throws<ArgumentException>(() => builder.Add(new NormalizedSpan(0, 1, string.Empty, NormalizationKind.Symbol)));
        Assert.Throws<ArgumentException>(() => builder.Add(new NormalizedSpan(0, 1, "x", NormalizationKind.Dropped)));
        builder.Add(new NormalizedSpan(0, 1, string.Empty, NormalizationKind.Dropped));
        Assert.Equal(string.Empty, builder.Build().Spans[0].Spoken);
    }

    /// <summary>Proves a null source is rejected.</summary>
    [Fact]
    public void AlignedTextBuilder_Constructor_NullSource_ThrowsArgumentNullException()
    {
        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => new AlignedTextBuilder(null!));
    }

    /// <summary>Proves a break edit must sit on a token start strictly inside the text.</summary>
    /// <param name="offset">The offset to try.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(7)]
    [InlineData(3)]
    [InlineData(99)]
    public void AlignedTextBuilder_ForceBreakAndSuppressBreak_InvalidOffset_ThrowsArgumentOutOfRangeException(int offset)
    {
        // Arrange: "abc def" has a space at index 3 and ends at 7
        var builder = new AlignedTextBuilder("abc def");

        // Act / Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => builder.ForceBreak(offset));
        Assert.Throws<ArgumentOutOfRangeException>(() => builder.SuppressBreak(offset));
    }

    /// <summary>Proves a break edit on a tag placeholder is rejected.</summary>
    [Fact]
    public void AlignedTextBuilder_ForceBreak_OnPlaceholder_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var builder = new AlignedTextBuilder("ab\uFFFCcd");

        // Act / Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => builder.ForceBreak(2));
    }

    /// <summary>Proves the built break lists are sorted and free of duplicates whatever the request order.</summary>
    [Fact]
    public void AlignedTextBuilder_Build_BreakRequestsInAnyOrder_AreSortedAndDistinct()
    {
        // Arrange
        var builder = new AlignedTextBuilder("aa bb cc dd");

        // Act
        builder.ForceBreak(6);
        builder.ForceBreak(3);
        builder.ForceBreak(6);
        builder.SuppressBreak(9);
        builder.SuppressBreak(3);
        var aligned = builder.Build();

        // Assert
        Assert.Equal([3, 6], aligned.ForcedBreaks);
        Assert.Equal([3, 9], aligned.SuppressedBreaks);
    }

    /// <summary>Proves clitic marks must be ordered, in bounds, non-empty and classified, and counting ambiguity accumulates.</summary>
    [Fact]
    public void AlignedTextBuilder_MarkCliticAndCountAmbiguous_ValidateAndAccumulate()
    {
        // Arrange
        var builder = new AlignedTextBuilder("it's we're");
        builder.MarkClitic(new CliticMark(2, 2, CliticKind.Is));

        // Act / Assert: overlapping, empty, unclassified and out-of-bounds marks are rejected
        Assert.Throws<ArgumentException>(() => builder.MarkClitic(new CliticMark(3, 2, CliticKind.Is)));
        Assert.Throws<ArgumentException>(() => builder.MarkClitic(new CliticMark(6, 0, CliticKind.Are)));
        Assert.Throws<ArgumentException>(() => builder.MarkClitic(new CliticMark(6, 3, CliticKind.None)));
        Assert.Throws<ArgumentException>(() => builder.MarkClitic(new CliticMark(8, 9, CliticKind.Are)));
        builder.MarkClitic(new CliticMark(7, 3, CliticKind.Are));
        builder.CountAmbiguous();
        builder.CountAmbiguous();
        var aligned = builder.Build();
        Assert.Equal(2, aligned.Clitics.Count);
        Assert.Equal(2, aligned.AmbiguousCount);
    }

    /// <summary>Proves a clitic mark whose range would overflow or leaves the text is rejected with ArgumentException and does not disturb later marks.</summary>
    [Fact]
    public void AlignedTextBuilder_MarkClitic_OverflowingOrOutOfBoundsRange_ThrowsArgumentException()
    {
        // Arrange
        var builder = new AlignedTextBuilder("it's we're");

        // Act / Assert: Start + Length would wrap negative, or start/length exceed the text
        Assert.Throws<ArgumentException>(() => builder.MarkClitic(new CliticMark(int.MaxValue, 1, CliticKind.Is)));
        Assert.Throws<ArgumentException>(() => builder.MarkClitic(new CliticMark(int.MaxValue, int.MaxValue, CliticKind.Is)));
        Assert.Throws<ArgumentException>(() => builder.MarkClitic(new CliticMark(1, int.MaxValue, CliticKind.Is)));
        Assert.Throws<ArgumentException>(() => builder.MarkClitic(new CliticMark(-1, 2, CliticKind.Is)));
        Assert.Throws<ArgumentException>(() => builder.MarkClitic(new CliticMark(11, 1, CliticKind.Is)));

        // Assert: the rejected marks left no state behind, so an in-order mark is still accepted
        builder.MarkClitic(new CliticMark(2, 2, CliticKind.Is));
        Assert.Single(builder.Build().Clitics);
    }

    /// <summary>Proves a span whose range would overflow is rejected with ArgumentException.</summary>
    [Fact]
    public void AlignedTextBuilder_Add_OverflowingRange_ThrowsArgumentException()
    {
        // Arrange
        var builder = new AlignedTextBuilder("abcdefghij");

        // Act / Assert
        Assert.Throws<ArgumentException>(() => builder.Add(new NormalizedSpan(int.MaxValue, 1, "x", NormalizationKind.Abbreviation)));
        Assert.Throws<ArgumentException>(() => builder.Add(new NormalizedSpan(int.MaxValue, int.MaxValue, "x", NormalizationKind.Abbreviation)));
        Assert.Throws<ArgumentException>(() => builder.Add(new NormalizedSpan(1, int.MaxValue, "x", NormalizationKind.Abbreviation)));
        Assert.Throws<ArgumentException>(() => builder.Add(new NormalizedSpan(11, 1, "x", NormalizationKind.Abbreviation)));
    }

    /// <summary>Proves the segments tile the text: changed spans and untouched gaps, in order, contiguous, covering every character.</summary>
    [Fact]
    public void AlignedText_Segments_SpansAndGaps_PartitionTheTextExactly()
    {
        // Arrange
        const string text = "Dr. Smith & Jones";
        var builder = new AlignedTextBuilder(text);
        builder.Add(new NormalizedSpan(0, 3, "Doctor", NormalizationKind.Abbreviation));
        builder.Add(new NormalizedSpan(10, 1, "and", NormalizationKind.Symbol));

        // Act
        var segments = builder.Build().Segments().ToList();

        // Assert
        Assert.Equal(["Doctor", " Smith ", "and", " Jones"], segments.Select(segment => segment.Spoken));
        Assert.Equal(new NormalizationKind?[] { NormalizationKind.Abbreviation, null, NormalizationKind.Symbol, null }, segments.Select(segment => segment.Kind));
        Assert.Equal(text.Length, segments.Sum(segment => segment.Length));
        var position = 0;
        foreach (var segment in segments)
        {
            Assert.Equal(position, segment.Start);
            position += segment.Length;
        }
    }

    /// <summary>Proves an empty text has no segments and a text without spans is one untouched gap.</summary>
    [Fact]
    public void AlignedText_Segments_EmptyTextAndNoSpans_YieldNothingAndOneGap()
    {
        // Arrange
        var empty = new AlignedTextBuilder(string.Empty).Build();
        var plain = new AlignedTextBuilder("plain text").Build();

        // Act / Assert
        Assert.Empty(empty.Segments());
        var only = Assert.Single(plain.Segments());
        Assert.Equal("plain text", only.Spoken);
        Assert.Null(only.Kind);
    }

    /// <summary>Proves applying the spoken forms replaces exactly the spans and leaves everything else (case, punctuation, whitespace) as written.</summary>
    [Fact]
    public void AlignedText_ApplySpokenForms_ReplacesOnlySpans()
    {
        // Arrange
        const string text = "  Dr. SMITH,  & \u200B co.  ";
        var builder = new AlignedTextBuilder(text);
        builder.Add(new NormalizedSpan(2, 3, "Doctor", NormalizationKind.Abbreviation));
        builder.Add(new NormalizedSpan(14, 1, "and", NormalizationKind.Symbol));
        builder.Add(new NormalizedSpan(16, 1, string.Empty, NormalizationKind.Dropped));

        // Act
        var spoken = builder.Build().ApplySpokenForms();

        // Assert
        Assert.Equal("  Doctor SMITH,  and  co.  ", spoken);
    }

    /// <summary>Proves reconciliation unions the tokenizer's breaks with the forced ones and removes the suppressed ones, ascending and unique.</summary>
    [Fact]
    public void AlignedText_ReconcileBreaks_TokenizerForcedAndSuppressed_AreCombined()
    {
        // Arrange
        var builder = new AlignedTextBuilder("aa bb cc dd ee ff");
        builder.ForceBreak(3);
        builder.ForceBreak(9);
        builder.ForceBreak(12);
        builder.SuppressBreak(6);
        builder.SuppressBreak(12);
        var aligned = builder.Build();

        // Act: the tokenizer breaks at 6 (suppressed), 9 (also forced) and 15
        var reconciled = aligned.ReconcileBreaks([6, 9, 15]);

        // Assert
        Assert.Equal([3, 9, 15], reconciled);
    }

    /// <summary>Proves reconciling with no edits returns the tokenizer's breaks, and a null list is rejected.</summary>
    [Fact]
    public void AlignedText_ReconcileBreaks_NoEditsAndNull_ReturnsInputAndRejectsNull()
    {
        // Arrange
        var aligned = new AlignedTextBuilder("aa bb cc").Build();

        // Act / Assert
        Assert.Equal([3, 6], aligned.ReconcileBreaks([3, 6]));
        Assert.Empty(aligned.ReconcileBreaks([]));
        Assert.Throws<ArgumentNullException>(() => aligned.ReconcileBreaks(null!));
    }

    /// <summary>Proves a token reports its end and its characters without copying the text.</summary>
    [Fact]
    public void NormalizationToken_EndAndIn_DescribeTheTokenSpan()
    {
        // Arrange
        var token = new NormalizationToken(4, 3, true);

        // Act / Assert
        Assert.Equal(7, token.End);
        Assert.Equal("def", token.In("abc def ghi").ToString());
    }

    /// <summary>Proves the public vocabularies keep their documented names and numeric values.</summary>
    [Fact]
    public void NormalizationKindAndCliticKind_Members_KeepNamesAndValues()
    {
        // Assert
        Assert.Equal(
            ["Abbreviation=1", "Initialism=2", "Symbol=3", "Apostrophe=4", "Dropped=5"],
            Enum.GetValues<NormalizationKind>().Select(value => $"{value}={(int)value}"));
        Assert.Equal(
            ["None=0", "Possessive=1", "Is=2", "Has=3", "Would=4", "Had=5", "Will=6", "Have=7", "Are=8", "Am=9", "Not=10", "Us=11"],
            Enum.GetValues<CliticKind>().Select(value => $"{value}={(int)value}"));
    }
}
