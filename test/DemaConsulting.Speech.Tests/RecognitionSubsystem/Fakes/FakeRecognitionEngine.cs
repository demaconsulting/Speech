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

    /// <summary>The exception to throw from <see cref="AcceptSamples"/>, when one was scripted.</summary>
    private readonly Exception? _acceptSamplesException;

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
    public FakeRecognitionEngine(
        IEnumerable<SpeechRecognitionResult>? scriptedResults = null,
        Exception? acceptSamplesException = null)
    {
        _scriptedResults = new Queue<SpeechRecognitionResult>(scriptedResults ?? []);
        _acceptSamplesException = acceptSamplesException;
    }

    /// <summary>Gets every mono sample this engine has been fed, in arrival order.</summary>
    public IReadOnlyList<float> AcceptedSamples => _acceptedSamples;

    /// <summary>Gets the number of <see cref="AcceptSamples"/> calls this engine has received.</summary>
    public int AcceptSamplesCallCount { get; private set; }

    /// <summary>Gets the number of <see cref="Reset"/> calls this engine has received.</summary>
    public int ResetCallCount { get; private set; }

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
    public void Reset() => ResetCallCount++;

    /// <inheritdoc/>
    public void Dispose() => DisposeCallCount++;
}
