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
