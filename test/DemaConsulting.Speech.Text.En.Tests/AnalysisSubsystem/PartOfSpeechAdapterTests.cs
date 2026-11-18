using DemaConsulting.Speech.Text.En.AnalysisSubsystem;
using DemaConsulting.Speech.Text.En.HeteronymSubsystem;
using DemaConsulting.Speech.Text.En.Tests.Fakes;

namespace DemaConsulting.Speech.Text.En.Tests.AnalysisSubsystem;

/// <summary>
///     Unit tests for <see cref="PartOfSpeechAdapter"/>: the single internal seam over the
///     <c>DemaConsulting.PartOfSpeech</c> package. The real package is used; tagger creation goes
///     through test factories so the process-wide shared tagger is never disturbed and failures and
///     cancellation can be injected deterministically.
/// </summary>
public sealed class PartOfSpeechAdapterTests
{
    /// <summary>Proves token offsets index the analyzed text and sentences come back in order.</summary>
    [Fact]
    public void PartOfSpeechAdapter_Analyze_MultiSentenceText_TokenOffsetsIndexSourceInSentenceOrder()
    {
        // Arrange
        const string text = "He left home. She's here\r\nand well-known.";
        var adapter = new CountingTaggerSource().CreateAdapter();

        // Act
        var sentences = adapter.Analyze(text, true, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, sentences.Count);
        var tokens = sentences.SelectMany(sentence => sentence).ToList();
        Assert.Equal(tokens.OrderBy(token => token.Start).ToList(), tokens);
        Assert.Equal(
            ["He", "left", "home", ".", "She", "'s", "here", "and", "well-known", "."],
            tokens.Select(token => text.Substring(token.Start, token.Length)));
    }

    /// <summary>Proves tagging reports the Universal tag name, a confidence in range and the known-word flag.</summary>
    [Fact]
    public void PartOfSpeechAdapter_Analyze_TagTrue_ReportsTagConfidenceAndKnownWord()
    {
        // Arrange
        var adapter = new CountingTaggerSource().CreateAdapter();

        // Act
        var tokens = adapter.Analyze("The qzxvbnk sat.", true, TestContext.Current.CancellationToken).Single();

        // Assert
        Assert.Equal("DET", tokens[0].Tag);
        Assert.All(tokens, token => Assert.NotNull(token.Tag));
        Assert.All(tokens, token => Assert.InRange(token.Confidence, 0.0, 1.0));
        Assert.True(tokens[0].IsKnownWord);
        Assert.False(tokens[1].IsKnownWord);
        Assert.Equal("PUNCT", tokens[3].Tag);
    }

    /// <summary>Proves a sentence is described token by token exactly as the context builder describes each token, so the analyzer and the trainer see the same sentence.</summary>
    [Fact]
    public void PartOfSpeechAdapter_DescribeSentence_TaggedSentence_DescribesEveryTokenLikeContextCreate()
    {
        // Arrange
        const string text = "The record was broken. She said so.";
        var adapter = new CountingTaggerSource().CreateAdapter();
        var sentences = adapter.Analyze(text, true, TestContext.Current.CancellationToken);
        var longer = PartOfSpeechAdapter.DescribeSentence(text, sentences[0], null);

        // Act: the second sentence, into the buffer of the longer first one
        var described = PartOfSpeechAdapter.DescribeSentence(text, sentences[1], longer);

        // Assert: the first Count entries describe the second sentence
        Assert.True(described.Length >= sentences[1].Count);
        for (var i = 0; i < sentences[1].Count; i++)
        {
            var token = sentences[1][i];
            Assert.Equal(HeteronymContext.Create(text, token.Start, token.Length, token.Tag, token.Confidence), described[i]);
        }
    }

    /// <summary>Proves splitting without tagging reports spans only: sentences and tokens in text order, none of them tagged.</summary>
    [Fact]
    public void PartOfSpeechAdapter_Analyze_TagFalse_ReportsUntaggedTokens()
    {
        // Arrange
        const string text = "He left. She stayed.";
        var adapter = new CountingTaggerSource().CreateAdapter();

        // Act
        var sentences = adapter.Analyze(text, false, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, sentences.Count);
        Assert.All(sentences.SelectMany(sentence => sentence), token => Assert.Null(token.Tag));
        Assert.Equal(
            ["He", "left", ".", "She", "stayed", "."],
            sentences.SelectMany(sentence => sentence).Select(token => text.Substring(token.Start, token.Length)));
    }

    /// <summary>Proves a text without tokens yields no sentences even when tagging is requested.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("  \r\n\t ")]
    public void PartOfSpeechAdapter_Analyze_TextWithoutTokens_ReturnsNoSentences(string text)
    {
        // Arrange
        var adapter = new CountingTaggerSource().CreateAdapter();

        // Act
        var sentences = adapter.Analyze(text, true, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(sentences);
    }

    /// <summary>Proves repeated calls on one adapter give the same tagged tokens every time.</summary>
    [Fact]
    public void PartOfSpeechAdapter_Analyze_RepeatedCalls_ReturnSameTaggedTokens()
    {
        // Arrange
        var adapter = new CountingTaggerSource().CreateAdapter();
        var expected = Describe(adapter, "The cat sat. The dog ran.");

        // Act
        var results = Enumerable.Range(0, 5)
            .Select(_ => Describe(adapter, "The cat sat. The dog ran."))
            .ToList();

        // Assert
        Assert.All(results, result => Assert.Equal(expected, result));
    }

    /// <summary>Proves threads that start at the same moment on a fresh adapter all get the same, complete result.</summary>
    [Fact]
    public async Task PartOfSpeechAdapter_Analyze_ConcurrentFirstUses_ReturnIdenticalResults()
    {
        // Arrange
        var adapter = new CountingTaggerSource().CreateAdapter();
        using var gate = new ManualResetEventSlim(false);

        // Act: release 32 tasks at the same moment
        var tasks = Enumerable.Range(0, 32)
            .Select(_ => Task.Run(
                () =>
                {
                    gate.Wait(TestContext.Current.CancellationToken);
                    return Describe(adapter, "The cat sat.");
                },
                TestContext.Current.CancellationToken))
            .ToArray();
        gate.Set();
        var results = await Task.WhenAll(tasks);

        // Assert
        Assert.Equal("The/DET cat/NOUN sat/VERB ./PUNCT", results[0]);
        Assert.All(results, result => Assert.Equal(results[0], result));
    }

    /// <summary>Proves an already-canceled token throws OperationCanceledException instead of returning a result.</summary>
    [Fact]
    public void PartOfSpeechAdapter_Analyze_CanceledToken_ThrowsOperationCanceledException()
    {
        // Arrange
        var adapter = new CountingTaggerSource().CreateAdapter();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        // Act / Assert
        Assert.Throws<OperationCanceledException>(() => adapter.Analyze("The cat sat.", true, cancellation.Token));
    }

    /// <summary>Proves a tagger factory that fails once does not poison the adapter: the first call throws and a later call succeeds with the right tags.</summary>
    [Fact]
    public void PartOfSpeechAdapter_Analyze_FactoryFailsOnce_NextCallSucceeds()
    {
        // Arrange: the first factory invocation throws, the second returns the tagger
        var adapter = new CountingTaggerSource(failures: 1).CreateAdapter();

        // Act / Assert: the first call propagates the failure
        Assert.Throws<InvalidOperationException>(() => adapter.Analyze("The cat sat.", true, TestContext.Current.CancellationToken));

        // The next calls succeed and give the tags of a healthy tagger
        Assert.Equal("The/DET cat/NOUN sat/VERB ./PUNCT", Describe(adapter, "The cat sat."));
        Assert.Equal("The/DET dog/NOUN ran/VERB ./PUNCT", Describe(adapter, "The dog ran."));
    }

    /// <summary>Proves concurrent callers racing a factory that fails once see at most one failure, and every other caller and every later call gets the complete result.</summary>
    [Fact]
    public async Task PartOfSpeechAdapter_Analyze_ConcurrentCallersAfterOneFailure_OnlyTheFailingCallSeesIt()
    {
        // Arrange
        var adapter = new CountingTaggerSource(failures: 1).CreateAdapter();
        using var gate = new ManualResetEventSlim(false);

        // Act: 32 callers race; the factory fails for one invocation only
        var tasks = Enumerable.Range(0, 32)
            .Select(_ => Task.Run(
                () =>
                {
                    gate.Wait(TestContext.Current.CancellationToken);
                    try
                    {
                        return Describe(adapter, "The cat sat.");
                    }
                    catch (InvalidOperationException)
                    {
                        return string.Empty;
                    }
                },
                TestContext.Current.CancellationToken))
            .ToArray();
        gate.Set();
        var results = await Task.WhenAll(tasks);

        // Assert: at most one failure, every other caller complete, and the adapter works afterwards
        Assert.True(results.Count(result => result.Length == 0) <= 1);
        Assert.All(results.Where(result => result.Length > 0), result => Assert.Equal("The/DET cat/NOUN sat/VERB ./PUNCT", result));
        Assert.Equal("The/DET cat/NOUN sat/VERB ./PUNCT", Describe(adapter, "The cat sat."));
    }

    /// <summary>Proves a factory that returns no tagger is reported as an invalid operation rather than a null reference.</summary>
    [Fact]
    public void PartOfSpeechAdapter_Analyze_FactoryReturnsNull_ThrowsInvalidOperationException()
    {
        // Arrange
        var adapter = new PartOfSpeechAdapter(() => null!);

        // Act / Assert
        Assert.Throws<InvalidOperationException>(() => adapter.Analyze("The cat sat.", true, TestContext.Current.CancellationToken));
    }

    /// <summary>Proves cancellation requested during tagger initialization throws OperationCanceledException instead of returning a result.</summary>
    [Fact]
    public void PartOfSpeechAdapter_Analyze_CanceledDuringTaggerInitialization_ThrowsOperationCanceledException()
    {
        // Arrange
        using var cancellation = new CancellationTokenSource();
        var adapter = new CountingTaggerSource(cancellation.Cancel).CreateAdapter();

        // Act / Assert
        Assert.Throws<OperationCanceledException>(() => adapter.Analyze("The cat sat.", true, cancellation.Token));
    }

    /// <summary>Proves cancellation requested while the only sentence is tagged throws instead of returning the result.</summary>
    [Fact]
    public void PartOfSpeechAdapter_Analyze_CanceledAfterOnlySentenceTagged_Throws()
    {
        // Arrange
        using var cancellation = new CancellationTokenSource();
        var adapter = new CountingTaggerSource().CreateAdapter(_ => cancellation.Cancel());

        // Act / Assert
        Assert.Throws<OperationCanceledException>(() => adapter.Analyze("The cat sat.", true, cancellation.Token));
    }

    /// <summary>Proves cancellation requested while the final of several sentences is tagged throws instead of returning the result.</summary>
    [Fact]
    public void PartOfSpeechAdapter_Analyze_CanceledAfterFinalSentenceTagged_Throws()
    {
        // Arrange: the second and last sentence is the one whose tagging cancels the request
        using var cancellation = new CancellationTokenSource();
        var adapter = new CountingTaggerSource().CreateAdapter(index =>
        {
            if (index == 1)
            {
                cancellation.Cancel();
            }
        });

        // Act / Assert
        Assert.Throws<OperationCanceledException>(() => adapter.Analyze("The cat sat. The dog ran.", true, cancellation.Token));
    }

    /// <summary>Proves a null factory or a null text is rejected.</summary>
    [Fact]
    public void PartOfSpeechAdapter_NullArguments_ThrowArgumentNullException()
    {
        // Arrange
        var adapter = new CountingTaggerSource().CreateAdapter();

        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => new PartOfSpeechAdapter(null!));
        Assert.Throws<ArgumentNullException>(() => adapter.Analyze(null!, true, TestContext.Current.CancellationToken));
    }

    /// <summary>Tags a text and describes each token as <c>text/TAG</c>, joined by spaces.</summary>
    /// <param name="adapter">The adapter.</param>
    /// <param name="text">The text.</param>
    /// <returns>The description of every token of every sentence, in order.</returns>
    private static string Describe(PartOfSpeechAdapter adapter, string text) =>
        string.Join(
            ' ',
            adapter.Analyze(text, true, TestContext.Current.CancellationToken)
                .SelectMany(sentence => sentence)
                .Select(token => $"{text.Substring(token.Start, token.Length)}/{token.Tag}"));
}
