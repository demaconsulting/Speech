using DemaConsulting.Speech.SynthesisSubsystem;
using DemaConsulting.Speech.Text.En.AnalysisSubsystem;
using DemaConsulting.Speech.Text.En.Tests.Fakes;

namespace DemaConsulting.Speech.Text.En.Tests.AnalysisSubsystem;

/// <summary>
///     Unit tests for <see cref="EnglishTextAnalyzer"/> and the <see cref="EnglishTextAnalysis"/>
///     result it produces. Its documented collaborators (the part-of-speech adapter, the
///     diagnostics reporter, the options and the annotation vocabulary) are real; tagger creation
///     is observed through a counting adapter.
/// </summary>
public sealed class EnglishTextAnalyzerTests
{
    /// <summary>The tag placeholder as a string, for building test texts.</summary>
    private const string Tag = "\uFFFC";

    /// <summary>Proves each word of a sentence gets one annotation of the right kind, positioned on the word in the source.</summary>
    [Fact]
    public void EnglishTextAnalyzer_Analyze_SimpleSentence_AnnotatesEachWordOnItsSourceSpan()
    {
        // Arrange
        const string text = "The cat sat on the mat.";
        var analyzer = new EnglishTextAnalyzer();

        // Act
        var result = analyzer.Analyze(text, TestContext.Current.CancellationToken);

        // Assert: six words annotated in order, each annotation exactly covering its source word
        Assert.Equal(6, result.Annotations.Count);
        Assert.All(result.Annotations, annotation => Assert.Equal(TextEnAnnotations.PartOfSpeech, annotation.Kind));
        Assert.Equal(
            ["The", "cat", "sat", "on", "the", "mat"],
            result.Annotations.Select(annotation => text.Substring(annotation.Start, annotation.Length)));
    }

    /// <summary>Proves the tags of a plainly unambiguous sentence match the expected Universal POS tags.</summary>
    [Fact]
    public void EnglishTextAnalyzer_Analyze_UnambiguousSentence_ProducesExpectedUniversalTags()
    {
        // Arrange
        var analyzer = new EnglishTextAnalyzer();

        // Act
        var result = analyzer.Analyze("The cat sat on the mat.", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(["DET", "NOUN", "VERB", "ADP", "DET", "NOUN"], result.Annotations.Select(annotation => annotation.Value));
    }

    /// <summary>Proves the same word takes a different tag in a different syntactic position (sentence context is used).</summary>
    [Fact]
    public void EnglishTextAnalyzer_Analyze_SameSpellingInDifferentContext_UsesContextForTag()
    {
        // Arrange
        const string text = "I will record it. This record is old.";
        var analyzer = new EnglishTextAnalyzer();

        // Act
        var result = analyzer.Analyze(text, TestContext.Current.CancellationToken);

        // Assert: "record" is a verb after "will" and a noun after "This"
        var records = result.Annotations
            .Where(annotation => text.Substring(annotation.Start, annotation.Length) == "record")
            .Select(annotation => annotation.Value)
            .ToList();
        Assert.Equal(["VERB", "NOUN"], records);
    }

    /// <summary>Proves punctuation and symbols carry no annotation.</summary>
    [Fact]
    public void EnglishTextAnalyzer_Analyze_PunctuationAndSymbols_CarryNoAnnotation()
    {
        // Arrange
        const string text = "Wow, it costs $5 (really)!";
        var analyzer = new EnglishTextAnalyzer();

        // Act
        var result = analyzer.Analyze(text, TestContext.Current.CancellationToken);

        // Assert: no annotation covers a comma, dollar sign, bracket or exclamation mark
        Assert.NotEmpty(result.Annotations);
        Assert.All(
            result.Annotations,
            annotation => Assert.DoesNotContain(
                text.Substring(annotation.Start, annotation.Length),
                new[] { ",", "$", "(", ")", "!" }));
    }

    /// <summary>Proves a text with several sentences gets authoritative breaks at the start of each later sentence.</summary>
    [Fact]
    public void EnglishTextAnalyzer_Analyze_MultipleSentences_ReturnsBreaksAtLaterSentenceStarts()
    {
        // Arrange
        const string text = "He left home. She stayed. Nobody knew why.";
        var analyzer = new EnglishTextAnalyzer();

        // Act
        var result = analyzer.Analyze(text, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result.SentenceBreaks);
        Assert.Equal([text.IndexOf("She", StringComparison.Ordinal), text.IndexOf("Nobody", StringComparison.Ordinal)], result.SentenceBreaks);
    }

    /// <summary>Proves an abbreviation period does not produce a sentence break.</summary>
    [Fact]
    public void EnglishTextAnalyzer_Analyze_AbbreviationPeriod_DoesNotBreakSentence()
    {
        // Arrange
        const string text = "Dr. Smith arrived. He left.";
        var analyzer = new EnglishTextAnalyzer();

        // Act
        var result = analyzer.Analyze(text, TestContext.Current.CancellationToken);

        // Assert: one break only, before "He"
        Assert.Equal([text.IndexOf("He", StringComparison.Ordinal)], result.SentenceBreaks);
    }

    /// <summary>Proves a single sentence still yields a non-null, empty break list, so the analyzer's opinion is authoritative.</summary>
    [Fact]
    public void EnglishTextAnalyzer_Analyze_SingleSentence_ReturnsEmptyNonNullBreaks()
    {
        // Arrange
        var analyzer = new EnglishTextAnalyzer();

        // Act
        var result = analyzer.Analyze("Just one sentence here.", TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result.SentenceBreaks);
        Assert.Empty(result.SentenceBreaks);
    }

    /// <summary>Proves empty, whitespace-only and punctuation-only texts yield no annotations and no tagger load.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("   \t\r\n ")]
    [InlineData("\uFFFC")]
    [InlineData("\uFFFC \uFFFC")]
    public void EnglishTextAnalyzer_Analyze_TextWithoutWords_ReturnsNoAnnotationsAndLoadsNoTagger(string text)
    {
        // Arrange
        var source = new CountingTaggerSource();
        var analyzer = new EnglishTextAnalyzer(new EnglishTextAnalyzerOptions(), source.CreateAdapter());

        // Act
        var result = analyzer.Analyze(text, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(result.Annotations);
        Assert.NotNull(result.SentenceBreaks);
        Assert.Empty(result.SentenceBreaks);
        Assert.Equal(0, source.Count);
    }

    /// <summary>Proves a null text is rejected.</summary>
    [Fact]
    public void EnglishTextAnalyzer_Analyze_NullText_ThrowsArgumentNullException()
    {
        // Arrange
        var analyzer = new EnglishTextAnalyzer();

        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => analyzer.Analyze(null!, TestContext.Current.CancellationToken));
        Assert.Throws<ArgumentNullException>(() => analyzer.AnalyzeDetailed(null!, TestContext.Current.CancellationToken));
    }

    /// <summary>Proves null options are rejected at construction.</summary>
    [Fact]
    public void EnglishTextAnalyzer_Constructor_NullOptions_ThrowsArgumentNullException()
    {
        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => new EnglishTextAnalyzer(null!));
    }

    /// <summary>Proves a tag placeholder between words is never covered by an annotation and the words around it are still annotated.</summary>
    [Fact]
    public void EnglishTextAnalyzer_Analyze_PlaceholderBetweenWords_IsNeverCovered()
    {
        // Arrange: the placeholder sits at index 5
        var text = $"I've {Tag} read it.";
        var analyzer = new EnglishTextAnalyzer();

        // Act
        var result = analyzer.Analyze(text, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            ["I", "'ve", "read", "it"],
            result.Annotations.Select(annotation => text.Substring(annotation.Start, annotation.Length)));
        Assert.DoesNotContain(result.Annotations, annotation => annotation.Start <= 5 && 5 < annotation.End);
    }

    /// <summary>Proves analyzing a text with placeholders gives exactly the result of analyzing it with the placeholders replaced by spaces (offsets preserved, context carried across the tag).</summary>
    [Fact]
    public void EnglishTextAnalyzer_Analyze_PlaceholderBetweenWords_EqualsAnalysisWithSpaceInItsPlace()
    {
        // Arrange
        var withTag = $"I've {Tag} read it. Say {Tag}{Tag} read again.";
        var withSpaces = withTag.Replace(Tag, " ");
        var analyzer = new EnglishTextAnalyzer();

        // Act
        var tagged = analyzer.Analyze(withTag, TestContext.Current.CancellationToken);
        var spaced = analyzer.Analyze(withSpaces, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(spaced.Annotations, tagged.Annotations);
        Assert.Equal(spaced.SentenceBreaks, tagged.SentenceBreaks);
    }

    /// <summary>Proves words touching a placeholder are separate tokens, so a placeholder never joins two words into one annotation.</summary>
    [Fact]
    public void EnglishTextAnalyzer_Analyze_PlaceholderTouchingWords_SeparatesTheWords()
    {
        // Arrange
        var text = $"foo{Tag}bar";
        var analyzer = new EnglishTextAnalyzer();

        // Act
        var result = analyzer.Analyze(text, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(["foo", "bar"], result.Annotations.Select(annotation => text.Substring(annotation.Start, annotation.Length)));
    }

    /// <summary>Proves a sentence break after a sentence end falls on the first word of the next sentence even when a placeholder follows the period.</summary>
    [Fact]
    public void EnglishTextAnalyzer_Analyze_PlaceholderAtSentenceBoundary_BreaksBeforeNextWord()
    {
        // Arrange
        var text = $"Hello there.{Tag} Welcome back.";
        var analyzer = new EnglishTextAnalyzer();

        // Act
        var result = analyzer.Analyze(text, TestContext.Current.CancellationToken);

        // Assert: the break is at "Welcome", never at the placeholder or the space
        Assert.Equal([text.IndexOf("Welcome", StringComparison.Ordinal)], result.SentenceBreaks);
    }

    /// <summary>Proves random text with surrogate pairs, emoji, lone surrogates, combining marks, whitespace and placeholders always yields well-formed annotations and breaks.</summary>
    [Fact]
    public void EnglishTextAnalyzer_Analyze_RandomText_AnnotationsAndBreaksAreWellFormed()
    {
        // Arrange: a deterministic generator over a deliberately hostile alphabet
        string[] pieces =
        [
            "the", "cat", "Zeta", "record", "I've", "don't", "a", "42", "3.14", "$5", "100%",
            ".", ",", "!", "?", ";", ":", "'", "\"", "(", ")", "-", "...", " ", "  ", "\t", "\n", "\r\n", "\u00A0", "\u2028",
            Tag, Tag + Tag, "\U0001F600", "\U0001D5D4", "e\u0301", "\uD83D", "\uDE00", "\0", "\u4F60\u597D", "caf\u00E9", "www.example.com",
        ];
        var analyzer = new EnglishTextAnalyzer();
        var random = new Random(20240607);

        for (var iteration = 0; iteration < 300; iteration++)
        {
            var text = string.Concat(Enumerable.Range(0, random.Next(0, 40)).Select(_ => pieces[random.Next(pieces.Length)]));

            // Act
            var result = analyzer.Analyze(text, TestContext.Current.CancellationToken);

            // Assert: every annotation is in bounds, starts and ends on non-whitespace, never touches a placeholder
            // or half of a surrogate pair, and carries a readable part of speech
            foreach (var annotation in result.Annotations)
            {
                Assert.True(annotation.Start >= 0 && annotation.End <= text.Length, $"out of bounds in iteration {iteration}");
                Assert.False(char.IsWhiteSpace(text[annotation.Start]), $"starts on whitespace in iteration {iteration}");
                Assert.False(char.IsWhiteSpace(text[annotation.End - 1]), $"ends on whitespace in iteration {iteration}");
                Assert.DoesNotContain(Tag, text.Substring(annotation.Start, annotation.Length), StringComparison.Ordinal);
                Assert.False(annotation.Start > 0 && char.IsLowSurrogate(text[annotation.Start]) && char.IsHighSurrogate(text[annotation.Start - 1]), $"splits a pair at the start in iteration {iteration}");
                Assert.False(annotation.End < text.Length && char.IsHighSurrogate(text[annotation.End - 1]) && char.IsLowSurrogate(text[annotation.End]), $"splits a pair at the end in iteration {iteration}");
                Assert.True(TextEnAnnotations.TryReadPartOfSpeech(annotation, out var tag) && tag is not ("PUNCT" or "SYM"));
            }

            // Assert: breaks are non-null, strictly ascending, inside the text and sit on a word start
            Assert.NotNull(result.SentenceBreaks);
            Assert.Equal(result.SentenceBreaks.OrderBy(b => b).Distinct(), result.SentenceBreaks);
            foreach (var sentenceBreak in result.SentenceBreaks)
            {
                Assert.InRange(sentenceBreak, 1, text.Length - 1);
                Assert.False(char.IsWhiteSpace(text[sentenceBreak]) || text[sentenceBreak] == Tag[0], $"break on whitespace or placeholder in iteration {iteration}");
            }
        }
    }

    /// <summary>Proves an already-canceled token throws before any work, without loading the tagger.</summary>
    [Fact]
    public void EnglishTextAnalyzer_Analyze_AlreadyCanceledToken_ThrowsWithoutLoadingTagger()
    {
        // Arrange
        var source = new CountingTaggerSource();
        var analyzer = new EnglishTextAnalyzer(new EnglishTextAnalyzerOptions(), source.CreateAdapter());
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        // Act / Assert
        Assert.Throws<OperationCanceledException>(() => analyzer.Analyze("The cat sat.", cancellation.Token));
        Assert.Equal(0, source.Count);
    }

    /// <summary>Proves cancellation requested while the tagger is first being created stops a multi-sentence analysis.</summary>
    [Fact]
    public void EnglishTextAnalyzer_Analyze_CanceledDuringTaggerInitialization_ThrowsOperationCanceledException()
    {
        // Arrange: the token is canceled the moment the tagger is first requested
        using var cancellation = new CancellationTokenSource();
        var source = new CountingTaggerSource(cancellation.Cancel);
        var sink = new RecordingDiagnostics();
        var analyzer = new EnglishTextAnalyzer(new EnglishTextAnalyzerOptions { Diagnostics = sink }, source.CreateAdapter());

        // Act / Assert: the check right after initialization observes the cancellation, before any sentence is tagged
        Assert.Throws<OperationCanceledException>(() => analyzer.Analyze("The cat sat. The dog ran. A bird flew.", cancellation.Token));
        Assert.Equal(1, source.Count);
        Assert.Empty(sink.Events);
    }

    /// <summary>Proves cancellation requested during the only sentence of a text is honored instead of returning a successful result.</summary>
    [Fact]
    public void EnglishTextAnalyzer_Analyze_CanceledDuringOnlySentence_ThrowsAndReportsNothing()
    {
        // Arrange: the token is canceled right after the only sentence was tagged
        using var cancellation = new CancellationTokenSource();
        var source = new CountingTaggerSource();
        var sink = new RecordingDiagnostics();
        var analyzer = new EnglishTextAnalyzer(
            new EnglishTextAnalyzerOptions { Diagnostics = sink },
            source.CreateAdapter(_ => cancellation.Cancel()));

        // Act / Assert
        Assert.Throws<OperationCanceledException>(() => analyzer.Analyze("The cat sat.", cancellation.Token));
        Assert.Empty(sink.Events);
    }

    /// <summary>Proves cancellation requested during the final sentence of several is honored instead of returning a successful result.</summary>
    [Fact]
    public void EnglishTextAnalyzer_Analyze_CanceledDuringFinalSentence_ThrowsAndReportsNothing()
    {
        // Arrange: the token is canceled right after the third and last sentence was tagged
        using var cancellation = new CancellationTokenSource();
        var source = new CountingTaggerSource();
        var sink = new RecordingDiagnostics();
        var analyzer = new EnglishTextAnalyzer(
            new EnglishTextAnalyzerOptions { Diagnostics = sink },
            source.CreateAdapter(index =>
            {
                if (index == 2)
                {
                    cancellation.Cancel();
                }
            }));

        // Act / Assert
        Assert.Throws<OperationCanceledException>(() => analyzer.Analyze("The cat sat. The dog ran. A bird flew.", cancellation.Token));
        Assert.Empty(sink.Events);
    }

    /// <summary>Proves a request that is not canceled still reports its one diagnostics event (the cancellation checks do not suppress success).</summary>
    [Fact]
    public void EnglishTextAnalyzer_Analyze_NotCanceled_ReportsOneDiagnosticsEvent()
    {
        // Arrange
        using var cancellation = new CancellationTokenSource();
        var sink = new RecordingDiagnostics();
        var analyzer = new EnglishTextAnalyzer(new EnglishTextAnalyzerOptions { Diagnostics = sink });

        // Act
        _ = analyzer.Analyze("The cat sat. The dog ran.", cancellation.Token);

        // Assert
        Assert.Single(sink.Events);
    }

    /// <summary>Proves the detailed analysis lists every token with its tag, a confidence in range and the known-word flag.</summary>
    [Fact]
    public void EnglishTextAnalyzer_AnalyzeDetailed_Sentence_ListsTokensWithTagConfidenceAndKnownWordFlag()
    {
        // Arrange
        const string text = "The qzxvbnk sat.";
        var analyzer = new EnglishTextAnalyzer();

        // Act
        var analysis = analyzer.AnalyzeDetailed(text, TestContext.Current.CancellationToken);

        // Assert: four tokens including the period, each with a tag; the invented spelling is not a known word
        Assert.Equal(["The", "qzxvbnk", "sat", "."], analysis.Tokens.Select(token => token.Text));
        Assert.All(analysis.Tokens, token => Assert.NotNull(token.PartOfSpeech));
        Assert.All(analysis.Tokens, token => Assert.InRange(token.Confidence, 0.0, 1.0));
        Assert.All(analysis.Tokens, token => Assert.Equal(token.Text, text.Substring(token.Start, token.Length)));
        Assert.True(analysis.Tokens[0].IsKnownWord);
        Assert.False(analysis.Tokens[1].IsKnownWord);
        Assert.Equal("PUNCT", analysis.Tokens[3].PartOfSpeech);
    }

    /// <summary>Proves the language-neutral result is exactly the annotations and breaks of the detailed analysis.</summary>
    [Fact]
    public void EnglishTextAnalyzer_Analyze_ComparedWithAnalyzeDetailed_ReturnsSameAnnotationsAndBreaks()
    {
        // Arrange
        const string text = "Dr. Smith will record it. This record is old.";
        var analyzer = new EnglishTextAnalyzer();

        // Act
        var result = analyzer.Analyze(text, TestContext.Current.CancellationToken);
        var detailed = analyzer.AnalyzeDetailed(text, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(detailed.Annotations, result.Annotations);
        Assert.Equal(detailed.SentenceBreaks, result.SentenceBreaks);
    }

    /// <summary>Proves both entry points share one analysis: identical annotations and breaks over placeholders, surrogates, CRLF and several sentences.</summary>
    /// <param name="text">The text to analyze through both entry points.</param>
    [Theory]
    [InlineData("")]
    [InlineData("Hello")]
    [InlineData("He left home. She stayed. Nobody knew why.")]
    [InlineData("I've \uFFFC read it. \uFFFC This record is old.\uFFFCPlease record it!")]
    [InlineData("\uFFFC\uFFFC\uFFFC")]
    [InlineData("Smile \uD83D\uDE00 and wave \uD83D\uDE00. Then go \uD83D\uDE00\uD83D\uDE00!")]
    [InlineData("Wave\uD83D\uDE00\uFFFC\uD83D\uDE00word. Next sentence \uD83D\uDE00\uFFFC ends.")]
    [InlineData("First line.\r\nSecond line, with a comma.\r\n\r\nThird: $5 (really)?\r\n")]
    [InlineData("Dr. Smith said: \"Go.\" Then qzxvbnk left at 5 p.m. on 12 May 2024.")]
    public void EnglishTextAnalyzer_Analyze_VariedTexts_MatchesAnalyzeDetailedAnnotationsAndBreaks(string text)
    {
        // Arrange
        var analyzer = new EnglishTextAnalyzer();

        // Act
        var result = analyzer.Analyze(text, TestContext.Current.CancellationToken);
        var detailed = analyzer.AnalyzeDetailed(text, TestContext.Current.CancellationToken);

        // Assert: same annotations and breaks, and the detailed path lists at least a token per annotation
        Assert.Equal(detailed.Annotations, result.Annotations);
        Assert.Equal(detailed.SentenceBreaks, result.SentenceBreaks);
        Assert.True(detailed.Tokens.Count >= detailed.Annotations.Count);
    }

    /// <summary>Proves selecting no annotations yields no annotations and no tagger load, but still supplies sentence breaks and untagged tokens.</summary>
    [Fact]
    public void EnglishTextAnalyzer_Analyze_PartOfSpeechNotSelected_ProducesNoAnnotationsAndLoadsNoTagger()
    {
        // Arrange
        const string text = "He left home. She stayed.";
        var source = new CountingTaggerSource();
        var options = new EnglishTextAnalyzerOptions { Annotations = TextEnAnnotationSelection.None };
        var analyzer = new EnglishTextAnalyzer(options, source.CreateAdapter());

        // Act
        var analysis = analyzer.AnalyzeDetailed(text, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(analysis.Annotations);
        Assert.Equal([text.IndexOf("She", StringComparison.Ordinal)], analysis.SentenceBreaks);
        Assert.Equal(7, analysis.Tokens.Count);
        Assert.All(analysis.Tokens, token => Assert.Null(token.PartOfSpeech));
        Assert.Equal(0, source.Count);
    }

    /// <summary>Proves many concurrent requests on one analyzer all succeed with identical results and load the tagger once.</summary>
    [Fact]
    public async Task EnglishTextAnalyzer_Analyze_ConcurrentRequestsOnOneAnalyzer_ReturnIdenticalResultsAndLoadTaggerOnce()
    {
        // Arrange
        const string text = "I've {0} read it. This record is old. Please record the answer.";
        var input = string.Format(System.Globalization.CultureInfo.InvariantCulture, text, Tag);
        var source = new CountingTaggerSource();
        var analyzer = new EnglishTextAnalyzer(new EnglishTextAnalyzerOptions(), source.CreateAdapter());

        // Act: the very first analyses race each other, so the lazy tagger creation is contended
        var tasks = Enumerable.Range(0, 24)
            .Select(_ => Task.Run(
                () => Enumerable.Range(0, 20)
                    .Select(_ => Key(analyzer.Analyze(input, TestContext.Current.CancellationToken)))
                    .ToList(),
                TestContext.Current.CancellationToken))
            .ToArray();
        var results = await Task.WhenAll(tasks);

        // Assert
        var expectedKey = Key(analyzer.Analyze(input, TestContext.Current.CancellationToken));
        Assert.All(results.SelectMany(keys => keys), key => Assert.Equal(expectedKey, key));
        Assert.Equal(1, source.Count);
    }

    /// <summary>
    ///     Proves the analyzer keeps no reference to the analyzed text or to its results after the call returns
    ///     (this library's own state only; the tagger package's separate, documented text-retention limitation is not covered here).
    /// </summary>
    [Fact]
    public void EnglishTextAnalyzer_Analyze_AfterReturn_KeepsNoReferenceToTextOrResults()
    {
        // Arrange
        var analyzer = new EnglishTextAnalyzer();

        // Act: analysis happens inside a helper the JIT must not inline, so no local of this method
        // (in Debug or Release) can keep the string or the results alive; only weak references return.
        // Collection is forced a bounded number of times because finalization and the collector's
        // timing differ between runtimes and operating systems; the forced collection is the point of
        // this test, so the analyzer rule against it (S1215) is waived for this block only.
        var references = AnalyzeAndForget(analyzer);
#pragma warning disable S1215
        for (var attempt = 0; attempt < 10 && references.Any(reference => reference.IsAlive); attempt++)
        {
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true);
            GC.WaitForPendingFinalizers();
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true);
        }
#pragma warning restore S1215

        // Assert: neither the input string nor either result object is still reachable
        Assert.All(references, reference => Assert.False(reference.IsAlive));
    }

    /// <summary>Analyzes a unique, non-interned string with both entry points and returns only weak references to the string and the results.</summary>
    /// <param name="analyzer">The analyzer.</param>
    /// <returns>Weak references to the analyzed string, the neutral result and the detailed analysis.</returns>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static WeakReference[] AnalyzeAndForget(EnglishTextAnalyzer analyzer)
    {
        // Built at run time from a fresh GUID, so it is neither a literal nor interned.
        var text = string.Concat("Quokka ", Guid.NewGuid().ToString("N"), " rested. ", Guid.NewGuid().ToString("N"), " ran.");
        var neutral = analyzer.Analyze(text, CancellationToken.None);
        var detailed = analyzer.AnalyzeDetailed(text, CancellationToken.None);
        return [new WeakReference(text), new WeakReference(neutral), new WeakReference(detailed)];
    }

    /// <summary>Builds a comparable key of a result's annotations and breaks.</summary>
    /// <param name="result">The result.</param>
    /// <returns>A string equal for equal results.</returns>
    private static string Key(TextAnalysisResult result) =>
        string.Join(',', result.Annotations.Select(a => $"{a.Start}:{a.Length}:{a.Kind}:{a.Value}"))
        + "|" + string.Join(',', result.SentenceBreaks!);
}
