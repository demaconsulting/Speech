using System.Reflection;
using DemaConsulting.Speech.ModelManagementSubsystem;
using DemaConsulting.Speech.Proofreader.Synthesis;

namespace DemaConsulting.Speech.Proofreader.Tests;

/// <summary>
///     Tests for <see cref="SentenceAudioCache"/>: a request joins or reuses earlier work, a failure is retried, and
///     eviction never throws away a newer result.
/// </summary>
public class SentenceAudioCacheTests
{
    /// <summary>The selection every test synthesizes with.</summary>
    private static readonly ModelSelection Selection = new("fake-model", "Fake", new Dictionary<string, object>());

    /// <summary>
    ///     Creates a model that is only passed through to the synthesis function under test.
    /// </summary>
    /// <returns>The model.</returns>
    private static ISynthesisModel CreateModel() => DispatchProxy.Create<ISynthesisModel, NullModelProxy>();

    /// <summary>
    ///     Validates that a request for a sentence already synthesized is served from the cache.
    /// </summary>
    [Fact]
    public async Task SentenceAudioCache_GetAsync_RepeatedRequest_SynthesizesOnce()
    {
        // Arrange
        var calls = 0;
        var cache = new SentenceAudioCache(
            (_, _, _) =>
            {
                Interlocked.Increment(ref calls);
                return Task.FromResult(new SentenceAudio([], []));
            });
        var model = CreateModel();

        // Act
        var first = await cache.GetAsync(model, Selection, "One.");
        var second = await cache.GetAsync(model, Selection, "One.");

        // Assert
        Assert.Same(first, second);
        Assert.Equal(1, calls);
        Assert.Same(first, cache.TryGetCompleted(Selection, "One."));
    }

    /// <summary>
    ///     Validates that a failed synthesis is retried by the next request, and that the retry's result is kept even
    ///     when other sentences are requested afterwards within the capacity.
    /// </summary>
    [Fact]
    public async Task SentenceAudioCache_GetAsync_RetryAfterFailure_IsCachedAndNotEvictedEarly()
    {
        // Arrange: the first synthesis of "A" fails; every other synthesis succeeds. Capacity is two entries.
        var callsPerText = new Dictionary<string, int>(StringComparer.Ordinal);
        var cache = new SentenceAudioCache(
            (_, _, text) =>
            {
                int count;
                lock (callsPerText)
                {
                    callsPerText[text] = count = callsPerText.GetValueOrDefault(text) + 1;
                }

                return Task.FromResult(text == "A" && count == 1 ? SentenceAudio.Failed("boom") : new SentenceAudio([], []));
            },
            capacity: 2);
        var model = CreateModel();

        // Act: fail, retry successfully, then request one more sentence (which fits in the capacity)
        var failed = await cache.GetAsync(model, Selection, "A");
        var retried = await cache.GetAsync(model, Selection, "A");
        _ = await cache.GetAsync(model, Selection, "B");
        var again = await cache.GetAsync(model, Selection, "A");

        // Assert: the failure was not kept, the retry succeeded, and it was neither evicted nor synthesized again
        Assert.False(failed.IsSuccess);
        Assert.True(retried.IsSuccess);
        Assert.Same(retried, again);
        Assert.Same(retried, cache.TryGetCompleted(Selection, "A"));
        Assert.Equal(2, callsPerText["A"]);
        Assert.Equal(1, callsPerText["B"]);
    }

    /// <summary>
    ///     Validates that the oldest entry is dropped once the capacity is exceeded.
    /// </summary>
    [Fact]
    public async Task SentenceAudioCache_GetAsync_BeyondCapacity_DropsOldestEntry()
    {
        // Arrange
        var cache = new SentenceAudioCache((_, _, _) => Task.FromResult(new SentenceAudio([], [])), capacity: 2);
        var model = CreateModel();

        // Act
        _ = await cache.GetAsync(model, Selection, "A");
        _ = await cache.GetAsync(model, Selection, "B");
        _ = await cache.GetAsync(model, Selection, "C");

        // Assert
        Assert.Null(cache.TryGetCompleted(Selection, "A"));
        Assert.NotNull(cache.TryGetCompleted(Selection, "B"));
        Assert.NotNull(cache.TryGetCompleted(Selection, "C"));
    }

    /// <summary>
    ///     A proxy that answers every member with its default value; the cache only forwards the model.
    /// </summary>
    public class NullModelProxy : DispatchProxy
    {
        /// <inheritdoc/>
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => null;
    }
}
