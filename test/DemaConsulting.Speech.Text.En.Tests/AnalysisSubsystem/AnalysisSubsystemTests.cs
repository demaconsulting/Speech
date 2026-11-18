using DemaConsulting.Speech.Diagnostics;
using DemaConsulting.Speech.SynthesisSubsystem;
using DemaConsulting.Speech.Text.En.AnalysisSubsystem;
using DemaConsulting.Speech.Text.En.Tests.Fakes;

namespace DemaConsulting.Speech.Text.En.Tests.AnalysisSubsystem;

/// <summary>
///     Subsystem tests for the AnalysisSubsystem: the analyzer, its options, the annotation
///     vocabulary, the part-of-speech adapter and the diagnostics reporter working together.
///     Tagger creation goes through test factories that inject cancellation deterministically.
/// </summary>
public sealed class AnalysisSubsystemTests
{
    /// <summary>The tag placeholder as a string, for building test texts.</summary>
    private const string Tag = "\uFFFC";

    /// <summary>Proves the annotations of an analysis can be read back through the typed reader at the words they annotate.</summary>
    [Fact]
    public void AnalysisSubsystem_Analyze_Sentence_AnnotationsReadBackThroughTypedReader()
    {
        // Arrange
        const string text = "The cat sat on the mat.";
        var analyzer = new EnglishTextAnalyzer(new EnglishTextAnalyzerOptions());

        // Act
        var result = analyzer.Analyze(text, TestContext.Current.CancellationToken);

        // Assert: every annotation reads back as a part of speech at its word
        var read = result.Annotations
            .Select(annotation => TextEnAnnotations.TryReadPartOfSpeech(annotation, out var tag)
                ? (Word: text.Substring(annotation.Start, annotation.Length), Tag: tag)
                : default)
            .ToList();
        Assert.Equal(
            [("The", "DET"), ("cat", "NOUN"), ("sat", "VERB"), ("on", "ADP"), ("the", "DET"), ("mat", "NOUN")],
            read);
    }

    /// <summary>Proves the normalization annotations of an analysis read back through the typed reader and are merged with the parts of speech, with the sentence breaks reconciled.</summary>
    [Fact]
    public void AnalysisSubsystem_Analyze_NormalizedText_AnnotationsReadBackAndBreaksAreReconciled()
    {
        // Arrange
        const string text = "I live on Main St. It is nice. Dr. Smith & Jones agree.";
        var analyzer = new EnglishTextAnalyzer(new EnglishTextAnalyzerOptions());

        // Act
        var result = analyzer.Analyze(text, TestContext.Current.CancellationToken);

        // Assert: part-of-speech and normalization annotations are both present and in text order
        Assert.Contains(result.Annotations, annotation => annotation.Kind == TextEnAnnotations.PartOfSpeech);
        Assert.Equal(result.Annotations.OrderBy(annotation => annotation.Start).Select(annotation => annotation.Start), result.Annotations.Select(annotation => annotation.Start));

        // the normalizations read back at their source spans
        var read = result.Annotations
            .Select(annotation => TextEnAnnotations.TryReadNormalized(annotation, out var spoken)
                ? (Source: text.Substring(annotation.Start, annotation.Length), Spoken: spoken)
                : default)
            .Where(pair => pair.Source is not null)
            .ToList();
        Assert.Equal([("St", "Street"), ("Dr.", "Doctor"), ("&", "and")], read);

        // the break after the sentence-final "Main St." is added, and none falls inside "Dr. Smith"
        Assert.Equal([text.IndexOf("It is", StringComparison.Ordinal), text.IndexOf("Dr. Smith", StringComparison.Ordinal)], result.SentenceBreaks);
    }

    /// <summary>Proves part-of-speech annotation can be switched off through the options, leaving sentence breaks, and an empty request returns no annotations and an empty, non-null break list.</summary>
    [Fact]
    public void AnalysisSubsystem_Analyze_PartOfSpeechNotSelected_ProducesNoAnnotationsButSentenceBreaks()
    {
        // Arrange
        const string text = "He left home. She stayed.";
        var analyzer = new EnglishTextAnalyzer(new EnglishTextAnalyzerOptions { Annotations = TextEnAnnotationSelection.None });
        var empty = new EnglishTextAnalyzer(new EnglishTextAnalyzerOptions());

        // Act
        var result = analyzer.Analyze(text, TestContext.Current.CancellationToken);
        var emptyResult = empty.Analyze(string.Empty, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(result.Annotations);
        Assert.Equal([text.IndexOf("She", StringComparison.Ordinal)], result.SentenceBreaks);
        Assert.Empty(emptyResult.Annotations);
        Assert.NotNull(emptyResult.SentenceBreaks);
        Assert.Empty(emptyResult.SentenceBreaks);
    }

    /// <summary>Proves several sentences, an abbreviation and a tag placeholder together give authoritative breaks at the later sentence starts only.</summary>
    [Fact]
    public void AnalysisSubsystem_Analyze_MultiSentenceText_ReturnsAuthoritativeBreaks()
    {
        // Arrange
        var text = $"Dr. Smith arrived. {Tag} He left. Nobody knew why.";
        var analyzer = new EnglishTextAnalyzer();

        // Act
        var result = analyzer.Analyze(text, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal([text.IndexOf("He", StringComparison.Ordinal), text.IndexOf("Nobody", StringComparison.Ordinal)], result.SentenceBreaks);
    }

    /// <summary>Proves a placeholder is never covered and the tagging context is carried across it.</summary>
    [Fact]
    public void AnalysisSubsystem_Analyze_PlaceholderBetweenWords_IsNeverCoveredAndContextIsCarried()
    {
        // Arrange
        var withTag = $"I will {Tag} record it.";
        var withSpace = "I will   record it.";
        var analyzer = new EnglishTextAnalyzer();

        // Act
        var tagged = analyzer.Analyze(withTag, TestContext.Current.CancellationToken);
        var spaced = analyzer.Analyze(withSpace, TestContext.Current.CancellationToken);

        // Assert: identical to the space version, and "record" is a verb because "will" before the tag is still context
        Assert.Equal(spaced.Annotations, tagged.Annotations);
        Assert.DoesNotContain(tagged.Annotations, annotation => annotation.Start <= 7 && 7 < annotation.End);
        var record = tagged.Annotations.Single(annotation => annotation.Kind == TextEnAnnotations.PartOfSpeech && withTag.Substring(annotation.Start, annotation.Length) == "record");
        Assert.Equal("VERB", record.Value);
    }

    /// <summary>Proves a request that fails because the tagger cannot be created does not disable the analyzer: a later request succeeds with the result of a healthy analyzer.</summary>
    [Fact]
    public void AnalysisSubsystem_Analyze_TaggerFailsOnce_LaterRequestSucceeds()
    {
        // Arrange: the first creation of the tagger fails
        const string text = "The cat sat on the mat.";
        var analyzer = new EnglishTextAnalyzer(new EnglishTextAnalyzerOptions(), new CountingTaggerSource(failures: 1).CreateAdapter());
        var expected = new EnglishTextAnalyzer().Analyze(text, TestContext.Current.CancellationToken);

        // Act
        Assert.Throws<InvalidOperationException>(() => analyzer.Analyze(text, TestContext.Current.CancellationToken));
        var result = analyzer.Analyze(text, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(expected.Annotations, result.Annotations);
        Assert.Equal(expected.SentenceBreaks, result.SentenceBreaks);
    }

    /// <summary>Proves many analyzers and requests sharing one adapter, racing on their first use, all give the same result as a sequential request.</summary>
    [Fact]
    public async Task AnalysisSubsystem_Analyze_ManyConcurrentRequests_AgreeWithSequentialResult()
    {
        // Arrange: several analyzers over one adapter, as a shared process-wide tagger would serve them
        var adapter = new CountingTaggerSource().CreateAdapter();
        var analyzers = Enumerable.Range(0, 4).Select(_ => new EnglishTextAnalyzer(new EnglishTextAnalyzerOptions(), adapter)).ToArray();
        const string text = "Please read the record. I will record it later. Then we leave.";
        var expected = string.Join(',', new EnglishTextAnalyzer().Analyze(text, TestContext.Current.CancellationToken).Annotations);

        // Act
        var tasks = Enumerable.Range(0, 32)
            .Select(index => Task.Run(
                () => string.Join(',', analyzers[index % analyzers.Length].Analyze(text, TestContext.Current.CancellationToken).Annotations),
                TestContext.Current.CancellationToken))
            .ToArray();
        var results = await Task.WhenAll(tasks);

        // Assert
        Assert.All(results, result => Assert.Equal(expected, result));
    }

    /// <summary>Proves a request reports one counts-only event, in the documented category, that contains no word of the text.</summary>
    [Fact]
    public void AnalysisSubsystem_Analyze_Request_ReportsOneCountsOnlyDiagnostic()
    {
        // Arrange: words chosen so none can occur in the fixed message
        const string text = "Quokka gazelles zigzag. Marmoset wombats yodel!";
        var sink = new RecordingDiagnostics();
        var analyzer = new EnglishTextAnalyzer(new EnglishTextAnalyzerOptions { Diagnostics = sink });

        // Act
        _ = analyzer.Analyze(text, TestContext.Current.CancellationToken);

        // Assert
        var reported = Assert.Single(sink.Events);
        Assert.Equal(SpeechDiagnosticLevel.Info, reported.Level);
        Assert.Equal("TextEn.Analysis", reported.Category);
        Assert.Matches(@"^Analyzed one text: 2 sentences, \d+ tokens, \d+ annotations, \d+ unknown words, \d+ normalizations, \d+ ambiguous readings, \d+ numeric fallbacks, \d+ overrides applied, \d+ overrides ignored\.$", reported.Message);
        foreach (var word in text.Split([' ', '.', '!'], StringSplitOptions.RemoveEmptyEntries))
        {
            Assert.DoesNotContain(word, reported.Message, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>Proves a diagnostics sink that throws never interrupts or changes the analysis.</summary>
    [Fact]
    public void AnalysisSubsystem_Analyze_ThrowingDiagnosticsSink_StillReturnsSameResult()
    {
        // Arrange
        const string text = "The cat sat. The dog ran.";
        var throwing = new EnglishTextAnalyzer(new EnglishTextAnalyzerOptions { Diagnostics = new RecordingDiagnostics(throwOnReport: true) });
        var quiet = new EnglishTextAnalyzer(new EnglishTextAnalyzerOptions());

        // Act
        var result = throwing.Analyze(text, TestContext.Current.CancellationToken);
        var expected = quiet.Analyze(text, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(expected.Annotations, result.Annotations);
        Assert.Equal(expected.SentenceBreaks, result.SentenceBreaks);
    }

    /// <summary>Proves cancellation requested during analysis is honored with an OperationCanceledException and reports nothing.</summary>
    [Fact]
    public void AnalysisSubsystem_Analyze_CancellationRequested_ThrowsOperationCanceledExceptionAndReportsNothing()
    {
        // Arrange: canceled as soon as the tagger is first requested, i.e. during the first sentence
        using var cancellation = new CancellationTokenSource();
        var source = new CountingTaggerSource(cancellation.Cancel);
        var sink = new RecordingDiagnostics();
        var analyzer = new EnglishTextAnalyzer(new EnglishTextAnalyzerOptions { Diagnostics = sink }, source.CreateAdapter());

        // Act / Assert
        Assert.Throws<OperationCanceledException>(() => analyzer.Analyze("The cat sat. The dog ran. A bird flew.", cancellation.Token));
        Assert.Empty(sink.Events);
    }

    /// <summary>Proves every action of a dictionary read from the documented format reaches a consumer through the typed readers, which is the only way a backend is meant to read them, and a consumer that applies the documented algorithm gets the expected spoken text and phonemes.</summary>
    [Fact]
    public void AnalysisSubsystem_Analyze_OverrideActions_ReadBackThroughTypedReaders()
    {
        // Arrange
        const string text = "Nginx runs a GIF in Polish, US and FBI and AT&T's SQL.";
        var overrides = NormalizationHarness.LoadSampleDictionary();
        var analyzer = new EnglishTextAnalyzer(new EnglishTextAnalyzerOptions { Overrides = overrides });

        // Act
        var result = analyzer.Analyze(text, TestContext.Current.CancellationToken);

        // Assert: the consumer algorithm of the documentation, written against the public readers only
        var replacements = new List<(int Start, int End, string Spoken, bool Spelled)>();
        var pronunciations = new List<(string Word, PronunciationScheme Scheme, string Phonemes)>();
        foreach (var annotation in result.Annotations)
        {
            if (TextEnAnnotations.TryReadReplacement(annotation, out var spoken, out var spelled))
            {
                replacements.Add((annotation.Start, annotation.End, spoken, spelled));
            }
            else if (TextEnAnnotations.TryReadPronunciation(annotation, out var scheme, out var phonemes))
            {
                pronunciations.Add((text.Substring(annotation.Start, annotation.Length), scheme, phonemes));
            }
        }

        var builder = new System.Text.StringBuilder();
        var position = 0;
        foreach (var replacement in replacements.OrderBy(item => item.Start))
        {
            builder.Append(text, position, replacement.Start - position).Append(replacement.Spoken);
            position = replacement.End;
        }

        builder.Append(text, position, text.Length - position);
        Assert.Equal("engine ex runs a G I F in Polish, US and FBI and A T and T's sequel.", builder.ToString());
        Assert.Equal([("Polish", PronunciationScheme.Ipa, "\u02C8po\u028Al\u026A\u0283")], pronunciations);
        Assert.Equal(["G I F", "A T", "T"], replacements.Where(item => item.Spelled).Select(item => item.Spoken));
        Assert.Contains(replacements, item => item.Spoken == "sequel" && !item.Spelled);
    }

    /// <summary>Proves the replacement annotations of every kind (normalization and spelling) are ascending and disjoint as one sequence, and pronunciations are disjoint from them, for a text mixing every rule and every override action.</summary>
    [Fact]
    public void AnalysisSubsystem_Analyze_OverrideAndSpelledText_ReplacementsAndPronunciationsAreDisjoint()
    {
        // Arrange
        const string text = "Dr. Smith & Nginx paid $5 for the GIF at 5 pm; Polish FBI US NASA's AT&T. J. Smith Jr. Ph.D. left, kg 5 kg.";
        var overrides = NormalizationHarness.LoadSampleDictionary();
        var analyzer = new EnglishTextAnalyzer(new EnglishTextAnalyzerOptions { Overrides = overrides });

        // Act
        var result = analyzer.Analyze(text, TestContext.Current.CancellationToken);

        // Assert
        var covering = result.Annotations.Where(annotation => annotation.Kind != TextEnAnnotations.PartOfSpeech).OrderBy(annotation => annotation.Start).ToList();
        for (var i = 1; i < covering.Count; i++)
        {
            Assert.True(covering[i - 1].End <= covering[i].Start, $"'{covering[i - 1].Kind}' overlaps '{covering[i].Kind}' at {covering[i].Start}");
        }

        Assert.Contains(covering, annotation => annotation.Kind == TextEnAnnotations.Pronunciation);
        Assert.Contains(covering, annotation => annotation.Kind == TextEnAnnotations.SpellOut);
        Assert.Contains(covering, annotation => annotation.Kind == TextEnAnnotations.Normalized);
    }

    /// <summary>Proves the reading annotations of an analysis with a model read back through the typed readers on the words they annotate, coexist with the part of speech on the same span and are the only annotations that do.</summary>
    [Fact]
    public void AnalysisSubsystem_Analyze_ReadingFamily_ReadsBackThroughTypedReadersAndCoexistsWithPartOfSpeech()
    {
        // Arrange
        const string text = "I want to record a record.";
        var provider = HeteronymFixtureModel.CreateProvider(new CountingModelSource(HeteronymFixtureModel.Build(HeteronymFixtureModel.RecordExamples)));
        var analyzer = new EnglishTextAnalyzer(new EnglishTextAnalyzerOptions(), PartOfSpeechAdapter.Shared, null, provider);

        // Act
        var result = analyzer.Analyze(text, TestContext.Current.CancellationToken);

        // Assert: reading id and confidence read back at "record", with the part of speech on the same span
        var readings = result.Annotations
            .Select(annotation => TextEnAnnotations.TryReadReading(annotation, out var id) ? (annotation.Start, annotation.Length, Id: id) : default)
            .Where(item => item.Id is not null)
            .ToList();
        Assert.Equal(["record.verb", "record.noun"], readings.Select(item => item.Id));
        foreach (var (start, length, _) in readings)
        {
            Assert.Equal("record", text.Substring(start, length));
            Assert.Contains(result.Annotations, annotation => TextEnAnnotations.TryReadPartOfSpeech(annotation, out _) && annotation.Start == start && annotation.Length == length);
            Assert.Contains(result.Annotations, annotation => TextEnAnnotations.TryReadReadingConfidence(annotation, out _) && annotation.Start == start && annotation.Length == length);
        }
    }

    /// <summary>Proves a reading override read from the tab-separated format and the options work together: the id is validated when the file is read, applied by the analyzer, and counted as an applied override.</summary>
    [Fact]
    public void AnalysisSubsystem_Analyze_ReadingOverrideFromTsv_AppliesAndCounts()
    {
        // Arrange
        const string text = "They read the record.";
        var overrides = PronunciationOverrides.FromTsv(new StringReader("read\tinsensitive\treading\tread.past\nrecord\tinsensitive\treading\trecord.noun\n"));
        var sink = new RecordingDiagnostics();
        var analyzer = new EnglishTextAnalyzer(new EnglishTextAnalyzerOptions { Overrides = overrides, Diagnostics = sink });

        // Act
        var result = analyzer.Analyze(text, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(["read.past", "record.noun"], result.Annotations.Where(annotation => annotation.Kind == TextEnAnnotations.Reading).Select(annotation => annotation.Value));
        Assert.Contains(sink.Events, e => e.Message.Contains("2 overrides applied, 0 overrides ignored", StringComparison.Ordinal));
        Assert.Throws<InvalidDataException>(() => PronunciationOverrides.FromTsv(new StringReader("read\tinsensitive\treading\trecord.noun\n")));
    }
}
