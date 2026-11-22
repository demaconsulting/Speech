using DemaConsulting.Speech.RecognitionSubsystem;
using DemaConsulting.Speech.SttEvaluation;

namespace DemaConsulting.Speech.Onnx.NemotronStt.Tests.Evaluation;

/// <summary>
///     Unit tests for <see cref="SttTranscriber"/>, proving that it feeds a backend the way a
///     recognition session does (fixed blocks, drain after each block, one flush at the end) and
///     records every result at its audio-clock position, using a scripted fake backend.
/// </summary>
public class SttTranscriberTests
{
    /// <summary>
    ///     Proves that audio is fed in 20 ms blocks, results are stamped with the number of samples
    ///     fed when they were reported, and the flush result is stamped at the end of the stream.
    /// </summary>
    [Fact]
    public void SttTranscriber_Transcribe_ScriptedBackend_RecordsResultsAtAudioPositions()
    {
        // Arrange: a provisional after the first block, a final after the third, and a flushed final
        var backend = new ScriptedBackend(
            new Dictionary<int, SpeechRecognitionResult>
            {
                [1] = new("hello", IsFinal: false),
                [3] = new("hello world", IsFinal: true),
            },
            flushed: new SpeechRecognitionResult("again", IsFinal: true));
        var samples = new float[16000 * 100 / 1000]; // 100 ms = five 20 ms blocks

        // Act
        var transcript = SttTranscriber.Transcribe(backend, samples, 16000);

        // Assert: five 320-sample blocks, results at 320 and 960 samples, the flush at 1600
        Assert.Equal([320, 320, 320, 320, 320], backend.BlockSizes);
        Assert.Equal([320L, 960L, 1600L], transcript.Events.Select(e => e.SamplePosition));
        Assert.Equal("hello world again", transcript.Text);
        Assert.Equal(2, transcript.SegmentCount);
        Assert.Equal(0.02, transcript.FirstResultSeconds!.Value, precision: 6);
        Assert.Equal(0.0, transcript.FinalLagSeconds!.Value, precision: 6);
    }

    /// <summary>
    ///     Proves that trailing silence is fed after the recording and counted in the fed audio but
    ///     not in the recording's own duration, and that the model's text hook is applied.
    /// </summary>
    [Fact]
    public void SttTranscriber_Transcribe_TrailingSilenceAndTextHook_FeedsSilenceAndNormalizesText()
    {
        // Arrange: one final reported after the last silence block
        var backend = new ScriptedBackend(
            new Dictionary<int, SpeechRecognitionResult> { [3] = new("hi", IsFinal: true) },
            flushed: null);
        var samples = new float[320];

        // Act: 40 ms of silence after a 20 ms recording, and an upper-casing text hook
        var transcript = SttTranscriber.Transcribe(
            backend,
            samples,
            16000,
            (text, isFinal) => isFinal ? text.ToUpperInvariant() : text,
            trailingSilenceSeconds: 0.04);

        // Assert
        Assert.Equal(3, backend.BlockSizes.Count);
        Assert.Equal(320, transcript.SpeechSamples);
        Assert.Equal(960, transcript.FedSamples);
        Assert.Equal("HI", transcript.Text);
        Assert.Equal(0.04, transcript.FinalLagSeconds!.Value, precision: 6);
    }

    /// <summary>
    ///     Proves that a negative trailing silence is rejected.
    /// </summary>
    [Fact]
    public void SttTranscriber_Transcribe_NegativeTrailingSilence_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var backend = new ScriptedBackend(new Dictionary<int, SpeechRecognitionResult>(), flushed: null);

        // Act / Assert
        Assert.Throws<ArgumentOutOfRangeException>(
            () => SttTranscriber.Transcribe(backend, new float[320], 16000, trailingSilenceSeconds: -1.0));
    }

    /// <summary>
    ///     A fake backend that reports scripted results after given blocks and a scripted flush
    ///     result, recording the size of every accepted block.
    /// </summary>
    /// <param name="resultsAfterBlock">The result to report after the given (1-based) block.</param>
    /// <param name="flushed">The result of <see cref="IRecognitionBackend.TryFlush"/>, or <see langword="null"/>.</param>
    private sealed class ScriptedBackend(
        IReadOnlyDictionary<int, SpeechRecognitionResult> resultsAfterBlock,
        SpeechRecognitionResult? flushed) : IRecognitionBackend
    {
        /// <summary>The result not yet reported by <see cref="TryDecode"/>.</summary>
        private SpeechRecognitionResult? _pending;

        /// <summary>Gets the size of every accepted block, in order.</summary>
        public List<int> BlockSizes { get; } = [];

        /// <inheritdoc/>
        public void AcceptSamples(ReadOnlySpan<float> monoSamples)
        {
            BlockSizes.Add(monoSamples.Length);
            _pending = resultsAfterBlock.GetValueOrDefault(BlockSizes.Count);
        }

        /// <inheritdoc/>
        public bool TryDecode(out SpeechRecognitionResult? result)
        {
            result = _pending;
            _pending = null;
            return result is not null;
        }

        /// <inheritdoc/>
        public bool TryFlush(out SpeechRecognitionResult? result)
        {
            result = flushed;
            return result is not null;
        }

        /// <inheritdoc/>
        public void Reset()
        {
            // Nothing to discard: the script has no utterance state
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            // Nothing to release
        }
    }
}
