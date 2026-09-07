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
