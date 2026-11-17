using DemaConsulting.Speech.SynthesisSubsystem;
using DemaConsulting.Speech.Text.En.AnalysisSubsystem;
using DemaConsulting.Speech.Text.En.NormalizationSubsystem;
using DemaConsulting.Speech.Text.En.Tests.Fakes;

namespace DemaConsulting.Speech.Text.En.Tests.AnalysisSubsystem;

/// <summary>
///     Unit tests for <see cref="EnglishTextAnalyzer"/> and the <see cref="EnglishTextAnalysis"/>
///     result it produces. Its documented collaborators (the part-of-speech adapter, the
///     diagnostics reporter, the options and the annotation vocabulary) are real; cancellation at
///     the tagger is injected through a test adapter.
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

    /// <summary>Proves a token without a letter or digit carries no part of speech even when the tagger gives it one.</summary>
    [Theory]
    [InlineData("\u200B", TextEnAnnotationSelection.PartOfSpeech)]
    [InlineData("\u200B", TextEnAnnotationSelection.PartOfSpeech | TextEnAnnotationSelection.Normalization)]
    [InlineData("*", TextEnAnnotationSelection.PartOfSpeech)]
    [InlineData("~", TextEnAnnotationSelection.PartOfSpeech | TextEnAnnotationSelection.Normalization)]
    [InlineData("\u2192", TextEnAnnotationSelection.PartOfSpeech)]
    [InlineData("\u2020", TextEnAnnotationSelection.PartOfSpeech | TextEnAnnotationSelection.Normalization)]
    public void EnglishTextAnalyzer_Analyze_TokenWithoutLetterOrDigit_CarriesNoPartOfSpeech(string symbol, TextEnAnnotationSelection selection)
    {
        // Arrange: the tagger tags such a token between two commas as an interjection or a noun
        var text = $"Hello, {symbol}, there";
        var analyzer = new EnglishTextAnalyzer(new EnglishTextAnalyzerOptions { Annotations = selection });

        // Act
        var detailed = analyzer.AnalyzeDetailed(text, TestContext.Current.CancellationToken);
        var result = analyzer.Analyze(text, TestContext.Current.CancellationToken);

        // Assert: the words keep their tags, the symbol has none, and both entry points agree
        var tagged = result.Annotations.Where(annotation => annotation.Kind == TextEnAnnotations.PartOfSpeech)
            .Select(annotation => text.Substring(annotation.Start, annotation.Length));
        Assert.Equal(["Hello", "there"], tagged);
        Assert.Equal(detailed.Annotations, result.Annotations);
    }

    /// <summary>Proves a numeral keeps its part of speech: it is content, unlike a bare symbol.</summary>
    [Fact]
    public void EnglishTextAnalyzer_Analyze_NumeralToken_KeepsItsPartOfSpeech()
    {
        // Arrange
        const string text = "Hello, 5, there";
        var analyzer = new EnglishTextAnalyzer(new EnglishTextAnalyzerOptions { Annotations = TextEnAnnotationSelection.PartOfSpeech });

        // Act
        var result = analyzer.Analyze(text, TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains(result.Annotations, annotation => annotation.Kind == TextEnAnnotations.PartOfSpeech && text.Substring(annotation.Start, annotation.Length) == "5");
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

    /// <summary>Proves empty, whitespace-only and punctuation-only texts yield no annotations and no sentence breaks.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("   \t\r\n ")]
    [InlineData("\uFFFC")]
    [InlineData("\uFFFC \uFFFC")]
    public void EnglishTextAnalyzer_Analyze_TextWithoutWords_ReturnsNoAnnotationsAndNoBreaks(string text)
    {
        // Arrange
        var analyzer = new EnglishTextAnalyzer();

        // Act
        var result = analyzer.Analyze(text, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(result.Annotations);
        Assert.NotNull(result.SentenceBreaks);
        Assert.Empty(result.SentenceBreaks);
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

                // part-of-speech annotations carry a readable tag; normalization annotations are readable too
                if (annotation.Kind == TextEnAnnotations.PartOfSpeech)
                {
                    Assert.True(TextEnAnnotations.TryReadPartOfSpeech(annotation, out var tag) && tag is not ("PUNCT" or "SYM"));
                }
                else
                {
                    Assert.True(TextEnAnnotations.TryReadReplacement(annotation, out _, out _), $"unknown kind in iteration {iteration}");
                }
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

    /// <summary>Proves an already-canceled token throws OperationCanceledException instead of returning a result.</summary>
    [Fact]
    public void EnglishTextAnalyzer_Analyze_AlreadyCanceledToken_ThrowsOperationCanceledException()
    {
        // Arrange
        var analyzer = new EnglishTextAnalyzer();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        // Act / Assert
        Assert.Throws<OperationCanceledException>(() => analyzer.Analyze("The cat sat.", cancellation.Token));
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

        // Act / Assert: the check right after initialization observes the cancellation, so no result is returned
        Assert.Throws<OperationCanceledException>(() => analyzer.Analyze("The cat sat. The dog ran. A bird flew.", cancellation.Token));
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

        // Assert: same annotations and breaks, one part-of-speech annotation at most per token, and the
        // detailed normalizations are exactly the replacement (en.norm and en.spell) annotations
        Assert.Equal(detailed.Annotations, result.Annotations);
        Assert.Equal(detailed.SentenceBreaks, result.SentenceBreaks);
        Assert.True(detailed.Tokens.Count >= detailed.Annotations.Count(annotation => annotation.Kind == TextEnAnnotations.PartOfSpeech));
        Assert.Equal(
            detailed.Annotations.Where(annotation => annotation.Kind is TextEnAnnotations.Normalized or TextEnAnnotations.SpellOut).Select(annotation => (annotation.Start, annotation.Length, annotation.Value)),
            detailed.Normalizations.Select(normalization => (normalization.Start, normalization.Length, normalization.SpokenText)));
    }

    /// <summary>Proves selecting no annotations yields no annotations, but still supplies sentence breaks and untagged tokens.</summary>
    [Fact]
    public void EnglishTextAnalyzer_Analyze_PartOfSpeechNotSelected_ProducesNoAnnotationsAndUntaggedTokens()
    {
        // Arrange
        const string text = "He left home. She stayed.";
        var options = new EnglishTextAnalyzerOptions { Annotations = TextEnAnnotationSelection.None };
        var analyzer = new EnglishTextAnalyzer(options);

        // Act
        var analysis = analyzer.AnalyzeDetailed(text, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(analysis.Annotations);
        Assert.Equal([text.IndexOf("She", StringComparison.Ordinal)], analysis.SentenceBreaks);
        Assert.Equal(7, analysis.Tokens.Count);
        Assert.All(analysis.Tokens, token => Assert.Null(token.PartOfSpeech));
    }

    /// <summary>Proves many concurrent requests on one analyzer, racing on its first use, all succeed with the same result as a sequential request.</summary>
    [Fact]
    public async Task EnglishTextAnalyzer_Analyze_ConcurrentRequestsOnOneAnalyzer_ReturnIdenticalResults()
    {
        // Arrange
        const string text = "I've {0} read it. This record is old. Please record the answer.";
        var input = string.Format(System.Globalization.CultureInfo.InvariantCulture, text, Tag);
        var analyzer = new EnglishTextAnalyzer(new EnglishTextAnalyzerOptions(), new CountingTaggerSource().CreateAdapter());
        var expectedKey = Key(new EnglishTextAnalyzer().Analyze(input, TestContext.Current.CancellationToken));

        // Act: the very first analyses race each other
        var tasks = Enumerable.Range(0, 24)
            .Select(_ => Task.Run(
                () => Enumerable.Range(0, 20)
                    .Select(_ => Key(analyzer.Analyze(input, TestContext.Current.CancellationToken)))
                    .ToList(),
                TestContext.Current.CancellationToken))
            .ToArray();
        var results = await Task.WhenAll(tasks);

        // Assert
        Assert.All(results.SelectMany(keys => keys), key => Assert.Equal(expectedKey, key));
    }

    /// <summary>
    ///     Proves a request is not influenced by earlier requests on the same analyzer and never discloses their text:
    ///     the result equals that of a fresh analyzer, and neither the result values nor the diagnostics carry words of
    ///     the other request.
    /// </summary>
    [Fact]
    public void EnglishTextAnalyzer_Analyze_AfterAnotherRequest_ResultEqualsFreshAnalyzerAndDisclosesNothingOfIt()
    {
        // Arrange: two requests with no word in common
        const string first = "Quokka marmoset rested. Zebra galloped.";
        const string second = "I read it again.";
        var sink = new RecordingDiagnostics();
        var used = new EnglishTextAnalyzer(new EnglishTextAnalyzerOptions { Diagnostics = sink });
        var fresh = new EnglishTextAnalyzer();

        // Act
        _ = used.Analyze(first, TestContext.Current.CancellationToken);
        _ = used.AnalyzeDetailed(first, TestContext.Current.CancellationToken);
        var reportedBefore = sink.Events.Count;
        var result = used.Analyze(second, TestContext.Current.CancellationToken);
        var detailed = used.AnalyzeDetailed(second, TestContext.Current.CancellationToken);

        // Assert
        var expected = fresh.AnalyzeDetailed(second, TestContext.Current.CancellationToken);
        Assert.Equal(expected.Annotations, result.Annotations);
        Assert.Equal(expected.SentenceBreaks, result.SentenceBreaks);
        Assert.Equal(expected.Tokens, detailed.Tokens);
        string[] otherWords = ["Quokka", "marmoset", "rested", "Zebra", "galloped"];
        Assert.All(result.Annotations, annotation => Assert.DoesNotContain(otherWords, word => annotation.Value.Contains(word, StringComparison.OrdinalIgnoreCase)));
        var reported = sink.Events.Skip(reportedBefore).ToList();
        Assert.Equal(2, reported.Count);
        Assert.All(reported, report => Assert.DoesNotContain(otherWords, word => report.Message.Contains(word, StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>Proves a normalization annotation follows the part of speech of the token it starts in, so annotations stay in text order.</summary>
    [Fact]
    public void EnglishTextAnalyzer_Analyze_AbbreviationToken_EmitsPartOfSpeechThenNormalization()
    {
        // Arrange
        const string text = "Dr. Smith lives here.";
        var analyzer = new EnglishTextAnalyzer();

        // Act
        var result = analyzer.Analyze(text, TestContext.Current.CancellationToken);

        // Assert: both annotations of "Dr." start at 0, the part of speech first; starts never decrease
        Assert.Equal([TextEnAnnotations.PartOfSpeech, TextEnAnnotations.Normalized], result.Annotations.Take(2).Select(annotation => annotation.Kind));
        Assert.Equal("Doctor", result.Annotations[1].Value);
        Assert.Equal(result.Annotations.OrderBy(annotation => annotation.Start).Select(annotation => annotation.Start), result.Annotations.Select(annotation => annotation.Start));
    }

    /// <summary>Proves normalization needs no part-of-speech selection: selecting only it produces only en.norm and tokens carry no part of speech.</summary>
    [Fact]
    public void EnglishTextAnalyzer_Analyze_NormalizationOnly_EmitsOnlyNormalizationsAndUntaggedTokens()
    {
        // Arrange
        const string text = "Dr. Smith & Mr. Jones live on Main St. It is nice.";
        var options = new EnglishTextAnalyzerOptions { Annotations = TextEnAnnotationSelection.Normalization };
        var analyzer = new EnglishTextAnalyzer(options);

        // Act
        var analysis = analyzer.AnalyzeDetailed(text, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(4, analysis.Annotations.Count);
        Assert.All(analysis.Annotations, annotation => Assert.Equal(TextEnAnnotations.Normalized, annotation.Kind));
        Assert.All(analysis.Tokens, token => Assert.Null(token.PartOfSpeech));
    }

    /// <summary>Proves selecting only part of speech (or nothing) leaves the tokenizer's breaks unreconciled and emits no normalization.</summary>
    [Fact]
    public void EnglishTextAnalyzer_Analyze_NormalizationNotSelected_KeepsTokenizerBreaksAndEmitsNoNormalization()
    {
        // Arrange: the tokenizer alone does not end the sentence after "Main St."
        const string text = "I live on Main St. It is nice.";
        var partOfSpeechOnly = new EnglishTextAnalyzer(new EnglishTextAnalyzerOptions { Annotations = TextEnAnnotationSelection.PartOfSpeech });
        var none = new EnglishTextAnalyzer(new EnglishTextAnalyzerOptions { Annotations = TextEnAnnotationSelection.None });
        var normalizing = new EnglishTextAnalyzer();

        // Act
        var tagged = partOfSpeechOnly.AnalyzeDetailed(text, TestContext.Current.CancellationToken);
        var untouched = none.AnalyzeDetailed(text, TestContext.Current.CancellationToken);
        var reconciled = normalizing.AnalyzeDetailed(text, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(tagged.SentenceBreaks);
        Assert.Empty(untouched.SentenceBreaks);
        Assert.Empty(tagged.Normalizations);
        Assert.DoesNotContain(tagged.Annotations, annotation => annotation.Kind == TextEnAnnotations.Normalized);
        Assert.Equal([text.IndexOf("It is", StringComparison.Ordinal)], reconciled.SentenceBreaks);
    }

    /// <summary>Proves the detailed analysis lists each normalization with its source text, spoken text and kind, and each clitic token with its role.</summary>
    [Fact]
    public void EnglishTextAnalyzer_AnalyzeDetailed_NormalizedText_ListsNormalizationsAndCliticRoles()
    {
        // Arrange
        const string text = "Dr. Smith & Jones can\u2019t go. John's book is here.";
        var analyzer = new EnglishTextAnalyzer();

        // Act
        var analysis = analyzer.AnalyzeDetailed(text, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            [("Dr.", "Doctor", NormalizationKind.Abbreviation), ("&", "and", NormalizationKind.Symbol), ("\u2019", "'", NormalizationKind.Apostrophe)],
            analysis.Normalizations.Select(normalization => (normalization.Text, normalization.SpokenText, normalization.Kind)));
        Assert.All(analysis.Normalizations, normalization => Assert.Equal(normalization.Text, text.Substring(normalization.Start, normalization.Length)));
        Assert.Equal(
            [("n\u2019t", CliticKind.Not), ("'s", CliticKind.Possessive)],
            analysis.Tokens.Where(token => token.Clitic != CliticKind.None).Select(token => (token.Text, token.Clitic)));
    }

    /// <summary>Proves clitic roles and normalizations are left empty when normalization is not selected.</summary>
    [Fact]
    public void EnglishTextAnalyzer_AnalyzeDetailed_NormalizationNotSelected_HasNoNormalizationsAndNoCliticRoles()
    {
        // Arrange
        var analyzer = new EnglishTextAnalyzer(new EnglishTextAnalyzerOptions { Annotations = TextEnAnnotationSelection.PartOfSpeech });

        // Act
        var analysis = analyzer.AnalyzeDetailed("Dr. Smith said it's fine.", TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(analysis.Normalizations);
        Assert.All(analysis.Tokens, token => Assert.Equal(CliticKind.None, token.Clitic));
    }

    /// <summary>Proves a tag placeholder between an abbreviation and its name is never covered, and context crosses the tag.</summary>
    [Fact]
    public void EnglishTextAnalyzer_Analyze_PlaceholderAfterAbbreviation_IsNeverCoveredAndContextCrossesIt()
    {
        // Arrange
        var text = $"Dr. {Tag} Smith waved.";
        var analyzer = new EnglishTextAnalyzer();

        // Act
        var result = analyzer.Analyze(text, TestContext.Current.CancellationToken);

        // Assert
        var normalization = Assert.Single(result.Annotations, annotation => annotation.Kind == TextEnAnnotations.Normalized);
        Assert.Equal("Dr.", text.Substring(normalization.Start, normalization.Length));
        Assert.Equal("Doctor", normalization.Value);
        Assert.DoesNotContain(result.Annotations, annotation => annotation.Start <= 4 && 4 < annotation.End);
    }

    /// <summary>Proves the diagnostics event counts normalizations and ambiguous abbreviations and contains no word.</summary>
    [Fact]
    public void EnglishTextAnalyzer_Analyze_NormalizedText_ReportsNormalizationAndAmbiguityCounts()
    {
        // Arrange: three normalizations, one of them an inconclusive "They St."
        var sink = new RecordingDiagnostics();
        var analyzer = new EnglishTextAnalyzer(new EnglishTextAnalyzerOptions { Diagnostics = sink });

        // Act
        _ = analyzer.Analyze("Dr. Smith & Jones said They St. said so.", TestContext.Current.CancellationToken);

        // Assert
        var reported = Assert.Single(sink.Events);
        Assert.Matches(@"^Analyzed one text: 1 sentences, \d+ tokens, \d+ annotations, \d+ unknown words, 3 normalizations, 1 ambiguous readings, 0 numeric fallbacks, 0 overrides applied, 0 overrides ignored\.$", reported.Message);
    }

    /// <summary>Proves cancellation requested inside normalization (a long text, canceled mid-pass) stops the request without a result or a report.</summary>
    [Fact]
    public void EnglishTextAnalyzer_Analyze_CanceledDuringNormalization_ThrowsAndReportsNothing()
    {
        // Arrange: more than 600 tokens, and an observer that cancels while token 300 is visited
        var text = string.Concat(Enumerable.Repeat("a ", 1000));
        using var cancellation = new CancellationTokenSource();
        var sink = new RecordingDiagnostics();
        var normalizer = new TextNormalizer(
            AbbreviationResolver.Default,
            AcronymRecognizer.Default,
            index =>
            {
                if (index == 300)
                {
                    cancellation.Cancel();
                }
            });
        var analyzer = new EnglishTextAnalyzer(
            new EnglishTextAnalyzerOptions { Annotations = TextEnAnnotationSelection.Normalization, Diagnostics = sink },
            new CountingTaggerSource().CreateAdapter(),
            normalizer);

        // Act / Assert
        Assert.Throws<OperationCanceledException>(() => analyzer.Analyze(text, cancellation.Token));
        Assert.Empty(sink.Events);
    }

    /// <summary>Lists the replacement and pronunciation annotations of a result as (source characters, kind, value), in order.</summary>
    /// <param name="result">The result.</param>
    /// <param name="text">The analyzed text.</param>
    /// <returns>The tuples; part-of-speech annotations are left out.</returns>
    private static List<(string Source, string Kind, string Value)> NonTagAnnotations(TextAnalysisResult result, string text) =>
        [.. result.Annotations.Where(annotation => annotation.Kind != TextEnAnnotations.PartOfSpeech).Select(annotation => (text.Substring(annotation.Start, annotation.Length), annotation.Kind, annotation.Value))];

    /// <summary>Proves a say override replaces the word by its plain words as one <c>en.norm</c> annotation on the exact token, and the word keeps its part of speech, listed before the replacement.</summary>
    [Fact]
    public void EnglishTextAnalyzer_Analyze_SayOverride_EmitsNormAndKeepsPartOfSpeech()
    {
        // Arrange
        const string text = "Use Nginx today.";
        var analyzer = new EnglishTextAnalyzer(new EnglishTextAnalyzerOptions { Overrides = PronunciationOverrides.Create([PronunciationOverrideEntry.Say("Nginx", "engine ex")]) });

        // Act
        var result = analyzer.Analyze(text, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal([("Nginx", TextEnAnnotations.Normalized, "engine ex")], NonTagAnnotations(result, text));
        var start = text.IndexOf("Nginx", StringComparison.Ordinal);
        Assert.Equal(
            [TextEnAnnotations.PartOfSpeech, TextEnAnnotations.Normalized],
            result.Annotations.Where(annotation => annotation.Start == start).Select(annotation => annotation.Kind));
        Assert.Equal("Use engine ex today.", NormalizationHarness.ApplyNormalizations(text, result.Annotations));
    }

    /// <summary>Proves a spell-out override produces one <c>en.spell</c> annotation whose value is the letters, readable through the typed reader.</summary>
    [Fact]
    public void EnglishTextAnalyzer_Analyze_SpellOutOverride_EmitsSpell()
    {
        // Arrange
        const string text = "A GIF file";
        var analyzer = new EnglishTextAnalyzer(new EnglishTextAnalyzerOptions { Overrides = PronunciationOverrides.Create([PronunciationOverrideEntry.SpellOut("gif")]) });

        // Act
        var result = analyzer.Analyze(text, TestContext.Current.CancellationToken);

        // Assert
        var spell = Assert.Single(result.Annotations, annotation => annotation.Kind == TextEnAnnotations.SpellOut);
        Assert.Equal("GIF", text.Substring(spell.Start, spell.Length));
        Assert.True(TextEnAnnotations.TryReadSpellOut(spell, out var letters));
        Assert.Equal("G I F", letters);
        Assert.Equal("A G I F file", NormalizationHarness.ApplyNormalizations(text, result.Annotations));
    }

    /// <summary>Proves a pronunciation override adds one <c>en.pron</c> annotation on the exact token without replacing it, and no replacement is produced for it.</summary>
    [Fact]
    public void EnglishTextAnalyzer_Analyze_PronunciationOverride_EmitsPron()
    {
        // Arrange
        const string text = "Polish food and polish shoes";
        var analyzer = new EnglishTextAnalyzer(new EnglishTextAnalyzerOptions
        {
            Overrides = PronunciationOverrides.Create(
            [
                PronunciationOverrideEntry.Pronounce("Polish", PronunciationScheme.Ipa, "\u02C8po\u028Al\u026A\u0283", caseSensitive: true),
                PronunciationOverrideEntry.Pronounce("polish", PronunciationScheme.Arpabet, "P AA1 L IH0 SH"),
            ]),
        });

        // Act
        var result = analyzer.Analyze(text, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            [("Polish", TextEnAnnotations.Pronunciation, "ipa:\u02C8po\u028Al\u026A\u0283"), ("polish", TextEnAnnotations.Pronunciation, "arpabet:P AA1 L IH0 SH")],
            NonTagAnnotations(result, text));
        var first = result.Annotations.First(annotation => annotation.Kind == TextEnAnnotations.Pronunciation);
        Assert.True(TextEnAnnotations.TryReadPronunciation(first, out var scheme, out var phonemes));
        Assert.Equal(PronunciationScheme.Ipa, scheme);
        Assert.Equal("\u02C8po\u028Al\u026A\u0283", phonemes);
        Assert.Equal(text, NormalizationHarness.ApplyNormalizations(text, result.Annotations));
    }

    /// <summary>Proves a skip override produces no replacement for its word and bypasses the built-in rule that would have changed it, while the word keeps its part of speech.</summary>
    [Fact]
    public void EnglishTextAnalyzer_Analyze_SkipOverride_EmitsNothingForToken()
    {
        // Arrange
        const string text = "US troops asked us";
        var without = new EnglishTextAnalyzer();
        var with = new EnglishTextAnalyzer(new EnglishTextAnalyzerOptions { Overrides = PronunciationOverrides.Create([PronunciationOverrideEntry.Skip("US", caseSensitive: true)]) });

        // Act
        var plain = without.Analyze(text, TestContext.Current.CancellationToken);
        var skipped = with.Analyze(text, TestContext.Current.CancellationToken);

        // Assert: the built-in rule spells US; the override removes exactly that annotation
        Assert.Equal([("US", TextEnAnnotations.SpellOut, "U S")], NonTagAnnotations(plain, text));
        Assert.Empty(NonTagAnnotations(skipped, text));
        Assert.Equal(
            plain.Annotations.Where(annotation => annotation.Kind == TextEnAnnotations.PartOfSpeech),
            skipped.Annotations.Where(annotation => annotation.Kind == TextEnAnnotations.PartOfSpeech));
    }

    /// <summary>Proves an override next to an audio tag placeholder is annotated on its own token only: the placeholder is never covered and a word on the far side of the tag is analyzed with it.</summary>
    [Fact]
    public void EnglishTextAnalyzer_Analyze_OverrideAdjacentToAudioTag_AnnotatesAroundPlaceholder()
    {
        // Arrange
        var text = $"Nginx{Tag}SQL{Tag} and {Tag}Nginx";
        var analyzer = new EnglishTextAnalyzer(new EnglishTextAnalyzerOptions
        {
            Overrides = PronunciationOverrides.Create([PronunciationOverrideEntry.Say("Nginx", "engine ex"), PronunciationOverrideEntry.Say("SQL", "sequel")]),
        });

        // Act
        var result = analyzer.Analyze(text, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            [("Nginx", "engine ex"), ("SQL", "sequel"), ("Nginx", "engine ex")],
            result.Annotations.Where(annotation => annotation.Kind == TextEnAnnotations.Normalized).Select(annotation => (text.Substring(annotation.Start, annotation.Length), annotation.Value)));
        Assert.All(result.Annotations, annotation => Assert.DoesNotContain(Tag, text.Substring(annotation.Start, annotation.Length), StringComparison.Ordinal));
    }

    /// <summary>Proves the detailed analysis lists a say and a spell override as normalizations of kind Override (the spelled one flagged), and leaves a pronunciation out.</summary>
    [Fact]
    public void EnglishTextAnalyzer_AnalyzeDetailed_ListsOverrideNormalizationKind()
    {
        // Arrange
        const string text = "Nginx GIF Polish FBI";
        var analyzer = new EnglishTextAnalyzer(new EnglishTextAnalyzerOptions
        {
            Overrides = PronunciationOverrides.Create(
            [
                PronunciationOverrideEntry.Say("Nginx", "engine ex"),
                PronunciationOverrideEntry.SpellOut("GIF"),
                PronunciationOverrideEntry.Pronounce("Polish", PronunciationScheme.Ipa, "\u02C8po\u028Al\u026A\u0283"),
            ]),
        });

        // Act
        var analysis = analyzer.AnalyzeDetailed(text, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            [("Nginx", "engine ex", NormalizationKind.Override, false), ("GIF", "G I F", NormalizationKind.Override, true), ("FBI", "F B I", NormalizationKind.Initialism, true)],
            analysis.Normalizations.Select(normalization => (normalization.Text, normalization.SpokenText, normalization.Kind, normalization.IsSpelled)));
        Assert.Single(analysis.Annotations, annotation => annotation.Kind == TextEnAnnotations.Pronunciation);
    }

    /// <summary>Proves both entry points agree with overrides, over placeholders, supplementary characters and several sentences.</summary>
    /// <param name="text">The text to analyze.</param>
    [Theory]
    [InlineData("")]
    [InlineData("Nginx")]
    [InlineData("Use Nginx. Then SQL; then Polish and polish, and GIF.")]
    [InlineData("\uFFFCNginx\uFFFC \uD83D\uDE00 SQL \uFFFC")]
    [InlineData("5 kg of Nginx, $5 million of SQL at 5 pm; US and FBI and NASA's AT&T's")]
    [InlineData("Dr. Smith said: \"Nginx.\" Then qzxvbnk left at 5 p.m. on 12 May 2024.")]
    public void EnglishTextAnalyzer_Analyze_AnalyzeAndDetailed_ProduceSameAnnotationsWithOverrides(string text)
    {
        // Arrange
        var analyzer = new EnglishTextAnalyzer(new EnglishTextAnalyzerOptions { Overrides = SampleOverrides() });

        // Act
        var result = analyzer.Analyze(text, TestContext.Current.CancellationToken);
        var detailed = analyzer.AnalyzeDetailed(text, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(detailed.Annotations, result.Annotations);
        Assert.Equal(detailed.SentenceBreaks, result.SentenceBreaks);
        Assert.Equal(
            detailed.Annotations.Where(annotation => annotation.Kind is TextEnAnnotations.Normalized or TextEnAnnotations.SpellOut).Select(annotation => (annotation.Start, annotation.Length, annotation.Value)),
            detailed.Normalizations.Select(normalization => (normalization.Start, normalization.Length, normalization.SpokenText)));
    }

    /// <summary>Proves overrides never add, force or suppress a sentence break: the breaks equal those of the same text where the overridden words are replaced by neutral words of the same length.</summary>
    [Fact]
    public void EnglishTextAnalyzer_Analyze_OverridesDoNotChangeSentenceBreaks()
    {
        // Arrange
        const string text = "Use Nginx. Then SQL. Polish food, GIF files; US troops. Done.";
        const string neutral = "Use Zzzzz. Then Zzz. Zzzzzz food, Zzz files; Zz troops. Done.";
        var analyzer = new EnglishTextAnalyzer(new EnglishTextAnalyzerOptions { Overrides = SampleOverrides() });
        var plain = new EnglishTextAnalyzer();

        // Act
        var withOverrides = analyzer.Analyze(text, TestContext.Current.CancellationToken);
        var reference = plain.Analyze(neutral, TestContext.Current.CancellationToken);
        var withoutOverrides = plain.Analyze(text, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(reference.SentenceBreaks, withOverrides.SentenceBreaks);
        Assert.Equal(withoutOverrides.SentenceBreaks, withOverrides.SentenceBreaks);
    }

    /// <summary>Proves an override on an abbreviation word, whose period the tokenizer splits off or which is sentence-final, never changes the sentence breaks, for every action, and both entry points agree.</summary>
    /// <param name="text">The text.</param>
    /// <param name="word">The word that gets an entry for each action in turn.</param>
    [Theory]
    [InlineData("See Fig. 5 for details.", "Fig")]
    [InlineData("We saw the plot in Fig. It was good.", "Fig")]
    [InlineData("Dr. Smith lives on Maple Dr. He likes it.", "Dr")]
    [InlineData("Visit St. Louis. Then go.", "St")]
    [InlineData("No. 5 is here. Then go.", "No")]
    [InlineData("Jan. 5 was cold. Then go.", "Jan")]
    [InlineData("We met in Jan. It was cold.", "Jan")]
    [InlineData("Hello\u2014Dr. Smith left. Bye.", "Dr")]
    [InlineData("John Smith Jr. Ph.D. He spoke.", "Ph.D")]
    [InlineData("\uFFFCFig\uFFFC. 5 for details. Then go.", "Fig")]
    public void EnglishTextAnalyzer_Analyze_OverridesOnAbbreviations_DoNotChangeSentenceBreaks(string text, string word)
    {
        // Arrange
        var plain = new EnglishTextAnalyzer().Analyze(text, TestContext.Current.CancellationToken);
        var entries = new List<PronunciationOverrideEntry>
        {
            PronunciationOverrideEntry.Skip(word),
            PronunciationOverrideEntry.Say(word, "other words"),
            PronunciationOverrideEntry.Pronounce(word, PronunciationScheme.Ipa, "abc"),
        };
        if (word.All(char.IsAsciiLetter))
        {
            entries.Add(PronunciationOverrideEntry.SpellOut(word));
        }

        foreach (var entry in entries)
        {
            var analyzer = new EnglishTextAnalyzer(new EnglishTextAnalyzerOptions { Overrides = PronunciationOverrides.Create([entry]) });

            // Act
            var result = analyzer.Analyze(text, TestContext.Current.CancellationToken);
            var detailed = analyzer.AnalyzeDetailed(text, TestContext.Current.CancellationToken);

            // Assert
            Assert.NotNull(plain.SentenceBreaks);
            Assert.NotNull(result.SentenceBreaks);
            Assert.True(plain.SentenceBreaks.SequenceEqual(result.SentenceBreaks), $"{entry.Action} '{word}' changed the breaks of '{text}'");
            Assert.Equal(result.SentenceBreaks, detailed.SentenceBreaks);
        }
    }

    /// <summary>Loads the documented sample dictionary.</summary>
    /// <returns>The dictionary.</returns>
    private static PronunciationOverrides SampleOverrides() =>
        PronunciationOverrides.FromTsv(new StringReader(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "TestData", "overrides-dictionary.tsv"))));

    /// <summary>Proves a non-empty override dictionary without the normalization family is rejected when the analyzer is built, the empty dictionary is harmless, and the options default to no overrides.</summary>
    [Fact]
    public void EnglishTextAnalyzer_Ctor_OverridesWithoutNormalization_Throws()
    {
        // Arrange
        var overrides = PronunciationOverrides.Create([PronunciationOverrideEntry.Skip("word")]);

        // Act / Assert
        foreach (var selection in new[] { TextEnAnnotationSelection.None, TextEnAnnotationSelection.PartOfSpeech })
        {
            var exception = Assert.Throws<ArgumentException>(() => new EnglishTextAnalyzer(new EnglishTextAnalyzerOptions { Annotations = selection, Overrides = overrides }));
            Assert.Equal("options", exception.ParamName);
        }

        Assert.NotNull(new EnglishTextAnalyzer(new EnglishTextAnalyzerOptions { Annotations = TextEnAnnotationSelection.Normalization, Overrides = overrides }));
    }

    /// <summary>Proves an empty (or absent) dictionary is accepted with any selection and changes nothing.</summary>
    [Fact]
    public void EnglishTextAnalyzer_Ctor_EmptyOverridesWithoutNormalization_IsAccepted()
    {
        // Arrange
        const string text = "Use Nginx today.";

        // Act
        var empty = new EnglishTextAnalyzer(new EnglishTextAnalyzerOptions { Annotations = TextEnAnnotationSelection.PartOfSpeech, Overrides = PronunciationOverrides.Empty });
        var absent = new EnglishTextAnalyzer(new EnglishTextAnalyzerOptions { Annotations = TextEnAnnotationSelection.PartOfSpeech });
        var emptyFull = new EnglishTextAnalyzer(new EnglishTextAnalyzerOptions { Overrides = PronunciationOverrides.Empty });
        var plain = new EnglishTextAnalyzer();

        // Assert
        Assert.Equal(Key(absent.Analyze(text, TestContext.Current.CancellationToken)), Key(empty.Analyze(text, TestContext.Current.CancellationToken)));
        Assert.Equal(Key(plain.Analyze(text, TestContext.Current.CancellationToken)), Key(emptyFull.Analyze(text, TestContext.Current.CancellationToken)));
    }

    /// <summary>Proves the analyzer keeps the dictionary it was built with: a later copy of the options with other overrides never reaches it.</summary>
    [Fact]
    public void EnglishTextAnalyzer_Ctor_OptionsCopiedAtConstruction_IgnoresLaterWith()
    {
        // Arrange
        const string text = "Nginx and Zorblax";
        var options = new EnglishTextAnalyzerOptions { Overrides = PronunciationOverrides.Create([PronunciationOverrideEntry.Say("Nginx", "engine ex")]) };
        var analyzer = new EnglishTextAnalyzer(options);

        // Act: derive other options, which must not affect the analyzer
        var later = options with { Overrides = PronunciationOverrides.Create([PronunciationOverrideEntry.Say("Zorblax", "zor blax")]) };
        var result = analyzer.Analyze(text, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal([("Nginx", TextEnAnnotations.Normalized, "engine ex")], NonTagAnnotations(result, text));
        Assert.NotSame(options.Overrides, later.Overrides);
    }

    /// <summary>Proves the shapes of spelled replacements are annotated <c>en.spell</c> (never <c>en.norm</c>) with the same spoken text as before.</summary>
    /// <param name="text">The text.</param>
    /// <param name="source">The source characters of the spelled replacement.</param>
    /// <param name="value">The expected value.</param>
    [Theory]
    [InlineData("The FBI left.", "FBI", "F B I")]
    [InlineData("the U.S. Army", "U.S.", "U S")]
    [InlineData("J. Smith left", "J.", "J")]
    [InlineData("John Smith Jr. Ph.D. spoke.", "Ph.D.", "P H D")]
    [InlineData("He is an M.D. here", "M.D.", "M D")]
    [InlineData("I ate at 5 p.m. today", "p.m.", "P M")]
    [InlineData("US troops asked us", "US", "U S")]
    [InlineData("a CD and a DVD", "CD", "C D")]
    public void EnglishTextAnalyzer_Analyze_Initialisms_EmitSpellInsteadOfNorm(string text, string source, string value)
    {
        // Arrange
        var analyzer = new EnglishTextAnalyzer(new EnglishTextAnalyzerOptions { Annotations = TextEnAnnotationSelection.Normalization });

        // Act
        var result = analyzer.Analyze(text, TestContext.Current.CancellationToken);

        // Assert
        var spell = Assert.Single(result.Annotations, annotation => annotation.Kind == TextEnAnnotations.SpellOut && text.Substring(annotation.Start, annotation.Length) == source);
        Assert.True(TextEnAnnotations.TryReadSpellOut(spell, out var letters));
        Assert.Equal(value, letters.Trim());
        Assert.DoesNotContain(result.Annotations, annotation => annotation.Kind == TextEnAnnotations.Normalized && annotation.Start == spell.Start);
    }

    /// <summary>Proves a word replacement (an abbreviation) stays an <c>en.norm</c> annotation: only letter-name replacements are spelled.</summary>
    [Fact]
    public void EnglishTextAnalyzer_Analyze_WordReplacement_StaysNorm()
    {
        // Arrange
        const string text = "Dr. Smith & Jones paid $5.";
        var analyzer = new EnglishTextAnalyzer(new EnglishTextAnalyzerOptions { Annotations = TextEnAnnotationSelection.Normalization });

        // Act
        var result = analyzer.Analyze(text, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotEmpty(result.Annotations);
        Assert.All(result.Annotations, annotation => Assert.Equal(TextEnAnnotations.Normalized, annotation.Kind));
    }

    /// <summary>Proves an ampersand initialism is decomposed into letter-group spans (<c>en.spell</c>) and <c>and</c> spans (<c>en.norm</c>), the possessive tail stays outside, and the spoken text is unchanged.</summary>
    /// <param name="text">The text.</param>
    /// <param name="expectedSpoken">The spoken text after applying the replacements.</param>
    /// <param name="expectedSpans">The expected (source, kind) of each span.</param>
    [Theory]
    [InlineData("AT&T's network", "A T and T's network", "AT:en.spell|&:en.norm|T:en.spell")]
    [InlineData("AT\u0026T\u2019s network", "A T and T's network", "AT:en.spell|&:en.norm|T:en.spell|\u2019:en.norm")]
    [InlineData("R&D", "R and D", "R:en.spell|&:en.norm|D:en.spell")]
    [InlineData("Q&A and R&D", "Q and A and R and D", "Q:en.spell|&:en.norm|A:en.spell|R:en.spell|&:en.norm|D:en.spell")]
    [InlineData("x AT&T y", "x A T and T y", "AT:en.spell|&:en.norm|T:en.spell")]
    public void EnglishTextAnalyzer_Analyze_AmpersandInitialism_EmitsLetterSpansAndAndNorm(string text, string expectedSpoken, string expectedSpans)
    {
        // Arrange
        var analyzer = new EnglishTextAnalyzer(new EnglishTextAnalyzerOptions { Annotations = TextEnAnnotationSelection.Normalization });

        // Act
        var result = analyzer.Analyze(text, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(expectedSpans, string.Join('|', result.Annotations.Select(annotation => $"{text.Substring(annotation.Start, annotation.Length)}:{annotation.Kind}")));
        Assert.Equal(expectedSpoken, NormalizationHarness.ApplyNormalizations(text, result.Annotations));
        Assert.All(
            result.Annotations.Where(annotation => annotation.Kind == TextEnAnnotations.SpellOut),
            annotation => Assert.True(TextEnAnnotations.TryReadSpellOut(annotation, out _)));
    }

    /// <summary>Proves that without the normalization family nothing is spelled or replaced: no <c>en.spell</c> and no <c>en.pron</c>.</summary>
    [Fact]
    public void EnglishTextAnalyzer_Analyze_NormalizationNotSelected_EmitsNoSpell()
    {
        // Arrange
        var analyzer = new EnglishTextAnalyzer(new EnglishTextAnalyzerOptions { Annotations = TextEnAnnotationSelection.PartOfSpeech });

        // Act
        var result = analyzer.Analyze("The FBI and U.S. Army, AT&T.", TestContext.Current.CancellationToken);

        // Assert
        Assert.All(result.Annotations, annotation => Assert.Equal(TextEnAnnotations.PartOfSpeech, annotation.Kind));
    }

    /// <summary>Proves every row of the override golden file through the public analyzer: replacements, spelled spans, pronunciations and the counts-only diagnostics all agree with the file, and every annotation is readable through the typed readers.</summary>
    [Fact]
    public void EnglishTextAnalyzer_Analyze_GoldenCases_MatchThroughThePublicApi()
    {
        // Arrange
        var overrides = SampleOverrides();
        var failures = new List<string>();

        foreach (var row in NormalizationHarness.ReadOverrideCases("overrides-cases.tsv"))
        {
            var sink = new RecordingDiagnostics();
            var analyzer = new EnglishTextAnalyzer(new EnglishTextAnalyzerOptions { Annotations = TextEnAnnotationSelection.Normalization, Overrides = overrides, Diagnostics = sink });

            // Act
            var result = analyzer.Analyze(row.Input, TestContext.Current.CancellationToken);

            // Assert
            var spoken = NormalizationHarness.ApplyNormalizations(row.Input, result.Annotations).Replace(Tag, " ", StringComparison.Ordinal);
            var pronunciations = result.Annotations.Where(annotation => annotation.Kind == TextEnAnnotations.Pronunciation).Select(annotation => row.Input.Substring(annotation.Start, annotation.Length) + "=" + annotation.Value).ToList();
            var spelled = result.Annotations.Where(annotation => annotation.Kind == TextEnAnnotations.SpellOut).Select(annotation => row.Input.Substring(annotation.Start, annotation.Length)).ToList();
            var message = Assert.Single(sink.Events).Message;
            if (spoken != row.Spoken || !pronunciations.SequenceEqual(row.Pronunciations) || !spelled.SequenceEqual(row.Spelled) || !message.EndsWith($"{row.Applied} overrides applied, {row.Ignored} overrides ignored.", StringComparison.Ordinal))
            {
                failures.Add($"{row.Id}: spoken '{spoken}', pron '{string.Join("|", pronunciations)}', spelled '{string.Join("|", spelled)}', message '{message}'");
            }

            Assert.All(result.Annotations.Where(annotation => annotation.Kind != TextEnAnnotations.PartOfSpeech), annotation =>
                Assert.True(
                    TextEnAnnotations.TryReadReplacement(annotation, out _, out _) || TextEnAnnotations.TryReadPronunciation(annotation, out _, out _),
                    $"{row.Id}: an annotation is not readable"));
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    /// <summary>Proves the diagnostics event reports the two override counts and never a word, a key or a payload of the dictionary or of the text.</summary>
    [Fact]
    public void EnglishTextAnalyzer_Analyze_Overrides_ReportCountsAndNoContent()
    {
        // Arrange: a sentinel key and payload that cannot occur in the fixed message
        var sink = new RecordingDiagnostics();
        var overrides = PronunciationOverrides.Create(
        [
            PronunciationOverrideEntry.Say("Zorblax", "quuxsentinel"),
            PronunciationOverrideEntry.Skip("kg"),
        ]);
        var analyzer = new EnglishTextAnalyzer(new EnglishTextAnalyzerOptions { Overrides = overrides, Diagnostics = sink });

        // Act: two applied, one ignored (the unit of a number)
        _ = analyzer.Analyze("Zorblax met Zorblax and 5 kg", TestContext.Current.CancellationToken);

        // Assert
        var message = Assert.Single(sink.Events).Message;
        Assert.EndsWith("2 overrides applied, 1 overrides ignored.", message, StringComparison.Ordinal);
        Assert.DoesNotContain("Zorblax", message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("quuxsentinel", message, StringComparison.Ordinal);
    }

    /// <summary>Proves a request canceled inside the override lookup stops without a result or a report.</summary>
    [Fact]
    public void EnglishTextAnalyzer_Analyze_CanceledDuringOverrideLookup_ThrowsAndReportsNothing()
    {
        // Arrange: 600 tokens; the dictionary's lookup is the first stage that visits every token, and the token is canceled before
        using var cancellation = new CancellationTokenSource();
        var sink = new RecordingDiagnostics();
        var analyzer = new EnglishTextAnalyzer(new EnglishTextAnalyzerOptions
        {
            Annotations = TextEnAnnotationSelection.Normalization,
            Overrides = PronunciationOverrides.Create([PronunciationOverrideEntry.Skip("zzzz")]),
            Diagnostics = sink,
        });
        cancellation.Cancel();

        // Act / Assert
        Assert.Throws<OperationCanceledException>(() => analyzer.Analyze(string.Concat(Enumerable.Repeat("a ", 600)), cancellation.Token));
        Assert.Empty(sink.Events);
    }

    /// <summary>Builds a comparable key of a result's annotations and breaks.</summary>
    /// <param name="result">The result.</param>
    /// <returns>A string equal for equal results.</returns>
    private static string Key(TextAnalysisResult result) =>
        string.Join(',', result.Annotations.Select(a => $"{a.Start}:{a.Length}:{a.Kind}:{a.Value}"))
        + "|" + string.Join(',', result.SentenceBreaks!);
}
