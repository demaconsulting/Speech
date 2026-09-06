using DemaConsulting.Speech.SynthesisSubsystem;

namespace DemaConsulting.Speech.Tests.SynthesisSubsystem.Fakes;

/// <summary>
///     Deterministic <see cref="ISynthesisEngine"/> test double that records every call it
///     receives and returns scripted (or generated) audio, so the synthesizer's chunking,
///     pipelining, and playback behavior can be verified with no native sherpa-onnx runtime and
///     no downloaded model.
/// </summary>
internal sealed class FakeSynthesisEngine : ISynthesisEngine
{
    /// <summary>The exception to throw from <see cref="Generate"/>, when one was scripted.</summary>
    private readonly Exception? _generateException;

    /// <summary>Every <see cref="Generate"/> call this engine has received, in order.</summary>
    private readonly List<(string Text, float Speed, int SpeakerId)> _generateCalls = [];

    /// <summary>The number of samples <see cref="Generate"/> synthesizes per character of input text.</summary>
    private readonly int _samplesPerCharacter;

    /// <summary>An artificial delay applied inside <see cref="Generate"/>, widening the window in which a concurrency violation would be observable.</summary>
    private readonly TimeSpan _simulatedGenerateDelay;

    /// <summary>The number of <see cref="Generate"/> calls currently in flight, guarded by <see cref="_concurrencyLock"/>.</summary>
    private int _inFlightGenerateCalls;

    /// <summary>Guards <see cref="_inFlightGenerateCalls"/> and <see cref="MaxConcurrentGenerateCalls"/>.</summary>
    private readonly object _concurrencyLock = new();

    /// <summary>
    ///     Initializes a new instance of the <see cref="FakeSynthesisEngine"/> class.
    /// </summary>
    /// <param name="sampleRate">The sample rate this engine reports. Defaults to <c>16000</c>.</param>
    /// <param name="samplesPerCharacter">
    ///     The number of samples <see cref="Generate"/> synthesizes per character of input text,
    ///     so a fixed, predictable amount of audio is produced without a native engine. Defaults
    ///     to <c>4</c>.
    /// </param>
    /// <param name="generateException">
    ///     An exception for <see cref="Generate"/> to throw, or <see langword="null"/> to
    ///     generate normally. Used to prove the synthesizer contains engine faults.
    /// </param>
    /// <param name="simulatedGenerateDelay">
    ///     An artificial delay <see cref="Generate"/> sleeps for before returning, widening the
    ///     window in which an accidental concurrent call would be observable via
    ///     <see cref="MaxConcurrentGenerateCalls"/>. Defaults to <see cref="TimeSpan.Zero"/> (no
    ///     delay).
    /// </param>
    public FakeSynthesisEngine(
        int sampleRate = 16000,
        int samplesPerCharacter = 4,
        Exception? generateException = null,
        TimeSpan simulatedGenerateDelay = default)
    {
        SampleRate = sampleRate;
        _samplesPerCharacter = samplesPerCharacter;
        _generateException = generateException;
        _simulatedGenerateDelay = simulatedGenerateDelay;
    }

    /// <inheritdoc/>
    public int SampleRate { get; }

    /// <summary>Gets every <see cref="Generate"/> call this engine has received, in arrival order.</summary>
    public IReadOnlyList<(string Text, float Speed, int SpeakerId)> GenerateCalls => _generateCalls;

    /// <summary>Gets the number of <see cref="Dispose"/> calls this engine has received.</summary>
    public int DisposeCallCount { get; private set; }

    /// <summary>
    ///     Gets the highest number of <see cref="Generate"/> calls this engine ever observed
    ///     overlapping in flight at once, used to prove the synthesis pipeline never calls
    ///     <see cref="Generate"/> concurrently on the same engine instance.
    /// </summary>
    public int MaxConcurrentGenerateCalls { get; private set; }

    /// <summary>Gets a value indicating whether this engine has been disposed.</summary>
    public bool IsDisposed { get; private set; }

    /// <inheritdoc/>
    public EngineAudio Generate(string text, float speed, int speakerId)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        lock (_concurrencyLock)
        {
            _inFlightGenerateCalls++;
            if (_inFlightGenerateCalls > MaxConcurrentGenerateCalls)
            {
                MaxConcurrentGenerateCalls = _inFlightGenerateCalls;
            }
        }

        try
        {
            _generateCalls.Add((text, speed, speakerId));

            if (_simulatedGenerateDelay > TimeSpan.Zero)
            {
                Thread.Sleep(_simulatedGenerateDelay);
            }

            if (_generateException is not null)
            {
                throw _generateException;
            }

            var samples = new float[text.Length * _samplesPerCharacter];
            for (var i = 0; i < samples.Length; i++)
            {
                samples[i] = 0.5f;
            }

            return new EngineAudio(samples, SampleRate);
        }
        finally
        {
            lock (_concurrencyLock)
            {
                _inFlightGenerateCalls--;
            }
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        IsDisposed = true;
        DisposeCallCount++;
    }
}
