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

    /// <summary>Proves a full stop flanked by digits is not a sentence boundary.</summary>
    [Fact]
    public void ChatterboxTurboCapabilityProfile_Render_DigitFlankedStop_IsNotASentenceBoundary()
    {
        // Act
        var plan = Render(ChatterboxTurboEndOfSentenceTagPolicy.Strip, Text("Version 2.5.1 costs 3.50 today. Done."));

        // Assert
        Assert.Equal(["Version 2.5.1 costs 3.50 today.", "Done."], plan.Segments.Select(s => s.Text));
    }

    /// <summary>Proves an over-long clause with no punctuation is split on a word budget without losing or reordering words.</summary>
    [Fact]
    public void ChatterboxTurboCapabilityProfile_Render_OverLongClause_SplitsOnWordBudgetKeepingEveryWord()
    {
        // Arrange
        var words = Enumerable.Range(0, 120).Select(i => "w" + i).ToArray();

        // Act
        var plan = Render(ChatterboxTurboEndOfSentenceTagPolicy.Strip, Text(string.Join(' ', words) + "."));

        // Assert
        var rejoined = string.Join(' ', plan.Segments.Select(s => s.Text)).TrimEnd('.').Split(' ');
        Assert.True(plan.Segments.Count > 1);
        Assert.Equal(words, rejoined);
    }

    /// <summary>Proves clauses are split at secondary boundaries (including with a digit-flanked colon kept intact) without losing words.</summary>
    [Fact]
    public void ChatterboxTurboCapabilityProfile_Render_LongSentenceWithClauses_SplitsAtClauseBoundaries()
    {
        // Arrange: ten clauses of about 30 characters, one holding a time like 12:30
        var clauses = Enumerable.Range(0, 10).Select(i => $"clause number {i} at 12:30 sharp").ToArray();
        var sentence = string.Join(", ", clauses) + ".";

        // Act
        var plan = Render(ChatterboxTurboEndOfSentenceTagPolicy.Strip, Text(sentence));

        // Assert: every unit ends at a clause boundary, within the limit, and nothing is lost
        Assert.True(plan.Segments.Count > 1);
        Assert.All(plan.Segments, segment => Assert.True(segment.Text.Length <= 200));
        Assert.All(plan.Segments, segment => Assert.Contains("12:30", segment.Text, StringComparison.Ordinal));
        Assert.Equal(sentence, string.Join(' ', plan.Segments.Select(s => s.Text)));
    }

    /// <summary>Proves a single token longer than the unit limit is cut so no unit exceeds the limit.</summary>
    [Fact]
    public void ChatterboxTurboCapabilityProfile_Render_SingleOverLongToken_IsCutToTheLimit()
    {
        // Arrange
        var token = new string('a', 450);

        // Act
        var plan = Render(ChatterboxTurboEndOfSentenceTagPolicy.Strip, Text(token));

        // Assert
        Assert.Equal(3, plan.Segments.Count);
        Assert.All(plan.Segments, segment => Assert.True(segment.Text.Length <= 200));
        Assert.Equal(token, string.Concat(plan.Segments.Select(s => s.Text)));
    }

    /// <summary>Proves the 200-character budget counts a tag as one character, so a unit may exceed it by the tag expansion.</summary>
    [Fact]
    public void ChatterboxTurboCapabilityProfile_Render_TagExpansion_MayExceedBudgetByTheExpansionOnly()
    {
        // Arrange: 199 characters of placeholder text, one of them a laugh tag (expanded to "[laugh]")
        var words = string.Join(' ', Enumerable.Repeat("abcd", 38)) + " abc";
        var plan = Render(ChatterboxTurboEndOfSentenceTagPolicy.Strip, Text(words + " "), Tag(NaturalLanguageAudioTag.Laughs), Text(" end"));

        // Assert: still one unit although the expanded text is longer than 200 characters
        Assert.Single(plan.Segments);
        Assert.Contains("[laugh]", plan.Segments[0].Text, StringComparison.Ordinal);
        Assert.InRange(plan.Segments[0].Text.Length, 201, 200 + "[laugh]".Length);
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

    /// <summary>Proves each mood-mapped tag selects the documented pack mood clip.</summary>
    [Theory]
    [InlineData(NaturalLanguageAudioTag.Excited, "excited")]
    [InlineData(NaturalLanguageAudioTag.Enthused, "enthused")]
    [InlineData(NaturalLanguageAudioTag.Happy, "happy")]
    [InlineData(NaturalLanguageAudioTag.Sad, "sad")]
    [InlineData(NaturalLanguageAudioTag.Angry, "anger")]
    [InlineData(NaturalLanguageAudioTag.Loud, "shout")]
    [InlineData(NaturalLanguageAudioTag.Calm, "calm")]
    [InlineData(NaturalLanguageAudioTag.Surprised, "surprised")]
    [InlineData(NaturalLanguageAudioTag.Bored, "tired")]
    [InlineData(NaturalLanguageAudioTag.Worried, "worried")]
    [InlineData(NaturalLanguageAudioTag.Panicked, "worried")]
    [InlineData(NaturalLanguageAudioTag.Confused, "confused")]
    [InlineData(NaturalLanguageAudioTag.Frustrated, "frustrated")]
    [InlineData(NaturalLanguageAudioTag.Neutral, "neutral")]
    [InlineData(NaturalLanguageAudioTag.Serious, "neutral")]
    public void ChatterboxTurboCapabilityProfile_Render_MoodTag_MarksFollowingTextWithMood(NaturalLanguageAudioTag tag, string mood)
    {
        // Act
        var plan = Render(ChatterboxTurboEndOfSentenceTagPolicy.Strip, Tag(tag), Text("Hello there."));

        // Assert
        Assert.Equal(ChatterboxTurboTagKind.Mood, ChatterboxTurboCapabilityProfile.Treatments[tag].Kind);
        var segment = Assert.Single(plan.Segments);
        Assert.Equal("Hello there.", ChatterboxTurboCapabilityProfile.ReadMoodMarker(segment.Text, out var found));
        Assert.Equal(mood, found);
        Assert.Contains(mood, ReferenceVoiceInfo.KnownMoods);
    }

    /// <summary>Proves whispers, sarcastic, soft and breathy stay stripped with no marker.</summary>
    [Theory]
    [InlineData(NaturalLanguageAudioTag.Whispers)]
    [InlineData(NaturalLanguageAudioTag.Sarcastic)]
    [InlineData(NaturalLanguageAudioTag.Soft)]
    [InlineData(NaturalLanguageAudioTag.Breathy)]
    public void ChatterboxTurboCapabilityProfile_Render_NonMoodStyleTags_StayStripped(NaturalLanguageAudioTag tag)
    {
        // Act
        var plan = Render(ChatterboxTurboEndOfSentenceTagPolicy.Strip, Tag(tag), Text("Hello there."));

        // Assert
        Assert.Equal(ChatterboxTurboTagKind.Strip, ChatterboxTurboCapabilityProfile.Treatments[tag].Kind);
        var segment = Assert.Single(plan.Segments);
        Assert.Equal("Hello there.", segment.Text);
    }

    /// <summary>Proves text before the first mood tag is unmarked and a mood lasts until the next mood tag.</summary>
    [Fact]
    public void ChatterboxTurboCapabilityProfile_Render_MoodRuns_AppliesUntilNextMoodTagInOrder()
    {
        // Act
        var plan = Render(
            ChatterboxTurboEndOfSentenceTagPolicy.Strip,
            Text("First plain. "),
            Tag(NaturalLanguageAudioTag.Happy),
            Text(" Good one. Great two. "),
            Tag(NaturalLanguageAudioTag.Sad),
            Text(" Bad three."));

        // Assert
        var read = plan.Segments.Select(segment => (Text: ChatterboxTurboCapabilityProfile.ReadMoodMarker(segment.Text, out var mood), Mood: mood)).ToArray();
        Assert.Equal(
            [("First plain.", (string?)null), ("Good one.", "happy"), ("Great two.", "happy"), ("Bad three.", "sad")],
            read);
    }

    /// <summary>Proves a later mood tag with no speech between replaces the earlier one.</summary>
    [Fact]
    public void ChatterboxTurboCapabilityProfile_Render_ConsecutiveMoodTags_LastWins()
    {
        // Act
        var plan = Render(
            ChatterboxTurboEndOfSentenceTagPolicy.Strip,
            Tag(NaturalLanguageAudioTag.Happy),
            Tag(NaturalLanguageAudioTag.Angry),
            Text("Go away."));

        // Assert
        var segment = Assert.Single(plan.Segments);
        ChatterboxTurboCapabilityProfile.ReadMoodMarker(segment.Text, out var mood);
        Assert.Equal("anger", mood);
    }

    /// <summary>Proves a mood change adds the join silence to the preceding segment, but equal moods do not.</summary>
    [Fact]
    public void ChatterboxTurboCapabilityProfile_Render_MoodChange_AddsJoinSilenceOnlyAtChanges()
    {
        // Act
        var plan = new ChatterboxTurboCapabilityProfile(ChatterboxTurboEndOfSentenceTagPolicy.Strip, 200).Render(
            [Text("Plain. "), Tag(NaturalLanguageAudioTag.Happy), Text(" One. Two. "), Tag(NaturalLanguageAudioTag.Happy), Text(" Three. "), Tag(NaturalLanguageAudioTag.Sad), Text(" Four.")],
            Model);

        // Assert: Plain|One (change), One|Two (same), Two|Three (same mood restated), Three|Four (change)
        Assert.Equal([200, 0, 0, 200, 0], plan.Segments.Select(segment => segment.PostSilenceMs));
    }

    /// <summary>Proves a zero join disables the silence and an explicit longer pause is never shortened.</summary>
    [Fact]
    public void ChatterboxTurboCapabilityProfile_Render_JoinSilence_ZeroDisablesAndLongerEllipsisPauseWins()
    {
        // Arrange
        var spans = new[] { Text("Wait... "), Tag(NaturalLanguageAudioTag.Sad), Text(" Okay.") };

        // Act
        var off = new ChatterboxTurboCapabilityProfile(ChatterboxTurboEndOfSentenceTagPolicy.Strip, 0).Render(spans, Model);
        var on = new ChatterboxTurboCapabilityProfile(ChatterboxTurboEndOfSentenceTagPolicy.Strip, 150).Render(spans, Model);

        // Assert: the 500 ms ellipsis pause stays above the 150 ms join
        Assert.Equal(500, off.Segments[0].PostSilenceMs);
        Assert.Equal(500, on.Segments[0].PostSilenceMs);
        Assert.Equal(0, on.Segments[1].PostSilenceMs);
    }

    /// <summary>Proves pause segments are unmarked and a pause between moods adds no extra join.</summary>
    [Fact]
    public void ChatterboxTurboCapabilityProfile_Render_PauseBetweenMoods_IsUnmarkedAndNotDoubled()
    {
        // Act
        var plan = Render(
            ChatterboxTurboEndOfSentenceTagPolicy.Strip,
            Tag(NaturalLanguageAudioTag.Happy),
            Text("Yes. "),
            Tag(NaturalLanguageAudioTag.ShortPause),
            Tag(NaturalLanguageAudioTag.Sad),
            Text("No."));

        // Assert
        Assert.Equal(3, plan.Segments.Count);
        Assert.Equal(string.Empty, plan.Segments[1].Text);
        Assert.Equal(300, plan.Segments[1].PostSilenceMs);
        Assert.Equal(0, plan.Segments[0].PostSilenceMs);
    }

    /// <summary>Proves a native tag inside a mood span stays inline and the end-of-sentence policy applies per segment.</summary>
    [Fact]
    public void ChatterboxTurboCapabilityProfile_Render_NativeTagInMoodSpan_KeepsPolicyPerSegment()
    {
        // Act
        var plan = Render(
            ChatterboxTurboEndOfSentenceTagPolicy.MoveBeforeFinalWord,
            Tag(NaturalLanguageAudioTag.Happy),
            Text("That was so funny "),
            Tag(NaturalLanguageAudioTag.Laughs),
            Text(". Really good "),
            Tag(NaturalLanguageAudioTag.Gasp));

        // Assert: both units are marked; the sentence-final gasp moved before the final word
        Assert.Equal(2, plan.Segments.Count);
        Assert.All(plan.Segments, segment =>
        {
            ChatterboxTurboCapabilityProfile.ReadMoodMarker(segment.Text, out var mood);
            Assert.Equal("happy", mood);
        });
        Assert.Equal("Really [gasp] good", ChatterboxTurboCapabilityProfile.ReadMoodMarker(plan.Segments[1].Text, out _));
    }

    /// <summary>Proves marker characters in user text are stripped and never alter the mood.</summary>
    [Fact]
    public void ChatterboxTurboCapabilityProfile_Render_MarkerCharactersInUserText_AreStripped()
    {
        // Arrange: a user-supplied marker character at the start of the text
        var injected = ChatterboxTurboCapabilityProfile.WithMoodMarker("sad", "Hello there.");

        // Act
        var plan = Render(ChatterboxTurboEndOfSentenceTagPolicy.Strip, Text(injected));

        // Assert
        var segment = Assert.Single(plan.Segments);
        Assert.Equal("Hello there.", segment.Text);
    }

    /// <summary>Proves the marker helpers round-trip and an out-of-range join is rejected.</summary>
    [Fact]
    public void ChatterboxTurboCapabilityProfile_MoodMarker_RoundTripsAndJoinIsValidated()
    {
        // Assert
        Assert.Equal("x", ChatterboxTurboCapabilityProfile.WithMoodMarker(null, "x"));
        Assert.Equal("x", ChatterboxTurboCapabilityProfile.WithMoodMarker("not-a-mood", "x"));
        Assert.Equal("x", ChatterboxTurboCapabilityProfile.ReadMoodMarker("x", out var none));
        Assert.Null(none);
        foreach (var mood in ReferenceVoiceInfo.KnownMoods)
        {
            Assert.Equal("hi", ChatterboxTurboCapabilityProfile.ReadMoodMarker(ChatterboxTurboCapabilityProfile.WithMoodMarker(mood, "hi"), out var read));
            Assert.Equal(mood, read);
        }

        Assert.Throws<ArgumentOutOfRangeException>(() => new ChatterboxTurboCapabilityProfile(ChatterboxTurboEndOfSentenceTagPolicy.Strip, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ChatterboxTurboCapabilityProfile(ChatterboxTurboEndOfSentenceTagPolicy.Strip, 1001));
    }

    /// <summary>Proves the mood marker lookup is case-insensitive and trimmed, and blank names add no marker.</summary>
    [Fact]
    public void ChatterboxTurboCapabilityProfile_WithMoodMarker_IsCaseInsensitiveAndTrimmed()
    {
        // Act and Assert
        foreach (var name in new[] { "SAD", " Sad ", "sad" })
        {
            Assert.Equal("hi", ChatterboxTurboCapabilityProfile.ReadMoodMarker(ChatterboxTurboCapabilityProfile.WithMoodMarker(name, "hi"), out var read));
            Assert.Equal("sad", read);
        }

        Assert.Equal("x", ChatterboxTurboCapabilityProfile.WithMoodMarker("", "x"));
        Assert.Equal("x", ChatterboxTurboCapabilityProfile.WithMoodMarker("   ", "x"));
    }

    /// <summary>Proves the join boundaries 0 and 1000 are accepted and an undefined policy is rejected.</summary>
    [Fact]
    public void ChatterboxTurboCapabilityProfile_Constructor_JoinBoundariesAcceptedAndUndefinedPolicyRejected()
    {
        // Act & Assert
        Assert.NotNull(new ChatterboxTurboCapabilityProfile(ChatterboxTurboEndOfSentenceTagPolicy.Strip, 0));
        Assert.NotNull(new ChatterboxTurboCapabilityProfile(ChatterboxTurboEndOfSentenceTagPolicy.Strip, ChatterboxTurboCapabilityProfile.MaxMoodJoinMilliseconds));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ChatterboxTurboCapabilityProfile(ChatterboxTurboEndOfSentenceTagPolicy.Strip, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ChatterboxTurboCapabilityProfile(ChatterboxTurboEndOfSentenceTagPolicy.Strip, ChatterboxTurboCapabilityProfile.MaxMoodJoinMilliseconds + 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ChatterboxTurboCapabilityProfile((ChatterboxTurboEndOfSentenceTagPolicy)99));
    }

    /// <summary>Proves null arguments to Render are rejected.</summary>
    [Fact]
    public void ChatterboxTurboCapabilityProfile_Render_NullArguments_Throw()
    {
        // Arrange
        var profile = new ChatterboxTurboCapabilityProfile(ChatterboxTurboEndOfSentenceTagPolicy.Strip);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => profile.Render(null!, Model));
        Assert.Throws<ArgumentNullException>(() => profile.Render([], null!));
    }

    /// <summary>Proves a trailing tag-only chunk joins the previous unit and is placed by the policy, and tags alone yield no speech.</summary>
    [Fact]
    public void ChatterboxTurboCapabilityProfile_Render_TrailingTagOnlyChunk_JoinsPreviousUnit()
    {
        // Act
        var joined = Render(ChatterboxTurboEndOfSentenceTagPolicy.MoveBeforeFinalWord, Text("Hello there. "), Tag(NaturalLanguageAudioTag.Laughs));
        var tagsOnly = Render(ChatterboxTurboEndOfSentenceTagPolicy.MoveBeforeFinalWord, Tag(NaturalLanguageAudioTag.Laughs));

        // Assert
        var segment = Assert.Single(joined.Segments);
        Assert.Equal("Hello [laugh] there.", segment.Text);
        Assert.Empty(tagsOnly.Segments);
    }

    /// <summary>Renders spans with a profile.</summary>
    private static SpeechPlan Render(ChatterboxTurboEndOfSentenceTagPolicy policy, params TaggedTextSpan[] spans) =>
        new ChatterboxTurboCapabilityProfile(policy).Render(spans, Model);

    /// <summary>Creates a plain-text span.</summary>
    private static TaggedTextSpan Text(string text) => TaggedTextSpan.PlainText(text);

    /// <summary>Creates a tag span.</summary>
    private static TaggedTextSpan Tag(NaturalLanguageAudioTag tag) => TaggedTextSpan.TagSpan(tag);
}
