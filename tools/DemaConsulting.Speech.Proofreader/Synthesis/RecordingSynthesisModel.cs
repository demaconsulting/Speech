using DemaConsulting.Speech.AudioSubsystem;
using DemaConsulting.Speech.ModelManagementSubsystem;
using DemaConsulting.Speech.SynthesisSubsystem;

namespace DemaConsulting.Speech.Proofreader.Synthesis;

/// <summary>
///     The text one synthesis backend call actually received, recorded for the diagnostics pane.
/// </summary>
/// <param name="Text">The segment text after normalization, audio-tag handling, and the model's capability profile.</param>
/// <param name="Annotations">The Text.En annotations that came with the segment (empty for a plain call).</param>
/// <param name="SpeakerId">The speaker id the backend was asked for.</param>
/// <param name="Speed">The speed ratio the backend was asked for.</param>
internal sealed record RecordedSegment(string Text, IReadOnlyList<SegmentAnnotation> Annotations, int SpeakerId, float Speed);

/// <summary>
///     Decorates a synthesis model so every text the library hands to its backend is recorded.
/// </summary>
/// <remarks>
///     The library normalizes, tags, analyzes, and chunks text before the backend ever sees it, and
///     that final form is what a phonemizer consumes. Recording it at the backend seam shows the
///     listener exactly what the voice was given without changing any library code: the factory only
///     needs an <see cref="ISynthesisModel"/>, so this wrapper delegates every member (including the
///     interface's default members, which must be redeclared or they would silently fall back to
///     their defaults) and returns a recording backend from <see cref="CreateBackend"/>. One
///     instance serves one engine; the owner serializes syntheses, so a capture window
///     (<see cref="BeginCapture"/> to <see cref="EndCapture"/>) holds exactly one sentence's calls.
///     Thread-safe for the recorder; the wrapped model's own thread safety applies to the rest.
/// </remarks>
internal sealed class RecordingSynthesisModel : ISynthesisModel
{
    /// <summary>The real model every member delegates to.</summary>
    private readonly ISynthesisModel _inner;

    /// <summary>Guards <see cref="_captured"/>.</summary>
    private readonly object _sync = new();

    /// <summary>The calls recorded since the last <see cref="BeginCapture"/>.</summary>
    private List<RecordedSegment> _captured = [];

    /// <summary>
    ///     Initializes a new instance of the <see cref="RecordingSynthesisModel"/> class.
    /// </summary>
    /// <param name="inner">The model to wrap. Must not be <see langword="null"/>.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="inner"/> is <see langword="null"/>.</exception>
    public RecordingSynthesisModel(ISynthesisModel inner)
    {
        ArgumentNullException.ThrowIfNull(inner);
        _inner = inner;
    }

    /// <inheritdoc/>
    public string Id => _inner.Id;

    /// <inheritdoc/>
    public string DisplayName => _inner.DisplayName;

    /// <inheritdoc/>
    public SpeechModelRole Role => _inner.Role;

    /// <inheritdoc/>
    public IReadOnlyList<ISpeechModelParameter> Parameters => _inner.Parameters;

    /// <inheritdoc/>
    public SpeechModelAudioTagSupport AudioTagSupport => _inner.AudioTagSupport;

    /// <inheritdoc/>
    public SpeechModelDownloadDescriptor DownloadDescriptor => _inner.DownloadDescriptor;

    /// <inheritdoc/>
    public AudioFormat PreferredAudioFormat => _inner.PreferredAudioFormat;

    /// <inheritdoc/>
    public string LicenseName => _inner.LicenseName;

    /// <inheritdoc/>
    public Uri? LicenseUrl => _inner.LicenseUrl;

    /// <inheritdoc/>
    public IModelCapabilityProfile CapabilityProfile => _inner.CapabilityProfile;

    /// <inheritdoc/>
    public ITextAnalyzer? TextAnalyzer => _inner.TextAnalyzer;

    /// <inheritdoc/>
    public Task InstallAsync(string stagedFilesDirectory, CancellationToken cancellationToken = default) =>
        _inner.InstallAsync(stagedFilesDirectory, cancellationToken);

    /// <inheritdoc/>
    public string NormalizeText(string text) => _inner.NormalizeText(text);

    /// <inheritdoc/>
    public int ResolveSpeakerId(IReadOnlyDictionary<string, object>? parameterValues) =>
        _inner.ResolveSpeakerId(parameterValues);

    /// <inheritdoc/>
    public ISynthesisBackend CreateBackend(string installedModelDirectory) =>
        new RecordingBackend(_inner.CreateBackend(installedModelDirectory), this);

    /// <summary>
    ///     Starts a capture window, discarding anything recorded before.
    /// </summary>
    public void BeginCapture()
    {
        lock (_sync)
        {
            _captured = [];
        }
    }

    /// <summary>
    ///     Ends the capture window.
    /// </summary>
    /// <returns>The backend calls recorded since <see cref="BeginCapture"/>, in call order.</returns>
    public IReadOnlyList<RecordedSegment> EndCapture()
    {
        lock (_sync)
        {
            var result = _captured;
            _captured = [];
            return result;
        }
    }

    /// <summary>
    ///     Appends one backend call to the current capture window.
    /// </summary>
    /// <param name="segment">The call to record.</param>
    private void Record(RecordedSegment segment)
    {
        lock (_sync)
        {
            _captured.Add(segment);
        }
    }

    /// <summary>
    ///     Forwards every call to the real backend after recording it.
    /// </summary>
    /// <remarks>
    ///     Always presents the annotated interface; for a backend without one the annotated call
    ///     falls back to the plain call, which is exactly what the library does for such backends.
    /// </remarks>
    private sealed class RecordingBackend(ISynthesisBackend inner, RecordingSynthesisModel owner) : IAnnotatedSynthesisBackend
    {
        /// <inheritdoc/>
        public int SampleRate => inner.SampleRate;

        /// <inheritdoc/>
        public BackendAudio Generate(string text, float speed, int speakerId)
        {
            owner.Record(new RecordedSegment(text, [], speakerId, speed));
            return inner.Generate(text, speed, speakerId);
        }

        /// <inheritdoc/>
        public BackendAudio GenerateAnnotated(string text, IReadOnlyList<SegmentAnnotation> annotations, float speed, int speakerId)
        {
            owner.Record(new RecordedSegment(text, annotations, speakerId, speed));
            return inner is IAnnotatedSynthesisBackend annotated
                ? annotated.GenerateAnnotated(text, annotations, speed, speakerId)
                : inner.Generate(text, speed, speakerId);
        }

        /// <inheritdoc/>
        public void Dispose() => inner.Dispose();
    }
}
