using DemaConsulting.Speech.AudioSubsystem;
using DemaConsulting.Speech.ModelManagementSubsystem;
using DemaConsulting.Speech.SynthesisSubsystem;

namespace DemaConsulting.Speech.Text.En.Tests.Fakes;

/// <summary>
///     Test-only <see cref="IAnnotatedSynthesisBackend"/> that records every segment it receives
///     (text and annotations) and produces constant audio, so the end-to-end tests can assert
///     what a real <c>SynthesisSession</c> delivers to an annotation-aware backend.
/// </summary>
internal sealed class RecordingAnnotatedBackend : IAnnotatedSynthesisBackend
{
    /// <summary>Guards <see cref="_calls"/>.</summary>
    private readonly object _lock = new();

    /// <summary>Every call received, in arrival order.</summary>
    private readonly List<(string Text, IReadOnlyList<SegmentAnnotation> Annotations)> _calls = [];

    /// <inheritdoc/>
    public int SampleRate => 16000;

    /// <summary>
    ///     Gets a snapshot of every call received, in arrival order; a plain
    ///     <see cref="Generate"/> call is recorded with an empty annotation list.
    /// </summary>
    public IReadOnlyList<(string Text, IReadOnlyList<SegmentAnnotation> Annotations)> Calls
    {
        get
        {
            lock (_lock)
            {
                return [.. _calls];
            }
        }
    }

    /// <inheritdoc/>
    public BackendAudio Generate(string text, float speed, int speakerId) => Record(text, []);

    /// <inheritdoc/>
    public BackendAudio GenerateAnnotated(string text, IReadOnlyList<SegmentAnnotation> annotations, float speed, int speakerId) =>
        Record(text, annotations);

    /// <inheritdoc/>
    public void Dispose()
    {
        // Nothing to release.
    }

    /// <summary>Records a call and returns deterministic audio proportional to the text.</summary>
    /// <param name="text">The synthesized text.</param>
    /// <param name="annotations">The annotations received with it.</param>
    /// <returns>Constant-amplitude audio.</returns>
    private BackendAudio Record(string text, IReadOnlyList<SegmentAnnotation> annotations)
    {
        lock (_lock)
        {
            _calls.Add((text, annotations));
        }

        var samples = new float[Math.Max(1, text.Length) * 4];
        Array.Fill(samples, 0.5f);
        return new BackendAudio(samples, SampleRate);
    }
}

/// <summary>
///     Test-only synthesis model whose text analyzer is the one under test and whose backend is
///     a <see cref="RecordingAnnotatedBackend"/>; it uses the library's default capability
///     profile and identity text normalization, exactly like a minimal real model package.
/// </summary>
internal sealed class AnalyzingSynthesisModel : ISynthesisModel
{
    /// <summary>The analyzer the model supplies through the text-analysis seam, or null for none.</summary>
    private readonly ITextAnalyzer? _analyzer;

    /// <summary>The backend <see cref="CreateBackend"/> returns.</summary>
    private readonly ISynthesisBackend _backend;

    /// <summary>
    ///     Initializes a new instance.
    /// </summary>
    /// <param name="analyzer">The analyzer to supply, or <see langword="null"/> for a model without text analysis.</param>
    /// <param name="backend">The backend to return from <see cref="CreateBackend"/>.</param>
    public AnalyzingSynthesisModel(ITextAnalyzer? analyzer, ISynthesisBackend backend)
    {
        _analyzer = analyzer;
        _backend = backend;
    }

    /// <inheritdoc/>
    public string Id => "text-en-test-model";

    /// <inheritdoc/>
    public string DisplayName => "Text.En Test Model";

    /// <inheritdoc/>
    public SpeechModelRole Role => SpeechModelRole.Synthesis;

    /// <inheritdoc/>
    public IReadOnlyList<ISpeechModelParameter> Parameters => [];

    /// <inheritdoc/>
    public SpeechModelAudioTagSupport AudioTagSupport => SpeechModelAudioTagSupport.None;

    /// <inheritdoc/>
    public SpeechModelDownloadDescriptor DownloadDescriptor { get; } = new(
    [
        new SpeechModelDownloadFile(new Uri("https://example.test/model.bin"), new string('0', 64), "model.bin"),
    ]);

    /// <inheritdoc/>
    public AudioFormat PreferredAudioFormat => AudioFormat.Mono(16000);

    /// <inheritdoc/>
    public ISynthesisBackend CreateBackend(string installedModelDirectory) => _backend;

    /// <inheritdoc/>
    ITextAnalyzer? ISynthesisModel.TextAnalyzer => _analyzer;
}
