using DemaConsulting.Speech.Diagnostics;
using DemaConsulting.Speech.SynthesisSubsystem;
using DemaConsulting.Speech.Text.En.AnalysisSubsystem;
using DemaConsulting.Speech.Text.En.NormalizationSubsystem;
using DemaConsulting.Speech.Text.En.Tests.Fakes;

namespace DemaConsulting.Speech.Text.En.Tests;

/// <summary>
///     System-level tests for SpeechTextEn: the English text analysis exercised through its public
///     API, and end to end through the real core <c>SynthesisSession</c> with a fake synthesis
///     model and an annotation-aware backend. The package metadata (license files and the pinned
///     dependency) is asserted by the FileAssert tests <c>TextEn_PackageLicenseFiles_Content</c> and
///     <c>TextEn_PackageDependencies_Nuspec</c>, which inspect the built package.
/// </summary>
public sealed class SpeechTextEnTests
{
    /// <summary>The tag placeholder as a string, for building test texts.</summary>
    private const string Tag = "\uFFFC";

    /// <summary>Proves the analyzer tags the words of a text with a part of speech a backend can read, and leaves punctuation unannotated.</summary>
    [Fact]
    public void SpeechTextEn_Analyze_Sentence_AnnotatesWordsWithReadablePartOfSpeech()
    {
        // Arrange
        const string text = "The cat sat on the mat.";
        ITextAnalyzer analyzer = new EnglishTextAnalyzer();

        // Act
        var result = analyzer.Analyze(text, TestContext.Current.CancellationToken);

        // Assert: one readable part-of-speech annotation per word, none on the final period
        Assert.Equal(6, result.Annotations.Count);
        Assert.All(result.Annotations, annotation => Assert.True(TextEnAnnotations.TryReadPartOfSpeech(annotation, out _)));
        Assert.Equal("NOUN", ReadTag(result, text, "cat"));
        Assert.Equal("DET", ReadTag(result, text, "The"));
        Assert.All(result.Annotations, annotation => Assert.NotEqual('.', text[annotation.Start]));
    }

    /// <summary>Proves selecting no annotations yields no annotations at all.</summary>
    [Fact]
    public void SpeechTextEn_Analyze_PartOfSpeechDisabled_ProducesNoAnnotations()
    {
        // Arrange
        var analyzer = new EnglishTextAnalyzer(new EnglishTextAnalyzerOptions { Annotations = TextEnAnnotationSelection.None });

        // Act
        var result = analyzer.Analyze("The cat sat on the mat.", TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(result.Annotations);
    }

    /// <summary>Proves the analyzer supplies authoritative sentence breaks that are placeholder-safe.</summary>
    [Fact]
    public void SpeechTextEn_Analyze_MultipleSentencesAndTag_ReturnsAuthoritativeBreaks()
    {
        // Arrange
        var text = $"Dr. Smith arrived.{Tag} He left. Nobody knew why.";
        var analyzer = new EnglishTextAnalyzer();

        // Act
        var result = analyzer.Analyze(text, TestContext.Current.CancellationToken);

        // Assert: non-null, one break at the first word of each later sentence, none inside "Dr. Smith"
        Assert.NotNull(result.SentenceBreaks);
        Assert.Equal([text.IndexOf("He", StringComparison.Ordinal), text.IndexOf("Nobody", StringComparison.Ordinal)], result.SentenceBreaks);
    }

    /// <summary>Proves a placeholder is never covered by an annotation, and the words around it are still annotated.</summary>
    [Fact]
    public void SpeechTextEn_Analyze_TagPlaceholder_IsNeverCoveredByAnAnnotation()
    {
        // Arrange
        var text = $"I've {Tag} read it. {Tag}{Tag}Say it again.";
        var analyzer = new EnglishTextAnalyzer();

        // Act
        var result = analyzer.Analyze(text, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotEmpty(result.Annotations);
        Assert.All(
            result.Annotations,
            annotation => Assert.DoesNotContain(Tag, text.Substring(annotation.Start, annotation.Length), StringComparison.Ordinal));
    }

    /// <summary>Proves an already-canceled token makes the analyzer throw OperationCanceledException.</summary>
    [Fact]
    public void SpeechTextEn_Analyze_CanceledToken_ThrowsOperationCanceledException()
    {
        // Arrange
        ITextAnalyzer analyzer = new EnglishTextAnalyzer();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        // Act / Assert
        Assert.Throws<OperationCanceledException>(() => analyzer.Analyze("The cat sat.", cancellation.Token));
    }

    /// <summary>Proves requests with different texts (numbers, abbreviations, overrides and heteronyms among them) running at once on one shared analyzer each get exactly the result they would get alone, so no request is influenced by another.</summary>
    /// <returns>A task that completes when every request has been checked.</returns>
    [Fact]
    public async Task SpeechTextEn_Analyze_ConcurrentRequestsWithDifferentTexts_EachGetsItsOwnResult()
    {
        // Arrange
        string[] texts =
        [
            "Please read the record.", "Dr. Smith & Jones paid $5 on Jan. 5.", "Quokka gazelles zigzag past the old mill.", "I want to record a record.",
            "Nginx and SQL; Polish food, a GIF.", "5 kg of Kubernetes at 5 pm; NASA's US and FBI.",
        ];
        var options = new EnglishTextAnalyzerOptions { Overrides = NormalizationHarness.LoadSampleDictionary() };
        var analyzer = new EnglishTextAnalyzer(options);
        var expected = texts.Select(text => Describe(new EnglishTextAnalyzer(options).Analyze(text, TestContext.Current.CancellationToken))).ToArray();

        // Act: the very first requests race for the tagger and the heteronym model
        var tasks = Enumerable.Range(0, 32)
            .Select(index => Task.Run(
                () => (Index: index % texts.Length, Result: Describe(analyzer.Analyze(texts[index % texts.Length], TestContext.Current.CancellationToken))),
                TestContext.Current.CancellationToken))
            .ToArray();
        var results = await Task.WhenAll(tasks);

        // Assert
        Assert.Contains(TextEnAnnotations.Reading, expected[3], StringComparison.Ordinal);
        Assert.All(results, result => Assert.Equal(expected[result.Index], result.Result));
    }

    /// <summary>Proves the result and the diagnostics of a request never disclose text of an earlier request on the same analyzer, and the result equals that of a fresh analyzer.</summary>
    [Fact]
    public void SpeechTextEn_Analyze_AfterAnotherRequest_ResultAndDiagnosticsDiscloseNothingOfIt()
    {
        // Arrange: two requests with no word in common
        const string earlier = "Quokka marmoset rested. Zebra galloped.";
        const string later = "I read it again.";
        var sink = new RecordingDiagnostics();
        var used = new EnglishTextAnalyzer(new EnglishTextAnalyzerOptions { Diagnostics = sink });

        // Act
        _ = used.Analyze(earlier, TestContext.Current.CancellationToken);
        var reportedBefore = sink.Events.Count;
        var result = used.Analyze(later, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(Describe(new EnglishTextAnalyzer().Analyze(later, TestContext.Current.CancellationToken)), Describe(result));
        string[] earlierWords = ["Quokka", "marmoset", "rested", "Zebra", "galloped"];
        Assert.All(result.Annotations, annotation => Assert.DoesNotContain(earlierWords, word => annotation.Value.Contains(word, StringComparison.OrdinalIgnoreCase)));
        var reported = sink.Events.Skip(reportedBefore).ToList();
        Assert.NotEmpty(reported);
        Assert.All(reported, report => Assert.DoesNotContain(earlierWords, word => report.Message.Contains(word, StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>Proves an analyzed request reports one counts-only event and a faulty sink never interrupts the analysis.</summary>
    [Fact]
    public void SpeechTextEn_Diagnostics_AnalyzedRequest_ReportsCountsOnlyAndSinkFailureIsContained()
    {
        // Arrange: words that cannot occur in the fixed message
        const string text = "Quokka gazelles zigzag.";
        var recording = new RecordingDiagnostics();
        var recordingAnalyzer = new EnglishTextAnalyzer(new EnglishTextAnalyzerOptions { Diagnostics = recording });
        var faultyAnalyzer = new EnglishTextAnalyzer(new EnglishTextAnalyzerOptions { Diagnostics = new RecordingDiagnostics(throwOnReport: true) });

        // Act
        _ = recordingAnalyzer.Analyze(text, TestContext.Current.CancellationToken);
        var faultyResult = faultyAnalyzer.Analyze(text, TestContext.Current.CancellationToken);

        // Assert
        var reported = Assert.Single(recording.Events);
        Assert.Equal("TextEn.Analysis", reported.Category);
        Assert.Matches(@"^Analyzed one text: 1 sentences, \d+ tokens, \d+ annotations, \d+ unknown words, \d+ normalizations, \d+ ambiguous readings, \d+ numeric fallbacks, \d+ overrides applied, \d+ overrides ignored\.$", reported.Message);
        Assert.DoesNotContain("Quokka", reported.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(3, faultyResult.Annotations.Count);
    }

    /// <summary>
    ///     Proves, end to end through a real <c>SynthesisSession</c>, a fake synthesis model and an annotation-aware backend with the real default
    ///     analyzer, that the normalization, spell-out, override and heteronym-reading annotations of a long text with audio tags reach the
    ///     backend, each on the exact characters of the chunk that holds the word: nothing is lost, duplicated or moved by the chunking, a
    ///     number or an abbreviation is never split from its annotation, and an override beats the model.
    /// </summary>
    [Fact]
    public async Task SpeechTextEn_SynthesisSession_EndToEnd_AnnotationsReachBackendAcrossChunksAndTags()
    {
        // Arrange: three paragraphs, well over the chunk length, each with numbers, abbreviations, an initialism, one override per action, a heteronym and three audio tags
        const string dictionary = "Nginx\tinsensitive\tsay\tengine ex\nGIF\tsensitive\tspell\nPolish\tsensitive\tpron\tipa:\u02C8po\u028Al\u026A\u0283\nNASA\tsensitive\tskip\nlead\tsensitive\treading\tlead.metal\n";
        const string paragraph = "Dr. Smith paid $1,234.50 at 5:30pm on January 5, 2024 [excited] and 50% went to the FBI. I want to record a record. Nginx and the GIF met Polish & Co. [whispers] NASA agreed. She will lead the team. John Smith Jr. Ph.D. spoke [short pause] again. ";
        var input = string.Concat(Enumerable.Repeat(paragraph, 3)).TrimEnd();
        var seenByAnalyzer = input.Replace("[excited]", Tag, StringComparison.Ordinal).Replace("[whispers]", Tag, StringComparison.Ordinal).Replace("[short pause]", Tag, StringComparison.Ordinal);
        var analyzer = new EnglishTextAnalyzer(new EnglishTextAnalyzerOptions { Overrides = PronunciationOverrides.FromTsv(new StringReader(dictionary)) });
        var expected = Overlays(analyzer.Analyze(seenByAnalyzer, TestContext.Current.CancellationToken).Annotations, seenByAnalyzer, includePartOfSpeech: true);
        var backend = new RecordingAnnotatedBackend();

        // Act
        await SynthesizeAsync(new AnalyzingSynthesisModel(analyzer, backend), input);

        // Assert: several chunks, no tag in any of them, and the words of the text arrive in order (the chunks only trim whitespace)
        var calls = backend.Calls;
        Assert.True(calls.Count > 3);
        Assert.All(calls, call => Assert.DoesNotContain(Tag, call.Text, StringComparison.Ordinal));
        Assert.Equal(
            string.Concat(seenByAnalyzer.Replace(Tag, string.Empty, StringComparison.Ordinal).Where(character => !char.IsWhiteSpace(character))),
            string.Concat(calls.SelectMany(call => call.Text).Where(character => !char.IsWhiteSpace(character))));

        // Assert: the annotations the backend received are exactly the analyzer's, on the same characters and in the same order
        var delivered = calls.SelectMany(call => Overlays(call.Annotations, call.Text, includePartOfSpeech: true)).ToList();
        Assert.Equal(expected, delivered);
        foreach (var kind in new[] { TextEnAnnotations.PartOfSpeech, TextEnAnnotations.Normalized, TextEnAnnotations.SpellOut, TextEnAnnotations.Pronunciation, TextEnAnnotations.Reading, TextEnAnnotations.ReadingConfidence })
        {
            Assert.Contains(delivered, item => item.Kind == kind);
        }

        // Assert: normalization and spelling, as a backend reads them
        Assert.Contains(("Dr.", TextEnAnnotations.Normalized, "Doctor"), delivered);
        Assert.Contains(("$1,234.50", TextEnAnnotations.Normalized, "one thousand two hundred thirty four dollars and fifty cents"), delivered);
        Assert.Contains(("FBI", TextEnAnnotations.SpellOut, "F B I"), delivered);
        Assert.Contains(("Ph.D.", TextEnAnnotations.SpellOut, "P H D"), delivered);

        // Assert: one override per action; the skipped word has no replacement
        Assert.Contains(("Nginx", TextEnAnnotations.Normalized, "engine ex"), delivered);
        Assert.Contains(("GIF", TextEnAnnotations.SpellOut, "G I F"), delivered);
        Assert.Contains(("Polish", TextEnAnnotations.Pronunciation, "ipa:\u02C8po\u028Al\u026A\u0283"), delivered);
        Assert.DoesNotContain(delivered, item => item.Source == "NASA" && item.Kind is TextEnAnnotations.Normalized or TextEnAnnotations.SpellOut);

        // Assert: the sentence breaks of the analysis, not every period, decide the chunks: an abbreviation and a name's credentials are not cut off
        Assert.DoesNotContain(calls, call => call.Text.EndsWith("Dr.", StringComparison.Ordinal) || call.Text.EndsWith("Jr.", StringComparison.Ordinal));
        Assert.Contains(calls, call => call.Text.Contains("John Smith Jr. Ph.D. spoke", StringComparison.Ordinal));

        // Assert: the heteronym readings, each with a confidence on the same span, in the same chunk; the override reading is certain
        var records = calls
            .SelectMany(call => call.Annotations
                .Where(annotation => annotation.Kind == TextEnAnnotations.Reading && call.Text.Substring(annotation.Start, annotation.Length) == "record")
                .Select(annotation => annotation.Value))
            .ToList();
        Assert.Equal(["record.verb", "record.noun", "record.verb", "record.noun", "record.verb", "record.noun"], records);
        foreach (var call in calls)
        {
            foreach (var reading in call.Annotations.Where(annotation => annotation.Kind == TextEnAnnotations.Reading))
            {
                var confidence = Assert.Single(call.Annotations, annotation => annotation.Kind == TextEnAnnotations.ReadingConfidence && annotation.Start == reading.Start && annotation.Length == reading.Length);
                Assert.True(TextEnAnnotations.TryReadReadingConfidence(confidence, out var value));
                Assert.InRange(value, 0.0, 1.0);
                if (call.Text.Substring(reading.Start, reading.Length) == "lead")
                {
                    Assert.Equal("lead.metal", reading.Value);
                    Assert.Equal("1.000", confidence.Value);
                }

                Assert.DoesNotContain(call.Annotations, annotation => annotation.Kind is TextEnAnnotations.Normalized or TextEnAnnotations.SpellOut or TextEnAnnotations.Pronunciation && annotation.Start < reading.End && reading.Start < annotation.End);
            }
        }
    }

    /// <summary>
    ///     Proves, through a real <c>SynthesisSession</c> and the real default analyzer, that wherever the chunk budget or a clause falls
    ///     (every padding is tried), no number, date, amount or symbol annotation is split from its text or lost, a chunk of only a symbol
    ///     or an invisible character is merged into a neighbor with its annotation, and a lone symbol keeps its normalization.
    /// </summary>
    [Fact]
    public async Task SpeechTextEn_SynthesisSession_NumbersAndSymbolsAtChunkCuts_AreNeverSplitOrLost()
    {
        // Arrange
        var backend = new RecordingAnnotatedBackend();
        var sawLoneSign = false;

        // Act / Assert
        await WithEngineAsync(new AnalyzingSynthesisModel(new EnglishTextAnalyzer(), backend), async engine =>
        {
            // The 200-character budget cut falls anywhere in a text of two-token amounts, times, dates and units
            for (var padding = 150; padding <= 199; padding++)
            {
                var text = string.Concat(Enumerable.Repeat("w", padding - 1)) + " $5 million at 5:30pm on January 5 2024 for 5 kg and 1,234.50 or 12/25/2024 end";
                var before = backend.Calls.Count;
                _ = await engine.SynthesizeAsync(text, TestContext.Current.CancellationToken);
                var expected = new EnglishTextAnalyzer().Analyze(text, TestContext.Current.CancellationToken).Annotations
                    .Where(annotation => annotation.Kind == TextEnAnnotations.Normalized)
                    .Select(annotation => (text.Substring(annotation.Start, annotation.Length), annotation.Value))
                    .ToList();
                Assert.True(expected.Count >= 9);
                Assert.Equal(expected, DeliveredNormalizations(backend.Calls.Skip(before).ToList()));
            }

            // The cut falls between a number and its percent sign, which then stands alone in a chunk
            for (var padding = 185; padding <= 199; padding++)
            {
                var text = string.Concat(Enumerable.Repeat("w", padding - 1)) + " 50 %";
                var before = backend.Calls.Count;
                _ = await engine.SynthesizeAsync(text, TestContext.Current.CancellationToken);
                var calls = backend.Calls.Skip(before).ToList();
                Assert.Equal([("50", "fifty"), ("%", "percent")], DeliveredNormalizations(calls));
                sawLoneSign |= calls.Any(call => call.Text == "%");
            }

            // The comma between a day and a year is a clause cut, so the two numbers land in different chunks
            var before2 = backend.Calls.Count;
            _ = await engine.SynthesizeAsync("He was born on January 5, 2024, then paid $1,234.50 at 5:30pm: 50 % done.", TestContext.Current.CancellationToken);
            var clauseCalls = backend.Calls.Skip(before2).ToList();
            Assert.True(clauseCalls.Count >= 3);
            Assert.Equal(
                [("5", "fifth"), ("2024", "twenty twenty four"), ("$1,234.50", "one thousand two hundred thirty four dollars and fifty cents"), ("5:30", "five thirty"), ("50", "fifty"), ("%", "percent")],
                DeliveredNormalizations(clauseCalls));
            Assert.Equal(
                [("pm", " P M")],
                clauseCalls.SelectMany(call => call.Annotations.Where(annotation => annotation.Kind == TextEnAnnotations.SpellOut).Select(annotation => (call.Text.Substring(annotation.Start, annotation.Length), annotation.Value))));

            // A lone symbol, and a symbol before clause punctuation, keep their chunk and normalization
            var before3 = backend.Calls.Count;
            _ = await engine.SynthesizeAsync("&", TestContext.Current.CancellationToken);
            _ = await engine.SynthesizeAsync("\u00A9; Brand", TestContext.Current.CancellationToken);
            var symbolCalls = backend.Calls.Skip(before3).ToList();
            Assert.Equal(["&", "\u00A9;", "Brand"], symbolCalls.Select(call => call.Text));
            Assert.Equal([("&", "and"), ("\u00A9", "copyright")], DeliveredNormalizations(symbolCalls));

            // A clause of only an invisible character or a bare symbol is merged into its neighbor, and carries no part of speech
            var before4 = backend.Calls.Count;
            _ = await engine.SynthesizeAsync("Hello, \u200B, there", TestContext.Current.CancellationToken);
            _ = await engine.SynthesizeAsync("Hello, *, there", TestContext.Current.CancellationToken);
            var mergedCalls = backend.Calls.Skip(before4).ToList();
            Assert.Equal(["Hello, \u200B,", "there", "Hello, *,", "there"], mergedCalls.Select(call => call.Text));
            var dropped = Assert.Single(mergedCalls[0].Annotations, annotation => annotation.Kind == TextEnAnnotations.Normalized);
            Assert.Equal(("\u200B", string.Empty), (mergedCalls[0].Text.Substring(dropped.Start, dropped.Length), dropped.Value));
            Assert.DoesNotContain(mergedCalls[0].Annotations, annotation => annotation.Kind == TextEnAnnotations.PartOfSpeech && mergedCalls[0].Text[annotation.Start] == '\u200B');
            Assert.DoesNotContain(mergedCalls[2].Annotations, annotation => mergedCalls[2].Text.Substring(annotation.Start, annotation.Length).Contains('*'));
        });
        Assert.True(sawLoneSign, "no padding put the percent sign alone in a chunk");
    }

    /// <summary>
    ///     Proves the default analyzer, with no host configuration, decides the reading of a heteronym word in
    ///     clear sentences (a curated set written independently of any labeled data set): one reading annotation
    ///     and one confidence in [0, 1] on exactly the word, next to its part-of-speech annotation.
    /// </summary>
    /// <param name="text">The sentence.</param>
    /// <param name="word">The heteronym word in it.</param>
    /// <param name="expectedReading">The reading id the word has in that sentence.</param>
    [Theory]
    [InlineData("They will record the meeting tonight.", "record", "record.verb")]
    [InlineData("She broke the world record last year.", "record", "record.noun")]
    [InlineData("The old pipes were made of lead.", "lead", "lead.metal")]
    [InlineData("Yesterday I read the whole book.", "read", "read.past")]
    [InlineData("A cold wind blew across the field.", "wind", "wind.air")]
    [InlineData("Be careful not to tear the paper.", "tear", "tear.rip")]
    [InlineData("He caught a large bass in the lake.", "bass", "bass.fish")]
    [InlineData("He drew the bow and shot an arrow.", "bow", "bow.weapon")]
    public void SpeechTextEn_Analyze_DefaultAnalyzerClearHeteronymSentences_EmitTheExpectedReading(string text, string word, string expectedReading)
    {
        // Arrange
        ITextAnalyzer analyzer = new EnglishTextAnalyzer();
        var start = text.IndexOf(word, StringComparison.Ordinal);

        // Act
        var result = analyzer.Analyze(text, TestContext.Current.CancellationToken);

        // Assert: exactly one reading and one confidence on exactly the word, and a part of speech beside them
        var onWord = result.Annotations.Where(annotation => annotation.Start == start && annotation.Length == word.Length).ToList();
        var reading = Assert.Single(onWord, annotation => annotation.Kind == TextEnAnnotations.Reading);
        var confidence = Assert.Single(onWord, annotation => annotation.Kind == TextEnAnnotations.ReadingConfidence);
        Assert.True(TextEnAnnotations.TryReadReading(reading, out var readingId));
        Assert.Equal(expectedReading, readingId);
        Assert.True(TextEnAnnotations.TryReadReadingConfidence(confidence, out var confidenceValue));
        Assert.InRange(confidenceValue, 0.0, 1.0);
        Assert.Contains(onWord, annotation => annotation.Kind == TextEnAnnotations.PartOfSpeech);
        Assert.Single(result.Annotations, annotation => annotation.Kind == TextEnAnnotations.Reading);
    }

    /// <summary>Proves a text with no heteronym word gets part-of-speech annotations only, so a host that does nothing sees no reading where there is nothing to decide.</summary>
    [Fact]
    public void SpeechTextEn_Analyze_DefaultAnalyzerTextWithoutHeteronym_EmitsNoReadingAnnotations()
    {
        // Arrange
        const string text = "The cat sat on the mat.";
        ITextAnalyzer analyzer = new EnglishTextAnalyzer();

        // Act
        var result = analyzer.Analyze(text, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotEmpty(result.Annotations);
        Assert.All(result.Annotations, annotation => Assert.Equal(TextEnAnnotations.PartOfSpeech, annotation.Kind));
    }

    /// <summary>Proves a reading override fixes the reading of a word through the public API, and the classifier of the default analyzer loses to it, with confidence 1.000.</summary>
    [Fact]
    public void SpeechTextEn_Analyze_ReadingOverride_BeatsClassifier()
    {
        // Arrange
        const string text = "I want to record a record.";
        var overrides = PronunciationOverrides.FromTsv(new StringReader("record\tinsensitive\treading\trecord.noun\n"));
        var withOverride = new EnglishTextAnalyzer(new EnglishTextAnalyzerOptions { Overrides = overrides });
        var withoutOverride = new EnglishTextAnalyzer();

        // Act
        var overridden = withOverride.Analyze(text, TestContext.Current.CancellationToken);
        var classified = withoutOverride.Analyze(text, TestContext.Current.CancellationToken);

        // Assert: the classifier reads the verb and then the noun; the override makes both the noun, certain
        Assert.Equal(["record.verb", "record.noun"], classified.Annotations.Where(annotation => annotation.Kind == TextEnAnnotations.Reading).Select(annotation => annotation.Value));
        Assert.Equal(["record.noun", "record.noun"], overridden.Annotations.Where(annotation => annotation.Kind == TextEnAnnotations.Reading).Select(annotation => annotation.Value));
        Assert.Equal(["1.000", "1.000"], overridden.Annotations.Where(annotation => annotation.Kind == TextEnAnnotations.ReadingConfidence).Select(annotation => annotation.Value));
    }

    /// <summary>Proves the heteronym diagnostics are counts only: no word, no reading id, and the analysis event keeps its exact wording.</summary>
    [Fact]
    public void SpeechTextEn_Diagnostics_Readings_AreCountsOnly()
    {
        // Arrange
        const string text = "I want to record a record.";
        var sink = new RecordingDiagnostics();
        var analyzer = new EnglishTextAnalyzer(new EnglishTextAnalyzerOptions { Diagnostics = sink });

        // Act
        _ = analyzer.Analyze(text, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, sink.Events.Count);
        Assert.Matches(@"^Analyzed one text: 1 sentences, \d+ tokens, \d+ annotations, \d+ unknown words, \d+ normalizations, \d+ ambiguous readings, \d+ numeric fallbacks, \d+ overrides applied, \d+ overrides ignored\.$", sink.Events[0].Message);
        Assert.Equal("TextEn.Heteronym", sink.Events[1].Category);
        Assert.Matches(@"^Assigned 2 heteronym readings: 2 classified, 0 single-candidate, 0 by override, \d+ below confidence 0\.60\.$", sink.Events[1].Message);
        Assert.All(sink.Events, e =>
        {
            Assert.DoesNotContain("record", e.Message, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("noun", e.Message, StringComparison.Ordinal);
        });
    }

    /// <summary>Proves an abbreviation is described by an <c>en.norm</c> annotation over its exact source characters, the text itself is never changed, and unchanged words get no such annotation.</summary>
    [Fact]
    public void SpeechTextEn_Analyze_Abbreviation_AnnotatesSpokenFormWithoutChangingText()
    {
        // Arrange
        const string text = "Dr. Smith lives here.";
        ITextAnalyzer analyzer = new EnglishTextAnalyzer();

        // Act
        var result = analyzer.Analyze(text, TestContext.Current.CancellationToken);

        // Assert: exactly one normalization, on "Dr.", whose reader gives the spoken form
        var normalization = Assert.Single(result.Annotations, annotation => annotation.Kind == TextEnAnnotations.Normalized);
        Assert.Equal("Dr.", text.Substring(normalization.Start, normalization.Length));
        Assert.True(TextEnAnnotations.TryReadNormalized(normalization, out var spoken));
        Assert.Equal("Doctor", spoken);
        Assert.Equal("Doctor Smith lives here.", NormalizationHarness.ApplyNormalizations(text, result.Annotations));
        Assert.Equal("Dr. Smith lives here.", text);
    }

    /// <summary>Proves the host can switch normalization off, and that normalization alone gives no part of speech.</summary>
    [Fact]
    public void SpeechTextEn_Analyze_NormalizationNotSelected_ProducesNoNormAnnotations()
    {
        // Arrange
        const string text = "Dr. Smith & Mr. Jones live on Main St. It is nice.";
        var tagsOnly = new EnglishTextAnalyzer(new EnglishTextAnalyzerOptions { Annotations = TextEnAnnotationSelection.PartOfSpeech });
        var normalizationOnly = new EnglishTextAnalyzer(new EnglishTextAnalyzerOptions { Annotations = TextEnAnnotationSelection.Normalization });

        // Act
        var tagged = tagsOnly.Analyze(text, TestContext.Current.CancellationToken);
        var normalized = normalizationOnly.Analyze(text, TestContext.Current.CancellationToken);

        // Assert
        Assert.DoesNotContain(tagged.Annotations, annotation => annotation.Kind == TextEnAnnotations.Normalized);
        Assert.NotEmpty(tagged.Annotations);
        Assert.All(normalized.Annotations, annotation => Assert.Equal(TextEnAnnotations.Normalized, annotation.Kind));
        Assert.NotEmpty(normalized.Annotations);
    }

    /// <summary>Proves an ambiguous abbreviation (St. and Dr. here) is resolved from its neighbors.</summary>
    /// <param name="text">The text.</param>
    /// <param name="source">The abbreviation as written.</param>
    /// <param name="spoken">The expected spoken form of the first occurrence.</param>
    [Theory]
    [InlineData("Visit St. Louis. Then go.", "St.", "Saint")]
    [InlineData("Maple Dr. He likes it.", "Dr", "Drive")]
    public void SpeechTextEn_Analyze_ContextDependentAbbreviations_ResolveFromNeighbors(string text, string source, string spoken)
    {
        // Arrange
        var analyzer = new EnglishTextAnalyzer();

        // Act
        var result = analyzer.Analyze(text, TestContext.Current.CancellationToken);

        // Assert: the first normalization is the abbreviation (the number after it is read by the numeric rule)
        var normalization = result.Annotations.First(annotation => annotation.Kind == TextEnAnnotations.Normalized);
        Assert.Equal(source, text.Substring(normalization.Start, normalization.Length));
        Assert.Equal(spoken, normalization.Value);
    }

    /// <summary>Proves a sentence-final abbreviation ends its sentence, keeps its period for the sentence end, and a title never ends one.</summary>
    [Fact]
    public void SpeechTextEn_Analyze_AbbreviationAtSentenceEnd_BreaksAfterItAndKeepsThePeriod()
    {
        // Arrange
        const string text = "I live on Main St. It is nice. Dr. Smith agrees. No. I don't know.";
        var analyzer = new EnglishTextAnalyzer();

        // Act
        var result = analyzer.Analyze(text, TestContext.Current.CancellationToken);

        // Assert: breaks exactly before It, Dr., No. and I; the period after Main St stays outside its span
        Assert.Equal(
            [text.IndexOf("It is", StringComparison.Ordinal), text.IndexOf("Dr. Smith", StringComparison.Ordinal), text.IndexOf("No. I", StringComparison.Ordinal), text.IndexOf("I don't", StringComparison.Ordinal)],
            result.SentenceBreaks);
        Assert.Equal("I live on Main Street. It is nice. Doctor Smith agrees. No. I don't know.", NormalizationHarness.ApplyNormalizations(text, result.Annotations));
    }

    /// <summary>Proves credentials after a name suffix stay in the name's sentence, while a sentence starter after them still ends it.</summary>
    [Fact]
    public void SpeechTextEn_Analyze_CredentialsAfterNameSuffix_StayInOneSentence()
    {
        // Arrange
        const string text = "John Smith Jr. Ph.D. spoke. Ann Lee Esq. Ph.D. M.D. left. I met John Smith Jr. He left.";
        var analyzer = new EnglishTextAnalyzer(new EnglishTextAnalyzerOptions { Annotations = TextEnAnnotationSelection.Normalization });

        // Act
        var result = analyzer.Analyze(text, TestContext.Current.CancellationToken);

        // Assert: breaks only before "Ann", "I" and "He"
        Assert.Equal(
            [text.IndexOf("Ann Lee", StringComparison.Ordinal), text.IndexOf("I met", StringComparison.Ordinal), text.IndexOf("He left", StringComparison.Ordinal)],
            result.SentenceBreaks);
        Assert.Equal(
            "John Smith Junior P H D spoke. Ann Lee Esquire P H D M D left. I met John Smith Junior. He left.",
            NormalizationHarness.ApplyNormalizations(text, result.Annotations));
    }

    /// <summary>Proves the detailed analysis records the grammatical role of each clitic, and the language-neutral path does not need it.</summary>
    [Fact]
    public void SpeechTextEn_AnalyzeDetailed_Clitics_RecordTheirRole()
    {
        // Arrange
        const string text = "It's been cold, John's book is here, and I don't know.";
        var analyzer = new EnglishTextAnalyzer();

        // Act
        var analysis = analyzer.AnalyzeDetailed(text, TestContext.Current.CancellationToken);

        // Assert
        var roles = analysis.Tokens.Where(token => token.Clitic != CliticKind.None).Select(token => (token.Text, token.Clitic)).ToList();
        Assert.Equal([("'s", CliticKind.Has), ("'s", CliticKind.Possessive), ("n't", CliticKind.Not)], roles);
    }

    /// <summary>Proves numbers, currency amounts, percentages, units, times and dates are spoken as plain words per token, the text itself is never rewritten, and the tokens that are not numbers are left unannotated.</summary>
    [Fact]
    public void SpeechTextEn_Analyze_NumbersCurrencyUnitsDatesAndTimes_AreSpokenAsWordsWithoutChangingText()
    {
        // Arrange: (text, the text as spoken)
        (string Text, string Spoken)[] rows =
        [
            (
                "I have 5 apples, 1,234 pears, 3.5 plums, the 21st, #5 and the 1990s.",
                "I have five apples, one thousand two hundred thirty four pears, three point five plums, the twenty first, number five and the nineteen nineties."),
            (
                "Pay $1,234.50, \u20AC5 and 50 % of 5 million USD for 3 kg, 1 ft. and 72\u00B0F.",
                "Pay one thousand two hundred thirty four dollars and fifty cents, five euros and fifty percent of five million dollars for three kilograms, one foot and seventy two degrees Fahrenheit."),
            (
                "On January 5th, 2024 at 5:30pm, 12/25/2024 at 17:45 and 7 am.",
                "On January fifth, twenty twenty four at five thirty P M, December twenty fifth twenty twenty four at seventeen forty five and seven A M."),
        ];
        var analyzer = new EnglishTextAnalyzer(new EnglishTextAnalyzerOptions { Annotations = TextEnAnnotationSelection.Normalization });

        foreach (var (text, spoken) in rows)
        {
            // Act
            var result = analyzer.Analyze(text, TestContext.Current.CancellationToken);

            // Assert: the annotations name the source characters, every value is plain words and the source is untouched
            Assert.Equal(spoken, NormalizationHarness.ApplyNormalizations(text, result.Annotations));
            Assert.All(
                result.Annotations.Where(annotation => annotation.Kind == TextEnAnnotations.Normalized),
                annotation => Assert.Matches("^[A-Za-z' ]+$", annotation.Value));
        }
    }

    /// <summary>Proves the sentence breaks around numbers, decimals, amounts, abbreviated dates and units fall exactly where the sentences end.</summary>
    [Fact]
    public void SpeechTextEn_Analyze_NumericSentenceBreaks_FollowTheSentenceEnd()
    {
        // Arrange: a decimal, an amount, a number and an abbreviated month before a period are not sentence ends by themselves
        const string text = "It costs $1.25. Then $2. Pi is 3.14. Item 5. Jan. 5, 2024 was cold. He left at 5 p.m. Then slept. He is 5 ft. tall. She is 5 ft. Then left.";
        var analyzer = new EnglishTextAnalyzer(new EnglishTextAnalyzerOptions { Annotations = TextEnAnnotationSelection.Normalization });

        // Act
        var result = analyzer.Analyze(text, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            "It costs $1.25.¦Then $2.¦Pi is 3.14.¦Item 5.¦Jan. 5, 2024 was cold.¦He left at 5 p.m.¦Then slept.¦He is 5 ft. tall.¦She is 5 ft.¦Then left.",
            NormalizationHarness.JoinSentences(text, result.SentenceBreaks!));
    }

    /// <summary>Proves month and weekday abbreviations resolve from the date around them, keep the sentence breaks that are real and drop the ones the tokenizer invented.</summary>
    [Fact]
    public void SpeechTextEn_Analyze_MonthAndWeekdayAbbreviations_ResolveFromDateContext()
    {
        // Arrange
        const string text = "Sat. 5 Jan. 2024 and Sun. Mar. 5 and Wed. 7:30. He left Mon. He came Tue.";
        var analyzer = new EnglishTextAnalyzer(new EnglishTextAnalyzerOptions { Annotations = TextEnAnnotationSelection.Normalization });

        // Act
        var result = analyzer.Analyze(text, TestContext.Current.CancellationToken);

        // Assert: one sentence for the dates (no false break after any abbreviation), then the two real ones
        Assert.Equal(
            "Saturday fifth January twenty twenty four and Sunday March fifth and Wednesday seven thirty. He left Monday. He came Tuesday.",
            NormalizationHarness.ApplyNormalizations(text, result.Annotations));
        Assert.Equal(
            [text.IndexOf("He left", StringComparison.Ordinal), text.IndexOf("He came", StringComparison.Ordinal)],
            result.SentenceBreaks);
    }

    /// <summary>Proves the documented defaults for ambiguous numbers (a bare year, a month-first date, a fraction) are applied and counted, and cued or equal readings are not.</summary>
    [Fact]
    public void SpeechTextEn_Analyze_AmbiguousNumbers_UseDocumentedDefaultsAndAreCounted()
    {
        // Arrange: four ambiguous readings (1999, 3/4, 3/4/2024, 1500) among cued and unambiguous ones
        const string text = "And 1999, 3/4, 3/4/2024, 1500, in 1066, about 1500 people, 12/25/2024, 12/12/2024, 2000.";
        var sink = new RecordingDiagnostics();
        var analyzer = new EnglishTextAnalyzer(new EnglishTextAnalyzerOptions { Annotations = TextEnAnnotationSelection.Normalization, Diagnostics = sink });

        // Act
        var result = analyzer.Analyze(text, TestContext.Current.CancellationToken);

        // Assert: the defaults
        Assert.Equal(
            "And nineteen ninety nine, three quarters, March fourth twenty twenty four, fifteen hundred, in ten sixty six, about one thousand five hundred people, December twenty fifth twenty twenty four, December twelfth twenty twenty four, two thousand.",
            NormalizationHarness.ApplyNormalizations(text, result.Annotations));

        // Assert: counted once each, and only the ambiguous ones
        var reported = Assert.Single(sink.Events);
        Assert.Matches(@"\d+ normalizations, 4 ambiguous readings, 0 numeric fallbacks, 0 overrides applied, 0 overrides ignored\.$", reported.Message);
    }

    /// <summary>Proves oversized numbers are bounded and counted: 16 to 64 digits are read digit by digit, longer ones and long fractions are left as written, and no value exceeds the documented size.</summary>
    [Fact]
    public void SpeechTextEn_Analyze_OversizedNumbers_AreBoundedAndCounted()
    {
        // Arrange: a 16-digit number, a 65-digit number and a fraction of 33 digits
        var sixteen = "1234567890123456";
        var sixtyFive = new string('7', 65);
        var fraction = "0." + new string('1', 33);
        var text = $"A {sixteen} and {sixtyFive} and {fraction} end.";
        var sink = new RecordingDiagnostics();
        var analyzer = new EnglishTextAnalyzer(new EnglishTextAnalyzerOptions { Annotations = TextEnAnnotationSelection.Normalization, Diagnostics = sink });

        // Act
        var result = analyzer.Analyze(text, TestContext.Current.CancellationToken);

        // Assert: only the 16-digit number is annotated (digit by digit); the other two stay as written
        var normalization = Assert.Single(result.Annotations, annotation => annotation.Kind == TextEnAnnotations.Normalized);
        Assert.Equal(sixteen, text.Substring(normalization.Start, normalization.Length));
        Assert.Equal("one two three four five six seven eight nine zero one two three four five six", normalization.Value);
        Assert.All(result.Annotations, annotation => Assert.True(annotation.Value.Length <= NumberVerbalizer.MaxSpokenLength));
        Assert.Matches(@"3 numeric fallbacks, 0 overrides applied, 0 overrides ignored\.$", Assert.Single(sink.Events).Message);
    }

    /// <summary>Proves a number, an audio tag and a unit with no spaces between them keep both annotations and the tag is never covered.</summary>
    [Fact]
    public void SpeechTextEn_Analyze_NumberNextToAudioTag_KeepsBothAnnotations()
    {
        // Arrange: "5<tag>kg" is a number and a unit around a placeholder
        var text = "It weighs 5" + Tag + "kg now, and $" + Tag + "5 or 50" + Tag + "% too.";
        var analyzer = new EnglishTextAnalyzer(new EnglishTextAnalyzerOptions { Annotations = TextEnAnnotationSelection.Normalization });

        // Act
        var result = analyzer.Analyze(text, TestContext.Current.CancellationToken);

        // Assert: "5" and "kg" are each annotated, "50" and "%" too, the symbol before a tag is left alone, and no annotation covers a placeholder
        var normalizations = result.Annotations.Where(annotation => annotation.Kind == TextEnAnnotations.Normalized).ToList();
        Assert.Equal(
            [("5", "five"), ("kg", "kilograms"), ("5", "five"), ("50", "fifty"), ("%", "percent")],
            normalizations.Select(annotation => (text.Substring(annotation.Start, annotation.Length), annotation.Value)));
        Assert.All(normalizations, annotation => Assert.DoesNotContain(Tag, text.Substring(annotation.Start, annotation.Length), StringComparison.Ordinal));
    }

    /// <summary>
    ///     Reads annotations as (start, length, kind, value) tuples.
    /// </summary>
    /// <param name="annotations">The annotations.</param>
    /// <returns>The tuples, in annotation order.</returns>
    private static List<(int Start, int Length, string Kind, string Value)> AsTuples(IEnumerable<SegmentAnnotation> annotations) =>
        [.. annotations.Select(annotation => (annotation.Start, annotation.Length, annotation.Kind, annotation.Value))];

    /// <summary>
    ///     Asserts that, in a text that is one unit repeated, every repetition away from the two ends of the text gets the same
    ///     annotations and sentence breaks as the same repetition in a short reference text of the same unit.
    /// </summary>
    /// <param name="analyzer">The analyzer that produced <paramref name="result"/>.</param>
    /// <param name="unit">The repeated unit.</param>
    /// <param name="text">The large text.</param>
    /// <param name="result">The analysis of the large text.</param>
    private static void AssertRepetitionsAnalyzedAlike(EnglishTextAnalyzer analyzer, string unit, string text, TextAnalysisResult result)
    {
        // The reference is long enough for one repetition to be clear of the edges (the last repetitions look ahead past the end of the text)
        const int referenceCopies = 9;
        const int referenceCopy = 4;
        const int edgeCopiesAtEnd = 3;
        var length = unit.Length;
        var copies = text.Length / length;
        var reference = analyzer.Analyze(string.Concat(Enumerable.Repeat(unit, referenceCopies)), TestContext.Current.CancellationToken);
        var interiorStart = length;
        var interiorEnd = (copies - edgeCopiesAtEnd) * length;
        var interiorCopies = copies - edgeCopiesAtEnd - 1;

        // Annotations
        var pattern = AsTuples(reference.Annotations.Where(annotation => annotation.Start >= referenceCopy * length && annotation.End <= (referenceCopy + 1) * length));
        var interior = AsTuples(result.Annotations.Where(annotation => annotation.Start >= interiorStart && annotation.End <= interiorEnd));
        Assert.NotEmpty(pattern);
        Assert.Equal(interiorCopies * pattern.Count, interior.Count);
        for (var i = 0; i < interior.Count; i++)
        {
            var expected = pattern[i % pattern.Count];
            var shift = (1 + (i / pattern.Count) - referenceCopy) * length;
            Assert.True(interior[i] == (expected.Start + shift, expected.Length, expected.Kind, expected.Value), $"'{unit}': annotation {i} differs");
        }

        // Sentence breaks
        var breakPattern = reference.SentenceBreaks!.Where(offset => offset >= referenceCopy * length && offset < (referenceCopy + 1) * length).ToList();
        var breaks = result.SentenceBreaks!.Where(offset => offset >= interiorStart && offset < interiorEnd).ToList();
        Assert.Equal(interiorCopies * breakPattern.Count, breaks.Count);
        for (var i = 0; i < breaks.Count; i++)
        {
            var shift = (1 + (i / breakPattern.Count) - referenceCopy) * length;
            Assert.True(breaks[i] == breakPattern[i % breakPattern.Count] + shift, $"'{unit}': sentence break {i} differs");
        }
    }

    /// <summary>
    ///     Reads the normalization annotations every backend call received, as pairs of the source characters and the value.
    /// </summary>
    /// <param name="calls">The backend calls, in arrival order.</param>
    /// <returns>The pairs, in the order delivered.</returns>
    private static List<(string Source, string Value)> DeliveredNormalizations(IReadOnlyList<(string Text, IReadOnlyList<SegmentAnnotation> Annotations)> calls) =>
        calls
            .SelectMany(call => call.Annotations
                .Where(annotation => annotation.Kind == TextEnAnnotations.Normalized)
                .Select(annotation => (Source: call.Text.Substring(annotation.Start, annotation.Length), annotation.Value)))
            .ToList();

    /// <summary>Loads a real engine for a model once, runs an action against it, then disposes the engine and the scratch model directory.</summary>
    /// <param name="model">The model whose analyzer and backend are under test.</param>
    /// <param name="action">The action to run against the engine.</param>
    /// <returns>A task that completes when the action has finished.</returns>
    private static async Task WithEngineAsync(AnalyzingSynthesisModel model, Func<ISpeechSynthesizerEngine, Task> action)
    {
        var directory = Directory.CreateTempSubdirectory("text-en-model-");
        try
        {
            await using var engine = await SpeechSynthesizerFactory.LoadAsync(
                model,
                directory.FullName,
                cancellationToken: TestContext.Current.CancellationToken);
            await action(engine);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    /// <summary>
    ///     Proves a large text (a quarter of a million characters) built of one repeated unit, for every numeric construct and for
    ///     realistic and adversarially repetitive units, is analyzed through the public API to the annotations and sentence breaks a
    ///     short text gives: away from the two ends every repetition is annotated and broken exactly alike.
    /// </summary>
    [Fact]
    public void SpeechTextEn_Analyze_LargeRepetitiveTexts_AnnotatesEveryRepetitionAlike()
    {
        // Arrange: normalization only, so no tagger is involved
        string[] units =
        [
            "Dr. Smith lives on Maple Dr. He likes it, etc. The FBI said it\u2019s w/o proof & more. ", "St. ", "A. ", "& ", "Dr. He ",
            "1,234.50 ", "$5.50 ", "5:30pm ", "January 5, 2024 ", "12/25/2024 ", "5 kg ", "50 % ", "Jan. 5 ", "5 ft. ", "5 million USD ", "555-123-4567 ", "1990s ", "3 1/2 ", "1999 ",
        ];
        var analyzer = new EnglishTextAnalyzer(new EnglishTextAnalyzerOptions { Annotations = TextEnAnnotationSelection.Normalization });
        using var hangGuard = new CancellationTokenSource(TimeSpan.FromMinutes(10));

        foreach (var unit in units)
        {
            var text = string.Concat(Enumerable.Repeat(unit, (250_000 / unit.Length) + 1));

            // Act
            var result = analyzer.Analyze(text, hangGuard.Token);

            // Assert
            AssertRepetitionsAnalyzedAlike(analyzer, unit, text, result);
        }
    }

    // ---------------------------------------------------------------- pronunciation overrides, spelled initialisms

    /// <summary>Lists the replacement, pronunciation and reading annotations of a result as (source, kind, value), in order.</summary>
    /// <param name="annotations">The annotations.</param>
    /// <param name="text">The analyzed text.</param>
    /// <param name="includePartOfSpeech">Whether the part-of-speech annotations are listed too.</param>
    /// <returns>The tuples; part-of-speech annotations are left out unless asked for.</returns>
    private static List<(string Source, string Kind, string Value)> Overlays(IEnumerable<SegmentAnnotation> annotations, string text, bool includePartOfSpeech = false) =>
        [.. annotations.Where(annotation => includePartOfSpeech || annotation.Kind != TextEnAnnotations.PartOfSpeech).Select(annotation => (text.Substring(annotation.Start, annotation.Length), annotation.Kind, annotation.Value))];

    /// <summary>Proves a host dictionary annotates whole words only, one annotation per action, on the exact source characters, and never rewrites the text.</summary>
    [Fact]
    public void SpeechTextEn_Analyze_Overrides_AnnotateWholeWordsPerAction()
    {
        // Arrange
        const string text = "Nginx and nginxes run a GIF, Polish SQL; NASA's US.";
        var analyzer = new EnglishTextAnalyzer(new EnglishTextAnalyzerOptions { Annotations = TextEnAnnotationSelection.Normalization, Overrides = NormalizationHarness.LoadSampleDictionary() });

        // Act
        var result = analyzer.Analyze(text, TestContext.Current.CancellationToken);

        // Assert: say, spell, pronounce and skip each act on their whole word only ("nginxes" is another word)
        Assert.Equal(
            [
                ("Nginx", TextEnAnnotations.Normalized, "engine ex"),
                ("GIF", TextEnAnnotations.SpellOut, "G I F"),
                ("Polish", TextEnAnnotations.Pronunciation, "ipa:\u02C8po\u028Al\u026A\u0283"),
                ("SQL", TextEnAnnotations.Normalized, "sequel"),
            ],
            Overlays(result.Annotations, text));
        Assert.Equal("Nginx and nginxes run a GIF, Polish SQL; NASA's US.", text);
    }

    /// <summary>Proves a consumer written only against the public typed readers collects every override action of a dictionary loaded from a file, with the documented algorithm.</summary>
    [Fact]
    public void SpeechTextEn_Analyze_OverrideActions_ReadBackThroughTypedReaders()
    {
        // Arrange
        const string text = "Nginx runs Kubernetes and kubernetes, a GIF and Polish food.";
        ITextAnalyzer analyzer = new EnglishTextAnalyzer(new EnglishTextAnalyzerOptions { Overrides = NormalizationHarness.LoadSampleDictionary() });

        // Act
        var result = analyzer.Analyze(text, TestContext.Current.CancellationToken);

        // Assert
        var replacements = result.Annotations
            .Select(annotation => TextEnAnnotations.TryReadReplacement(annotation, out var spoken, out var spelled) ? (annotation.Start, annotation.End, spoken, spelled) : default)
            .Where(item => item.spoken is not null)
            .OrderBy(item => item.Start)
            .ToList();
        var builder = new System.Text.StringBuilder();
        var position = 0;
        foreach (var (start, end, spoken, _) in replacements)
        {
            builder.Append(text, position, start - position).Append(spoken);
            position = end;
        }

        builder.Append(text, position, text.Length - position);
        Assert.Equal("engine ex runs koo ber net eez and kubernetes, a G I F and Polish food.", builder.ToString());
        Assert.Single(replacements, item => item.spelled);
        var pronunciation = Assert.Single(result.Annotations, annotation => TextEnAnnotations.TryReadPronunciation(annotation, out _, out _));
        Assert.Equal("Polish", text.Substring(pronunciation.Start, pronunciation.Length));
    }

    /// <summary>Proves the documented precedence: a user override beats the built-in acronym table, vowel-less rule, abbreviation lexicon, symbol rule and elision rule, and the built-in reading returns when the override is absent.</summary>
    [Fact]
    public void SpeechTextEn_Analyze_OverridePrecedence_UserOverridesBuiltInRules()
    {
        // Arrange: (text, the override, spoken with the override, spoken without it)
        (string Text, PronunciationOverrideEntry Entry, string With, string Without)[] rows =
        [
            ("US troops", PronunciationOverrideEntry.Skip("US", true), "US troops", "U S troops"),
            ("a SQL query", PronunciationOverrideEntry.Say("SQL", "sequel"), "a sequel query", "a S Q L query"),
            ("Mr Smith", PronunciationOverrideEntry.Say("Mr", "mister"), "mister Smith", "Mister Smith"),
            ("the AT&T deal", PronunciationOverrideEntry.Skip("AT&T"), "the AT&T deal", "the A T and T deal"),
            ("hello y\u2019all", PronunciationOverrideEntry.Skip("y\u2019all"), "hello y\u2019all", "hello y'all"),
            ("an FBI agent", PronunciationOverrideEntry.SpellOut("FBI"), "an F B I agent", "an F B I agent"),
            ("a CIA file", PronunciationOverrideEntry.Say("CIA", "see eye ay"), "a see eye ay file", "a C I A file"),
        ];
        var options = new EnglishTextAnalyzerOptions { Annotations = TextEnAnnotationSelection.Normalization };

        foreach (var (text, entry, with, without) in rows)
        {
            // Act
            var overridden = new EnglishTextAnalyzer(options with { Overrides = PronunciationOverrides.Create([entry]) }).Analyze(text, TestContext.Current.CancellationToken);
            var builtIn = new EnglishTextAnalyzer(options).Analyze(text, TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(with, NormalizationHarness.ApplyNormalizations(text, overridden.Annotations));
            Assert.Equal(without, NormalizationHarness.ApplyNormalizations(text, builtIn.Annotations));
        }

        // a case-sensitive entry is not applied to another case
        var sensitive = new EnglishTextAnalyzer(options with { Overrides = PronunciationOverrides.Create([PronunciationOverrideEntry.Say("Polish", "shine", true)]) });
        const string cased = "Polish and polish";
        Assert.Equal("shine and polish", NormalizationHarness.ApplyNormalizations(cased, sensitive.Analyze(cased, TestContext.Current.CancellationToken).Annotations));
    }

    /// <summary>Proves an override on an abbreviation word keeps the sentence breaks and the chunks: <c>Fig</c> said as figure in <c>See Fig. 5 for details.</c> gives no break and one chunk, and <c>Dr</c> skipped after an em dash keeps the sentence in one piece.</summary>
    [Fact]
    public void SpeechTextEn_Analyze_OverrideOnAbbreviationWord_KeepsSentenceBreaksAndChunks()
    {
        // Arrange
        const string figure = "See Fig. 5 for details.";
        const string dash = "Hello\u2014Dr. Smith left.";
        var say = new EnglishTextAnalyzer(new EnglishTextAnalyzerOptions { Overrides = PronunciationOverrides.Create([PronunciationOverrideEntry.Say("Fig", "figure")]) });
        var skip = new EnglishTextAnalyzer(new EnglishTextAnalyzerOptions { Overrides = PronunciationOverrides.Create([PronunciationOverrideEntry.Skip("Dr")]) });
        var plain = new EnglishTextAnalyzer();

        // Act
        var figureResult = say.Analyze(figure, TestContext.Current.CancellationToken);
        var plainFigure = plain.Analyze(figure, TestContext.Current.CancellationToken);
        var dashResult = skip.Analyze(dash, TestContext.Current.CancellationToken);
        var figureChunks = SentenceChunker.ChunkAligned(figure, figureResult.Annotations, figureResult.SentenceBreaks);
        var dashChunks = SentenceChunker.ChunkAligned(dash, dashResult.Annotations, dashResult.SentenceBreaks);

        // Assert
        Assert.NotNull(figureResult.SentenceBreaks);
        Assert.NotNull(dashResult.SentenceBreaks);
        Assert.NotNull(plainFigure.SentenceBreaks);
        Assert.Empty(figureResult.SentenceBreaks);
        Assert.Empty(plainFigure.SentenceBreaks);
        var annotation = Assert.Single(figureResult.Annotations, item => item.Kind == TextEnAnnotations.Normalized && item.Start == 4);
        Assert.Equal(("Fig", "figure"), (figure.Substring(annotation.Start, annotation.Length), annotation.Value));
        Assert.Equal([figure], figureChunks.Select(chunk => chunk.Text));
        Assert.Equal(plain.Analyze(dash, TestContext.Current.CancellationToken).SentenceBreaks, dashResult.SentenceBreaks);
        Assert.Empty(dashResult.SentenceBreaks);
        Assert.Equal([dash], dashChunks.Select(chunk => chunk.Text));
    }

    /// <summary>Proves an override on a word that an earlier number, currency or time consumed is ignored (the built-in reading stands) and counted, and the same word on its own is overridden.</summary>
    [Fact]
    public void SpeechTextEn_Analyze_OverrideOnConsumedToken_IsIgnoredAndCounted()
    {
        // Arrange
        var sink = new RecordingDiagnostics();
        var overrides = PronunciationOverrides.Create([PronunciationOverrideEntry.Say("kg", "kay gee"), PronunciationOverrideEntry.Say("million", "mill yon")]);
        var analyzer = new EnglishTextAnalyzer(new EnglishTextAnalyzerOptions { Annotations = TextEnAnnotationSelection.Normalization, Overrides = overrides, Diagnostics = sink });
        const string consumed = "It weighs 5 kg and costs $3 million.";
        const string alone = "The kg and the million.";

        // Act
        var consumedResult = analyzer.Analyze(consumed, TestContext.Current.CancellationToken);
        var aloneResult = analyzer.Analyze(alone, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("It weighs five kilograms and costs three million dollars.", NormalizationHarness.ApplyNormalizations(consumed, consumedResult.Annotations));
        Assert.Equal("The kay gee and the mill yon.", NormalizationHarness.ApplyNormalizations(alone, aloneResult.Annotations));
        Assert.EndsWith("0 overrides applied, 2 overrides ignored.", sink.Events[0].Message, StringComparison.Ordinal);
        Assert.EndsWith("2 overrides applied, 0 overrides ignored.", sink.Events[1].Message, StringComparison.Ordinal);
    }

    /// <summary>Proves the documented sample file loads through a stream reader and drives an analysis end to end, and a bad line is refused by its number and a fixed reason, never by its content.</summary>
    [Fact]
    public void SpeechTextEn_Overrides_Dictionary_LoadsFromFileValidatesAndDrivesAnalysis()
    {
        // Arrange
        var overrides = NormalizationHarness.LoadSampleDictionary();
        var analyzer = new EnglishTextAnalyzer(new EnglishTextAnalyzerOptions { Overrides = overrides });
        const string text = "Wi-Fi and Nginx-based Kubernetes apps; Cafe, \u0130stanbul.";

        // Act
        var result = analyzer.Analyze(text, TestContext.Current.CancellationToken);
        var refused = Assert.Throws<InvalidDataException>(() => PronunciationOverrides.FromTsv(new StringReader("Nginx\tinsensitive\tsay\tengine ex\nQuuxsentinel\twrong\tsay\tx\n")));

        // Assert
        Assert.Equal(23, overrides.Count);
        Assert.Equal("why fie and engine ex based koo ber net eez apps; Cafe, istanbul.", NormalizationHarness.ApplyNormalizations(text, result.Annotations));
        Assert.Single(result.Annotations, annotation => annotation.Kind == TextEnAnnotations.Pronunciation && annotation.Value == "arpabet:K AE0 F EY1");
        Assert.StartsWith("Line 2:", refused.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("Quuxsentinel", refused.Message, StringComparison.Ordinal);
    }

    /// <summary>Proves the shapes of spelled replacements - initialisms, dotted initialisms, single-letter initials (including the lone <c>A</c> and <c>I</c> a phoneme backend could take for words), credentials and meridiems - are annotated as spelled, so a consumer never reads them as words.</summary>
    [Fact]
    public void SpeechTextEn_Analyze_SpelledInitialisms_AreAnnotatedAsSpellOut()
    {
        // Arrange
        const string text = "A. Smith met the FBI at 5 p.m. in the U.S. with J. Doe, Ph.D.";
        var analyzer = new EnglishTextAnalyzer(new EnglishTextAnalyzerOptions { Annotations = TextEnAnnotationSelection.Normalization });

        // Act
        var result = analyzer.Analyze(text, TestContext.Current.CancellationToken);

        // Assert: every spelled replacement is en.spell and reads back as letters; none is en.norm
        var spelled = result.Annotations.Where(annotation => annotation.Kind == TextEnAnnotations.SpellOut).Select(annotation => text.Substring(annotation.Start, annotation.Length)).ToList();
        Assert.Equal(["A.", "FBI", "p.m.", "U.S.", "J.", "Ph.D"], spelled);
        Assert.All(
            result.Annotations,
            annotation => Assert.True(
                annotation.Kind == TextEnAnnotations.SpellOut
                    ? TextEnAnnotations.TryReadSpellOut(annotation, out var letters) && letters.Trim().Replace(" ", string.Empty, StringComparison.Ordinal).All(char.IsAsciiLetterUpper)
                    : TextEnAnnotations.TryReadNormalized(annotation, out _)));
        Assert.DoesNotContain(
            result.Annotations,
            annotation => annotation.Kind == TextEnAnnotations.Normalized && System.Text.RegularExpressions.Regex.IsMatch(annotation.Value.Trim(), "^[A-Z]( [A-Z])*$"));
        Assert.Equal("A Smith met the F B I at 5 P M in the U S with J Doe, P H D.", NormalizationHarness.ApplyNormalizations(text, result.Annotations).Replace("five", "5", StringComparison.Ordinal));
    }

    /// <summary>Proves a meridiem glued to its hour or time (<c>5pm</c>, <c>5PM</c>, <c>5:30pm</c>) is two adjacent, non-overlapping spans inside the token - the number as <c>en.norm</c> words and the meridiem letters as typed <c>en.spell</c> - and the separate-token <c>5 p.m.</c> is the same pair, so a consumer never infers spelling from an <c>en.norm</c> value.</summary>
    /// <param name="text">The text.</param>
    /// <param name="numberSource">The source of the <c>en.norm</c> span.</param>
    /// <param name="numberValue">The value of the <c>en.norm</c> span.</param>
    /// <param name="meridiemSource">The source of the <c>en.spell</c> span.</param>
    /// <param name="meridiemValue">The value of the <c>en.spell</c> span, with its separating space when it touches the number.</param>
    [Theory]
    [InlineData("at 5pm today", "5", "five", "pm", " P M")]
    [InlineData("at 5 p.m. today", "5", "five", "p.m.", "P M")]
    public void SpeechTextEn_Analyze_Meridiems_AreTypedSpellOutSpans(string text, string numberSource, string numberValue, string meridiemSource, string meridiemValue)
    {
        // Arrange
        var analyzer = new EnglishTextAnalyzer(new EnglishTextAnalyzerOptions { Annotations = TextEnAnnotationSelection.Normalization });

        // Act
        var result = analyzer.Analyze(text, TestContext.Current.CancellationToken);

        // Assert: the number is en.norm words, the meridiem is en.spell letters, both readable through the typed reader
        Assert.Equal(
            [(numberSource, TextEnAnnotations.Normalized, numberValue), (meridiemSource, TextEnAnnotations.SpellOut, meridiemValue)],
            Overlays(result.Annotations, text));
        var number = result.Annotations.Single(annotation => annotation.Kind == TextEnAnnotations.Normalized);
        var meridiem = result.Annotations.Single(annotation => annotation.Kind == TextEnAnnotations.SpellOut);
        Assert.True(TextEnAnnotations.TryReadReplacement(number, out _, out var numberSpelled));
        Assert.False(numberSpelled);
        Assert.True(TextEnAnnotations.TryReadReplacement(meridiem, out _, out var meridiemSpelled));
        Assert.True(meridiemSpelled);

        // Assert: the spans are in order and do not overlap; glued ones abut exactly inside one token
        Assert.True(number.End <= meridiem.Start);
        if (!text.Contains("p.m.", StringComparison.Ordinal))
        {
            Assert.Equal(number.End, meridiem.Start);
        }

        // Assert: exactly one separator between the two words
        var spoken = NormalizationHarness.ApplyNormalizations(text, result.Annotations);
        Assert.DoesNotContain("  ", spoken, StringComparison.Ordinal);
        Assert.Contains(numberValue + " " + meridiemValue.Trim(), spoken, StringComparison.Ordinal);
    }

    /// <summary>Proves a host <c>Say</c> override whose payload is a single capital letter stays an <c>en.norm</c> word that no reader reports as spelled, so <c>en.norm</c> never carries a spell-out promise.</summary>
    [Fact]
    public void SpeechTextEn_Analyze_SayOverrideWithSingleLetterPayload_IsNotSpelled()
    {
        // Arrange: "foo" is spoken as the word "I", not as the letter name
        const string text = "Use foo today.";
        var overrides = PronunciationOverrides.Create([PronunciationOverrideEntry.Say("foo", "I")]);
        var analyzer = new EnglishTextAnalyzer(new EnglishTextAnalyzerOptions { Annotations = TextEnAnnotationSelection.Normalization, Overrides = overrides });

        // Act
        var result = analyzer.Analyze(text, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal([("foo", TextEnAnnotations.Normalized, "I")], Overlays(result.Annotations, text));
        var annotation = Assert.Single(result.Annotations);
        Assert.True(TextEnAnnotations.TryReadReplacement(annotation, out var value, out var isSpelled));
        Assert.Equal("I", value);
        Assert.False(isSpelled);
        Assert.False(TextEnAnnotations.TryReadSpellOut(annotation, out _));
    }

    /// <summary>Proves the diagnostics event reports only the counts, including the two override counts, and never a key, a payload, a phoneme string or a word of the analyzed text.</summary>
    [Fact]
    public void SpeechTextEn_Diagnostics_Overrides_ReportCountsOnlyAndNeverDictionaryContent()
    {
        // Arrange: sentinel keys and payloads that cannot occur in the fixed message
        var sink = new RecordingDiagnostics();
        var overrides = PronunciationOverrides.Create(
        [
            PronunciationOverrideEntry.Say("Zorblaxsentinel", "quuxsentinel"),
            PronunciationOverrideEntry.Pronounce("Frobsentinel", PronunciationScheme.Arpabet, "F R AA1 B"),
            PronunciationOverrideEntry.SpellOut("Wxyzsentinel"),
            PronunciationOverrideEntry.Skip("kg"),
        ]);
        var analyzer = new EnglishTextAnalyzer(new EnglishTextAnalyzerOptions { Overrides = overrides, Diagnostics = sink });

        // Act
        _ = analyzer.Analyze("Zorblaxsentinel met Frobsentinel and Wxyzsentinel, weighing 5 kg.", TestContext.Current.CancellationToken);

        // Assert
        var reported = Assert.Single(sink.Events);
        Assert.Equal("TextEn.Analysis", reported.Category);
        Assert.Matches(@"^Analyzed one text: 1 sentences, \d+ tokens, \d+ annotations, \d+ unknown words, \d+ normalizations, \d+ ambiguous readings, \d+ numeric fallbacks, 3 overrides applied, 1 overrides ignored\.$", reported.Message);
        foreach (var forbidden in new[] { "sentinel", "Zorblax", "Frob", "Wxyz", "quux", "F R AA1", "arpabet", "ipa", "kg", "weighing" })
        {
            Assert.DoesNotContain(forbidden, reported.Message, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>Synthesizes a text with a real engine for a model, then disposes the engine and the scratch model directory.</summary>
    /// <param name="model">The model whose analyzer and backend are under test.</param>
    /// <param name="text">The text to synthesize.</param>
    /// <returns>A task that completes when synthesis has finished.</returns>
    private static async Task SynthesizeAsync(AnalyzingSynthesisModel model, string text)
    {
        var directory = Directory.CreateTempSubdirectory("text-en-model-");
        try
        {
            await using var engine = await SpeechSynthesizerFactory.LoadAsync(
                model,
                directory.FullName,
                cancellationToken: TestContext.Current.CancellationToken);
            _ = await engine.SynthesizeAsync(text, TestContext.Current.CancellationToken);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    /// <summary>Reads the part of speech of the first annotation covering exactly the given word.</summary>
    /// <param name="result">The analysis result.</param>
    /// <param name="text">The analyzed text.</param>
    /// <param name="word">The word to look for.</param>
    /// <returns>The annotation's part-of-speech value.</returns>
    private static string ReadTag(TextAnalysisResult result, string text, string word)
    {
        var annotation = result.Annotations.First(candidate => text.Substring(candidate.Start, candidate.Length) == word);
        Assert.True(TextEnAnnotations.TryReadPartOfSpeech(annotation, out var tag));
        return tag;
    }

    /// <summary>Builds a comparable description of a result's annotations and breaks.</summary>
    /// <param name="result">The result.</param>
    /// <returns>A string equal for equal results.</returns>
    private static string Describe(TextAnalysisResult result) =>
        string.Join(',', result.Annotations.Select(a => $"{a.Start}:{a.Length}:{a.Kind}:{a.Value}"))
        + "|" + string.Join(',', result.SentenceBreaks!);
}
