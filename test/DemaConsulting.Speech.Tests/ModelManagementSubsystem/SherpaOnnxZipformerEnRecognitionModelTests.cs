using DemaConsulting.Speech.ModelManagementSubsystem;
using DemaConsulting.Speech.Tests.ModelManagementSubsystem.Fakes;

namespace DemaConsulting.Speech.Tests.ModelManagementSubsystem;

/// <summary>
///     Unit tests for <see cref="SherpaOnnxZipformerEnRecognitionModel"/>, proving its declared
///     catalog metadata, download descriptor shape, archive install behavior (against a small
///     synthetic <c>.tar.bz2</c> fixture, never the real ~70 MiB production download), and
///     engine-configuration wiring - all deterministically and without any real network access
///     or native sherpa-onnx runtime.
/// </summary>
public sealed class SherpaOnnxZipformerEnRecognitionModelTests : IDisposable
{
    /// <summary>The archive's own top-level folder name, matching the real published archive.</summary>
    private const string ExtractedFolderName = "sherpa-onnx-streaming-zipformer-en-2023-06-26";

    /// <summary>A scratch root directory unique to this test instance, cleaned up on dispose.</summary>
    private readonly string _testRoot;

    /// <summary>
    ///     Initializes a fresh, unique scratch directory for each test.
    /// </summary>
    public SherpaOnnxZipformerEnRecognitionModelTests()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), "DemaConsulting.Speech.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testRoot);
    }

    /// <summary>
    ///     Deletes the scratch directory tree created for this test instance.
    /// </summary>
    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testRoot))
            {
                Directory.Delete(_testRoot, recursive: true);
            }
        }
        catch (IOException)
        {
            // Best-effort cleanup only; a leftover temp directory does not fail the test.
        }
    }

    /// <summary>
    ///     Proves this model declares its expected, stable catalog identity.
    /// </summary>
    [Fact]
    public void SherpaOnnxZipformerEnRecognitionModel_Identity_DeclaresExpectedValues()
    {
        // Arrange
        var model = new SherpaOnnxZipformerEnRecognitionModel();

        // Act & Assert
        Assert.Equal("streaming-zipformer-en-2023-06-26", model.Id);
        Assert.Equal(SherpaOnnxZipformerEnRecognitionModel.ModelId, model.Id);
        Assert.False(string.IsNullOrWhiteSpace(model.DisplayName));
        Assert.Equal(SpeechModelRole.Recognition, model.Role);
        Assert.Empty(model.Parameters);
        Assert.Equal(SpeechModelAudioTagSupport.None, model.AudioTagSupport);
    }

    /// <summary>
    ///     Proves this model declares exactly one HTTPS download file, whose checksum is a
    ///     well-formed 64-character hexadecimal SHA-256 digest and whose relative install path
    ///     names a <c>.tar.bz2</c> archive.
    /// </summary>
    [Fact]
    public void SherpaOnnxZipformerEnRecognitionModel_DownloadDescriptor_DeclaresSingleValidatedArchiveFile()
    {
        // Arrange
        var model = new SherpaOnnxZipformerEnRecognitionModel();

        // Act
        var file = Assert.Single(model.DownloadDescriptor.Files);

        // Assert
        Assert.Equal("https", file.Uri.Scheme);
        Assert.Equal(
            "https://github.com/k2-fsa/sherpa-onnx/releases/download/asr-models/sherpa-onnx-streaming-zipformer-en-2023-06-26.tar.bz2",
            file.Uri.ToString());
        Assert.Equal(64, file.Sha256Checksum.Length);
        Assert.True(file.Sha256Checksum.All(char.IsAsciiHexDigit));
        Assert.EndsWith(".tar.bz2", file.RelativeInstallPath, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves that <see cref="IRecognitionModel.AudioFormat"/> reports the model's trained
    ///     mono 16 kHz feature format.
    /// </summary>
    [Fact]
    public void SherpaOnnxZipformerEnRecognitionModel_AudioFormat_IsMono16000()
    {
        // Arrange
        IRecognitionModel model = new SherpaOnnxZipformerEnRecognitionModel();

        // Act & Assert
        Assert.Equal(16000, model.AudioFormat.SampleRate);
        Assert.Equal(1, model.AudioFormat.ChannelCount);
    }

    /// <summary>
    ///     Proves that <see cref="IRecognitionModel.CreateEngineConfig(string)"/> resolves the int8
    ///     encoder/decoder/joiner/tokens paths against the archive's extracted top-level folder,
    ///     and wires the proven feature/decoding configuration.
    /// </summary>
    [Fact]
    public void SherpaOnnxZipformerEnRecognitionModel_CreateEngineConfig_ResolvesInt8FilesAndFeatureConfig()
    {
        // Arrange
        IRecognitionModel model = new SherpaOnnxZipformerEnRecognitionModel();
        var installedDirectory = _testRoot;

        // Act
        var config = model.CreateEngineConfig(installedDirectory);

        // Assert
        var modelFolder = Path.Combine(installedDirectory, ExtractedFolderName);
        Assert.Equal(16000, config.FeatConfig.SampleRate);
        Assert.Equal(80, config.FeatConfig.FeatureDim);
        Assert.Equal(
            Path.Combine(modelFolder, "encoder-epoch-99-avg-1-chunk-16-left-128.int8.onnx"),
            config.ModelConfig.Transducer.Encoder);
        Assert.Equal(
            Path.Combine(modelFolder, "decoder-epoch-99-avg-1-chunk-16-left-128.int8.onnx"),
            config.ModelConfig.Transducer.Decoder);
        Assert.Equal(
            Path.Combine(modelFolder, "joiner-epoch-99-avg-1-chunk-16-left-128.int8.onnx"),
            config.ModelConfig.Transducer.Joiner);
        Assert.Equal(Path.Combine(modelFolder, "tokens.txt"), config.ModelConfig.Tokens);
        Assert.Equal("zipformer2", config.ModelConfig.ModelType);
        Assert.Equal("cpu", config.ModelConfig.Provider);
        Assert.Equal("greedy_search", config.DecodingMethod);
        Assert.Equal(1, config.EnableEndpoint);
    }

    /// <summary>
    ///     Proves that this model does not opt into the post-endpoint warm-up-replay feature,
    ///     inheriting <see cref="IRecognitionModel"/>'s disabled <c>0</c> default - a deliberate,
    ///     empirically justified choice (see
    ///     <c>.agent-logs/planning-nemotron-endpoint-word-loss-warmup-replay-fix-9d4b71.md</c>:
    ///     forcing the feature on for this model produced a genuine regression, duplicated text,
    ///     on a real recording). This test is a regression guard against that mechanism ever
    ///     being accidentally enabled for this model.
    /// </summary>
    [Fact]
    public void SherpaOnnxZipformerEnRecognitionModel_PostEndpointWarmupWindowMs_IsDisabledDefault()
    {
        // Arrange
        IRecognitionModel model = new SherpaOnnxZipformerEnRecognitionModel();

        // Act & Assert
        Assert.Equal(0, model.PostEndpointWarmupWindowMs);
    }

    /// <summary>
    ///     Proves that <see cref="ArgumentException"/> is thrown for a null or empty installed
    ///     directory, rather than silently building an unusable configuration.
    /// </summary>
    [Fact]
    public void SherpaOnnxZipformerEnRecognitionModel_CreateEngineConfig_EmptyDirectory_ThrowsArgumentException()
    {
        // Arrange
        IRecognitionModel model = new SherpaOnnxZipformerEnRecognitionModel();

        // Act & Assert
        Assert.Throws<ArgumentException>(() => model.CreateEngineConfig(string.Empty));
    }

    /// <summary>
    ///     Proves that <see cref="ISpeechModel.InstallAsync"/> extracts a synthetic <c>.tar.bz2</c>
    ///     archive (shaped like the real published archive: one top-level folder holding the
    ///     model's files) placed at this model's own declared relative install path, and removes
    ///     the archive afterward - never depending on any real network access or the real
    ///     ~70 MiB production download.
    /// </summary>
    [Fact]
    public async Task SherpaOnnxZipformerEnRecognitionModel_InstallAsync_SyntheticArchive_ExtractsFilesAndRemovesArchive()
    {
        // Arrange
        var model = new SherpaOnnxZipformerEnRecognitionModel();
        var relativeInstallPath = Assert.Single(model.DownloadDescriptor.Files).RelativeInstallPath;
        var tokensContent = "fake-tokens"u8.ToArray();
        var archiveBytes = TarBz2ArchiveFixtures.BuildArchiveBytes(
        [
            (ExtractedFolderName + "/tokens.txt", tokensContent),
        ]);
        var archivePath = Path.Combine(_testRoot, relativeInstallPath);
        await File.WriteAllBytesAsync(archivePath, archiveBytes, TestContext.Current.CancellationToken);

        // Act
        await model.InstallAsync(_testRoot, TestContext.Current.CancellationToken);

        // Assert
        var extractedTokensPath = Path.Combine(_testRoot, ExtractedFolderName, "tokens.txt");
        Assert.True(File.Exists(extractedTokensPath));
        Assert.Equal(
            tokensContent,
            await File.ReadAllBytesAsync(extractedTokensPath, TestContext.Current.CancellationToken));
        Assert.False(File.Exists(archivePath));
    }

    /// <summary>
    ///     Proves that <see cref="IRecognitionModel.NormalizeText(string,bool)"/> delegates to
    ///     <see cref="UppercaseTranscriptRestorer.RestoreFinal"/> for a finalized result.
    /// </summary>
    [Fact]
    public void SherpaOnnxZipformerEnRecognitionModel_NormalizeText_Final_RestoresCasingContractionsAndPunctuation()
    {
        // Arrange
        IRecognitionModel model = new SherpaOnnxZipformerEnRecognitionModel();

        // Act
        var normalized = model.NormalizeText("IM NOT SURE", isFinal: true);

        // Assert
        Assert.Equal("I'm not sure.", normalized);
    }

    /// <summary>
    ///     Proves that <see cref="IRecognitionModel.NormalizeText(string,bool)"/> delegates to
    ///     <see cref="UppercaseTranscriptRestorer.RestoreProvisional"/> for a provisional result -
    ///     cheap casing only, no contraction restoration, no terminal punctuation.
    /// </summary>
    [Fact]
    public void SherpaOnnxZipformerEnRecognitionModel_NormalizeText_Provisional_AppliesCheapCasingOnly()
    {
        // Arrange
        IRecognitionModel model = new SherpaOnnxZipformerEnRecognitionModel();

        // Act
        var normalized = model.NormalizeText("IM NOT SURE", isFinal: false);

        // Assert: no contraction restoration, no trailing period
        Assert.Equal("Im not sure", normalized);
    }
}
