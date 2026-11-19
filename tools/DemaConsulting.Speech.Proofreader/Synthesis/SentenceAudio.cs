using DemaConsulting.Speech.SynthesisSubsystem;

namespace DemaConsulting.Speech.Proofreader.Synthesis;

/// <summary>
///     The result of synthesizing one sentence with one model configuration.
/// </summary>
/// <remarks>
///     Either audio (<see cref="Speech"/>) together with what the backend consumed
///     (<see cref="Segments"/>), or an honest <see cref="Error"/> explaining why there is none, such
///     as a model that is not installed or a native runtime that is missing. Immutable.
/// </remarks>
internal sealed class SentenceAudio
{
    /// <summary>
    ///     Initializes a successful result.
    /// </summary>
    /// <param name="speech">The synthesized speech segments. Must not be <see langword="null"/>.</param>
    /// <param name="segments">The backend calls recorded while synthesizing. Must not be <see langword="null"/>.</param>
    /// <exception cref="ArgumentNullException">Thrown when an argument is <see langword="null"/>.</exception>
    public SentenceAudio(IReadOnlyList<SynthesizedSpeech> speech, IReadOnlyList<RecordedSegment> segments)
    {
        ArgumentNullException.ThrowIfNull(speech);
        ArgumentNullException.ThrowIfNull(segments);

        Speech = speech;
        Segments = segments;
    }

    /// <summary>
    ///     Initializes a failed result.
    /// </summary>
    /// <param name="error">The reason there is no audio. Must not be empty.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="error"/> is empty.</exception>
    private SentenceAudio(string error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);

        Speech = [];
        Segments = [];
        Error = error;
    }

    /// <summary>Gets the synthesized speech segments (empty on failure).</summary>
    public IReadOnlyList<SynthesizedSpeech> Speech { get; }

    /// <summary>Gets what the backend consumed (empty on failure).</summary>
    public IReadOnlyList<RecordedSegment> Segments { get; }

    /// <summary>Gets the reason there is no audio, or <see langword="null"/> on success.</summary>
    public string? Error { get; }

    /// <summary>Gets a value indicating whether the synthesis succeeded.</summary>
    public bool IsSuccess => Error is null;

    /// <summary>
    ///     Creates a failed result.
    /// </summary>
    /// <param name="error">The reason there is no audio.</param>
    /// <returns>A result whose <see cref="IsSuccess"/> is <see langword="false"/>.</returns>
    public static SentenceAudio Failed(string error) => new(error);
}
