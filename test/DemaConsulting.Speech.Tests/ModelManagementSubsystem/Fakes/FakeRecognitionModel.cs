using DemaConsulting.Speech.AudioSubsystem;
using DemaConsulting.Speech.ModelManagementSubsystem;
using SherpaOnnx;

namespace DemaConsulting.Speech.Tests.ModelManagementSubsystem.Fakes;

/// <summary>
///     Test-only <see cref="IRecognitionModel"/> implementation exercising a declared parameter
///     of every descriptor kind, used by <c>SpeechModelContractTests</c>,
///     <c>SpeechModelDescriptorTests</c>, and <c>SpeechModelCatalogTests</c> without depending on
///     any real, production model class.
/// </summary>
public sealed class FakeRecognitionModel : IRecognitionModel
{
    /// <summary>
    ///     Whether this instance was constructed to declare a zip-archive download payload and
    ///     override <see cref="InstallAsync"/> to unpack it.
    /// </summary>
    private readonly bool _useZipArchivePayload;

    /// <summary>
    ///     The engine input rate this fake declares through
    ///     <see cref="IRecognitionModel.AudioFormat"/>.
    /// </summary>
    private readonly int _sampleRate;

    /// <summary>
    ///     The injectable <see cref="IRecognitionModel.NormalizeText(string,bool)"/> implementation,
    ///     or <see langword="null"/> to use the interface's identity default.
    /// </summary>
    private readonly Func<string, bool, string>? _normalizeText;

    /// <summary>
    ///     Initializes a new instance of the <see cref="FakeRecognitionModel"/> class.
    /// </summary>
    /// <param name="id">The model identifier. Defaults to <c>"fake-recognition-model"</c>.</param>
    /// <param name="downloadDescriptor">
    ///     The download descriptor this model declares, or <see langword="null"/> to build a
    ///     single-file descriptor with a placeholder URI/checksum (never dereferenced unless a
    ///     test actually downloads it), or a zip-archive descriptor when
    ///     <paramref name="useZipArchivePayload"/> is <see langword="true"/>.
    /// </param>
    /// <param name="useZipArchivePayload">
    ///     When <see langword="true"/>, this model declares a zip-archive
    ///     <see cref="DownloadDescriptor"/> (via <see cref="FakeModelDescriptors.ZipArchiveDescriptor"/>,
    ///     unless <paramref name="downloadDescriptor"/> overrides it) and overrides
    ///     <see cref="InstallAsync"/> to extract that archive's entries into the staging directory
    ///     and remove the archive file. Defaults to <see langword="false"/>, preserving every
    ///     existing call site's single-file, default-<see cref="InstallAsync"/> behavior.
    /// </param>
    /// <param name="sampleRate">
    ///     The engine input rate, in Hz, this model declares. Defaults to <c>16000</c>, the rate
    ///     used by the common streaming sherpa-onnx models.
    /// </param>
    /// <param name="normalizeText">
    ///     An optional injectable <see cref="IRecognitionModel.NormalizeText(string,bool)"/>
    ///     implementation (given the raw text and <c>isFinal</c>, returns the normalized text),
    ///     or <see langword="null"/> (the default) to use the interface's identity default,
    ///     preserving every existing call site's pass-through behavior.
    /// </param>
    public FakeRecognitionModel(
        string id = "fake-recognition-model",
        SpeechModelDownloadDescriptor? downloadDescriptor = null,
        bool useZipArchivePayload = false,
        int sampleRate = 16000,
        Func<string, bool, string>? normalizeText = null)
    {
        Id = id;
        _useZipArchivePayload = useZipArchivePayload;
        _sampleRate = sampleRate;
        _normalizeText = normalizeText;
        DownloadDescriptor = downloadDescriptor ?? (useZipArchivePayload
            ? FakeModelDescriptors.ZipArchiveDescriptor(id)
            : FakeModelDescriptors.SingleFileDescriptor(id));
    }

    /// <inheritdoc/>
    public string Id { get; }

    /// <inheritdoc/>
    public string DisplayName => "Fake Recognition Model";

    /// <inheritdoc/>
    public SpeechModelRole Role => SpeechModelRole.Recognition;

    /// <inheritdoc/>
    public IReadOnlyList<ISpeechModelParameter> Parameters { get; } =
    [
        new NumericParameter("sensitivity", "Sensitivity", "Microphone input sensitivity.", new NumericParameterBounds(0, 1, 0.1, 0.5)),
        new ChoiceParameter(
            "language",
            "Language",
            "Recognition language.",
            [new ChoiceParameterOption("en-us", "English (US)"), new ChoiceParameterOption("en-gb", "English (UK)")],
            "en-us"),
        new BooleanParameter("denoise", "Denoise Input", "Applies noise suppression before recognition.", true),
    ];

    /// <inheritdoc/>
    public SpeechModelAudioTagSupport AudioTagSupport => SpeechModelAudioTagSupport.None;

    /// <inheritdoc/>
    public SpeechModelDownloadDescriptor DownloadDescriptor { get; }

    /// <inheritdoc/>
    AudioFormat IRecognitionModel.AudioFormat => AudioFormat.Mono(_sampleRate);

    /// <summary>
    ///     Delegates to the injected <c>normalizeText</c> callback when one was supplied,
    ///     otherwise falls through to the interface's identity default so every existing call
    ///     site keeps its pass-through behavior unchanged.
    /// </summary>
    /// <inheritdoc/>
    string IRecognitionModel.NormalizeText(string text, bool isFinal) =>
        _normalizeText is null ? ((IRecognitionModel)this).NormalizeText(text) : _normalizeText(text, isFinal);

    /// <summary>
    ///     Builds a minimal but structurally valid <see cref="OnlineRecognizerConfig"/> whose
    ///     feature configuration carries this fake's declared sample rate and whose transducer
    ///     file paths are resolved against the supplied directory.
    /// </summary>
    /// <remarks>
    ///     Deliberately constructs only the managed configuration struct: no native sherpa-onnx
    ///     library is loaded and no file is opened, so this fake works on any CI runner with no
    ///     model downloaded and no native runtime present.
    /// </remarks>
    /// <inheritdoc/>
    OnlineRecognizerConfig IRecognitionModel.CreateEngineConfig(string installedModelDirectory)
    {
        ArgumentException.ThrowIfNullOrEmpty(installedModelDirectory);

        var config = new OnlineRecognizerConfig();
        config.FeatConfig.SampleRate = _sampleRate;
        config.ModelConfig.Tokens = Path.Combine(installedModelDirectory, "tokens.txt");
        config.ModelConfig.Transducer.Encoder = Path.Combine(installedModelDirectory, "encoder.onnx");
        config.ModelConfig.Transducer.Decoder = Path.Combine(installedModelDirectory, "decoder.onnx");
        config.ModelConfig.Transducer.Joiner = Path.Combine(installedModelDirectory, "joiner.onnx");
        return config;
    }

    /// <summary>
    ///     When constructed with <c>useZipArchivePayload: true</c>, extracts the downloaded zip
    ///     archive (found at <see cref="FakeModelDescriptors.ZipArchiveRelativeInstallPath"/>
    ///     within <paramref name="stagedFilesDirectory"/>) into that same directory, then deletes
    ///     the archive file, leaving only its extracted entries. Otherwise relies on the
    ///     <see cref="ISpeechModel"/> default no-op implementation entirely (this override is not
    ///     called at all).
    /// </summary>
    /// <inheritdoc/>
    public Task InstallAsync(string stagedFilesDirectory, CancellationToken cancellationToken = default)
    {
        if (!_useZipArchivePayload)
        {
            return Task.CompletedTask;
        }

        var archivePath = Path.Combine(stagedFilesDirectory, FakeModelDescriptors.ZipArchiveRelativeInstallPath);
        System.IO.Compression.ZipFile.ExtractToDirectory(archivePath, stagedFilesDirectory);
        File.Delete(archivePath);
        return Task.CompletedTask;
    }
}
