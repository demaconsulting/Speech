using DemaConsulting.Speech.SynthesisSubsystem;

namespace DemaConsulting.Speech.Tests.SynthesisSubsystem.Fakes;

/// <summary>
///     Deterministic <see cref="IAnnotatedSynthesisBackend"/> test double that records plain
///     <see cref="Generate"/> calls and <see cref="GenerateAnnotated"/> calls separately, so a
///     test can prove which path the session used and which annotations it delivered, with no
///     native inference runtime.
/// </summary>
internal sealed class FakeAnnotatedSynthesisEngine : IAnnotatedSynthesisBackend
{
    /// <summary>The number of samples synthesized per character of input text.</summary>
    private const int SamplesPerCharacter = 4;

    /// <summary>Guards the recorded call lists.</summary>
    private readonly object _lock = new();

    /// <summary>Every plain <see cref="Generate"/> call, in order.</summary>
    private readonly List<(string Text, float Speed, int SpeakerId)> _generateCalls = [];

    /// <summary>Every <see cref="GenerateAnnotated"/> call, in order.</summary>
    private readonly List<(string Text, IReadOnlyList<SegmentAnnotation> Annotations, float Speed, int SpeakerId)> _annotatedCalls = [];

    /// <summary>
    ///     Initializes a new instance of the <see cref="FakeAnnotatedSynthesisEngine"/> class.
    /// </summary>
    /// <param name="sampleRate">The sample rate this engine reports. Defaults to <c>16000</c>.</param>
    public FakeAnnotatedSynthesisEngine(int sampleRate = 16000)
    {
        SampleRate = sampleRate;
    }

    /// <inheritdoc/>
    public int SampleRate { get; }

    /// <summary>Gets every plain <see cref="Generate"/> call this engine has received, in arrival order.</summary>
    public IReadOnlyList<(string Text, float Speed, int SpeakerId)> GenerateCalls
    {
        get
        {
            lock (_lock)
            {
                return [.. _generateCalls];
            }
        }
    }

    /// <summary>Gets every <see cref="GenerateAnnotated"/> call this engine has received, in arrival order.</summary>
    public IReadOnlyList<(string Text, IReadOnlyList<SegmentAnnotation> Annotations, float Speed, int SpeakerId)> AnnotatedCalls
    {
        get
        {
            lock (_lock)
            {
                return [.. _annotatedCalls];
            }
        }
    }

    /// <summary>Gets every text this engine synthesized through either path, in arrival order.</summary>
    public IReadOnlyList<string> AllTexts
    {
        get
        {
            lock (_lock)
            {
                // The session synthesizes one segment at a time, so the interleaving is recoverable
                // only through a shared ordering list.
                return [.. _order];
            }
        }
    }

    /// <summary>Every text received through either path, in arrival order.</summary>
    private readonly List<string> _order = [];

    /// <inheritdoc/>
    public BackendAudio Generate(string text, float speed, int speakerId)
    {
        lock (_lock)
        {
            _generateCalls.Add((text, speed, speakerId));
            _order.Add(text);
        }

        return CreateAudio(text);
    }

    /// <inheritdoc/>
    public BackendAudio GenerateAnnotated(string text, IReadOnlyList<SegmentAnnotation> annotations, float speed, int speakerId)
    {
        lock (_lock)
        {
            _annotatedCalls.Add((text, annotations, speed, speakerId));
            _order.Add(text);
        }

        return CreateAudio(text);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        // Nothing to release.
    }

    /// <summary>
    ///     Creates deterministic audio for a text.
    /// </summary>
    /// <param name="text">The synthesized text.</param>
    /// <returns>Constant-amplitude audio proportional to the text length.</returns>
    private BackendAudio CreateAudio(string text)
    {
        var samples = new float[text.Length * SamplesPerCharacter];
        Array.Fill(samples, 0.5f);
        return new BackendAudio(samples, SampleRate);
    }
}
