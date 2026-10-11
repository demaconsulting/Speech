using DemaConsulting.Speech.ModelManagementSubsystem;
using DemaConsulting.Speech.Onnx.ChatterboxTurbo.ModelManagementSubsystem;
using DemaConsulting.Speech.Onnx.ChatterboxTurbo.SynthesisSubsystem;
using DemaConsulting.Speech.SynthesisSubsystem;

namespace DemaConsulting.Speech.Onnx.ChatterboxTurbo.Tests.SynthesisSubsystem;

/// <summary>
///     Unit tests for <see cref="ChatterboxTurboCapabilityProfile"/>.
/// </summary>
public sealed class ChatterboxTurboCapabilityProfileTests
{
    /// <summary>The model passed to the profile (only used as an <see cref="ISpeechModel"/> value).</summary>
    private static readonly OnnxChatterboxTurboEnglishSynthesisModel Model = new();

    /// <summary>Proves a mid-sentence native tag stays in the same synthesis unit as the model's exact token.</summary>
    [Fact]
    public void ChatterboxTurboCapabilityProfile_Render_MidSentenceLaugh_StaysInlineWithNativeToken()
    {
        // Act
        var plan = Render(
            ChatterboxTurboEndOfSentenceTagPolicy.MoveBeforeFinalWord,
            Text("That was so "),
            Tag(NaturalLanguageAudioTag.Laughs),
            Text(" funny today."));

        // Assert
        var segment = Assert.Single(plan.Segments);
        Assert.Equal("That was so [laugh] funny today.", segment.Text);
    }

    /// <summary>Proves clear-throat and gasp use the model's own token text.</summary>
    [Theory]
    [InlineData(NaturalLanguageAudioTag.ClearsThroat, "[clear throat]")]
    [InlineData(NaturalLanguageAudioTag.Gasp, "[gasp]")]
    [InlineData(NaturalLanguageAudioTag.Laughs, "[laugh]")]
    public void ChatterboxTurboCapabilityProfile_Render_NativeTags_UseModelTokens(NaturalLanguageAudioTag tag, string token)
    {
        // Act
        var plan = Render(
            ChatterboxTurboEndOfSentenceTagPolicy.MoveBeforeFinalWord,
            Text("Well "),
            Tag(tag),
            Text(" then we go."));

        // Assert
        Assert.Contains(token, Assert.Single(plan.Segments).Text);
    }

    /// <summary>Proves unsupported tags (sigh, emotions, volume) are never emitted as model tokens.</summary>
    [Theory]
    [InlineData(NaturalLanguageAudioTag.Sighs)]
    [InlineData(NaturalLanguageAudioTag.Excited)]
    [InlineData(NaturalLanguageAudioTag.Sad)]
    [InlineData(NaturalLanguageAudioTag.Whispers)]
    [InlineData(NaturalLanguageAudioTag.Breathy)]
    [InlineData(NaturalLanguageAudioTag.Snorts)]
    [InlineData(NaturalLanguageAudioTag.Emphasis)]
    public void ChatterboxTurboCapabilityProfile_Render_UnsupportedTags_NeverEmitBracketText(NaturalLanguageAudioTag tag)
    {
        // Act
        var plan = Render(ChatterboxTurboEndOfSentenceTagPolicy.MoveBeforeFinalWord, Text("Hello "), Tag(tag), Text(" there my friend."));

        // Assert
        Assert.All(plan.Segments, segment => Assert.DoesNotContain("[", segment.Text));
        Assert.Contains(plan.Segments, segment => segment.Text.Contains("friend."));
    }

    /// <summary>Proves a sigh becomes real silence rather than a token.</summary>
    [Fact]
    public void ChatterboxTurboCapabilityProfile_Render_Sigh_BecomesSilenceSegment()
    {
        // Act
        var plan = Render(ChatterboxTurboEndOfSentenceTagPolicy.Strip, Text("Oh well."), Tag(NaturalLanguageAudioTag.Sighs), Text(" Fine."));

        // Assert
        Assert.Equal(3, plan.Segments.Count);
        Assert.Equal(string.Empty, plan.Segments[1].Text);
        Assert.Equal(300, plan.Segments[1].PostSilenceMs);
    }

    /// <summary>Proves pause tags become silence of the documented length.</summary>
    [Theory]
    [InlineData(NaturalLanguageAudioTag.ShortPause, 300)]
    [InlineData(NaturalLanguageAudioTag.LongPause, 900)]
    public void ChatterboxTurboCapabilityProfile_Render_PauseTags_BecomeSilence(NaturalLanguageAudioTag tag, int milliseconds)
    {
        // Act
        var plan = Render(ChatterboxTurboEndOfSentenceTagPolicy.Strip, Text("Before."), Tag(tag), Text("After."));

        // Assert
        Assert.Equal(milliseconds, plan.Segments[1].PostSilenceMs);
        Assert.Equal(string.Empty, plan.Segments[1].Text);
    }

    /// <summary>Proves the default policy relocates a sentence-final tag before the final word.</summary>
    [Fact]
    public void ChatterboxTurboCapabilityProfile_Render_TrailingTag_MoveBeforeFinalWord_RelocatesTag()
    {
        // Act
        var plan = Render(
            ChatterboxTurboEndOfSentenceTagPolicy.MoveBeforeFinalWord,
            Text("That was really funny."),
            Tag(NaturalLanguageAudioTag.Laughs));

        // Assert
        Assert.Equal("That was really [laugh] funny.", Assert.Single(plan.Segments).Text);
    }

    /// <summary>Proves the suffix policy appends the tag with its known-good suffix, or drops tags without one.</summary>
    [Fact]
    public void ChatterboxTurboCapabilityProfile_Render_TrailingTag_Suffix_AppendsOrDrops()
    {
        // Act
        var laugh = Render(ChatterboxTurboEndOfSentenceTagPolicy.Suffix, Text("Funny."), Tag(NaturalLanguageAudioTag.Laughs));
        var gasp = Render(ChatterboxTurboEndOfSentenceTagPolicy.Suffix, Text("Funny."), Tag(NaturalLanguageAudioTag.Gasp));

        // Assert
        Assert.Equal("Funny. [laugh] ...", Assert.Single(laugh.Segments).Text);
        Assert.Equal("Funny.", Assert.Single(gasp.Segments).Text);
    }

    /// <summary>Proves the strip policy drops a sentence-final tag entirely.</summary>
    [Fact]
    public void ChatterboxTurboCapabilityProfile_Render_TrailingTag_Strip_DropsTag()
    {
        // Act
        var plan = Render(ChatterboxTurboEndOfSentenceTagPolicy.Strip, Text("That was funny."), Tag(NaturalLanguageAudioTag.Laughs));

        // Assert
        Assert.Equal("That was funny.", Assert.Single(plan.Segments).Text);
    }

    /// <summary>Proves a tag standing alone between sentences leads the next sentence rather than becoming its own unit.</summary>
    [Fact]
    public void ChatterboxTurboCapabilityProfile_Render_TagBetweenSentences_LeadsNextSentence()
    {
        // Act
        var plan = Render(
            ChatterboxTurboEndOfSentenceTagPolicy.MoveBeforeFinalWord,
            Text("First one. "),
            Tag(NaturalLanguageAudioTag.Gasp),
            Text(" Second one."));

        // Assert
        Assert.Equal(2, plan.Segments.Count);
        Assert.Equal("First one.", plan.Segments[0].Text);
        Assert.Equal("[gasp] Second one.", plan.Segments[1].Text);
    }

    /// <summary>Proves text is split into sentences, numerals survive, and an ellipsis adds a longer pause.</summary>
    [Fact]
    public void ChatterboxTurboCapabilityProfile_Render_Sentences_AreSplitWithEllipsisPause()
    {
        // Act
        var plan = Render(ChatterboxTurboEndOfSentenceTagPolicy.Strip, Text("It costs 3.14 dollars. Wait... What?! Yes."));

        // Assert
        Assert.Equal(["It costs 3.14 dollars.", "Wait...", "What?!", "Yes."], plan.Segments.Select(s => s.Text));
        Assert.Equal(500, plan.Segments[1].PostSilenceMs);
        Assert.Equal(0, plan.Segments[0].PostSilenceMs);
    }

    /// <summary>Proves an over-long sentence is split into units no longer than the limit.</summary>
    [Fact]
    public void ChatterboxTurboCapabilityProfile_Render_LongSentence_IsSplit()
    {
        // Arrange
        var sentence = string.Join(' ', Enumerable.Repeat("word", 120)) + ".";

        // Act
        var plan = Render(ChatterboxTurboEndOfSentenceTagPolicy.Strip, Text(sentence));

        // Assert
        Assert.True(plan.Segments.Count > 1);
        Assert.All(plan.Segments, segment => Assert.True(segment.Text.Length <= 200));
    }

    /// <summary>Proves empty input produces an empty plan and null arguments are rejected.</summary>
    [Fact]
    public void ChatterboxTurboCapabilityProfile_Render_EmptyOrInvalid_BehavesSafely()
    {
        // Arrange
        var profile = new ChatterboxTurboCapabilityProfile(ChatterboxTurboEndOfSentenceTagPolicy.Strip);

        // Assert
        Assert.Empty(profile.Render([], Model).Segments);
        Assert.Throws<ArgumentNullException>(() => profile.Render(null!, Model));
        Assert.Throws<ArgumentNullException>(() => profile.Render([], null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ChatterboxTurboCapabilityProfile((ChatterboxTurboEndOfSentenceTagPolicy)99));
    }

    /// <summary>Proves every library tag has a table row and none maps to an emotion token.</summary>
    [Fact]
    public void ChatterboxTurboCapabilityProfile_Treatments_CoverEveryTagWithoutEmotionTokens()
    {
        // Assert
        Assert.Equal(Enum.GetValues<NaturalLanguageAudioTag>().Length, ChatterboxTurboCapabilityProfile.Treatments.Count);
        string[] emotions = ["[angry]", "[fear]", "[surprised]", "[whisper]", "[dramatic]", "[narration]", "[crying]", "[happy]", "[sarcastic]", "[sigh]"];
        Assert.DoesNotContain(
            ChatterboxTurboCapabilityProfile.Treatments.Values,
            treatment => treatment.Token is not null && emotions.Contains(treatment.Token));
    }

    /// <summary>Renders spans with a profile.</summary>
    private static SpeechPlan Render(ChatterboxTurboEndOfSentenceTagPolicy policy, params TaggedTextSpan[] spans) =>
        new ChatterboxTurboCapabilityProfile(policy).Render(spans, Model);

    /// <summary>Creates a plain-text span.</summary>
    private static TaggedTextSpan Text(string text) => TaggedTextSpan.PlainText(text);

    /// <summary>Creates a tag span.</summary>
    private static TaggedTextSpan Tag(NaturalLanguageAudioTag tag) => TaggedTextSpan.TagSpan(tag);
}
