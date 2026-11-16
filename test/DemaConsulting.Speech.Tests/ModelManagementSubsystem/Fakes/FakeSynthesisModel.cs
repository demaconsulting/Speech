using DemaConsulting.Speech.AudioSubsystem;
using DemaConsulting.Speech.ModelManagementSubsystem;
using DemaConsulting.Speech.SynthesisSubsystem;
using DemaConsulting.Speech.Tests.SynthesisSubsystem.Fakes;

namespace DemaConsulting.Speech.Tests.ModelManagementSubsystem.Fakes;

/// <summary>
///     Test-only <see cref="ISynthesisModel"/> implementation exercising a declared
///     <see cref="SpeechModelAudioTagSupport.ParameterMapped"/> declaration and a single
///     numeric parameter, used by <c>SpeechModelContractTests</c>, <c>SpeechModelDescriptorTests</c>,
///     and <c>SpeechModelCatalogTests</c> without depending on any real, production model class.
/// </summary>
public sealed class FakeSynthesisModel : ISynthesisModel
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="FakeSynthesisModel"/> class.
    /// </summary>
    /// <param name="id">The model identifier. Defaults to <c>"fake-synthesis-model"</c>.</param>
    /// <param name="downloadDescriptor">
    ///     The download descriptor this model declares, or <see langword="null"/> to build a
    ///     single-file descriptor with a placeholder URI/checksum (never dereferenced unless a
    ///     test actually downloads it).
    /// </param>
    /// <param name="resolveSpeakerId">
    ///     The function <see cref="ISynthesisModel.ResolveSpeakerId"/> delegates to, or
    ///     <see langword="null"/> to keep the default hook's behavior (always <c>0</c>). Lets
    ///     tests prove a resolved, non-zero speaker id reaches
    ///     <c>ISynthesisBackend.Generate</c> without a real multi-speaker
    ///     model.
    /// </param>
    /// <param name="parameters">
    ///     The declared parameters, or <see langword="null"/> to keep the default single
    ///     numeric "tempo" parameter.
    /// </param>
    /// <param name="textAnalyzer">
    ///     The analyzer <see cref="ISynthesisModel.TextAnalyzer"/> returns, or
    ///     <see langword="null"/> to keep the default hook's behavior (no analyzer).
    /// </param>
    /// <param name="createBackend">
    ///     Creates the backend <see cref="ISynthesisModel.CreateBackend"/> returns, or
    ///     <see langword="null"/> for a fresh <see cref="FakeSynthesisEngine"/>.
    /// </param>
    public FakeSynthesisModel(
        string id = "fake-synthesis-model",
        SpeechModelDownloadDescriptor? downloadDescriptor = null,
        Func<IReadOnlyDictionary<string, object>?, int>? resolveSpeakerId = null,
        IReadOnlyList<ISpeechModelParameter>? parameters = null,
        ITextAnalyzer? textAnalyzer = null,
        Func<ISynthesisBackend>? createBackend = null)
    {
        Id = id;
        DownloadDescriptor = downloadDescriptor ?? FakeModelDescriptors.SingleFileDescriptor(id);
        _resolveSpeakerId = resolveSpeakerId;
        _textAnalyzer = textAnalyzer;
        _createBackend = createBackend;
        if (parameters is not null)
        {
            Parameters = parameters;
        }
    }

    /// <summary>The scripted <see cref="ISynthesisModel.ResolveSpeakerId"/> delegate, or <see langword="null"/>.</summary>
    private readonly Func<IReadOnlyDictionary<string, object>?, int>? _resolveSpeakerId;

    /// <summary>The analyzer <see cref="ISynthesisModel.TextAnalyzer"/> returns, or <see langword="null"/>.</summary>
    private readonly ITextAnalyzer? _textAnalyzer;

    /// <summary>The scripted backend factory, or <see langword="null"/> for the default fake engine.</summary>
    private readonly Func<ISynthesisBackend>? _createBackend;

    /// <inheritdoc/>
    public string Id { get; }

    /// <inheritdoc/>
    public string DisplayName => "Fake Synthesis Model";

    /// <inheritdoc/>
    public SpeechModelRole Role => SpeechModelRole.Synthesis;

    /// <inheritdoc/>
    public IReadOnlyList<ISpeechModelParameter> Parameters { get; } =
    [
        new NumericParameter("tempo", "Tempo", "Speaking rate multiplier.", new NumericParameterBounds(0.5, 2.0, 0.05, 1.0), "x"),
    ];

    /// <inheritdoc/>
    public SpeechModelAudioTagSupport AudioTagSupport => SpeechModelAudioTagSupport.ParameterMapped;

    /// <inheritdoc/>
    public SpeechModelDownloadDescriptor DownloadDescriptor { get; }

    /// <inheritdoc/>
    public AudioFormat PreferredAudioFormat => AudioFormat.Mono(24000);

    /// <summary>
    ///     Returns a fresh <see cref="FakeSynthesisEngine"/> backend, deterministic and requiring
    ///     no native sherpa-onnx runtime and no downloaded model, so this fake works on any CI
    ///     runner.
    /// </summary>
    /// <inheritdoc/>
    ISynthesisBackend ISynthesisModel.CreateBackend(string installedModelDirectory)
    {
        ArgumentException.ThrowIfNullOrEmpty(installedModelDirectory);

        return _createBackend?.Invoke() ?? new FakeSynthesisEngine();
    }

    /// <inheritdoc/>
    ITextAnalyzer? ISynthesisModel.TextAnalyzer => _textAnalyzer;

    /// <inheritdoc/>
    int ISynthesisModel.ResolveSpeakerId(IReadOnlyDictionary<string, object>? parameterValues) =>
        _resolveSpeakerId?.Invoke(parameterValues) ?? 0;
}
