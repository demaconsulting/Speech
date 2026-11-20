using System.Globalization;
using DemaConsulting.Speech.Diagnostics;
using DemaConsulting.Speech.SynthesisSubsystem;
using DemaConsulting.Speech.Text.En.AnalysisSubsystem;
using DemaConsulting.Speech.Text.En.HeteronymSubsystem;
using DemaConsulting.Speech.Text.En.Tests.Fakes;

namespace DemaConsulting.Speech.Text.En.Tests.AnalysisSubsystem;

/// <summary>
///     Unit tests for the heteronym reading behavior of <see cref="EnglishTextAnalyzer"/>: the reading and
///     confidence annotations, their coexistence rules, the reading override, laziness, cancellation, the
///     detailed tokens and the counts-only diagnostics. The model is a fixture built through the real
///     pipeline and format and supplied through a counting provider; the tagger is the real one.
/// </summary>
public sealed class EnglishTextAnalyzerReadingTests
{
    /// <summary>The fixture model trained on the <c>record</c> examples, built once.</summary>
    private static readonly Lazy<byte[]> RecordModel = new(() => HeteronymFixtureModel.Build(HeteronymFixtureModel.RecordExamples));

    /// <summary>Proves a sentence with both readings of <c>record</c> gets, on exactly each word's span, the part of speech, the reading and the confidence in that order, valid and in text order.</summary>
    [Fact]
    public void EnglishTextAnalyzer_Analyze_RecordSentence_EmitsReadingPairsOnExactSpansAfterPartOfSpeech()
    {
        // Arrange
        const string text = "I want to record a record.";
        var analyzer = CreateAnalyzer(new CountingModelSource(RecordModel.Value));

        // Act
        var result = analyzer.Analyze(text, TestContext.Current.CancellationToken);

        // Assert: per word the three annotations share one span, in the order upos, reading, confidence
        var record = result.Annotations.Where(annotation => text.Substring(annotation.Start, annotation.Length) == "record").ToList();
        Assert.Equal(
            [TextEnAnnotations.PartOfSpeech, TextEnAnnotations.Reading, TextEnAnnotations.ReadingConfidence, TextEnAnnotations.PartOfSpeech, TextEnAnnotations.Reading, TextEnAnnotations.ReadingConfidence],
            record.Select(annotation => annotation.Kind));
        Assert.Equal(["record.verb", "record.noun"], Readings(result.Annotations).Select(reading => reading.Id));
        Assert.All(Readings(result.Annotations), reading =>
        {
            Assert.Equal("record", text.Substring(reading.Start, reading.Length));
            Assert.InRange(reading.Confidence, 0.0, 1.0);
            Assert.Matches("^[01]\\.[0-9]{3}$", reading.ConfidenceText);
        });
        Assert.Equal(result.Annotations.OrderBy(annotation => annotation.Start).Select(annotation => annotation.Start), result.Annotations.Select(annotation => annotation.Start));
    }

    /// <summary>Proves a default analyzer (the embedded model) emits the readings of the sentence, also when only normalization and readings are selected, and that requests without a heteronym word emit no readings and do not disturb later requests.</summary>
    [Fact]
    public void EnglishTextAnalyzer_Analyze_DefaultAnalyzer_EmitsEmbeddedModelReadings()
    {
        // Arrange
        const string text = "I want to record a record.";
        var byDefault = new EnglishTextAnalyzer();
        var fresh = new EnglishTextAnalyzer();
        var readingOnly = new EnglishTextAnalyzer(
            new EnglishTextAnalyzerOptions { Annotations = TextEnAnnotationSelection.Normalization | TextEnAnnotationSelection.Reading });

        // Act
        var empty = byDefault.Analyze(string.Empty, TestContext.Current.CancellationToken);
        var plain = byDefault.Analyze("The cat sat on the mat.", TestContext.Current.CancellationToken);
        var defaultResult = byDefault.Analyze(text, TestContext.Current.CancellationToken);
        var readingResult = readingOnly.Analyze(text, TestContext.Current.CancellationToken);
        var freshResult = fresh.Analyze(text, TestContext.Current.CancellationToken);

        // Assert: both readings, each with its confidence on the word's span, and no part of speech for the reading-and-normalization selection
        Assert.Equal(["record.verb", "record.noun"], Readings(defaultResult.Annotations).Select(reading => reading.Id));
        Assert.Equal(["record.verb", "record.noun"], Readings(readingResult.Annotations).Select(reading => reading.Id));
        Assert.All(Readings(readingResult.Annotations), reading => Assert.InRange(reading.Confidence, 0.0, 1.0));
        Assert.DoesNotContain(readingResult.Annotations, annotation => annotation.Kind == TextEnAnnotations.PartOfSpeech);

        // requests without a heteronym word emit nothing, and the later request equals that of a fresh analyzer
        Assert.Empty(Readings(empty.Annotations));
        Assert.Empty(Readings(plain.Annotations));
        Assert.Equal(
            Readings(freshResult.Annotations).Select(reading => (reading.Id, reading.ConfidenceText)),
            Readings(defaultResult.Annotations).Select(reading => (reading.Id, reading.ConfidenceText)));
    }

    /// <summary>
    ///     Proves the embedded model reads an adjective heteronym after a degree adverb or next to a coordinated adjective
    ///     as the adjective, including the listener-flagged <i>very minute and clear</i> (tiny, not the time unit),
    ///     while the noun stays a noun.
    /// </summary>
    /// <param name="text">The sentence.</param>
    /// <param name="word">The heteronym in it.</param>
    /// <param name="expected">The reading the word must get.</param>
    [Theory]
    [InlineData("To the eastward, of course, these little boats were grey and very minute and clear, but to the westward they were little boats of gold.", "minute", "minute.tiny")]
    [InlineData("The difference was extremely minute.", "minute", "minute.tiny")]
    [InlineData("They were very close.", "close", "close.adj-noun")]
    [InlineData("She was so content.", "content", "content.satisfied")]
    [InlineData("Wait a minute and then go.", "minute", "minute.time")]
    [InlineData("Most content is free.", "content", "content.noun")]
    public void EnglishTextAnalyzer_Analyze_DegreeAdverbOrCoordinatedAdjective_ReadsTheAdjective(string text, string word, string expected)
    {
        // Arrange
        var analyzer = new EnglishTextAnalyzer();
        var start = text.IndexOf(word, StringComparison.Ordinal);

        // Act
        var result = analyzer.Analyze(text, TestContext.Current.CancellationToken);

        // Assert: the reading on the word's span
        var reading = Assert.Single(result.Annotations, annotation => annotation.Kind == TextEnAnnotations.Reading && annotation.Start == start);
        Assert.Equal(expected, reading.Value);
    }
    /// <summary>Proves an analyzer given no model source emits no classified readings, also when only normalization and readings are selected.</summary>
    [Fact]
    public void EnglishTextAnalyzer_Analyze_NoModelSource_EmitsNoReadings()
    {
        // Arrange
        const string text = "I want to record a record.";
        var none = new EnglishTextAnalyzer(new EnglishTextAnalyzerOptions(), PartOfSpeechAdapter.Shared, null, HeteronymModelProvider.None);
        var readingOnly = new EnglishTextAnalyzer(
            new EnglishTextAnalyzerOptions { Annotations = TextEnAnnotationSelection.Normalization | TextEnAnnotationSelection.Reading },
            PartOfSpeechAdapter.Shared,
            null,
            HeteronymModelProvider.None);

        // Act
        var defaultResult = none.Analyze(text, TestContext.Current.CancellationToken);
        var readingResult = readingOnly.Analyze(text, TestContext.Current.CancellationToken);

        // Assert: no reading kinds anywhere, and nothing at all for the reading-and-normalization selection of this text
        Assert.DoesNotContain(defaultResult.Annotations, annotation => annotation.Kind is TextEnAnnotations.Reading or TextEnAnnotations.ReadingConfidence);
        Assert.Empty(readingResult.Annotations);
    }

    /// <summary>Proves no reading is emitted when the reading family is not selected, even with a model available.</summary>
    [Fact]
    public void EnglishTextAnalyzer_Analyze_ReadingNotSelected_EmitsNoReadings()
    {
        // Arrange
        var source = new CountingModelSource(RecordModel.Value);
        var analyzer = new EnglishTextAnalyzer(
            new EnglishTextAnalyzerOptions { Annotations = TextEnAnnotationSelection.Normalization },
            PartOfSpeechAdapter.Shared,
            null,
            HeteronymFixtureModel.CreateProvider(source));

        // Act
        var result = analyzer.Analyze("I want to record a record.", TestContext.Current.CancellationToken);

        // Assert
        Assert.DoesNotContain(result.Annotations, annotation => annotation.Kind is TextEnAnnotations.Reading or TextEnAnnotations.ReadingConfidence);
    }

    /// <summary>Proves a reading-only selection tags the text for context and emits readings but no part-of-speech annotation, and the detailed tokens stay untagged.</summary>
    [Fact]
    public void EnglishTextAnalyzer_Analyze_ReadingOnlySelection_EmitsReadingsWithoutPartOfSpeech()
    {
        // Arrange
        const string text = "I want to record a record.";
        var analyzer = CreateAnalyzer(new CountingModelSource(RecordModel.Value), new EnglishTextAnalyzerOptions { Annotations = TextEnAnnotationSelection.Reading });

        // Act
        var result = analyzer.Analyze(text, TestContext.Current.CancellationToken);
        var detailed = analyzer.AnalyzeDetailed(text, TestContext.Current.CancellationToken);

        // Assert
        Assert.DoesNotContain(result.Annotations, annotation => annotation.Kind == TextEnAnnotations.PartOfSpeech);
        Assert.Equal(["record.verb", "record.noun"], Readings(result.Annotations).Select(reading => reading.Id));
        Assert.All(detailed.Tokens, token =>
        {
            Assert.Null(token.PartOfSpeech);
            Assert.Equal(0.0, token.Confidence);
            Assert.False(token.IsKnownWord);
        });
    }

    /// <summary>Proves a single candidate form gets confidence 1.000 when the family is active (a model exists), and nothing without a model source.</summary>
    [Fact]
    public void EnglishTextAnalyzer_Analyze_SingleCandidateForm_EmitsConfidenceOneOnlyWithModel()
    {
        // Arrange: pick a form with exactly one candidate
        var inventory = ReadingInventory.Default;
        var form = Enumerable.Range(0, inventory.FormCount).First(f => inventory.GetCandidates(f).Length == 1);
        var word = inventory.FormAt(form);
        var expectedId = inventory.ReadingIdAt(inventory.GetCandidates(form)[0]);
        var text = $"We saw the {word} there.";
        var sink = new RecordingDiagnostics();
        var withModel = CreateAnalyzer(new CountingModelSource(RecordModel.Value), new EnglishTextAnalyzerOptions { Diagnostics = sink });
        var withoutModel = new EnglishTextAnalyzer(new EnglishTextAnalyzerOptions(), PartOfSpeechAdapter.Shared, null, HeteronymModelProvider.None);

        // Act
        var result = withModel.Analyze(text, TestContext.Current.CancellationToken);
        var without = withoutModel.Analyze(text, TestContext.Current.CancellationToken);

        // Assert
        var reading = Assert.Single(Readings(result.Annotations));
        Assert.Equal(expectedId, reading.Id);
        Assert.Equal("1.000", reading.ConfidenceText);
        Assert.Equal(word, text.Substring(reading.Start, reading.Length));
        Assert.DoesNotContain(without.Annotations, annotation => annotation.Kind == TextEnAnnotations.Reading);
        Assert.Contains(sink.Events, e => e.Category == "TextEn.Heteronym" && e.Message.StartsWith("Assigned 1 heteronym readings: 0 classified, 1 single-candidate, 0 by override, 0 below", StringComparison.Ordinal));
    }

    /// <summary>Proves hyphenated compounds get no reading, while uppercase, sentence-initial and possessive uses of the word do, on the word's own token only.</summary>
    [Fact]
    public void EnglishTextAnalyzer_Analyze_WordShapes_ReadingOnWholeTokenOnly()
    {
        // Arrange
        var analyzer = CreateAnalyzer(new CountingModelSource(RecordModel.Value));

        // Act
        var compound = Readings(analyzer.Analyze("A record-breaking year.", TestContext.Current.CancellationToken).Annotations);
        var upper = Readings(analyzer.Analyze("They RECORD it.", TestContext.Current.CancellationToken).Annotations);
        var initial = Readings(analyzer.Analyze("Record it now.", TestContext.Current.CancellationToken).Annotations);
        var text = "The record's value fell.";
        var possessive = Readings(analyzer.Analyze(text, TestContext.Current.CancellationToken).Annotations);

        // Assert
        Assert.Empty(compound);
        Assert.Single(upper);
        Assert.Single(initial);
        var onlyPossessive = Assert.Single(possessive);
        Assert.Equal("record", text.Substring(onlyPossessive.Start, onlyPossessive.Length));
    }

    /// <summary>Proves tag placeholders are separators: the words on both sides are annotated at the exact source offsets and the context is carried across the tag.</summary>
    [Fact]
    public void EnglishTextAnalyzer_Analyze_TagPlaceholders_KeepOffsetsAndReadings()
    {
        // Arrange
        var text = "I want to record \uFFFC a record.";
        var analyzer = CreateAnalyzer(new CountingModelSource(RecordModel.Value));

        // Act
        var result = analyzer.Analyze(text, TestContext.Current.CancellationToken);

        // Assert
        var readings = Readings(result.Annotations);
        Assert.Equal(2, readings.Count);
        Assert.All(readings, reading => Assert.Equal("record", text.Substring(reading.Start, reading.Length)));
        Assert.Equal(text.IndexOf("record", StringComparison.Ordinal), readings[0].Start);
        Assert.Equal(text.LastIndexOf("record", StringComparison.Ordinal), readings[1].Start);
    }

    /// <summary>Proves a token that carries or lies inside a replacement, a pronunciation or any other override never gets a reading, while the rest of the text still does; checked over a mixed corpus. Also proves an override of the heteronym word itself, whatever its action, suppresses the classifier for that word only.</summary>
    [Fact]
    public void EnglishTextAnalyzer_Analyze_MixedCorpus_ReadingNeverOverlapsReplacementOrPronunciation()
    {
        // Arrange
        var overrides = PronunciationOverrides.Create(
        [
            PronunciationOverrideEntry.Say("wound", "hurt"),
            PronunciationOverrideEntry.SpellOut("lead"),
            PronunciationOverrideEntry.Pronounce("tear", PronunciationScheme.Ipa, "tɪɹ"),
            PronunciationOverrideEntry.Skip("close"),
        ]);
        var analyzer = CreateAnalyzer(new CountingModelSource(RecordModel.Value), new EnglishTextAnalyzerOptions { Overrides = overrides });
        string[] corpus =
        [
            "Dr. Read said to record 5 minutes of the record and lead the wound to close a tear.",
            "It costs $5 million to record a record, and 3 kg of lead is read by Mr. Smith at 5pm.",
            "The wound was close; a tear fell on the record. Read it now! I read 20 pages of the record.",
            "FBI agents record U.S. records; the lead record reads 5 reads.",
        ];

        // Act / Assert
        var totalReadings = 0;
        foreach (var text in corpus)
        {
            var result = analyzer.Analyze(text, TestContext.Current.CancellationToken);
            var covered = result.Annotations
                .Where(annotation => annotation.Kind is TextEnAnnotations.Normalized or TextEnAnnotations.SpellOut or TextEnAnnotations.Pronunciation)
                .ToList();
            var readings = Readings(result.Annotations);
            totalReadings += readings.Count;
            Assert.All(readings, reading =>
            {
                Assert.DoesNotContain(covered, other => other.Start < reading.Start + reading.Length && reading.Start < other.End);
                Assert.Contains(result.Annotations, annotation => annotation.Kind == TextEnAnnotations.ReadingConfidence && annotation.Start == reading.Start && annotation.Length == reading.Length);
            });
            Assert.Equal(readings.Count, result.Annotations.Count(annotation => annotation.Kind == TextEnAnnotations.ReadingConfidence));
        }

        Assert.True(totalReadings > 0, "the corpus must contain classified readings");

        // a skip, say, spell-out or pronunciation override on the heteronym word suppresses the classifier for that word only
        const string recordText = "I want to record a record.";
        var entries = new[]
        {
            PronunciationOverrideEntry.Skip("record"),
            PronunciationOverrideEntry.Say("record", "recorded"),
            PronunciationOverrideEntry.SpellOut("record"),
            PronunciationOverrideEntry.Pronounce("record", PronunciationScheme.Ipa, "r\u025Bk\u0254rd"),
        };
        foreach (var entry in entries)
        {
            var suppressed = CreateAnalyzer(new CountingModelSource(RecordModel.Value), new EnglishTextAnalyzerOptions { Overrides = PronunciationOverrides.Create([entry]) });
            Assert.Empty(Readings(suppressed.Analyze(recordText, TestContext.Current.CancellationToken).Annotations));
        }

        // an override of another word leaves both readings alone
        var other = CreateAnalyzer(new CountingModelSource(RecordModel.Value), new EnglishTextAnalyzerOptions { Overrides = PronunciationOverrides.Create([PronunciationOverrideEntry.Skip("want")]) });
        Assert.Equal(2, Readings(other.Analyze(recordText, TestContext.Current.CancellationToken).Annotations).Count);
    }

    /// <summary>Proves a reading override beats the classifier, emits the id with confidence 1.000 without any model, and is counted as applied and as an override reading.</summary>
    [Fact]
    public void EnglishTextAnalyzer_Analyze_ReadingOverride_BeatsClassifierAndNeedsNoModel()
    {
        // Arrange: the model would say verb for the first "record"
        const string text = "I want to record a record.";
        var overrides = PronunciationOverrides.Create([PronunciationOverrideEntry.Reading("record", "record.noun")]);
        var sink = new RecordingDiagnostics();
        var withModel = CreateAnalyzer(new CountingModelSource(RecordModel.Value), new EnglishTextAnalyzerOptions { Overrides = overrides, Diagnostics = sink });
        var withoutModel = new EnglishTextAnalyzer(new EnglishTextAnalyzerOptions { Overrides = overrides });

        // Act
        var modelResult = withModel.Analyze(text, TestContext.Current.CancellationToken);
        var plainResult = withoutModel.Analyze(text, TestContext.Current.CancellationToken);

        // Assert
        Assert.All(new[] { modelResult, plainResult }, result =>
        {
            var readings = Readings(result.Annotations);
            Assert.Equal(["record.noun", "record.noun"], readings.Select(reading => reading.Id));
            Assert.All(readings, reading => Assert.Equal("1.000", reading.ConfidenceText));
        });
        Assert.Contains(sink.Events, e => e.Message.Contains("2 overrides applied", StringComparison.Ordinal));
        Assert.Contains(sink.Events, e => e.Category == "TextEn.Heteronym" && e.Message.StartsWith("Assigned 2 heteronym readings: 0 classified, 0 single-candidate, 2 by override, 0 below", StringComparison.Ordinal));
    }

    /// <summary>Proves a reading override is ignored when the reading family is not selected, and the override leaves the spoken text as written.</summary>
    [Fact]
    public void EnglishTextAnalyzer_Analyze_ReadingOverrideWithoutReadingFamily_EmitsNothing()
    {
        // Arrange
        var overrides = PronunciationOverrides.Create([PronunciationOverrideEntry.Reading("record", "record.noun")]);
        var analyzer = new EnglishTextAnalyzer(new EnglishTextAnalyzerOptions { Annotations = TextEnAnnotationSelection.PartOfSpeech | TextEnAnnotationSelection.Normalization, Overrides = overrides });

        // Act
        var result = analyzer.Analyze("A record.", TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(Readings(result.Annotations));
        Assert.DoesNotContain(result.Annotations, annotation => annotation.Kind is TextEnAnnotations.Normalized or TextEnAnnotations.SpellOut or TextEnAnnotations.Pronunciation);
    }

    /// <summary>Proves the detailed analysis carries the reading, the confidence and the low-confidence flag on the right tokens, and its annotations equal the language-neutral path's.</summary>
    [Fact]
    public void EnglishTextAnalyzer_AnalyzeDetailed_Readings_AreOnTokensAndEqualAnalyze()
    {
        // Arrange
        const string text = "I want to record a record.";
        var analyzer = CreateAnalyzer(new CountingModelSource(RecordModel.Value), new EnglishTextAnalyzerOptions { LowConfidenceThreshold = 1.0 });

        // Act
        var detailed = analyzer.AnalyzeDetailed(text, TestContext.Current.CancellationToken);
        var neutral = analyzer.Analyze(text, TestContext.Current.CancellationToken);

        // Assert
        var withReading = detailed.Tokens.Where(token => token.Reading is not null).ToList();
        Assert.Equal(["record.verb", "record.noun"], withReading.Select(token => token.Reading));
        Assert.All(withReading, token =>
        {
            Assert.Equal("record", token.Text);
            Assert.InRange(token.ReadingConfidence, 0.5, 1.0);
            Assert.True(token.IsLowConfidenceReading);
        });
        Assert.All(detailed.Tokens.Where(token => token.Reading is null), token =>
        {
            Assert.Equal(0.0, token.ReadingConfidence);
            Assert.False(token.IsLowConfidenceReading);
        });
        Assert.Equal(Key(neutral.Annotations), Key(detailed.Annotations));
    }

    /// <summary>Proves the reading confidence is written with an invariant period and three decimals under any culture.</summary>
    [Fact]
    public void EnglishTextAnalyzer_Analyze_ConfidenceText_IsInvariantUnderTurkishAndFrenchCultures()
    {
        // Arrange
        var analyzer = CreateAnalyzer(new CountingModelSource(RecordModel.Value));
        var previous = CultureInfo.CurrentCulture;
        try
        {
            foreach (var name in new[] { "tr-TR", "fr-FR", "ar-SA" })
            {
                CultureInfo.CurrentCulture = new CultureInfo(name);

                // Act
                var readings = Readings(analyzer.Analyze("I want to record a record.", TestContext.Current.CancellationToken).Annotations);

                // Assert
                Assert.Equal(2, readings.Count);
                Assert.All(readings, reading => Assert.Matches("^[01]\\.[0-9]{3}$", reading.ConfidenceText));
            }
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    /// <summary>Proves the low-confidence count follows the threshold option: zero counts none, one counts every classified reading, and a classified reading is emitted either way.</summary>
    /// <param name="threshold">The threshold.</param>
    /// <param name="expectedLow">The expected low-confidence count.</param>
    [Theory]
    [InlineData(0.0, 0)]
    [InlineData(0.6, 0)]
    [InlineData(1.0, 2)]
    public void EnglishTextAnalyzer_Analyze_LowConfidenceThreshold_CountsClassifiedReadingsBelowIt(double threshold, int expectedLow)
    {
        // Arrange
        var sink = new RecordingDiagnostics();
        var analyzer = CreateAnalyzer(new CountingModelSource(RecordModel.Value), new EnglishTextAnalyzerOptions { Diagnostics = sink, LowConfidenceThreshold = threshold });

        // Act
        var result = analyzer.Analyze("I want to record a record.", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, Readings(result.Annotations).Count);
        var message = Assert.Single(sink.Events, e => e.Category == "TextEn.Heteronym").Message;
        Assert.Equal($"Assigned 2 heteronym readings: 2 classified, 0 single-candidate, 0 by override, {expectedLow} below confidence {threshold.ToString("0.00", CultureInfo.InvariantCulture)}.", message);
    }

    /// <summary>Proves the heteronym event is one fixed-word message with digits only: no words, no reading ids, no culture-specific digits, at most one per request, and none when nothing was assigned.</summary>
    [Fact]
    public void EnglishTextAnalyzer_Analyze_Diagnostics_HeteronymEventIsCountsOnlyAndAtMostOnce()
    {
        // Arrange
        var sink = new RecordingDiagnostics();
        var analyzer = CreateAnalyzer(new CountingModelSource(RecordModel.Value), new EnglishTextAnalyzerOptions { Diagnostics = sink });
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("ar-SA");

            // Act
            _ = analyzer.Analyze("I want to record a record.", TestContext.Current.CancellationToken);
            _ = analyzer.Analyze("The cat sat on the mat.", TestContext.Current.CancellationToken);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }

        // Assert: two analysis events, one heteronym event for the first request only
        Assert.Equal(2, sink.Events.Count(e => e.Category == "TextEn.Analysis"));
        var heteronym = Assert.Single(sink.Events, e => e.Category == "TextEn.Heteronym");
        Assert.Equal(SpeechDiagnosticLevel.Info, heteronym.Level);
        Assert.Matches("^Assigned [0-9]+ heteronym readings: [0-9]+ classified, [0-9]+ single-candidate, [0-9]+ by override, [0-9]+ below confidence [0-9]\\.[0-9]{2}\\.$", heteronym.Message);
        Assert.DoesNotContain("record", heteronym.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("noun", heteronym.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Proves a corrupt or incompatible (newer inventory) model produces no readings and exactly one model warning per analyzer however many requests follow, while a model that is simply absent is silent.</summary>
    [Fact]
    public void EnglishTextAnalyzer_Analyze_ModelWarning_OncePerAnalyzerOnlyForUnusableModel()
    {
        // Arrange
        var corrupt = RecordModel.Value.ToArray();
        corrupt[^1] ^= 0x01;
        var warnings = new RecordingDiagnostics();
        var silent = new RecordingDiagnostics();
        var incompatibleWarnings = new RecordingDiagnostics();
        var badAnalyzer = CreateAnalyzer(new CountingModelSource(corrupt), new EnglishTextAnalyzerOptions { Diagnostics = warnings });
        var absentAnalyzer = CreateAnalyzer(new CountingModelSource(null), new EnglishTextAnalyzerOptions { Diagnostics = silent });
        var incompatible = HeteronymFixtureModel.Build(HeteronymFixtureModel.RecordExamples, inventoryVersion: ReadingInventory.CurrentVersion + 1);
        var incompatibleAnalyzer = CreateAnalyzer(new CountingModelSource(incompatible), new EnglishTextAnalyzerOptions { Diagnostics = incompatibleWarnings });

        // Act
        var results = new[]
        {
            badAnalyzer.Analyze("I want to record a record.", TestContext.Current.CancellationToken),
            badAnalyzer.Analyze("They will record the meeting.", TestContext.Current.CancellationToken),
            absentAnalyzer.Analyze("I want to record a record.", TestContext.Current.CancellationToken),
            incompatibleAnalyzer.Analyze("I want to record a record.", TestContext.Current.CancellationToken),
            incompatibleAnalyzer.Analyze("I want to record a record.", TestContext.Current.CancellationToken),
        };

        // Assert
        Assert.All(results, result => Assert.Empty(Readings(result.Annotations)));
        var warning = Assert.Single(warnings.Events, e => e.Category == "TextEn.Model");
        Assert.Equal(SpeechDiagnosticLevel.Warning, warning.Level);
        Assert.Equal("Heteronym model unusable (invalid or incompatible file); readings disabled.", warning.Message);
        Assert.DoesNotContain(silent.Events, e => e.Category is "TextEn.Model" or "TextEn.Heteronym");
        Assert.DoesNotContain(warnings.Events, e => e.Category == "TextEn.Heteronym");
        Assert.Single(incompatibleWarnings.Events, e => e.Category == "TextEn.Model");
    }

    /// <summary>Proves cancellation between candidate words and during the last candidate throws without returning a result or reporting any event.</summary>
    /// <param name="cancelAfter">The number of classifications after which the observer cancels.</param>
    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public void EnglishTextAnalyzer_Analyze_Cancellation_ThrowsAndReportsNothing(int cancelAfter)
    {
        // Arrange: three candidate words
        const string text = "I want to record a record, then record it.";
        using var cancellation = new CancellationTokenSource();
        var source = new CountingModelSource(RecordModel.Value);
        var sink = new RecordingDiagnostics();
        var provider = HeteronymFixtureModel.CreateProvider(source, count =>
        {
            if (count == cancelAfter)
            {
                cancellation.Cancel();
            }
        });
        var analyzer = new EnglishTextAnalyzer(new EnglishTextAnalyzerOptions { Diagnostics = sink }, PartOfSpeechAdapter.Shared, null, provider);

        // Act / Assert
        Assert.Throws<OperationCanceledException>(() => analyzer.Analyze(text, cancellation.Token));
        Assert.Empty(sink.Events);
    }

    /// <summary>Proves one analyzer shared by 32 concurrent callers gives the same results as a sequential request.</summary>
    [Fact]
    public async Task EnglishTextAnalyzer_Analyze_ConcurrentCallers_GetIdenticalResults()
    {
        // Arrange
        const string text = "I want to record a record. They will record the meeting. This record is old.";
        var analyzer = CreateAnalyzer(new CountingModelSource(RecordModel.Value));
        var expected = Key(analyzer.Analyze(text, TestContext.Current.CancellationToken).Annotations);

        // Act
        var keys = await Task.WhenAll(Enumerable.Range(0, 32).Select(index => Task.Run(
            () => Key(analyzer.Analyze(text, CancellationToken.None).Annotations),
            TestContext.Current.CancellationToken)));

        // Assert
        Assert.All(keys, key => Assert.Equal(expected, key));
    }

    /// <summary>Proves a request whose heteronym word is in a later sentence builds the context of that sentence only: the readings match the single-sentence results.</summary>
    [Fact]
    public void EnglishTextAnalyzer_Analyze_MultipleSentences_ReadingsMatchSingleSentenceAnalysis()
    {
        // Arrange
        var analyzer = CreateAnalyzer(new CountingModelSource(RecordModel.Value));
        string[] sentences = ["They will record the meeting.", "This record is old."];

        // Act
        var together = Readings(analyzer.Analyze(string.Join(' ', sentences), TestContext.Current.CancellationToken).Annotations);
        var apart = sentences.SelectMany(sentence => Readings(analyzer.Analyze(sentence, TestContext.Current.CancellationToken).Annotations)).ToList();

        // Assert
        Assert.Equal(apart.Select(reading => (reading.Id, reading.ConfidenceText)), together.Select(reading => (reading.Id, reading.ConfidenceText)));
    }

    /// <summary>
    ///     Creates an analyzer over a counting model source.
    /// </summary>
    /// <param name="source">The model source.</param>
    /// <param name="options">The options; defaults when null.</param>
    /// <param name="adapter">The part-of-speech adapter; the shared one when null.</param>
    /// <returns>The analyzer.</returns>
    private static EnglishTextAnalyzer CreateAnalyzer(CountingModelSource source, EnglishTextAnalyzerOptions? options = null, PartOfSpeechAdapter? adapter = null) =>
        new(options ?? new EnglishTextAnalyzerOptions(), adapter ?? PartOfSpeechAdapter.Shared, null, HeteronymFixtureModel.CreateProvider(source));

    /// <summary>
    ///     Pairs the reading annotations of a result with their confidence annotations.
    /// </summary>
    /// <param name="annotations">The annotations.</param>
    /// <returns>One entry per reading annotation, in text order.</returns>
    private static List<(int Start, int Length, string Id, string ConfidenceText, double Confidence)> Readings(IReadOnlyList<SegmentAnnotation> annotations)
    {
        var readings = new List<(int, int, string, string, double)>();
        foreach (var annotation in annotations.Where(a => a.Kind == TextEnAnnotations.Reading))
        {
            Assert.True(TextEnAnnotations.TryReadReading(annotation, out var id));
            var confidence = Assert.Single(annotations, other => other.Kind == TextEnAnnotations.ReadingConfidence && other.Start == annotation.Start && other.Length == annotation.Length);
            Assert.True(TextEnAnnotations.TryReadReadingConfidence(confidence, out var value));
            readings.Add((annotation.Start, annotation.Length, id, confidence.Value, value));
        }

        return readings;
    }

    /// <summary>
    ///     Builds a comparable key of annotations.
    /// </summary>
    /// <param name="annotations">The annotations.</param>
    /// <returns>A string equal for equal annotation lists.</returns>
    private static string Key(IReadOnlyList<SegmentAnnotation> annotations) =>
        string.Join(',', annotations.Select(a => $"{a.Start}:{a.Length}:{a.Kind}:{a.Value}"));
}
