using DemaConsulting.Speech.SynthesisSubsystem;

namespace DemaConsulting.Speech.Tests.SynthesisSubsystem;

/// <summary>
///     Unit tests for <see cref="AudioTagParser"/>, the Layer 1 scanner that recognizes closed
///     Natural Language Audio Tag bracket syntax and reports it as an ordered sequence of
///     <see cref="TaggedTextSpan"/>.
/// </summary>
public class AudioTagParserTests
{
    /// <summary>
    ///     Supplies every documented alias for every canonical tag, sourced directly from
    ///     <see cref="AudioTagCatalog.Tags"/> so this theory can never drift out of sync with the
    ///     catalog it exercises.
    /// </summary>
    public static IEnumerable<TheoryDataRow<string, NaturalLanguageAudioTag>> AllAliases =>
        AudioTagCatalog.Tags.SelectMany(
            descriptor => descriptor.Aliases,
            (descriptor, alias) => new TheoryDataRow<string, NaturalLanguageAudioTag>(alias, descriptor.Tag));

    /// <summary>
    ///     Proves every documented alias, bracketed alone, parses to a single
    ///     <see cref="TaggedTextSpanKind.Tag"/> span carrying its canonical tag - the closed-set
    ///     requirement that every listed alias family resolves correctly.
    /// </summary>
    [Theory]
    [MemberData(nameof(AllAliases))]
    public void AudioTagParser_Parse_EveryDocumentedAlias_ResolvesToItsCanonicalTagSpan(string alias, NaturalLanguageAudioTag expectedTag)
    {
        // Act
        var spans = AudioTagParser.Parse($"[{alias}]");

        // Assert: exactly one tag span, carrying the expected canonical tag
        var span = Assert.Single(spans);
        Assert.Equal(TaggedTextSpanKind.Tag, span.Kind);
        Assert.Equal(expectedTag, span.Tag);
        Assert.Equal(string.Empty, span.Text);
    }

    /// <summary>
    ///     Proves alias matching is case-insensitive, since a caller should not need to match the
    ///     catalog's canonical casing.
    /// </summary>
    [Fact]
    public void AudioTagParser_Parse_UppercaseTag_ResolvesToCanonicalTag()
    {
        // Act
        var spans = AudioTagParser.Parse("[EXCITED]");

        // Assert
        var span = Assert.Single(spans);
        Assert.Equal(TaggedTextSpanKind.Tag, span.Kind);
        Assert.Equal(NaturalLanguageAudioTag.Excited, span.Tag);
    }

    /// <summary>
    ///     Proves extra internal whitespace inside a multi-word tag is tolerated, mirroring
    ///     <see cref="AudioTagCatalog"/>'s normalization contract.
    /// </summary>
    [Fact]
    public void AudioTagParser_Parse_TagWithExtraInternalWhitespace_ResolvesToCanonicalTag()
    {
        // Act
        var spans = AudioTagParser.Parse("[very   slow]");

        // Assert
        var span = Assert.Single(spans);
        Assert.Equal(NaturalLanguageAudioTag.VerySlow, span.Tag);
    }

    /// <summary>
    ///     Proves plain narration text with no bracket syntax at all parses to a single literal
    ///     span, unchanged.
    /// </summary>
    [Fact]
    public void AudioTagParser_Parse_PlainTextWithNoTags_ReturnsSinglePlainTextSpan()
    {
        // Act
        var spans = AudioTagParser.Parse("Hello, how are you today?");

        // Assert
        var span = Assert.Single(spans);
        Assert.Equal(TaggedTextSpanKind.PlainText, span.Kind);
        Assert.Equal("Hello, how are you today?", span.Text);
        Assert.Null(span.Tag);
    }

    /// <summary>
    ///     Proves an empty input string produces no spans at all, rather than a spurious empty
    ///     plain-text span.
    /// </summary>
    [Fact]
    public void AudioTagParser_Parse_EmptyInput_ReturnsEmptySpanList()
    {
        // Act
        var spans = AudioTagParser.Parse(string.Empty);

        // Assert
        Assert.Empty(spans);
    }

    /// <summary>
    ///     Proves text mixing plain narration with a recognized tag produces spans in the correct
    ///     order, splitting the plain text around the tag.
    /// </summary>
    [Fact]
    public void AudioTagParser_Parse_MixedPlainTextAndTag_ReturnsOrderedSpans()
    {
        // Act
        var spans = AudioTagParser.Parse("I can't believe it! [excited] That's amazing!");

        // Assert: plain, tag, plain, in order
        Assert.Collection(
            spans,
            span =>
            {
                Assert.Equal(TaggedTextSpanKind.PlainText, span.Kind);
                Assert.Equal("I can't believe it! ", span.Text);
            },
            span =>
            {
                Assert.Equal(TaggedTextSpanKind.Tag, span.Kind);
                Assert.Equal(NaturalLanguageAudioTag.Excited, span.Tag);
            },
            span =>
            {
                Assert.Equal(TaggedTextSpanKind.PlainText, span.Kind);
                Assert.Equal(" That's amazing!", span.Text);
            });
    }

    /// <summary>
    ///     Proves two recognized tags with no text between them each become their own span, so
    ///     adjacent tags never merge or get lost.
    /// </summary>
    [Fact]
    public void AudioTagParser_Parse_AdjacentTags_ReturnsTwoSeparateTagSpans()
    {
        // Act
        var spans = AudioTagParser.Parse("[whispers][short pause]");

        // Assert
        Assert.Collection(
            spans,
            span => Assert.Equal(NaturalLanguageAudioTag.Whispers, span.Tag),
            span => Assert.Equal(NaturalLanguageAudioTag.ShortPause, span.Tag));
    }

    /// <summary>
    ///     Proves a recognized tag at the very start of the input, with trailing plain text,
    ///     produces the tag span first with no leading empty plain-text span.
    /// </summary>
    [Fact]
    public void AudioTagParser_Parse_TagAtStartOfText_ReturnsTagSpanFirst()
    {
        // Act
        var spans = AudioTagParser.Parse("[laughs] that's a good one");

        // Assert
        Assert.Collection(
            spans,
            span => Assert.Equal(NaturalLanguageAudioTag.Laughs, span.Tag),
            span =>
            {
                Assert.Equal(TaggedTextSpanKind.PlainText, span.Kind);
                Assert.Equal(" that's a good one", span.Text);
            });
    }

    /// <summary>
    ///     Proves a recognized tag at the very end of the input, with leading plain text,
    ///     produces the tag span last with no trailing empty plain-text span.
    /// </summary>
    [Fact]
    public void AudioTagParser_Parse_TagAtEndOfText_ReturnsTagSpanLast()
    {
        // Act
        var spans = AudioTagParser.Parse("I'm so tired [sighs]");

        // Assert
        Assert.Collection(
            spans,
            span =>
            {
                Assert.Equal(TaggedTextSpanKind.PlainText, span.Kind);
                Assert.Equal("I'm so tired ", span.Text);
            },
            span => Assert.Equal(NaturalLanguageAudioTag.Sighs, span.Tag));
    }

    /// <summary>
    ///     Proves an unrecognized tag name inside otherwise well-formed brackets passes through
    ///     literally, brackets included, rather than throwing or being silently dropped.
    /// </summary>
    [Fact]
    public void AudioTagParser_Parse_UnknownBracketedWord_PassesThroughAsLiteralText()
    {
        // Act
        var spans = AudioTagParser.Parse("This is [nonsense] text.");

        // Assert
        var span = Assert.Single(spans);
        Assert.Equal(TaggedTextSpanKind.PlainText, span.Kind);
        Assert.Equal("This is [nonsense] text.", span.Text);
    }

    /// <summary>
    ///     Proves an unclosed opening bracket passes through as literal text instead of throwing
    ///     or discarding the rest of the input.
    /// </summary>
    [Fact]
    public void AudioTagParser_Parse_UnclosedOpeningBracket_PassesThroughAsLiteralText()
    {
        // Act
        var spans = AudioTagParser.Parse("Wait, what[ is happening");

        // Assert
        var span = Assert.Single(spans);
        Assert.Equal(TaggedTextSpanKind.PlainText, span.Kind);
        Assert.Equal("Wait, what[ is happening", span.Text);
    }

    /// <summary>
    ///     Proves empty brackets pass through as literal text rather than resolving to any tag.
    /// </summary>
    [Fact]
    public void AudioTagParser_Parse_EmptyBrackets_PassesThroughAsLiteralText()
    {
        // Act
        var spans = AudioTagParser.Parse("Um, [] okay then.");

        // Assert
        var span = Assert.Single(spans);
        Assert.Equal(TaggedTextSpanKind.PlainText, span.Kind);
        Assert.Equal("Um, [] okay then.", span.Text);
    }

    /// <summary>
    ///     Proves a stray closing bracket with no matching opener is passed through literally as
    ///     ordinary text.
    /// </summary>
    [Fact]
    public void AudioTagParser_Parse_StrayClosingBracket_PassesThroughAsLiteralText()
    {
        // Act
        var spans = AudioTagParser.Parse("That was odd] wasn't it");

        // Assert
        var span = Assert.Single(spans);
        Assert.Equal(TaggedTextSpanKind.PlainText, span.Kind);
        Assert.Equal("That was odd] wasn't it", span.Text);
    }

    /// <summary>
    ///     Proves a recognized tag immediately followed by an unrecognized bracketed word keeps
    ///     both as distinct, correctly-typed spans in order.
    /// </summary>
    [Fact]
    public void AudioTagParser_Parse_RecognizedTagFollowedByUnknownBracket_ReturnsTagThenLiteralSpans()
    {
        // Act
        var spans = AudioTagParser.Parse("[fast][gibberish]");

        // Assert
        Assert.Collection(
            spans,
            span =>
            {
                Assert.Equal(TaggedTextSpanKind.Tag, span.Kind);
                Assert.Equal(NaturalLanguageAudioTag.Fast, span.Tag);
            },
            span =>
            {
                Assert.Equal(TaggedTextSpanKind.PlainText, span.Kind);
                Assert.Equal("[gibberish]", span.Text);
            });
    }
}
