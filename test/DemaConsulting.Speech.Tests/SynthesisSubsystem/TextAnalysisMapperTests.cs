using DemaConsulting.Speech.SynthesisSubsystem;

namespace DemaConsulting.Speech.Tests.SynthesisSubsystem;

/// <summary>
///     Unit tests for the internal <c>TextAnalysisMapper</c>, which builds the analysis text from
///     parsed spans and re-bases analyzer results onto the plain-text spans.
/// </summary>
public class TextAnalysisMapperTests
{
    /// <summary>The placeholder character, as a string, for readable expected values.</summary>
    private static readonly string Placeholder = TextAnalysisConventions.TagPlaceholder.ToString();

    /// <summary>
    ///     Builds the standard three-span fixture: plain <c>"I've "</c>, a tag, then plain
    ///     <c>" read it. Dr. Smith left."</c>. In the analysis text the placeholder sits at index 5
    ///     and the second span starts at index 6.
    /// </summary>
    /// <returns>The parsed spans.</returns>
    private static IReadOnlyList<TaggedTextSpan> StandardSpans() =>
        AudioTagParser.Parse("I've [whispers] read it. Dr. Smith left.");

    /// <summary>
    ///     Proves each recognized tag becomes exactly one placeholder and plain text is kept in
    ///     order.
    /// </summary>
    [Fact]
    public void TextAnalysisMapper_BuildAnalysisText_PlainAndTagSpans_UsesOnePlaceholderPerTag()
    {
        // Arrange
        var spans = AudioTagParser.Parse("Hello [short pause] world [fast][excited] end");

        // Act
        var text = TextAnalysisMapper.BuildAnalysisText(spans);

        // Assert
        Assert.Equal($"Hello {Placeholder} world {Placeholder}{Placeholder} end", text);
    }

    /// <summary>
    ///     Proves text without tags (including unrecognized bracket text) is passed through
    ///     unchanged.
    /// </summary>
    [Fact]
    public void TextAnalysisMapper_BuildAnalysisText_PlainTextOnly_EqualsInput()
    {
        // Arrange
        const string input = "Just words [not a tag] here.";

        // Act
        var text = TextAnalysisMapper.BuildAnalysisText(AudioTagParser.Parse(input));

        // Assert
        Assert.Equal(input, text);
    }

    /// <summary>
    ///     Proves a tags-only sequence yields only placeholders, and a null argument is rejected.
    /// </summary>
    [Fact]
    public void TextAnalysisMapper_BuildAnalysisText_TagsOnly_YieldsOnlyPlaceholders()
    {
        // Arrange
        var spans = AudioTagParser.Parse("[fast][excited]");

        // Act
        var text = TextAnalysisMapper.BuildAnalysisText(spans);

        // Assert
        Assert.Equal($"{Placeholder}{Placeholder}", text);
        Assert.Throws<ArgumentNullException>(() => TextAnalysisMapper.BuildAnalysisText(null!));
    }

    /// <summary>
    ///     Proves an annotation inside the second plain span is attached to it with a
    ///     span-local offset.
    /// </summary>
    [Fact]
    public void TextAnalysisMapper_Annotate_AnnotationInsideSecondSpan_RebasedToSpanLocalOffset()
    {
        // Arrange: "read" sits at index 7 of the analysis text, which is index 1 of the second span
        var spans = StandardSpans();
        var analysis = new TextAnalysisResult([new SegmentAnnotation(7, 4, "reading", "red")], null);

        // Act
        var result = TextAnalysisMapper.Annotate(spans, analysis);

        // Assert
        var annotation = Assert.Single(result[2].Annotations);
        Assert.Equal(1, annotation.Start);
        Assert.Equal("read", result[2].Text.Substring(annotation.Start, annotation.Length));
        Assert.Empty(result[0].Annotations);
    }

    /// <summary>
    ///     Proves tag spans pass through as the very same instances, and plain spans without
    ///     results are not replaced.
    /// </summary>
    [Fact]
    public void TextAnalysisMapper_Annotate_TagSpans_PassThroughUnchanged()
    {
        // Arrange
        var spans = StandardSpans();
        var analysis = new TextAnalysisResult([new SegmentAnnotation(7, 4, "reading", "red")], null);

        // Act
        var result = TextAnalysisMapper.Annotate(spans, analysis);

        // Assert
        Assert.Same(spans[1], result[1]);
        Assert.Same(spans[0], result[0]);
        Assert.NotSame(spans[2], result[2]);
    }

    /// <summary>
    ///     Proves an annotation that covers a placeholder is dropped, never clipped.
    /// </summary>
    [Fact]
    public void TextAnalysisMapper_Annotate_AnnotationCoveringPlaceholder_Dropped()
    {
        // Arrange: indexes 4..6 cover the trailing space, the placeholder and the next space
        var spans = StandardSpans();
        var analysis = new TextAnalysisResult([new SegmentAnnotation(4, 3, "k", "v")], null);

        // Act
        var result = TextAnalysisMapper.Annotate(spans, analysis);

        // Assert
        Assert.Same(spans, result);
    }

    /// <summary>
    ///     Proves an annotation spanning two plain spans (across a tag) is dropped.
    /// </summary>
    [Fact]
    public void TextAnalysisMapper_Annotate_AnnotationSpanningTwoPlainSpans_Dropped()
    {
        // Arrange: indexes 3..7 reach from the first span into the second
        var spans = StandardSpans();
        var analysis = new TextAnalysisResult([new SegmentAnnotation(3, 5, "k", "v")], null);

        // Act
        var result = TextAnalysisMapper.Annotate(spans, analysis);

        // Assert
        Assert.Same(spans, result);
    }

    /// <summary>
    ///     Proves an annotation extending beyond the analysis text is dropped.
    /// </summary>
    [Fact]
    public void TextAnalysisMapper_Annotate_AnnotationBeyondTextLength_Dropped()
    {
        // Arrange
        var spans = StandardSpans();
        var length = TextAnalysisMapper.BuildAnalysisText(spans).Length;
        var analysis = new TextAnalysisResult([new SegmentAnnotation(length - 2, 5, "k", "v")], null);

        // Act
        var result = TextAnalysisMapper.Annotate(spans, analysis);

        // Assert
        Assert.Same(spans, result);
    }

    /// <summary>
    ///     Proves annotations that exactly fill a span, or touch a placeholder without crossing
    ///     it, are kept.
    /// </summary>
    [Fact]
    public void TextAnalysisMapper_Annotate_AnnotationFillingSpanOrTouchingPlaceholder_IsKept()
    {
        // Arrange: the whole first span (0..5), and the whole second span (6..31)
        var spans = StandardSpans();
        var length = TextAnalysisMapper.BuildAnalysisText(spans).Length;
        var analysis = new TextAnalysisResult(
            [new SegmentAnnotation(0, 5, "a", "1"), new SegmentAnnotation(6, length - 6, "b", "2")],
            null);

        // Act
        var result = TextAnalysisMapper.Annotate(spans, analysis);

        // Assert
        Assert.Equal(new SegmentAnnotation(0, 5, "a", "1"), Assert.Single(result[0].Annotations));
        Assert.Equal(new SegmentAnnotation(0, length - 6, "b", "2"), Assert.Single(result[2].Annotations));
    }

    /// <summary>
    ///     Proves overlapping annotations and their input order are preserved within a span.
    /// </summary>
    [Fact]
    public void TextAnalysisMapper_Annotate_OverlappingAnnotations_KeepInputOrder()
    {
        // Arrange
        var spans = StandardSpans();
        var analysis = new TextAnalysisResult(
            [new SegmentAnnotation(7, 4, "z", "1"), new SegmentAnnotation(7, 2, "a", "2")],
            null);

        // Act
        var result = TextAnalysisMapper.Annotate(spans, analysis);

        // Assert
        Assert.Equal(["z", "a"], result[2].Annotations.Select(annotation => annotation.Kind));
    }

    /// <summary>
    ///     Proves supplied breaks are assigned to the span that contains them, relative to that
    ///     span, including breaks at the edge of a placeholder.
    /// </summary>
    [Fact]
    public void TextAnalysisMapper_Annotate_BreaksProvided_AssignedToContainingSpansWithLocalOffsets()
    {
        // Arrange: 5 = end of first span, 6 = start of second span, 15 = after " read it."
        var spans = StandardSpans();
        var analysis = new TextAnalysisResult([], [5, 6, 15]);

        // Act
        var result = TextAnalysisMapper.Annotate(spans, analysis);

        // Assert
        Assert.Equal([5], result[0].SentenceBreaks);
        Assert.Equal([0, 9], result[2].SentenceBreaks);
        Assert.Null(result[1].SentenceBreaks);
    }

    /// <summary>
    ///     Proves a break that sits between two consecutive placeholders (inside no plain span) is
    ///     attached to the tag span before it as tag offset 1, and one beyond the text is dropped.
    /// </summary>
    [Fact]
    public void TextAnalysisMapper_Annotate_BreakBetweenConsecutiveTags_AttachedAfterPrecedingTagSpan()
    {
        // Arrange: "a" + two tags + "b" gives analysis text "a??b"; index 2 is between the tags
        var spans = AudioTagParser.Parse("a[fast][excited]b");
        var analysis = new TextAnalysisResult([], [2, 100]);

        // Act
        var result = TextAnalysisMapper.Annotate(spans, analysis);

        // Assert: the break rides on the first tag; the second tag is the same instance
        Assert.Equal([1], result[1].SentenceBreaks);
        Assert.Same(spans[2], result[2]);
        Assert.Null(result[2].SentenceBreaks);
        Assert.Empty(result[0].SentenceBreaks!);
        Assert.Empty(result[3].SentenceBreaks!);
    }

    /// <summary>
    ///     Proves a break before a leading tag is dropped, while one between two leading tags is
    ///     attached to the first of them.
    /// </summary>
    [Fact]
    public void TextAnalysisMapper_Annotate_BreakBeforeLeadingTag_Dropped()
    {
        // Arrange: two tags then "b" gives analysis text "??b"
        var spans = AudioTagParser.Parse("[fast][excited]b");

        // Act
        var dropped = TextAnalysisMapper.Annotate(spans, new TextAnalysisResult([], [0]));
        var attached = TextAnalysisMapper.Annotate(spans, new TextAnalysisResult([], [1]));

        // Assert
        Assert.Null(dropped[0].SentenceBreaks);
        Assert.Null(dropped[1].SentenceBreaks);
        Assert.Equal([1], attached[0].SentenceBreaks);
        Assert.Null(attached[1].SentenceBreaks);
    }

    /// <summary>
    ///     Proves a break after the last tag of a trailing tag run is attached to that last tag.
    /// </summary>
    [Fact]
    public void TextAnalysisMapper_Annotate_BreakAfterTrailingTag_AttachedToLastTag()
    {
        // Arrange: "a" then two tags gives analysis text "a??"; index 3 is the end of the text
        var spans = AudioTagParser.Parse("a[fast][excited]");

        // Act
        var result = TextAnalysisMapper.Annotate(spans, new TextAnalysisResult([], [3]));

        // Assert
        Assert.Null(result[1].SentenceBreaks);
        Assert.Equal([1], result[2].SentenceBreaks);
    }

    /// <summary>
    ///     Proves an authoritative but empty break list still gives every plain span a non-null
    ///     empty list.
    /// </summary>
    [Fact]
    public void TextAnalysisMapper_Annotate_EmptyBreaksList_GivesEveryPlainSpanEmptyList()
    {
        // Arrange
        var spans = StandardSpans();

        // Act
        var result = TextAnalysisMapper.Annotate(spans, new TextAnalysisResult([], []));

        // Assert
        Assert.NotSame(spans, result);
        Assert.Empty(result[0].SentenceBreaks!);
        Assert.Empty(result[2].SentenceBreaks!);
    }

    /// <summary>
    ///     Proves spans keep a null break list when the analysis expresses no sentence opinion.
    /// </summary>
    [Fact]
    public void TextAnalysisMapper_Annotate_BreaksNull_SpansKeepNullBreaks()
    {
        // Arrange
        var spans = StandardSpans();
        var analysis = new TextAnalysisResult([new SegmentAnnotation(7, 4, "k", "v")], null);

        // Act
        var result = TextAnalysisMapper.Annotate(spans, analysis);

        // Assert
        Assert.All(result, span => Assert.Null(span.SentenceBreaks));
    }

    /// <summary>
    ///     Proves an empty result returns the very same list instance, and a tags-only list is
    ///     left alone even when annotations are supplied.
    /// </summary>
    [Fact]
    public void TextAnalysisMapper_Annotate_EmptyResult_ReturnsSameSpanListInstance()
    {
        // Arrange
        var spans = StandardSpans();
        var tagsOnly = AudioTagParser.Parse("[fast][excited]");

        // Act
        var empty = TextAnalysisMapper.Annotate(spans, TextAnalysisResult.Empty);
        var tags = TextAnalysisMapper.Annotate(tagsOnly, new TextAnalysisResult([new SegmentAnnotation(0, 1, "k", "v")], null));

        // Assert
        Assert.Same(spans, empty);
        Assert.Same(tagsOnly, tags);
    }

    /// <summary>
    ///     Proves null arguments are rejected.
    /// </summary>
    [Fact]
    public void TextAnalysisMapper_Annotate_NullArguments_Throw()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => TextAnalysisMapper.Annotate(null!, TextAnalysisResult.Empty));
        Assert.Throws<ArgumentNullException>(() => TextAnalysisMapper.Annotate(StandardSpans(), null!));
    }
}
