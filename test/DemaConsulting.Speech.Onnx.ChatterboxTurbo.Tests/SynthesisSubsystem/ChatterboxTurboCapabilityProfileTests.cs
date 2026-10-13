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
    /// <summary>The model passed to the profile; the real model is used only as an <see cref="ISpeechModel"/> value.</summary>
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

    /// <summary>Proves a laugh tag renders as the model's own <c>[laugh]</c> token.</summary>
    [Fact]
    public void ChatterboxTurboCapabilityProfile_Render_LaughTag_UsesModelToken() =>
        AssertNativeToken(NaturalLanguageAudioTag.Laughs, "[laugh]");

    /// <summary>Proves a throat-clear tag renders as the model's own <c>[clear throat]</c> token.</summary>
    [Fact]
    public void ChatterboxTurboCapabilityProfile_Render_ClearThroatTag_UsesModelToken() =>
        AssertNativeToken(NaturalLanguageAudioTag.ClearsThroat, "[clear throat]");

    /// <summary>Proves a gasp tag renders as the model's own <c>[gasp]</c> token.</summary>
    [Fact]
    public void ChatterboxTurboCapabilityProfile_Render_GaspTag_UsesModelToken() =>
        AssertNativeToken(NaturalLanguageAudioTag.Gasp, "[gasp]");

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

    /// <summary>Proves a short pause becomes 300 ms of silence.</summary>
    [Fact]
    public void ChatterboxTurboCapabilityProfile_Render_ShortPause_BecomesThreeHundredMillisecondSilence()
    {
        // Act
        var plan = Render(ChatterboxTurboEndOfSentenceTagPolicy.Strip, Text("Before."), Tag(NaturalLanguageAudioTag.ShortPause), Text("After."));

        // Assert
        Assert.Equal(300, plan.Segments[1].PostSilenceMs);
        Assert.Equal(string.Empty, plan.Segments[1].Text);
    }

    /// <summary>Proves a long pause becomes 900 ms of silence.</summary>
    [Fact]
    public void ChatterboxTurboCapabilityProfile_Render_LongPause_BecomesNineHundredMillisecondSilence()
    {
        // Act
        var plan = Render(ChatterboxTurboEndOfSentenceTagPolicy.Strip, Text("Before."), Tag(NaturalLanguageAudioTag.LongPause), Text("After."));

        // Assert
        Assert.Equal(900, plan.Segments[1].PostSilenceMs);
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

    /// <summary>Proves the suffix policy appends a laugh tag with its known-good suffix.</summary>
    [Fact]
    public void ChatterboxTurboCapabilityProfile_Render_TrailingTag_Suffix_AppendsSuffixedTag()
    {
        // Act
        var plan = Render(ChatterboxTurboEndOfSentenceTagPolicy.Suffix, Text("Funny."), Tag(NaturalLanguageAudioTag.Laughs));

        // Assert
        Assert.Equal("Funny. [laugh] ...", Assert.Single(plan.Segments).Text);
    }

    /// <summary>Proves the suffix policy drops a sentence-final tag that has no suffix form (gasp and throat clear).</summary>
    [Theory]
    [InlineData(NaturalLanguageAudioTag.Gasp)]
    [InlineData(NaturalLanguageAudioTag.ClearsThroat)]
    public void ChatterboxTurboCapabilityProfile_Render_TrailingTag_SuffixWithoutSuffixForm_DropsTag(NaturalLanguageAudioTag tag)
    {
        // Act
        var plan = Render(ChatterboxTurboEndOfSentenceTagPolicy.Suffix, Text("Funny."), Tag(tag));

        // Assert
        Assert.Equal("Funny.", Assert.Single(plan.Segments).Text);
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

    /// <summary>Proves text is split into sentences and a decimal numeral survives inside its sentence.</summary>
    [Fact]
    public void ChatterboxTurboCapabilityProfile_Render_Sentences_AreSplit()
    {
        // Act
        var plan = Render(ChatterboxTurboEndOfSentenceTagPolicy.Strip, Text("It costs 3.14 dollars. Wait... What?! Yes."));

        // Assert
        Assert.Equal(["It costs 3.14 dollars.", "Wait...", "What?!", "Yes."], plan.Segments.Select(s => s.Text));
    }

    /// <summary>Proves a sentence ending in an ellipsis gets a 500 ms pause and other sentences get none.</summary>
    [Fact]
    public void ChatterboxTurboCapabilityProfile_Render_EllipsisSentence_GetsFiveHundredMillisecondPause()
    {
        // Act
        var plan = Render(ChatterboxTurboEndOfSentenceTagPolicy.Strip, Text("It is over. Wait... What?! Yes."));

        // Assert
        Assert.Equal([0, 500, 0, 0], plan.Segments.Select(segment => segment.PostSilenceMs));
    }

    /// <summary>Proves an over-long sentence is split into units no longer than the limit, keeping every word in order.</summary>
    [Fact]
    public void ChatterboxTurboCapabilityProfile_Render_LongSentence_IsSplitKeepingEveryWord()
    {
        // Arrange
        var words = Enumerable.Range(0, 120).Select(i => "word" + i).ToArray();

        // Act
        var plan = Render(ChatterboxTurboEndOfSentenceTagPolicy.Strip, Text(string.Join(' ', words) + "."));

        // Assert
        Assert.True(plan.Segments.Count > 1);
        Assert.All(plan.Segments, segment => Assert.True(segment.Text.Length <= 200));
        Assert.Equal(words, string.Join(' ', plan.Segments.Select(s => s.Text)).TrimEnd('.').Split(' '));
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

    /// <summary>Proves an over-long sentence is split at its comma, semicolon and colon boundaries without losing words.</summary>
    [Fact]
    public void ChatterboxTurboCapabilityProfile_Render_LongSentenceWithClauses_SplitsAtClauseBoundaries()
    {
        // Arrange: six clauses of 61 characters plus a boundary mark; three fit into one unit (188), four would not
        var filler = string.Join(' ', Enumerable.Repeat("word", 11));
        string[] marks = [",", ";", ":", ",", ";", "."];
        var clauses = marks.Select((mark, i) => $"part {i} {filler}{mark}").ToArray();
        var sentence = string.Join(' ', clauses);

        // Act
        var plan = Render(ChatterboxTurboEndOfSentenceTagPolicy.Strip, Text(sentence));

        // Assert: the first unit ends at the third clause's colon and nothing is lost
        Assert.Equal(2, plan.Segments.Count);
        Assert.EndsWith(":", plan.Segments[0].Text, StringComparison.Ordinal);
        Assert.Equal(string.Join(' ', clauses[..3]), plan.Segments[0].Text);
        Assert.Equal(sentence, string.Join(' ', plan.Segments.Select(s => s.Text)));
    }

    /// <summary>Proves a comma or colon flanked by digits is not a clause boundary of a long sentence.</summary>
    /// <param name="numeral">The numeral holding the boundary character.</param>
    [Theory]
    [InlineData("1,000")]
    [InlineData("12:30")]
    public void ChatterboxTurboCapabilityProfile_Render_DigitFlankedClauseBoundary_StaysInsideItsUnit(string numeral)
    {
        // Arrange: one 260-character clause with the numeral near the 200-character split point
        var head = string.Join(' ', Enumerable.Repeat("word", 38));
        var tail = string.Join(' ', Enumerable.Repeat("word", 12));
        var sentence = $"{head} {numeral} {tail}.";

        // Act
        var plan = Render(ChatterboxTurboEndOfSentenceTagPolicy.Strip, Text(sentence));

        // Assert: the numeral is never cut in two and no word is lost
        Assert.True(plan.Segments.Count > 1);
        Assert.Contains(plan.Segments, segment => segment.Text.Contains(numeral, StringComparison.Ordinal));
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

    /// <summary>Proves empty input produces an empty plan.</summary>
    [Fact]
    public void ChatterboxTurboCapabilityProfile_Render_EmptyInput_ProducesEmptyPlan()
    {
        // Arrange
        var profile = new ChatterboxTurboCapabilityProfile(ChatterboxTurboEndOfSentenceTagPolicy.Strip);

        // Act
        var plan = profile.Render([], Model);

        // Assert
        Assert.Empty(plan.Segments);
    }

    /// <summary>Proves whitespace-only and tag-only text produce no speech segments.</summary>
    [Fact]
    public void ChatterboxTurboCapabilityProfile_Render_WhitespaceOnlyText_ProducesEmptyPlan()
    {
        // Act
        var plan = Render(ChatterboxTurboEndOfSentenceTagPolicy.Strip, Text("   \t "));

        // Assert
        Assert.Empty(plan.Segments);
    }

    /// <summary>Proves every library tag has a table row and none maps to an emotion token.</summary>
    [Fact]
    public void ChatterboxTurboCapabilityProfile_Treatments_CoverEveryTagWithoutEmotionTokens()
    {
        // Arrange
        string[] emotions = ["[angry]", "[fear]", "[surprised]", "[whisper]", "[dramatic]", "[narration]", "[crying]", "[happy]", "[sarcastic]", "[sigh]"];

        // Act
        var treatments = ChatterboxTurboCapabilityProfile.Treatments;

        // Assert
        Assert.Equal(Enum.GetValues<NaturalLanguageAudioTag>().Length, treatments.Count);
        Assert.DoesNotContain(
            treatments.Values,
            treatment => treatment.Token is not null && emotions.Contains(treatment.Token));
    }

    /// <summary>Proves each mood-mapped tag selects the documented pack mood clip.</summary>
    /// <param name="tag">The mood tag.</param>
    /// <param name="mood">The expected reference-clip mood.</param>
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

    /// <summary>Proves the fifteen mood tags map onto thirteen distinct reference-clip moods.</summary>
    [Fact]
    public void ChatterboxTurboCapabilityProfile_Treatments_FifteenMoodTagsMapToThirteenDistinctMoods()
    {
        // Act
        var moods = ChatterboxTurboCapabilityProfile.Treatments.Values
            .Where(treatment => treatment.Kind == ChatterboxTurboTagKind.Mood)
            .Select(treatment => treatment.Mood)
            .ToArray();

        // Assert
        Assert.Equal(15, moods.Length);
        Assert.Equal(13, moods.Distinct().Count());
        Assert.All(moods, mood => Assert.Contains(mood, ReferenceVoiceInfo.KnownMoods));
    }

    /// <summary>Proves the alias tags share the mood clip of the tag they are named after.</summary>
    /// <param name="alias">The alias tag.</param>
    /// <param name="target">The tag whose mood it shares.</param>
    [Theory]
    [InlineData(NaturalLanguageAudioTag.Panicked, NaturalLanguageAudioTag.Worried)]
    [InlineData(NaturalLanguageAudioTag.Serious, NaturalLanguageAudioTag.Neutral)]
    public void ChatterboxTurboCapabilityProfile_Treatments_AliasTags_ShareTheirTargetMood(NaturalLanguageAudioTag alias, NaturalLanguageAudioTag target)
    {
        // Act
        var treatments = ChatterboxTurboCapabilityProfile.Treatments;

        // Assert
        Assert.Equal(treatments[target].Mood, treatments[alias].Mood);
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

    /// <summary>Proves a zero join silence adds no silence at a mood change.</summary>
    [Fact]
    public void ChatterboxTurboCapabilityProfile_Render_JoinSilenceZero_AddsNoSilenceAtMoodChange()
    {
        // Arrange
        var spans = new[] { Text("Plain. "), Tag(NaturalLanguageAudioTag.Sad), Text(" Okay.") };

        // Act
        var plan = new ChatterboxTurboCapabilityProfile(ChatterboxTurboEndOfSentenceTagPolicy.Strip, 0).Render(spans, Model);

        // Assert
        Assert.Equal([0, 0], plan.Segments.Select(segment => segment.PostSilenceMs));
    }

    /// <summary>Proves an ellipsis pause longer than the join silence is kept as is at a mood change.</summary>
    [Fact]
    public void ChatterboxTurboCapabilityProfile_Render_EllipsisPauseLongerThanJoin_WinsAtMoodChange()
    {
        // Arrange
        var spans = new[] { Text("Wait... "), Tag(NaturalLanguageAudioTag.Sad), Text(" Okay.") };

        // Act
        var plan = new ChatterboxTurboCapabilityProfile(ChatterboxTurboEndOfSentenceTagPolicy.Strip, 150).Render(spans, Model);

        // Assert: the 500 ms ellipsis pause stays above the 150 ms join
        Assert.Equal(500, plan.Segments[0].PostSilenceMs);
        Assert.Equal(0, plan.Segments[1].PostSilenceMs);
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

    /// <summary>Proves native-tag placeholder characters in user text are removed rather than expanded into model tokens.</summary>
    [Fact]
    public void ChatterboxTurboCapabilityProfile_Render_PlaceholderCharactersInUserText_AreRemoved()
    {
        // Arrange: the first placeholder code point would otherwise expand to a native token such as [laugh]
        var injected = "Hello \uE000\uE001\uE002 there.";

        // Act
        var plan = Render(ChatterboxTurboEndOfSentenceTagPolicy.Strip, Text(injected));

        // Assert
        var segment = Assert.Single(plan.Segments);
        Assert.DoesNotContain("[", segment.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(segment.Text, character => character >= '\uE000' && character <= '\uE0FF');
        Assert.Contains("Hello", segment.Text, StringComparison.Ordinal);
        Assert.Contains("there.", segment.Text, StringComparison.Ordinal);
    }

    /// <summary>Proves each known mood survives a write and read of its marker unchanged.</summary>
    [Fact]
    public void ChatterboxTurboCapabilityProfile_MoodMarker_RoundTripsEveryKnownMood()
    {
        // Act
        var results = ReferenceVoiceInfo.KnownMoods
            .Select(mood => (Mood: mood, Text: ChatterboxTurboCapabilityProfile.ReadMoodMarker(ChatterboxTurboCapabilityProfile.WithMoodMarker(mood, "hi"), out var read), Read: read))
            .ToArray();

        // Assert
        Assert.All(results, result =>
        {
            Assert.Equal("hi", result.Text);
            Assert.Equal(result.Mood, result.Read);
        });
    }

    /// <summary>Proves text without a marker reads back unchanged with no mood.</summary>
    [Fact]
    public void ChatterboxTurboCapabilityProfile_ReadMoodMarker_TextWithoutMarker_ReturnsTextAndNoMood()
    {
        // Act
        var text = ChatterboxTurboCapabilityProfile.ReadMoodMarker("x", out var mood);

        // Assert
        Assert.Equal("x", text);
        Assert.Null(mood);
    }

    /// <summary>Proves the mood marker lookup is case-insensitive and trimmed.</summary>
    /// <param name="name">The mood name in varying case and spacing.</param>
    [Theory]
    [InlineData("SAD")]
    [InlineData(" Sad ")]
    [InlineData("sad")]
    public void ChatterboxTurboCapabilityProfile_WithMoodMarker_IsCaseInsensitiveAndTrimmed(string name)
    {
        // Act
        var marked = ChatterboxTurboCapabilityProfile.WithMoodMarker(name, "hi");

        // Assert
        Assert.Equal("hi", ChatterboxTurboCapabilityProfile.ReadMoodMarker(marked, out var read));
        Assert.Equal("sad", read);
    }

    /// <summary>Proves a null, blank or unknown mood adds no marker.</summary>
    /// <param name="name">The mood name that must add no marker.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-mood")]
    public void ChatterboxTurboCapabilityProfile_WithMoodMarker_NullBlankOrUnknownMood_AddsNoMarker(string? name)
    {
        // Act
        var marked = ChatterboxTurboCapabilityProfile.WithMoodMarker(name, "x");

        // Assert
        Assert.Equal("x", marked);
    }

    /// <summary>Proves the join silence boundaries 0 and 1000 are accepted.</summary>
    [Fact]
    public void ChatterboxTurboCapabilityProfile_Constructor_JoinBoundaries_AreAccepted()
    {
        // Act
        var lower = new ChatterboxTurboCapabilityProfile(ChatterboxTurboEndOfSentenceTagPolicy.Strip, 0);
        var upper = new ChatterboxTurboCapabilityProfile(ChatterboxTurboEndOfSentenceTagPolicy.Strip, ChatterboxTurboCapabilityProfile.MaxMoodJoinMilliseconds);

        // Assert
        Assert.NotNull(lower);
        Assert.NotNull(upper);
        Assert.Equal(1000, ChatterboxTurboCapabilityProfile.MaxMoodJoinMilliseconds);
    }

    /// <summary>Proves a join silence outside 0 to 1000 ms is rejected with an <see cref="ArgumentOutOfRangeException"/>.</summary>
    /// <param name="milliseconds">The out-of-range join silence.</param>
    [Theory]
    [InlineData(-1)]
    [InlineData(1001)]
    public void ChatterboxTurboCapabilityProfile_Constructor_JoinOutsideRange_ThrowsArgumentOutOfRangeException(int milliseconds)
    {
        // Act
        var exception = Record.Exception(() => new ChatterboxTurboCapabilityProfile(ChatterboxTurboEndOfSentenceTagPolicy.Strip, milliseconds));

        // Assert
        Assert.IsType<ArgumentOutOfRangeException>(exception);
    }

    /// <summary>Proves an undefined end-of-sentence policy is rejected.</summary>
    [Fact]
    public void ChatterboxTurboCapabilityProfile_Constructor_UndefinedPolicy_ThrowsArgumentOutOfRangeException()
    {
        // Act
        var exception = Record.Exception(() => new ChatterboxTurboCapabilityProfile((ChatterboxTurboEndOfSentenceTagPolicy)99));

        // Assert
        Assert.IsType<ArgumentOutOfRangeException>(exception);
    }

    /// <summary>Proves null arguments to Render are rejected.</summary>
    [Fact]
    public void ChatterboxTurboCapabilityProfile_Render_NullArguments_Throw()
    {
        // Arrange
        var profile = new ChatterboxTurboCapabilityProfile(ChatterboxTurboEndOfSentenceTagPolicy.Strip);

        // Act
        var nullSpans = Record.Exception(() => profile.Render(null!, Model));
        var nullModel = Record.Exception(() => profile.Render([], null!));

        // Assert
        Assert.IsType<ArgumentNullException>(nullSpans);
        Assert.IsType<ArgumentNullException>(nullModel);
    }

    /// <summary>Proves a trailing tag-only chunk joins the previous unit and is placed by the policy.</summary>
    [Fact]
    public void ChatterboxTurboCapabilityProfile_Render_TrailingTagOnlyChunk_JoinsPreviousUnit()
    {
        // Act
        var joined = Render(ChatterboxTurboEndOfSentenceTagPolicy.MoveBeforeFinalWord, Text("Hello there. "), Tag(NaturalLanguageAudioTag.Laughs));

        // Assert
        var segment = Assert.Single(joined.Segments);
        Assert.Equal("Hello [laugh] there.", segment.Text);
    }

    /// <summary>Proves a tag with no neighbouring speech at all is silently dropped.</summary>
    [Fact]
    public void ChatterboxTurboCapabilityProfile_Render_TagWithNoSpeech_IsDropped()
    {
        // Act
        var tagsOnly = Render(ChatterboxTurboEndOfSentenceTagPolicy.MoveBeforeFinalWord, Tag(NaturalLanguageAudioTag.Laughs));

        // Assert
        Assert.Empty(tagsOnly.Segments);
    }

    /// <summary>Renders spans with a profile.</summary>
    private static SpeechPlan Render(ChatterboxTurboEndOfSentenceTagPolicy policy, params TaggedTextSpan[] spans) =>
        new ChatterboxTurboCapabilityProfile(policy).Render(spans, Model);

    /// <summary>Asserts a native tag mid-sentence renders as its exact model token.</summary>
    private static void AssertNativeToken(NaturalLanguageAudioTag tag, string token)
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

    /// <summary>Creates a plain-text span.</summary>
    private static TaggedTextSpan Text(string text) => TaggedTextSpan.PlainText(text);

    /// <summary>Creates a tag span.</summary>
    private static TaggedTextSpan Tag(NaturalLanguageAudioTag tag) => TaggedTextSpan.TagSpan(tag);
}
