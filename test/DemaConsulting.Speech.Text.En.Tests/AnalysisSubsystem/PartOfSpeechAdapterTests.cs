using DemaConsulting.Speech.Text.En.AnalysisSubsystem;
using DemaConsulting.Speech.Text.En.Tests.Fakes;

namespace DemaConsulting.Speech.Text.En.Tests.AnalysisSubsystem;

/// <summary>
///     Unit tests for <see cref="PartOfSpeechAdapter"/>: the single internal seam over the
///     <c>DemaConsulting.PartOfSpeech</c> package. The real package is used; tagger creation is
///     observed through counting factories so the process-wide shared tagger is never disturbed.
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

    /// <summary>Proves splitting without tagging reports spans only and never creates the tagger.</summary>
    [Fact]
    public void PartOfSpeechAdapter_Analyze_TagFalse_ReportsUntaggedTokensAndCreatesNoTagger()
    {
        // Arrange
        var source = new CountingTaggerSource();
        var adapter = source.CreateAdapter();

        // Act
        var sentences = adapter.Analyze("He left. She stayed.", false, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, sentences.Count);
        Assert.All(sentences.SelectMany(sentence => sentence), token => Assert.Null(token.Tag));
        Assert.Equal(0, source.Count);
        Assert.False(adapter.IsTaggerCreated);
    }

    /// <summary>Proves a text without tokens does not create the tagger even when tagging is requested.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("  \r\n\t ")]
    public void PartOfSpeechAdapter_Analyze_TextWithoutTokens_ReturnsNoSentencesAndCreatesNoTagger(string text)
    {
        // Arrange
        var source = new CountingTaggerSource();
        var adapter = source.CreateAdapter();

        // Act
        var sentences = adapter.Analyze(text, true, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(sentences);
        Assert.Equal(0, source.Count);
    }

    /// <summary>Proves the tagger is not created by construction and is created exactly once across several calls.</summary>
    [Fact]
    public void PartOfSpeechAdapter_Analyze_RepeatedCalls_CreateTaggerLazilyAndOnce()
    {
        // Arrange
        var source = new CountingTaggerSource();
        var adapter = source.CreateAdapter();
        Assert.Equal(0, source.Count);
        Assert.False(adapter.IsTaggerCreated);

        // Act
        for (var i = 0; i < 5; i++)
        {
            _ = adapter.Analyze("The cat sat. The dog ran.", true, TestContext.Current.CancellationToken);
        }

        // Assert
        Assert.Equal(1, source.Count);
        Assert.True(adapter.IsTaggerCreated);
    }

    /// <summary>Proves concurrent first uses create the tagger exactly once.</summary>
    [Fact]
    public async Task PartOfSpeechAdapter_Analyze_ConcurrentFirstUses_CreateTaggerOnce()
    {
        // Arrange
        var source = new CountingTaggerSource();
        var adapter = source.CreateAdapter();
        using var gate = new ManualResetEventSlim(false);

        // Act: release 32 tasks at the same moment
        var tasks = Enumerable.Range(0, 32)
            .Select(_ => Task.Run(
                () =>
                {
                    gate.Wait(TestContext.Current.CancellationToken);
                    return adapter.Analyze("The cat sat.", true, TestContext.Current.CancellationToken).Count;
                },
                TestContext.Current.CancellationToken))
            .ToArray();
        gate.Set();
        var counts = await Task.WhenAll(tasks);

        // Assert
        Assert.All(counts, count => Assert.Equal(1, count));
        Assert.Equal(1, source.Count);
    }

    /// <summary>Proves an already-canceled token throws before the text is split or the tagger created.</summary>
    [Fact]
    public void PartOfSpeechAdapter_Analyze_CanceledToken_ThrowsWithoutCreatingTagger()
    {
        // Arrange
        var source = new CountingTaggerSource();
        var adapter = source.CreateAdapter();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        // Act / Assert
        Assert.Throws<OperationCanceledException>(() => adapter.Analyze("The cat sat.", true, cancellation.Token));
        Assert.Equal(0, source.Count);
    }

    /// <summary>Proves a tagger factory that fails once does not poison the adapter: the first call throws, the next retries and succeeds, and the tagger is created once.</summary>
    [Fact]
    public void PartOfSpeechAdapter_Analyze_FactoryFailsOnce_NextCallRetriesAndSucceeds()
    {
        // Arrange: the first factory invocation throws, the second returns the tagger
        var source = new CountingTaggerSource(failures: 1);
        var adapter = source.CreateAdapter();

        // Act / Assert: the first call propagates the failure and leaves nothing created
        Assert.Throws<InvalidOperationException>(() => adapter.Analyze("The cat sat.", true, TestContext.Current.CancellationToken));
        Assert.Equal(1, source.Count);
        Assert.False(adapter.IsTaggerCreated);

        // The second call retries, succeeds, and from then on the tagger is reused
        var sentences = adapter.Analyze("The cat sat.", true, TestContext.Current.CancellationToken);
        _ = adapter.Analyze("The dog ran.", true, TestContext.Current.CancellationToken);
        Assert.Single(sentences);
        Assert.Equal(2, source.Count);
        Assert.True(adapter.IsTaggerCreated);
    }

    /// <summary>Proves concurrent callers racing a factory that fails once see exactly one failure and create the tagger exactly once successfully.</summary>
    [Fact]
    public async Task PartOfSpeechAdapter_Analyze_ConcurrentCallersAfterOneFailure_CreateTaggerOnceSuccessfully()
    {
        // Arrange
        var source = new CountingTaggerSource(failures: 1);
        var adapter = source.CreateAdapter();
        using var gate = new ManualResetEventSlim(false);

        // Act: 32 callers race; the lock serializes creation, so exactly the first one to get in fails
        var tasks = Enumerable.Range(0, 32)
            .Select(_ => Task.Run(
                () =>
                {
                    gate.Wait(TestContext.Current.CancellationToken);
                    try
                    {
                        return adapter.Analyze("The cat sat.", true, TestContext.Current.CancellationToken).Count;
                    }
                    catch (InvalidOperationException)
                    {
                        return -1;
                    }
                },
                TestContext.Current.CancellationToken))
            .ToArray();
        gate.Set();
        var counts = await Task.WhenAll(tasks);

        // Assert: one failure, 31 successes, and two factory invocations in total (one failed, one succeeded)
        Assert.Equal(1, counts.Count(count => count == -1));
        Assert.Equal(31, counts.Count(count => count == 1));
        Assert.Equal(2, source.Count);
    }

    /// <summary>Proves a factory that returns no tagger is reported as an invalid operation rather than a null reference.</summary>
    [Fact]
    public void PartOfSpeechAdapter_Analyze_FactoryReturnsNull_ThrowsInvalidOperationException()
    {
        // Arrange
        var adapter = new PartOfSpeechAdapter(() => null!);

        // Act / Assert
        Assert.Throws<InvalidOperationException>(() => adapter.Analyze("The cat sat.", true, TestContext.Current.CancellationToken));
        Assert.False(adapter.IsTaggerCreated);
    }

    /// <summary>Proves cancellation requested during tagger initialization throws before any sentence is tagged.</summary>
    [Fact]
    public void PartOfSpeechAdapter_Analyze_CanceledDuringTaggerInitialization_ThrowsBeforeTaggingAnySentence()
    {
        // Arrange
        using var cancellation = new CancellationTokenSource();
        var source = new CountingTaggerSource(cancellation.Cancel);
        var tagged = new List<int>();
        var adapter = source.CreateAdapter(tagged.Add);

        // Act / Assert
        Assert.Throws<OperationCanceledException>(() => adapter.Analyze("The cat sat.", true, cancellation.Token));
        Assert.Equal(1, source.Count);
        Assert.Empty(tagged);
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
        // Arrange
        using var cancellation = new CancellationTokenSource();
        var tagged = new List<int>();
        var adapter = new CountingTaggerSource().CreateAdapter(index =>
        {
            tagged.Add(index);
            if (index == 1)
            {
                cancellation.Cancel();
            }
        });

        // Act / Assert: both sentences were tagged, yet no result is returned
        Assert.Throws<OperationCanceledException>(() => adapter.Analyze("The cat sat. The dog ran.", true, cancellation.Token));
        Assert.Equal([0, 1], tagged);
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

    /// <summary>Proves the shared adapter is one process-wide instance.</summary>
    [Fact]
    public void PartOfSpeechAdapter_Shared_ReturnsSameInstance()
    {
        // Act / Assert
        Assert.Same(PartOfSpeechAdapter.Shared, PartOfSpeechAdapter.Shared);
    }
}
