using DemaConsulting.Speech.Diagnostics;
using DemaConsulting.Speech.RecognitionSubsystem;

namespace DemaConsulting.Speech.Tests.RecognitionSubsystem;

/// <summary>
///     Unit tests for <see cref="RecognitionResultBuffer"/>, exercising its provisional-coalescing
///     and final-result-queueing backpressure policy (Decision #5) directly, without a session.
/// </summary>
public class RecognitionResultBufferTests
{
    /// <summary>
    ///     Proves that a provisional result buffered before its own final is superseded (never
    ///     delivered) once that final is added, rather than being delivered after it as a stale
    ///     partial transcript: the buffer always drains queued finals before the provisional
    ///     slot, so an un-cleared provisional would otherwise surface after its already-superseded
    ///     final.
    /// </summary>
    [Fact]
    public async Task RecognitionResultBuffer_AddResult_FinalAfterProvisional_SupersedesProvisional()
    {
        // Arrange
        var buffer = new RecognitionResultBuffer(NullSpeechDiagnostics.Instance, "RecognitionSubsystem");

        // Act: a provisional arrives, then its own final - both before anything reads the buffer
        buffer.AddResult(new SpeechRecognitionEvent(new SpeechRecognitionResult("hello", IsFinal: false)));
        buffer.AddResult(new SpeechRecognitionEvent(new SpeechRecognitionResult("hello world", IsFinal: true)));
        buffer.Complete();

        // Assert: only the final survived - the superseded provisional was never delivered
        var results = await CollectAsync(buffer);
        var result = Assert.Single(results);
        Assert.True(result.Result.IsFinal);
        Assert.Equal("hello world", result.Result.Text);
    }

    /// <summary>
    ///     Proves that a provisional result read before its own final arrives is still delivered
    ///     normally - superseding only ever discards a not-yet-consumed provisional, never one
    ///     already handed to the consumer.
    /// </summary>
    [Fact]
    public async Task RecognitionResultBuffer_AddResult_ProvisionalThenFinalForDifferentUtterances_DeliversBoth()
    {
        // Arrange
        var buffer = new RecognitionResultBuffer(NullSpeechDiagnostics.Instance, "RecognitionSubsystem");

        // Act: a provisional for one utterance, then (after it would have been read) a final for
        // a later, unrelated utterance
        buffer.AddResult(new SpeechRecognitionEvent(new SpeechRecognitionResult("partial", IsFinal: false)));
        var afterFirstRead = await CollectOneAsync(buffer);
        buffer.AddResult(new SpeechRecognitionEvent(new SpeechRecognitionResult("done", IsFinal: true)));
        buffer.Complete();
        var afterSecondRead = await CollectAsync(buffer);

        // Assert: both results were delivered, each exactly once
        Assert.Equal("partial", afterFirstRead.Text);
        Assert.False(afterFirstRead.IsFinal);
        var second = Assert.Single(afterSecondRead);
        Assert.Equal("done", second.Result.Text);
        Assert.True(second.Result.IsFinal);
    }

    private static async Task<List<SpeechRecognitionEvent>> CollectAsync(RecognitionResultBuffer buffer)
    {
        var results = new List<SpeechRecognitionEvent>();
        await foreach (var result in buffer.ReadAllAsync(TestContext.Current.CancellationToken))
        {
            results.Add(result);
        }

        return results;
    }

    private static async Task<SpeechRecognitionResult> CollectOneAsync(RecognitionResultBuffer buffer)
    {
        await using var enumerator = buffer.ReadAllAsync(TestContext.Current.CancellationToken).GetAsyncEnumerator();
        var hasResult = await enumerator.MoveNextAsync();
        Assert.True(hasResult, "Expected at least one buffered result.");
        return enumerator.Current.Result;
    }
}
