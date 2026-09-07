using DemaConsulting.Speech.ModelManagementSubsystem;
using DemaConsulting.Speech.Tests.ModelManagementSubsystem.Fakes;

namespace DemaConsulting.Speech.Tests.ModelManagementSubsystem;

/// <summary>
///     Unit tests for <see cref="SherpaOnnxNemotronStreamingEnRecognitionModel"/>, proving its
///     declared catalog metadata, download descriptor shape, archive install behavior (against a
///     small synthetic <c>.tar.bz2</c> fixture, never the real ~442 MiB production download), and
///     engine-configuration wiring - deliberately mirroring
///     <c>SherpaOnnxZipformerEnRecognitionModelTests</c>'s shape, since both models share the
///     same <c>OnlineModelConfig.Transducer</c> configuration surface.
/// </summary>
public sealed class SherpaOnnxNemotronStreamingEnRecognitionModelTests : IDisposable
{
    /// <summary>The archive's own top-level folder name, matching the real published archive.</summary>
    private const string ExtractedFolderName = "sherpa-onnx-nemotron-speech-streaming-en-0.6b-560ms-int8-2026-04-25";

    /// <summary>A scratch root directory unique to this test instance, cleaned up on dispose.</summary>
    private readonly string _testRoot;

    /// <summary>
    ///     Initializes a fresh, unique scratch directory for each test.
    /// </summary>
    public SherpaOnnxNemotronStreamingEnRecognitionModelTests()
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
    ///     Proves this model declares its expected, stable catalog identity, and that its
    ///     display name visibly distinguishes it as NVIDIA Open Model License content, unlike the
    ///     Apache-2.0-licensed Zipformer model.
    /// </summary>
    [Fact]
    public void SherpaOnnxNemotronStreamingEnRecognitionModel_Identity_DeclaresExpectedValues()
    {
        // Arrange
        var model = new SherpaOnnxNemotronStreamingEnRecognitionModel();

        // Act & Assert
        Assert.Equal("nemotron-speech-streaming-en-0.6b-560ms-int8-2026-04-25", model.Id);
        Assert.Equal(SherpaOnnxNemotronStreamingEnRecognitionModel.ModelId, model.Id);
        Assert.Contains("NVIDIA Open Model License", model.DisplayName, StringComparison.Ordinal);
        Assert.Equal(SpeechModelRole.Recognition, model.Role);
        Assert.Empty(model.Parameters);
        Assert.Equal(SpeechModelAudioTagSupport.None, model.AudioTagSupport);
    }

    /// <summary>
    ///     Proves this model declares exactly one HTTPS download file, whose checksum is a
    ///     well-formed 64-character hexadecimal SHA-256 digest and whose relative install path
    ///     names a <c>.tar.bz2</c> archive, fetched directly from sherpa-onnx's official GitHub
    ///     Releases mirror (never a bundled/redistributed copy of the NVIDIA weights).
    /// </summary>
    [Fact]
    public void SherpaOnnxNemotronStreamingEnRecognitionModel_DownloadDescriptor_DeclaresSingleValidatedArchiveFile()
    {
        // Arrange
        var model = new SherpaOnnxNemotronStreamingEnRecognitionModel();

        // Act
        var file = Assert.Single(model.DownloadDescriptor.Files);

        // Assert
        Assert.Equal("https", file.Uri.Scheme);
        Assert.Equal("github.com", file.Uri.Host);
        Assert.Equal(
            "https://github.com/k2-fsa/sherpa-onnx/releases/download/asr-models/sherpa-onnx-nemotron-speech-streaming-en-0.6b-560ms-int8-2026-04-25.tar.bz2",
            file.Uri.ToString());
        Assert.Equal(64, file.Sha256Checksum.Length);
        Assert.True(file.Sha256Checksum.All(char.IsAsciiHexDigit));
        Assert.EndsWith(".tar.bz2", file.RelativeInstallPath, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves that <see cref="IRecognitionModel.AudioFormat"/> reports the model's declared
    ///     mono 16 kHz feature format.
    /// </summary>
    [Fact]
    public void SherpaOnnxNemotronStreamingEnRecognitionModel_AudioFormat_IsMono16000()
    {
        // Arrange
        IRecognitionModel model = new SherpaOnnxNemotronStreamingEnRecognitionModel();

        // Act & Assert
        Assert.Equal(16000, model.AudioFormat.SampleRate);
        Assert.Equal(1, model.AudioFormat.ChannelCount);
    }

    /// <summary>
    ///     Proves that <see cref="IRecognitionModel.CreateEngineConfig(string)"/> resolves the int8
    ///     encoder/decoder/joiner/tokens paths against the archive's extracted top-level folder,
    ///     using the exact same <c>Transducer</c> configuration shape as
    ///     <see cref="SherpaOnnxZipformerEnRecognitionModel"/> - proving the two classes really
    ///     do share one config surface, as the phase 7 planning report's native-dispatch tracing
    ///     concluded.
    /// </summary>
    [Fact]
    public void SherpaOnnxNemotronStreamingEnRecognitionModel_CreateEngineConfig_ResolvesInt8FilesAndFeatureConfig()
    {
        // Arrange
        IRecognitionModel model = new SherpaOnnxNemotronStreamingEnRecognitionModel();
        var installedDirectory = _testRoot;

        // Act
        var config = model.CreateEngineConfig(installedDirectory);

        // Assert
        var modelFolder = Path.Combine(installedDirectory, ExtractedFolderName);
        Assert.Equal(16000, config.FeatConfig.SampleRate);
        Assert.Equal(Path.Combine(modelFolder, "encoder.int8.onnx"), config.ModelConfig.Transducer.Encoder);
        Assert.Equal(Path.Combine(modelFolder, "decoder.int8.onnx"), config.ModelConfig.Transducer.Decoder);
        Assert.Equal(Path.Combine(modelFolder, "joiner.int8.onnx"), config.ModelConfig.Transducer.Joiner);
        Assert.Equal(Path.Combine(modelFolder, "tokens.txt"), config.ModelConfig.Tokens);
        Assert.Equal("cpu", config.ModelConfig.Provider);
        Assert.Equal("greedy_search", config.DecodingMethod);
        Assert.Equal(1, config.EnableEndpoint);
    }

    /// <summary>
    ///     Proves that this model deliberately leaves <c>ModelConfig.ModelType</c> unset (empty),
    ///     since sherpa-onnx's native dispatch chooses the NeMo-cache-aware decoding path from
    ///     the decoder file's own metadata, not from this field, on the CPU provider path this
    ///     project uses.
    /// </summary>
    [Fact]
    public void SherpaOnnxNemotronStreamingEnRecognitionModel_CreateEngineConfig_ModelTypeIsUnset()
    {
        // Arrange
        IRecognitionModel model = new SherpaOnnxNemotronStreamingEnRecognitionModel();

        // Act
        var config = model.CreateEngineConfig(_testRoot);

        // Assert
        Assert.True(string.IsNullOrEmpty(config.ModelConfig.ModelType));
    }

    /// <summary>
    ///     Proves that this model opts into the post-endpoint warm-up-replay feature at its
    ///     empirically validated 800ms value (see
    ///     <c>.agent-logs/planning-nemotron-endpoint-word-loss-warmup-replay-fix-9d4b71.md</c>),
    ///     unlike <see cref="SherpaOnnxZipformerEnRecognitionModel"/>, which keeps the disabled
    ///     default.
    /// </summary>
    [Fact]
    public void SherpaOnnxNemotronStreamingEnRecognitionModel_PostEndpointWarmupWindowMs_Is800()
    {
        // Arrange
        IRecognitionModel model = new SherpaOnnxNemotronStreamingEnRecognitionModel();

        // Act & Assert
        Assert.Equal(800, model.PostEndpointWarmupWindowMs);
    }

    /// <summary>
    ///     Proves that <see cref="ArgumentException"/> is thrown for a null or empty installed
    ///     directory, rather than silently building an unusable configuration.
    /// </summary>
    [Fact]
    public void SherpaOnnxNemotronStreamingEnRecognitionModel_CreateEngineConfig_EmptyDirectory_ThrowsArgumentException()
    {
        // Arrange
        IRecognitionModel model = new SherpaOnnxNemotronStreamingEnRecognitionModel();

        // Act & Assert
        Assert.Throws<ArgumentException>(() => model.CreateEngineConfig(string.Empty));
    }

    /// <summary>
    ///     Proves that <see cref="ISpeechModel.InstallAsync"/> extracts a synthetic <c>.tar.bz2</c>
    ///     archive (shaped like the real published archive: one top-level folder holding the
    ///     model's files) placed at this model's own declared relative install path, and removes
    ///     the archive afterward - never depending on any real network access or the real
    ///     ~442 MiB production download.
    /// </summary>
    [Fact]
    public async Task SherpaOnnxNemotronStreamingEnRecognitionModel_InstallAsync_SyntheticArchive_ExtractsFilesAndRemovesArchive()
    {
        // Arrange
        var model = new SherpaOnnxNemotronStreamingEnRecognitionModel();
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
}
