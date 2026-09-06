using DemaConsulting.Speech.ModelManagementSubsystem;
using SherpaOnnx;

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
    ///     <c>ISynthesisEngine.Generate</c> without a real multi-speaker
    ///     model.
    /// </param>
    public FakeSynthesisModel(
        string id = "fake-synthesis-model",
        SpeechModelDownloadDescriptor? downloadDescriptor = null,
        Func<IReadOnlyDictionary<string, object>?, int>? resolveSpeakerId = null)
    {
        Id = id;
        DownloadDescriptor = downloadDescriptor ?? FakeModelDescriptors.SingleFileDescriptor(id);
        _resolveSpeakerId = resolveSpeakerId;
    }

    /// <summary>The scripted <see cref="ISynthesisModel.ResolveSpeakerId"/> delegate, or <see langword="null"/>.</summary>
    private readonly Func<IReadOnlyDictionary<string, object>?, int>? _resolveSpeakerId;

    /// <inheritdoc/>
    public string Id { get; }

    /// <inheritdoc/>
    public string DisplayName => "Fake Synthesis Model";

    /// <inheritdoc/>
    public SpeechModelRole Role => SpeechModelRole.Synthesis;

    /// <inheritdoc/>
    public IReadOnlyList<ISpeechModelParameter> Parameters { get; } =
    [
        new NumericParameter("tempo", "Tempo", "Speaking rate multiplier.", 0.5, 2.0, 0.05, 1.0, "x"),
    ];

    /// <inheritdoc/>
    public SpeechModelAudioTagSupport AudioTagSupport => SpeechModelAudioTagSupport.ParameterMapped;

    /// <inheritdoc/>
    public SpeechModelDownloadDescriptor DownloadDescriptor { get; }

    /// <summary>
    ///     Builds a minimal but structurally valid VITS <see cref="OfflineTtsConfig"/> whose file
    ///     paths are resolved against the supplied directory.
    /// </summary>
    /// <remarks>
    ///     Deliberately constructs only the managed configuration struct: no native sherpa-onnx
    ///     library is loaded and no file is opened, so this fake works on any CI runner with no
    ///     model downloaded and no native runtime present.
    /// </remarks>
    /// <inheritdoc/>
    OfflineTtsConfig ISynthesisModel.CreateEngineConfig(string installedModelDirectory)
    {
        ArgumentException.ThrowIfNullOrEmpty(installedModelDirectory);

        var config = new OfflineTtsConfig();
        config.Model.Vits.Model = Path.Combine(installedModelDirectory, "model.onnx");
        config.Model.Vits.Lexicon = Path.Combine(installedModelDirectory, "lexicon.txt");
        config.Model.Vits.Tokens = Path.Combine(installedModelDirectory, "tokens.txt");
        config.Model.Vits.DataDir = Path.Combine(installedModelDirectory, "espeak-ng-data");
        return config;
    }

    /// <inheritdoc/>
    int ISynthesisModel.ResolveSpeakerId(IReadOnlyDictionary<string, object>? parameterValues) =>
        _resolveSpeakerId?.Invoke(parameterValues) ?? 0;
}
