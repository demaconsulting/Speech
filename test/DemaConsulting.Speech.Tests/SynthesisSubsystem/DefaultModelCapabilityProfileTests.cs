using DemaConsulting.Speech.ModelManagementSubsystem;
using DemaConsulting.Speech.SynthesisSubsystem;
using DemaConsulting.Speech.Tests.ModelManagementSubsystem.Fakes;

namespace DemaConsulting.Speech.Tests.SynthesisSubsystem;

/// <summary>
///     Unit tests for <see cref="DefaultModelCapabilityProfile"/>, covering representative tags
///     across every <see cref="SpeechModelAudioTagSupport"/> rendering strategy.
/// </summary>
public class DefaultModelCapabilityProfileTests
{
    /// <summary>
    ///     Proves that a pause tag always renders as real inserted silence, regardless of the
    ///     model's declared support, per this library's decision that pauses require no model
    ///     cooperation.
    /// </summary>
    [Theory]
    [InlineData(SpeechModelAudioTagSupport.Native)]
    [InlineData(SpeechModelAudioTagSupport.ParameterMapped)]
    [InlineData(SpeechModelAudioTagSupport.None)]
    public void DefaultModelCapabilityProfile_Render_ShortPause_AlwaysRendersAsSilenceSegment(
        SpeechModelAudioTagSupport support)
    {
        // Arrange
        var model = new StubSpeechModel(support);
        var spans = AudioTagParser.Parse("Hello [short pause] world.");

        // Act
        var plan = DefaultModelCapabilityProfile.Instance.Render(spans, model);

        // Assert: exactly one pure-silence segment appears between the two flushed text segments
        var pauseSegment = Assert.Single(plan.Segments, segment => segment.Text.Length == 0);
        Assert.True(pauseSegment.PostSilenceMs > 0);
    }

    /// <summary>
    ///     Proves that a long pause renders a longer silence than a short pause.
    /// </summary>
    [Fact]
    public void DefaultModelCapabilityProfile_Render_LongPause_RendersLongerSilenceThanShortPause()
    {
        // Arrange
        var model = new StubSpeechModel(SpeechModelAudioTagSupport.None);

        // Act
        var shortPlan = DefaultModelCapabilityProfile.Instance.Render(
            AudioTagParser.Parse("a [short pause] b"), model);
        var longPlan = DefaultModelCapabilityProfile.Instance.Render(
            AudioTagParser.Parse("a [long pause] b"), model);

        // Assert
        var shortSilence = shortPlan.Segments.Single(segment => segment.Text.Length == 0).PostSilenceMs;
        var longSilence = longPlan.Segments.Single(segment => segment.Text.Length == 0).PostSilenceMs;
        Assert.True(longSilence > shortSilence);
    }

    /// <summary>
    ///     Proves that a model declaring <see cref="SpeechModelAudioTagSupport.Native"/> support
    ///     receives the tag passed through inline as canonical bracket text.
    /// </summary>
    [Fact]
    public void DefaultModelCapabilityProfile_Render_NativeSupport_PassesThroughCanonicalBracketText()
    {
        // Arrange
        var model = new StubSpeechModel(SpeechModelAudioTagSupport.Native);
        var spans = AudioTagParser.Parse("[excited] Hello!");

        // Act
        var plan = DefaultModelCapabilityProfile.Instance.Render(spans, model);

        // Assert: the canonical bracket text appears inline in the flushed text
        Assert.Contains(plan.Segments, segment => segment.Text.Contains("[excited]", StringComparison.Ordinal));
    }

    /// <summary>
    ///     Proves that a model declaring <see cref="SpeechModelAudioTagSupport.ParameterMapped"/>
    ///     support maps a recognized pace tag onto its declared speed-convention parameter.
    /// </summary>
    [Fact]
    public void DefaultModelCapabilityProfile_Render_ParameterMappedSupport_FastTag_MapsToSpeedParameter()
    {
        // Arrange: a model declaring a "tempo" numeric parameter
        var model = new StubSpeechModel(
            SpeechModelAudioTagSupport.ParameterMapped,
            [new NumericParameter("tempo", "Tempo", "Speaking rate.", new NumericParameterBounds(0.5, 2.0, 0.05, 1.0))]);
        var spans = AudioTagParser.Parse("[fast] Hello world.");

        // Act
        var plan = DefaultModelCapabilityProfile.Instance.Render(spans, model);

        // Assert: the segment carrying the following words has a "tempo" override above the default
        var segment = plan.Segments.Single(candidate => candidate.Text.Length > 0);
        Assert.NotNull(segment.ParameterOverrides);
        Assert.True((double)segment.ParameterOverrides["tempo"] > 1.0);
    }

    /// <summary>
    ///     Proves that a model declaring <see cref="SpeechModelAudioTagSupport.ParameterMapped"/>
    ///     support silently strips a tag with no recognized numeric convention (an emotion tag),
    ///     never worse than plain narration.
    /// </summary>
    [Fact]
    public void DefaultModelCapabilityProfile_Render_ParameterMappedSupport_EmotionTag_StripsWithNoOverride()
    {
        // Arrange: a model with no declared parameters at all
        var model = new StubSpeechModel(SpeechModelAudioTagSupport.ParameterMapped);
        var spans = AudioTagParser.Parse("[excited] Hello world.");

        // Act
        var plan = DefaultModelCapabilityProfile.Instance.Render(spans, model);

        // Assert: the words are still spoken, with no override and no bracket text leaked in
        var segment = Assert.Single(plan.Segments);
        Assert.DoesNotContain("[excited]", segment.Text, StringComparison.Ordinal);
        Assert.Contains("Hello world", segment.Text, StringComparison.Ordinal);
        Assert.Null(segment.ParameterOverrides);
    }

    /// <summary>
    ///     Proves every new mood tag is stripped, with the words still spoken and no bracket text
    ///     leaked, for models that cannot act on emotion tags.
    /// </summary>
    [Theory]
    [InlineData("[angry]")]
    [InlineData("[calm]")]
    [InlineData("[happy]")]
    [InlineData("[enthused]")]
    [InlineData("[surprised]")]
    [InlineData("[worried]")]
    [InlineData("[confused]")]
    [InlineData("[frustrated]")]
    [InlineData("[neutral]")]
    public void DefaultModelCapabilityProfile_Render_NoneSupport_MoodTag_StripsAndSpeaksWords(string tag)
    {
        // Arrange
        var model = new StubSpeechModel(SpeechModelAudioTagSupport.None);

        // Act
        var plan = DefaultModelCapabilityProfile.Instance.Render(AudioTagParser.Parse($"{tag} Hello world."), model);

        // Assert
        var segment = Assert.Single(plan.Segments);
        Assert.Equal("Hello world.", segment.Text);
        Assert.Null(segment.ParameterOverrides);
    }

    /// <summary>
    ///     Proves a native model receives the canonical bracket text of a new mood tag.
    /// </summary>
    [Fact]
    public void DefaultModelCapabilityProfile_Render_NativeSupport_MoodTag_PassesCanonicalText()
    {
        // Arrange
        var model = new StubSpeechModel(SpeechModelAudioTagSupport.Native);

        // Act
        var plan = DefaultModelCapabilityProfile.Instance.Render(AudioTagParser.Parse("[angry] Hello!"), model);

        // Assert
        Assert.Contains(plan.Segments, segment => segment.Text.Contains("[angry]", StringComparison.Ordinal));
    }
    /// <summary>
    ///     Proves that a model declaring no support at all
    ///     (<see cref="SpeechModelAudioTagSupport.None"/>) always strips every tag, leaving only
    ///     the plain words.
    /// </summary>
    [Fact]
    public void DefaultModelCapabilityProfile_Render_NoneSupport_StripsAllTags()
    {
        // Arrange
        var model = new StubSpeechModel(SpeechModelAudioTagSupport.None);
        var spans = AudioTagParser.Parse("[excited] Hello [loud] world!");

        // Act
        var plan = DefaultModelCapabilityProfile.Instance.Render(spans, model);

        // Assert
        var text = string.Concat(plan.Segments.Select(segment => segment.Text));
        Assert.DoesNotContain('[', text);
        Assert.Contains("Hello", text, StringComparison.Ordinal);
        Assert.Contains("world", text, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves that plain text with no tags at all still renders as one or more chunked
    ///     segments reconstructing the original words.
    /// </summary>
    [Fact]
    public void DefaultModelCapabilityProfile_Render_PlainTextOnly_RendersChunkedSegments()
    {
        // Arrange
        var model = new StubSpeechModel(SpeechModelAudioTagSupport.None);
        var spans = AudioTagParser.Parse("Just plain narration with no tags at all.");

        // Act
        var plan = DefaultModelCapabilityProfile.Instance.Render(spans, model);

        // Assert
        Assert.NotEmpty(plan.Segments);
        Assert.All(plan.Segments, segment => Assert.True(segment.Text.Length > 0));
    }

    /// <summary>
    ///     Proves that <see cref="DefaultModelCapabilityProfile.Render"/> rejects null arguments.
    /// </summary>
    [Fact]
    public void DefaultModelCapabilityProfile_Render_NullArguments_ThrowsArgumentNullException()
    {
        // Arrange
        var model = new StubSpeechModel(SpeechModelAudioTagSupport.None);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => DefaultModelCapabilityProfile.Instance.Render(null!, model));
        Assert.Throws<ArgumentNullException>(() => DefaultModelCapabilityProfile.Instance.Render([], null!));
    }

    /// <summary>
    ///     Proves that a chunk ending in a genuine ellipsis renders a longer
    ///     <see cref="SpeechSegment.PostSilenceMs"/> than an ordinary sentence-ending chunk,
    ///     whose boundary still adds zero silence.
    /// </summary>
    [Fact]
    public void DefaultModelCapabilityProfile_Render_ChunkEndingInEllipsis_SetsLongerPostSilenceThanOrdinarySentenceEnd()
    {
        // Arrange
        var model = new StubSpeechModel(SpeechModelAudioTagSupport.None);
        var spans = AudioTagParser.Parse("Wait for it... Then it happened.");

        // Act
        var plan = DefaultModelCapabilityProfile.Instance.Render(spans, model);

        // Assert: the ellipsis-ending chunk gets a longer pause than the ordinary sentence end
        var ellipsisSegment = plan.Segments.Single(segment => segment.Text.EndsWith("...", StringComparison.Ordinal));
        var ordinarySegment = plan.Segments.Single(segment => segment.Text.EndsWith("happened.", StringComparison.Ordinal));
        Assert.True(ellipsisSegment.PostSilenceMs > 0);
        Assert.Equal(0, ordinarySegment.PostSilenceMs);
    }

    /// <summary>
    ///     Proves that the ellipsis-triggered pause duration is distinct from both the short-pause
    ///     and long-pause tag durations.
    /// </summary>
    [Fact]
    public void DefaultModelCapabilityProfile_Render_ChunkEndingInEllipsis_PauseDiffersFromShortAndLongTagPauses()
    {
        // Arrange
        var model = new StubSpeechModel(SpeechModelAudioTagSupport.None);

        // Act
        var ellipsisPlan = DefaultModelCapabilityProfile.Instance.Render(
            AudioTagParser.Parse("Wait for it... Then it happened."), model);
        var shortPlan = DefaultModelCapabilityProfile.Instance.Render(
            AudioTagParser.Parse("a [short pause] b"), model);
        var longPlan = DefaultModelCapabilityProfile.Instance.Render(
            AudioTagParser.Parse("a [long pause] b"), model);

        // Assert
        var ellipsisSilence = ellipsisPlan.Segments
            .Single(segment => segment.Text.EndsWith("...", StringComparison.Ordinal)).PostSilenceMs;
        var shortSilence = shortPlan.Segments.Single(segment => segment.Text.Length == 0).PostSilenceMs;
        var longSilence = longPlan.Segments.Single(segment => segment.Text.Length == 0).PostSilenceMs;
        Assert.NotEqual(shortSilence, ellipsisSilence);
        Assert.NotEqual(longSilence, ellipsisSilence);
    }

    /// <summary>
    ///     Proves that a chunk ending in a whitespace-spaced ellipsis (e.g. <c>". . ."</c>) also
    ///     receives the same longer, ellipsis-triggered pause as an adjacent-dot ellipsis.
    /// </summary>
    [Fact]
    public void DefaultModelCapabilityProfile_Render_ChunkEndingInSpacedEllipsis_AlsoSetsEllipsisPostSilence()
    {
        // Arrange
        var model = new StubSpeechModel(SpeechModelAudioTagSupport.None);
        var spans = AudioTagParser.Parse("Sentence one . . . . . Sentence two.");

        // Act
        var plan = DefaultModelCapabilityProfile.Instance.Render(spans, model);

        // Assert: the first (spaced-ellipsis-ending) chunk gets the ellipsis pause; the final
        // ordinary sentence end still adds zero silence
        var firstSegment = plan.Segments.Single(segment => segment.Text.StartsWith("Sentence one", StringComparison.Ordinal));
        var secondSegment = plan.Segments.Single(segment => segment.Text.StartsWith("Sentence two", StringComparison.Ordinal));
        Assert.True(firstSegment.PostSilenceMs > 0);
        Assert.Equal(0, secondSegment.PostSilenceMs);
    }

    /// <summary>
    ///     Proves that a parameter-mapped tag in the middle of narration changes only the words that
    ///     follow it: the words before the tag keep no override.
    /// </summary>
    [Fact]
    public void DefaultModelCapabilityProfile_Render_ParameterMappedTagMidText_OverridesOnlyFollowingText()
    {
        // Arrange: a model declaring a "tempo" numeric parameter and text with a tag in the middle
        var model = new StubSpeechModel(
            SpeechModelAudioTagSupport.ParameterMapped,
            [new NumericParameter("tempo", "Tempo", "Speaking rate.", new NumericParameterBounds(0.5, 2.0, 0.05, 1.0))]);
        var spans = AudioTagParser.Parse("Hello [fast] world.");

        // Act
        var plan = DefaultModelCapabilityProfile.Instance.Render(spans, model);

        // Assert: two segments; only the second, spoken after the tag, carries the tempo override
        Assert.Equal(2, plan.Segments.Count);
        Assert.Contains("Hello", plan.Segments[0].Text, StringComparison.Ordinal);
        Assert.Null(plan.Segments[0].ParameterOverrides);
        Assert.Contains("world", plan.Segments[1].Text, StringComparison.Ordinal);
        Assert.NotNull(plan.Segments[1].ParameterOverrides);
        Assert.True((double)plan.Segments[1].ParameterOverrides!["tempo"] > 1.0);
    }

    /// <summary>
    ///     Proves that two consecutive parameter-mapped tags with no text between them both apply
    ///     to the text that follows them.
    /// </summary>
    [Fact]
    public void DefaultModelCapabilityProfile_Render_ConsecutiveParameterMappedTags_BothApplyToFollowingText()
    {
        // Arrange: a model declaring both a "tempo" and a "volume" numeric parameter
        var model = new StubSpeechModel(
            SpeechModelAudioTagSupport.ParameterMapped,
            [
                new NumericParameter("tempo", "Tempo", "Speaking rate.", new NumericParameterBounds(0.5, 2.0, 0.05, 1.0)),
                new NumericParameter("volume", "Volume", "Loudness.", new NumericParameterBounds(0.1, 2.0, 0.05, 1.0)),
            ]);
        var spans = AudioTagParser.Parse("Hello. [fast][loud] World.");

        // Act
        var plan = DefaultModelCapabilityProfile.Instance.Render(spans, model);

        // Assert: the last segment carries both overrides; the first carries none
        Assert.Null(plan.Segments[0].ParameterOverrides);
        var last = plan.Segments[^1];
        Assert.NotNull(last.ParameterOverrides);
        Assert.True(last.ParameterOverrides.ContainsKey("tempo"));
        Assert.True(last.ParameterOverrides.ContainsKey("volume"));
    }

    /// <summary>
    ///     Proves that <see cref="SpeechPlan"/> rejects a null segment list.
    /// </summary>
    [Fact]
    public void SpeechPlan_Constructor_NullSegments_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new SpeechPlan(null!));
    }

    /// <summary>
    ///     Proves that <see cref="SpeechPlan"/> rejects a segment list containing a null element.
    /// </summary>
    [Fact]
    public void SpeechPlan_Constructor_NullSegmentElement_ThrowsArgumentException()
    {
        // Arrange
        var segments = new SpeechSegment[] { new("Hello.", 0, 0, null), null! };

        // Act & Assert
        Assert.Throws<ArgumentException>(() => new SpeechPlan(segments));
    }

    /// <summary>
    ///     Proves that <see cref="SpeechPlan"/> keeps its own copy of the segments, so a later change
    ///     to the caller's list does not alter the plan.
    /// </summary>
    [Fact]
    public void SpeechPlan_Constructor_CallerListChangedLater_PlanIsUnaffected()
    {
        // Arrange
        var segments = new List<SpeechSegment> { new("Hello.", 0, 0, null) };
        var plan = new SpeechPlan(segments);

        // Act
        segments.Add(new SpeechSegment("Extra.", 0, 0, null));

        // Assert
        Assert.Single(plan.Segments);
    }

    /// <summary>
    ///     Proves that <see cref="SpeechSegment"/> rejects a null text.
    /// </summary>
    [Fact]
    public void SpeechSegment_Constructor_NullText_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new SpeechSegment(null!, 0, 0, null));
    }

    /// <summary>
    ///     Proves that <see cref="SpeechSegment"/> rejects a negative pre-silence or post-silence.
    /// </summary>
    [Theory]
    [InlineData(-1, 0, "PreSilenceMs")]
    [InlineData(0, -1, "PostSilenceMs")]
    public void SpeechSegment_Constructor_NegativeSilence_ThrowsArgumentOutOfRangeException(
        int preSilenceMs,
        int postSilenceMs,
        string expectedParamName)
    {
        // Act
        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => new SpeechSegment("Hello.", preSilenceMs, postSilenceMs, null));

        // Assert
        Assert.Equal(expectedParamName, exception.ParamName);
    }

    /// <summary>
    ///     Proves that <see cref="SpeechSegment"/> accepts empty text and zero silence, the valid
    ///     boundary values.
    /// </summary>
    [Fact]
    public void SpeechSegment_Constructor_EmptyTextAndZeroSilence_IsAccepted()
    {
        // Act
        var segment = new SpeechSegment(string.Empty, 0, 0, null);

        // Assert
        Assert.Equal(string.Empty, segment.Text);
        Assert.Equal(0, segment.PreSilenceMs);
        Assert.Equal(0, segment.PostSilenceMs);
        Assert.Null(segment.ParameterOverrides);
    }

    /// <summary>
    ///     Proves a plain-text span's annotation is carried by the segment that contains it.
    /// </summary>
    [Fact]
    public void DefaultModelCapabilityProfile_Render_AnnotatedSpan_SegmentCarriesRebasedAnnotation()
    {
        // Arrange
        var model = new StubSpeechModel(SpeechModelAudioTagSupport.None);
        var spans = new[] { TaggedTextSpan.PlainText("Say read it.", [new SegmentAnnotation(4, 4, "reading", "red")], null) };

        // Act
        var plan = DefaultModelCapabilityProfile.Instance.Render(spans, model);

        // Assert
        var segment = Assert.Single(plan.Segments);
        var annotation = Assert.Single(segment.Annotations);
        Assert.Equal("read", segment.Text.Substring(annotation.Start, annotation.Length));
    }

    /// <summary>
    ///     Proves an annotation after a stripped tag is re-based into the merged text buffer.
    /// </summary>
    [Fact]
    public void DefaultModelCapabilityProfile_Render_AnnotationAfterStrippedTag_RebasedIntoMergedBuffer()
    {
        // Arrange: the stripped tag leaves "I've " + " read it." => "I've  read it."
        var model = new StubSpeechModel(SpeechModelAudioTagSupport.None);
        var spans = new[]
        {
            TaggedTextSpan.PlainText("I've "),
            TaggedTextSpan.TagSpan(NaturalLanguageAudioTag.Whispers),
            TaggedTextSpan.PlainText(" read it.", [new SegmentAnnotation(1, 4, "reading", "red")], null),
        };

        // Act
        var plan = DefaultModelCapabilityProfile.Instance.Render(spans, model);

        // Assert
        var segment = Assert.Single(plan.Segments);
        Assert.Equal("I've  read it.", segment.Text);
        var annotation = Assert.Single(segment.Annotations);
        Assert.Equal(6, annotation.Start);
        Assert.Equal("read", segment.Text.Substring(annotation.Start, annotation.Length));
    }

    /// <summary>
    ///     Proves a passed-through native tag's inserted text shifts later annotation offsets.
    /// </summary>
    [Fact]
    public void DefaultModelCapabilityProfile_Render_NativeTag_AnnotationOffsetsShiftedPastInsertedTagText()
    {
        // Arrange: the native tag inserts "[excited]" (9 characters) into the text
        var model = new StubSpeechModel(SpeechModelAudioTagSupport.Native);
        var spans = new[]
        {
            TaggedTextSpan.PlainText("I've "),
            TaggedTextSpan.TagSpan(NaturalLanguageAudioTag.Excited),
            TaggedTextSpan.PlainText(" read it.", [new SegmentAnnotation(1, 4, "reading", "red")], null),
        };

        // Act
        var plan = DefaultModelCapabilityProfile.Instance.Render(spans, model);

        // Assert
        var segment = Assert.Single(plan.Segments);
        Assert.Equal("I've [excited] read it.", segment.Text);
        var annotation = Assert.Single(segment.Annotations);
        Assert.Equal(15, annotation.Start);
        Assert.Equal("read", segment.Text.Substring(annotation.Start, annotation.Length));
    }

    /// <summary>
    ///     Proves a parameter-mapped tag flushes the buffer, so each side's annotations stay with
    ///     their own segment (and that segment's overrides).
    /// </summary>
    [Fact]
    public void DefaultModelCapabilityProfile_Render_ParameterMappedTag_AnnotationsStayWithTheirOwnSegment()
    {
        // Arrange
        var model = new StubSpeechModel(
            SpeechModelAudioTagSupport.ParameterMapped,
            [new NumericParameter("tempo", "Tempo", "Speaking rate.", new NumericParameterBounds(0.5, 2.0, 0.05, 1.0))]);
        var spans = new[]
        {
            TaggedTextSpan.PlainText("Say read.", [new SegmentAnnotation(4, 4, "reading", "red")], null),
            TaggedTextSpan.TagSpan(NaturalLanguageAudioTag.Fast),
            TaggedTextSpan.PlainText("Then read.", [new SegmentAnnotation(5, 4, "reading", "reed")], null),
        };

        // Act
        var plan = DefaultModelCapabilityProfile.Instance.Render(spans, model);

        // Assert
        Assert.Equal(2, plan.Segments.Count);
        Assert.Null(plan.Segments[0].ParameterOverrides);
        Assert.Equal("red", Assert.Single(plan.Segments[0].Annotations).Value);
        Assert.NotNull(plan.Segments[1].ParameterOverrides);
        var second = Assert.Single(plan.Segments[1].Annotations);
        Assert.Equal("reed", second.Value);
        Assert.Equal("read", plan.Segments[1].Text.Substring(second.Start, second.Length));
    }

    /// <summary>
    ///     Proves a pause tag flushes the buffer, so annotations stay with their own segment and
    ///     the silence segment carries none.
    /// </summary>
    [Fact]
    public void DefaultModelCapabilityProfile_Render_PauseTag_AnnotationsStayWithTheirOwnSegment()
    {
        // Arrange
        var model = new StubSpeechModel(SpeechModelAudioTagSupport.None);
        var spans = new[]
        {
            TaggedTextSpan.PlainText("Say read.", [new SegmentAnnotation(4, 4, "reading", "red")], null),
            TaggedTextSpan.TagSpan(NaturalLanguageAudioTag.ShortPause),
            TaggedTextSpan.PlainText("Then read.", [new SegmentAnnotation(5, 4, "reading", "reed")], null),
        };

        // Act
        var plan = DefaultModelCapabilityProfile.Instance.Render(spans, model);

        // Assert
        Assert.Equal(3, plan.Segments.Count);
        Assert.Equal("red", Assert.Single(plan.Segments[0].Annotations).Value);
        Assert.Empty(plan.Segments[1].Annotations);
        Assert.Equal(string.Empty, plan.Segments[1].Text);
        Assert.Equal("reed", Assert.Single(plan.Segments[2].Annotations).Value);
    }

    /// <summary>
    ///     Proves analyzer sentence breaks on a span replace the punctuation heuristic.
    /// </summary>
    [Fact]
    public void DefaultModelCapabilityProfile_Render_SpanBreaksSupplied_SplitsAtAnalyzerBreaksNotPunctuation()
    {
        // Arrange
        var model = new StubSpeechModel(SpeechModelAudioTagSupport.None);
        var spans = new[] { TaggedTextSpan.PlainText("Dr. Smith arrived. He left.", [], [18]) };

        // Act
        var plan = DefaultModelCapabilityProfile.Instance.Render(spans, model);

        // Assert
        Assert.Equal(["Dr. Smith arrived.", "He left."], plan.Segments.Select(segment => segment.Text));
    }

    /// <summary>
    ///     Proves an analyzer sentence break on either side of a stripped tag splits the text at
    ///     the seam.
    /// </summary>
    [Fact]
    public void DefaultModelCapabilityProfile_Render_SentenceBreakAtTagSeam_SplitsAtSeam()
    {
        // Arrange: buffer is "Hello world" + " Next thing"; both breaks fall at offset 11
        var model = new StubSpeechModel(SpeechModelAudioTagSupport.None);
        var spans = new[]
        {
            TaggedTextSpan.PlainText("Hello world", [], [11]),
            TaggedTextSpan.TagSpan(NaturalLanguageAudioTag.Whispers),
            TaggedTextSpan.PlainText(" Next thing", [], [0]),
        };

        // Act
        var plan = DefaultModelCapabilityProfile.Instance.Render(spans, model);

        // Assert
        Assert.Equal(["Hello world", "Next thing"], plan.Segments.Select(segment => segment.Text));
    }

    /// <summary>
    ///     Proves a sentence-break list on an empty plain span does not leak into the next
    ///     non-empty span: that span, which has no analysis of its own, keeps its normal
    ///     punctuation splits.
    /// </summary>
    [Fact]
    public void DefaultModelCapabilityProfile_Render_EmptySpanWithBreaks_DoesNotMakeNextSpanAuthoritative()
    {
        // Arrange: the empty span's [0] must be discarded when it is flushed away
        var model = new StubSpeechModel(SpeechModelAudioTagSupport.None);
        var spans = new[]
        {
            TaggedTextSpan.PlainText(string.Empty, [], [0]),
            TaggedTextSpan.TagSpan(NaturalLanguageAudioTag.ShortPause),
            TaggedTextSpan.PlainText("One. Two."),
        };

        // Act
        var plan = DefaultModelCapabilityProfile.Instance.Render(spans, model);

        // Assert
        Assert.Equal(
            ["One.", "Two."],
            plan.Segments.Where(segment => segment.Text.Length > 0).Select(segment => segment.Text));
    }

    /// <summary>
    ///     Proves a sentence-break list on an empty plain span does not leak into a following
    ///     non-empty span even when no flush intervenes (a parameter-mapped tag on an empty
    ///     buffer, or no tag at all).
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void DefaultModelCapabilityProfile_Render_EmptySpanWithBreaksNoFlush_NextSpanKeepsPunctuationSplits(bool withTag)
    {
        // Arrange
        var model = new StubSpeechModel(SpeechModelAudioTagSupport.ParameterMapped);
        var spans = new List<TaggedTextSpan> { TaggedTextSpan.PlainText(string.Empty, [], [0]) };
        if (withTag)
        {
            spans.Add(TaggedTextSpan.TagSpan(NaturalLanguageAudioTag.Fast));
        }

        spans.Add(TaggedTextSpan.PlainText("One. Two."));

        // Act
        var plan = DefaultModelCapabilityProfile.Instance.Render(spans, model);

        // Assert
        Assert.Equal(["One.", "Two."], plan.Segments.Select(segment => segment.Text));
    }

    /// <summary>
    ///     Builds the span run <c>First</c>, two tags, <c>Second</c> where the first tag carries
    ///     the given sentence breaks and the plain spans carry authoritative empty break lists.
    /// </summary>
    private static TaggedTextSpan[] FirstTwoTagsSecond(
        NaturalLanguageAudioTag firstTag,
        NaturalLanguageAudioTag secondTag,
        IReadOnlyList<int>? firstTagBreaks) =>
    [
        TaggedTextSpan.PlainText("First", [], []),
        TaggedTextSpan.TagSpan(firstTag) with { SentenceBreaks = firstTagBreaks },
        TaggedTextSpan.TagSpan(secondTag),
        TaggedTextSpan.PlainText("Second", [], []),
    ];

    /// <summary>Returns the canonical bracket text the default profile passes through for a tag.</summary>
    private static string NativeText(NaturalLanguageAudioTag tag) =>
        $"[{AudioTagCatalog.Tags.First(descriptor => descriptor.Tag == tag).Aliases[0]}]";

    /// <summary>
    ///     Proves a break carried on the first of two consecutive stripped tags splits the text
    ///     at the seam between them.
    /// </summary>
    [Fact]
    public void DefaultModelCapabilityProfile_Render_TagSpanBreakBetweenConsecutiveTags_NoneSupport_SplitsAtSeam()
    {
        // Arrange
        var model = new StubSpeechModel(SpeechModelAudioTagSupport.None);
        var spans = FirstTwoTagsSecond(NaturalLanguageAudioTag.Fast, NaturalLanguageAudioTag.Excited, [1]);

        // Act
        var plan = DefaultModelCapabilityProfile.Instance.Render(spans, model);

        // Assert
        Assert.Equal(["First", "Second"], plan.Segments.Select(segment => segment.Text));
    }

    /// <summary>
    ///     Proves a break between two native tags splits between the two tags' passed-through
    ///     text, exactly where the analyzer placed it.
    /// </summary>
    [Fact]
    public void DefaultModelCapabilityProfile_Render_TagSpanBreakBetweenConsecutiveTags_NativeSupport_SplitsBetweenTagTexts()
    {
        // Arrange
        var model = new StubSpeechModel(SpeechModelAudioTagSupport.Native);
        var spans = FirstTwoTagsSecond(NaturalLanguageAudioTag.Angry, NaturalLanguageAudioTag.Excited, [1]);

        // Act
        var plan = DefaultModelCapabilityProfile.Instance.Render(spans, model);

        // Assert
        Assert.Equal(
            [
                "First" + NativeText(NaturalLanguageAudioTag.Angry),
                NativeText(NaturalLanguageAudioTag.Excited) + "Second",
            ],
            plan.Segments.Select(segment => segment.Text));
    }

    /// <summary>
    ///     Proves a break between parameter-mapped tags adds no artifacts: the tags already end
    ///     the segment, so the result is the same two segments.
    /// </summary>
    [Fact]
    public void DefaultModelCapabilityProfile_Render_TagSpanBreakBetweenConsecutiveTags_ParameterMappedSupport_TwoSegments()
    {
        // Arrange
        var model = new StubSpeechModel(
            SpeechModelAudioTagSupport.ParameterMapped,
            [new NumericParameter("tempo", "Tempo", "Speaking rate.", new NumericParameterBounds(0.5, 2.0, 0.05, 1.0))]);
        var withBreak = FirstTwoTagsSecond(NaturalLanguageAudioTag.Fast, NaturalLanguageAudioTag.Slow, [1]);
        var withoutBreak = FirstTwoTagsSecond(NaturalLanguageAudioTag.Fast, NaturalLanguageAudioTag.Slow, null);

        // Act
        var plan = DefaultModelCapabilityProfile.Instance.Render(withBreak, model);
        var control = DefaultModelCapabilityProfile.Instance.Render(withoutBreak, model);

        // Assert
        Assert.Equal(["First", "Second"], plan.Segments.Select(segment => segment.Text));
        Assert.Equal(control.Segments.Select(segment => segment.Text), plan.Segments.Select(segment => segment.Text));
        Assert.Equal(
            control.Segments.Select(segment => segment.ParameterOverrides?["tempo"]),
            plan.Segments.Select(segment => segment.ParameterOverrides?["tempo"]));
    }

    /// <summary>
    ///     Proves a break at tag offset 0 splits immediately before the tag's passed-through text.
    /// </summary>
    [Fact]
    public void DefaultModelCapabilityProfile_Render_TagSpanBreakBeforeOffsetZero_SplitsBeforeTag()
    {
        // Arrange
        var model = new StubSpeechModel(SpeechModelAudioTagSupport.Native);
        var spans = FirstTwoTagsSecond(NaturalLanguageAudioTag.Angry, NaturalLanguageAudioTag.Excited, [0]);

        // Act
        var plan = DefaultModelCapabilityProfile.Instance.Render(spans, model);

        // Assert
        Assert.Equal(
            [
                "First",
                NativeText(NaturalLanguageAudioTag.Angry) + NativeText(NaturalLanguageAudioTag.Excited) + "Second",
            ],
            plan.Segments.Select(segment => segment.Text));
    }

    /// <summary>
    ///     Control: authoritative empty break lists with no tag-span break keep the text across a
    ///     tag run in one segment, so the absence of a break still means no split.
    /// </summary>
    [Fact]
    public void DefaultModelCapabilityProfile_Render_AuthoritativeEmptyBreaksAcrossTagRun_StaysOneSegment()
    {
        // Arrange
        var model = new StubSpeechModel(SpeechModelAudioTagSupport.None);
        var spans = FirstTwoTagsSecond(NaturalLanguageAudioTag.Fast, NaturalLanguageAudioTag.Excited, null);

        // Act
        var plan = DefaultModelCapabilityProfile.Instance.Render(spans, model);

        // Assert
        Assert.Equal("FirstSecond", Assert.Single(plan.Segments).Text);
    }

    /// <summary>
    ///     Proves spans without annotations or breaks render to exactly the segments, with value
    ///     equality, that the profile produced before annotations existed.
    /// </summary>
    [Fact]
    public void DefaultModelCapabilityProfile_Render_SpansWithoutAnnotations_SegmentsEqualPreChangeShape()
    {
        // Arrange
        var model = new StubSpeechModel(SpeechModelAudioTagSupport.None);
        var spans = AudioTagParser.Parse("Hello, world. Wait... done.");

        // Act
        var plan = DefaultModelCapabilityProfile.Instance.Render(spans, model);

        // Assert
        Assert.Equal(
            [
                new SpeechSegment("Hello,", 0, 0, null),
                new SpeechSegment("world.", 0, 0, null),
                new SpeechSegment("Wait...", 0, DefaultModelCapabilityProfile.EllipsisPauseMilliseconds, null),
                new SpeechSegment("done.", 0, 0, null),
            ],
            plan.Segments);
    }

    /// <summary>
    ///     Proves the default profile is a public singleton an external profile can delegate to.
    /// </summary>
    [Fact]
    public void DefaultModelCapabilityProfile_Instance_IsPublicSingletonUsableByExternalProfile()
    {
        // Act
        var type = typeof(DefaultModelCapabilityProfile);

        // Assert
        Assert.True(type.IsPublic);
        Assert.Same(DefaultModelCapabilityProfile.Instance, DefaultModelCapabilityProfile.Instance);
        Assert.Empty(type.GetConstructors());
        Assert.IsAssignableFrom<IModelCapabilityProfile>(DefaultModelCapabilityProfile.Instance);
    }

    /// <summary>
    ///     Proves the public chunk-then-render step returns segments carrying chunk-relative
    ///     annotations.
    /// </summary>
    [Fact]
    public void DefaultModelCapabilityProfile_RenderText_AnnotatedText_ReturnsSegmentsWithRebasedAnnotations()
    {
        // Arrange
        const string text = "First one. Second read here.";

        // Act
        var segments = DefaultModelCapabilityProfile.RenderText(text, [new SegmentAnnotation(18, 4, "reading", "red")]);

        // Assert
        Assert.Equal(["First one.", "Second read here."], segments.Select(segment => segment.Text));
        Assert.Empty(segments[0].Annotations);
        var annotation = Assert.Single(segments[1].Annotations);
        Assert.Equal("read", segments[1].Text.Substring(annotation.Start, annotation.Length));
    }

    /// <summary>
    ///     Proves a chunk ending in an ellipsis gets the ellipsis pause, and overrides are applied
    ///     to every segment.
    /// </summary>
    [Fact]
    public void DefaultModelCapabilityProfile_RenderText_EllipsisChunk_SetsEllipsisPostSilence()
    {
        // Arrange
        var overrides = new Dictionary<string, object> { ["tempo"] = 1.2 };

        // Act
        var segments = DefaultModelCapabilityProfile.RenderText("Wait... done.", null, null, overrides);

        // Assert
        Assert.Equal(DefaultModelCapabilityProfile.EllipsisPauseMilliseconds, segments[0].PostSilenceMs);
        Assert.Equal(0, segments[1].PostSilenceMs);
        Assert.All(segments, segment => Assert.Same(overrides, segment.ParameterOverrides));
    }

    /// <summary>
    ///     Proves a null text is rejected.
    /// </summary>
    [Fact]
    public void DefaultModelCapabilityProfile_RenderText_NullText_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => DefaultModelCapabilityProfile.RenderText(null!));
    }

    /// <summary>
    ///     Proves the public pause constants keep their documented values.
    /// </summary>
    [Fact]
    public void DefaultModelCapabilityProfile_PauseConstants_HaveDocumentedValues()
    {
        // Act & Assert
        Assert.Equal(300, DefaultModelCapabilityProfile.ShortPauseMilliseconds);
        Assert.Equal(900, DefaultModelCapabilityProfile.LongPauseMilliseconds);
        Assert.Equal(500, DefaultModelCapabilityProfile.EllipsisPauseMilliseconds);
    }

    /// <summary>
    ///     Proves a segment has no annotations by default.
    /// </summary>
    [Fact]
    public void SpeechSegment_Annotations_DefaultsToEmpty()
    {
        // Act
        var segment = new SpeechSegment("Hello", 0, 0, null);

        // Assert
        Assert.Empty(segment.Annotations);
    }

    /// <summary>
    ///     Proves an annotation that extends beyond the segment text is rejected.
    /// </summary>
    [Fact]
    public void SpeechSegment_Annotations_OutOfRange_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new SpeechSegment("Hello", 0, 0, null)
        {
            Annotations = [new SegmentAnnotation(3, 5, "k", "v")],
        });
    }

    /// <summary>
    ///     Proves a null annotation element is rejected.
    /// </summary>
    [Fact]
    public void SpeechSegment_Annotations_NullElement_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new SpeechSegment("Hello", 0, 0, null) { Annotations = [null!] });
    }

    /// <summary>
    ///     Proves the segment keeps a private copy of the caller's annotation list.
    /// </summary>
    [Fact]
    public void SpeechSegment_Annotations_CallerListChangedLater_SegmentIsUnaffected()
    {
        // Arrange
        var annotations = new List<SegmentAnnotation> { new(0, 2, "k", "v") };
        var segment = new SpeechSegment("Hello", 0, 0, null) { Annotations = annotations };

        // Act
        annotations.Clear();

        // Assert
        Assert.Single(segment.Annotations);
    }

    /// <summary>
    ///     Proves a <c>with</c> expression cannot shorten the text so that an existing annotation
    ///     would be orphaned.
    /// </summary>
    [Fact]
    public void SpeechSegment_With_ShorterTextWouldOrphanAnnotation_ThrowsArgumentException()
    {
        // Arrange
        var segment = new SpeechSegment("Hello world", 0, 0, null)
        {
            Annotations = [new SegmentAnnotation(6, 5, "k", "v")],
        };

        // Act & Assert
        Assert.Throws<ArgumentException>(() => segment with { Text = "Hello" });
        Assert.Equal("Hello world!", (segment with { Text = "Hello world!" }).Text);
    }

    /// <summary>
    ///     A minimal, declared-support-only <see cref="ISpeechModel"/> stand-in, so this test
    ///     class can drive every <see cref="SpeechModelAudioTagSupport"/> value directly without
    ///     depending on <see cref="FakeSynthesisModel"/>'s fixed <c>ParameterMapped</c> declaration.
    /// </summary>
    private sealed class StubSpeechModel : ISpeechModel
    {
        public StubSpeechModel(
            SpeechModelAudioTagSupport audioTagSupport,
            IReadOnlyList<ISpeechModelParameter>? parameters = null)
        {
            AudioTagSupport = audioTagSupport;
            Parameters = parameters ?? [];
        }

        public string Id => "stub-model";

        public string DisplayName => "Stub Model";

        public SpeechModelRole Role => SpeechModelRole.Synthesis;

        public IReadOnlyList<ISpeechModelParameter> Parameters { get; }

        public SpeechModelAudioTagSupport AudioTagSupport { get; }

        public SpeechModelDownloadDescriptor DownloadDescriptor => FakeModelDescriptors.SingleFileDescriptor(Id);
    }
}
