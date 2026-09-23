using DemaConsulting.Speech.RecognitionSubsystem;

namespace DemaConsulting.Speech.Tests.RecognitionSubsystem.Fakes;

/// <summary>
///     Deterministic <see cref="IRecognitionEngine"/> test double that records the mono samples
///     it was fed and yields a scripted sequence of recognition results, so the recognizer's
///     threading, resampling, and event-emission behavior can be verified with no native
///     sherpa-onnx runtime and no downloaded model.
/// </summary>
internal sealed class FakeRecognitionEngine : IRecognitionEngine
{
    /// <summary>The results this engine yields, in order, one per <see cref="TryDecode"/> call.</summary>
    private readonly Queue<SpeechRecognitionResult> _scriptedResults;

    /// <summary>The result <see cref="TryFlush"/> yields, or <see langword="null"/> to yield none.</summary>
    private readonly SpeechRecognitionResult? _scriptedFlushResult;

    /// <summary>The exception to throw from <see cref="AcceptSamples"/>, when one was scripted.</summary>
    private readonly Exception? _acceptSamplesException;

    /// <summary>The exception to throw from <see cref="Reset"/>, when one was scripted.</summary>
    private readonly Exception? _resetException;

    /// <summary>The exception to throw from <see cref="TryFlush"/>, when one was scripted.</summary>
    private readonly Exception? _flushException;

    /// <summary>Every mono sample this engine has been fed, in the order it arrived.</summary>
    private readonly List<float> _acceptedSamples = [];

    /// <summary>
    ///     Initializes a new instance of the <see cref="FakeRecognitionEngine"/> class.
    /// </summary>
    /// <param name="scriptedResults">
    ///     The results to yield from successive <see cref="TryDecode"/> calls, or
    ///     <see langword="null"/> to yield none.
    /// </param>
    /// <param name="acceptSamplesException">
    ///     An exception for <see cref="AcceptSamples"/> to throw, or <see langword="null"/> to
    ///     accept samples normally. Used to prove the recognizer contains engine faults.
    /// </param>
    /// <param name="resetException">
    ///     An exception for <see cref="Reset"/> to throw, or <see langword="null"/> to reset
    ///     normally. Used to prove the recognizer's teardown still completes and reports the
    ///     fault when the engine's reset fails.
    /// </param>
    /// <param name="scriptedFlushResult">
    ///     The result <see cref="TryFlush"/> yields, or <see langword="null"/> to yield none.
    ///     Used to prove the recognizer delivers a flushed trailing result when stopping.
    /// </param>
    /// <param name="flushException">
    ///     An exception for <see cref="TryFlush"/> to throw, or <see langword="null"/> to flush
    ///     normally. Used to prove the recognizer's teardown still completes and reports the
    ///     fault when the engine's flush fails.
    /// </param>
    public FakeRecognitionEngine(
        IEnumerable<SpeechRecognitionResult>? scriptedResults = null,
        Exception? acceptSamplesException = null,
        Exception? resetException = null,
        SpeechRecognitionResult? scriptedFlushResult = null,
        Exception? flushException = null)
    {
        _scriptedResults = new Queue<SpeechRecognitionResult>(scriptedResults ?? []);
        _acceptSamplesException = acceptSamplesException;
        _resetException = resetException;
        _scriptedFlushResult = scriptedFlushResult;
        _flushException = flushException;
    }

    /// <summary>Gets every mono sample this engine has been fed, in arrival order.</summary>
    public IReadOnlyList<float> AcceptedSamples => _acceptedSamples;

    /// <summary>Gets the number of <see cref="AcceptSamples"/> calls this engine has received.</summary>
    public int AcceptSamplesCallCount { get; private set; }

    /// <summary>Gets the number of <see cref="Reset"/> calls this engine has received.</summary>
    public int ResetCallCount { get; private set; }

    /// <summary>Gets the number of <see cref="TryFlush"/> calls this engine has received.</summary>
    public int FlushCallCount { get; private set; }

    /// <summary>Gets the number of <see cref="Dispose"/> calls this engine has received.</summary>
    public int DisposeCallCount { get; private set; }

    /// <inheritdoc/>
    public void AcceptSamples(ReadOnlySpan<float> monoSamples)
    {
        AcceptSamplesCallCount++;

        if (_acceptSamplesException is not null)
        {
            throw _acceptSamplesException;
        }

        _acceptedSamples.AddRange(monoSamples.ToArray());
    }

    /// <inheritdoc/>
    public bool TryDecode(out SpeechRecognitionResult? result)
    {
        if (_scriptedResults.Count == 0)
        {
            result = null;
            return false;
        }

        result = _scriptedResults.Dequeue();
        return true;
    }

    /// <inheritdoc/>
    public bool TryFlush(out SpeechRecognitionResult? result)
    {
        FlushCallCount++;

        if (_flushException is not null)
        {
            throw _flushException;
        }

        result = _scriptedFlushResult;
        return result is not null;
    }

    /// <inheritdoc/>
    public void Reset()
    {
        ResetCallCount++;

        if (_resetException is not null)
        {
            throw _resetException;
        }
    }

    /// <inheritdoc/>
    public void Dispose() => DisposeCallCount++;
}
