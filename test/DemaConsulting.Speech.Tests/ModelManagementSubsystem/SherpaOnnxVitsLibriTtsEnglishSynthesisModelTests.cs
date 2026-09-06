using DemaConsulting.Speech.AudioSubsystem;
using DemaConsulting.Speech.ModelManagementSubsystem;
using DemaConsulting.Speech.SynthesisSubsystem;
using DemaConsulting.Speech.Tests.ModelManagementSubsystem.Fakes;

namespace DemaConsulting.Speech.Tests.ModelManagementSubsystem;

/// <summary>
///     Unit tests for <see cref="SherpaOnnxVitsLibriTtsEnglishSynthesisModel"/>, proving its
///     declared catalog metadata, download descriptor shape, archive install behavior (against a
///     small synthetic <c>.tar.bz2</c> fixture, never the real ~78 MiB production download), and
///     engine-configuration wiring - all deterministically and without any real network access
///     or native sherpa-onnx runtime.
/// </summary>
public sealed class SherpaOnnxVitsLibriTtsEnglishSynthesisModelTests : IDisposable
{
    /// <summary>The archive's own top-level folder name, matching the real published archive.</summary>
    private const string ExtractedFolderName = "vits-piper-en_US-libritts_r-medium";

    /// <summary>A scratch root directory unique to this test instance, cleaned up on dispose.</summary>
    private readonly string _testRoot;

    /// <summary>
    ///     Initializes a fresh, unique scratch directory for each test.
    /// </summary>
    public SherpaOnnxVitsLibriTtsEnglishSynthesisModelTests()
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
    ///     Proves this model declares its expected, stable catalog identity, and that its display
    ///     name visibly names the CC BY 4.0 license, unlike the Apache-2.0/NVIDIA-licensed
    ///     recognition models registered alongside it.
    /// </summary>
    [Fact]
    public void SherpaOnnxVitsLibriTtsEnglishSynthesisModel_Identity_DeclaresExpectedValues()
    {
        // Arrange
        var model = new SherpaOnnxVitsLibriTtsEnglishSynthesisModel();

        // Act & Assert
        Assert.Equal("vits-piper-en_US-libritts_r-medium", model.Id);
        Assert.Equal(SherpaOnnxVitsLibriTtsEnglishSynthesisModel.ModelId, model.Id);
        Assert.Contains("CC BY 4.0", model.DisplayName, StringComparison.Ordinal);
        Assert.Equal(SpeechModelRole.Synthesis, model.Role);
        Assert.Single(model.Parameters);
        Assert.Equal(SpeechModelAudioTagSupport.None, model.AudioTagSupport);
    }

    /// <summary>
    ///     Proves this model declares exactly one HTTPS download file, whose checksum is a
    ///     well-formed 64-character hexadecimal SHA-256 digest and whose relative install path
    ///     names a <c>.tar.bz2</c> archive, fetched directly from sherpa-onnx's official GitHub
    ///     Releases mirror.
    /// </summary>
    [Fact]
    public void SherpaOnnxVitsLibriTtsEnglishSynthesisModel_DownloadDescriptor_DeclaresSingleValidatedArchiveFile()
    {
        // Arrange
        var model = new SherpaOnnxVitsLibriTtsEnglishSynthesisModel();

        // Act
        var file = Assert.Single(model.DownloadDescriptor.Files);

        // Assert
        Assert.Equal("https", file.Uri.Scheme);
        Assert.Equal("github.com", file.Uri.Host);
        Assert.Equal(
            "https://github.com/k2-fsa/sherpa-onnx/releases/download/tts-models/vits-piper-en_US-libritts_r-medium.tar.bz2",
            file.Uri.ToString());
        Assert.Equal(64, file.Sha256Checksum.Length);
        Assert.True(file.Sha256Checksum.All(char.IsAsciiHexDigit));
        Assert.EndsWith(".tar.bz2", file.RelativeInstallPath, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves that this model declares exactly one tunable parameter: a numeric speaker-index
    ///     selection spanning its full declared range of 904 speakers, with a default of 0.
    /// </summary>
    [Fact]
    public void SherpaOnnxVitsLibriTtsEnglishSynthesisModel_Parameters_DeclaresSpeakerNumericParameter()
    {
        // Arrange
        var model = new SherpaOnnxVitsLibriTtsEnglishSynthesisModel();

        // Act
        var parameter = Assert.Single(model.Parameters);

        // Assert
        var numericParameter = Assert.IsType<NumericParameter>(parameter);
        Assert.Equal(SherpaOnnxVitsLibriTtsEnglishSynthesisModel.SpeakerParameterId, numericParameter.Id);
        Assert.Equal(0, numericParameter.Minimum);
        Assert.Equal(903, numericParameter.Maximum);
        Assert.Equal(0, numericParameter.Default);
        Assert.True(numericParameter.IsInteger);
    }

    /// <summary>
    ///     Proves that this model exposes its best-effort preferred mono playback format.
    /// </summary>
    [Fact]
    public void SherpaOnnxVitsLibriTtsEnglishSynthesisModel_PreferredAudioFormat_IsMono22050()
    {
        // Arrange
        ISynthesisModel model = new SherpaOnnxVitsLibriTtsEnglishSynthesisModel();

        // Act
        var preferredFormat = model.PreferredAudioFormat;

        // Assert
        Assert.Equal(new AudioFormat(22050, 1), preferredFormat);
    }

    /// <summary>
    ///     Proves that <see cref="ISynthesisModel.ResolveSpeakerId"/> resolves valid numeric
    ///     speaker values across the declared range, including both boundaries.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(450)]
    [InlineData(902)]
    [InlineData(903)]
    public void SherpaOnnxVitsLibriTtsEnglishSynthesisModel_ResolveSpeakerId_ValidValue_ReturnsThatValue(int speaker)
    {
        // Arrange
        ISynthesisModel model = new SherpaOnnxVitsLibriTtsEnglishSynthesisModel();
        IReadOnlyDictionary<string, object> parameterValues = new Dictionary<string, object>
        {
            [SherpaOnnxVitsLibriTtsEnglishSynthesisModel.SpeakerParameterId] = (double)speaker,
        };

        // Act
        var speakerId = model.ResolveSpeakerId(parameterValues);

        // Assert
        Assert.Equal(speaker, speakerId);
    }

    /// <summary>
    ///     Proves that <see cref="ISynthesisModel.ResolveSpeakerId"/> accepts a boxed
    ///     <see langword="int"/> value, not only a boxed <see langword="double"/>, since a caller
    ///     may build the parameter bag programmatically rather than through a generic numeric
    ///     parameter UI.
    /// </summary>
    [Fact]
    public void SherpaOnnxVitsLibriTtsEnglishSynthesisModel_ResolveSpeakerId_BoxedInt_ReturnsThatValue()
    {
        // Arrange
        ISynthesisModel model = new SherpaOnnxVitsLibriTtsEnglishSynthesisModel();
        IReadOnlyDictionary<string, object> parameterValues = new Dictionary<string, object>
        {
            [SherpaOnnxVitsLibriTtsEnglishSynthesisModel.SpeakerParameterId] = 450,
        };

        // Act
        var speakerId = model.ResolveSpeakerId(parameterValues);

        // Assert
        Assert.Equal(450, speakerId);
    }

    /// <summary>
    ///     Proves that an out-of-range numeric value falls back to the default speaker id (0)
    ///     rather than throwing.
    /// </summary>
    [Theory]
    [InlineData(-1)]
    [InlineData(904)]
    [InlineData(100000)]
    public void SherpaOnnxVitsLibriTtsEnglishSynthesisModel_ResolveSpeakerId_OutOfRangeValue_ReturnsDefaultId(int speaker)
    {
        // Arrange
        ISynthesisModel model = new SherpaOnnxVitsLibriTtsEnglishSynthesisModel();
        IReadOnlyDictionary<string, object> parameterValues = new Dictionary<string, object>
        {
            [SherpaOnnxVitsLibriTtsEnglishSynthesisModel.SpeakerParameterId] = (double)speaker,
        };

        // Act
        var speakerId = model.ResolveSpeakerId(parameterValues);

        // Assert
        Assert.Equal(0, speakerId);
    }

    /// <summary>
    ///     Proves that a non-numeric value falls back to the default speaker id (0) rather than
    ///     throwing.
    /// </summary>
    [Fact]
    public void SherpaOnnxVitsLibriTtsEnglishSynthesisModel_ResolveSpeakerId_NonNumericValue_ReturnsDefaultId()
    {
        // Arrange
        ISynthesisModel model = new SherpaOnnxVitsLibriTtsEnglishSynthesisModel();
        IReadOnlyDictionary<string, object> parameterValues = new Dictionary<string, object>
        {
            [SherpaOnnxVitsLibriTtsEnglishSynthesisModel.SpeakerParameterId] = "not-a-number",
        };

        // Act
        var speakerId = model.ResolveSpeakerId(parameterValues);

        // Assert
        Assert.Equal(0, speakerId);
    }

    /// <summary>
    ///     Proves that a <see langword="null"/> parameter value bag falls back to the default
    ///     speaker id (0) rather than throwing.
    /// </summary>
    [Fact]
    public void SherpaOnnxVitsLibriTtsEnglishSynthesisModel_ResolveSpeakerId_NullBag_ReturnsDefaultId()
    {
        // Arrange
        ISynthesisModel model = new SherpaOnnxVitsLibriTtsEnglishSynthesisModel();

        // Act
        var speakerId = model.ResolveSpeakerId(null);

        // Assert
        Assert.Equal(0, speakerId);
    }

    /// <summary>
    ///     Proves that a bag missing the declared speaker key falls back to the default speaker
    ///     id (0) rather than throwing.
    /// </summary>
    [Fact]
    public void SherpaOnnxVitsLibriTtsEnglishSynthesisModel_ResolveSpeakerId_MissingKey_ReturnsDefaultId()
    {
        // Arrange
        ISynthesisModel model = new SherpaOnnxVitsLibriTtsEnglishSynthesisModel();
        IReadOnlyDictionary<string, object> parameterValues = new Dictionary<string, object>
        {
            ["some-other-parameter"] = "value",
        };

        // Act
        var speakerId = model.ResolveSpeakerId(parameterValues);

        // Assert
        Assert.Equal(0, speakerId);
    }

    /// <summary>
    ///     Proves that <see cref="ISynthesisModel.CreateEngineConfig"/> resolves the VITS
    ///     model/tokens/data-dir paths against the archive's extracted top-level folder, and
    ///     wires this model's own proven noise-scale/length-scale configuration, leaving
    ///     <c>Lexicon</c> unset since this archive ships no <c>lexicon.txt</c>.
    /// </summary>
    [Fact]
    public void SherpaOnnxVitsLibriTtsEnglishSynthesisModel_CreateEngineConfig_ResolvesVitsFilesAndConfig()
    {
        // Arrange
        ISynthesisModel model = new SherpaOnnxVitsLibriTtsEnglishSynthesisModel();
        var installedDirectory = _testRoot;

        // Act
        var config = model.CreateEngineConfig(installedDirectory);

        // Assert
        var modelFolder = Path.Combine(installedDirectory, ExtractedFolderName);
        Assert.Equal(Path.Combine(modelFolder, "en_US-libritts_r-medium.onnx"), config.Model.Vits.Model);
        Assert.Equal(Path.Combine(modelFolder, "tokens.txt"), config.Model.Vits.Tokens);
        Assert.Equal(Path.Combine(modelFolder, "espeak-ng-data"), config.Model.Vits.DataDir);
        Assert.True(string.IsNullOrEmpty(config.Model.Vits.Lexicon));
        Assert.Equal(0.333f, config.Model.Vits.NoiseScale);
        Assert.Equal(0.333f, config.Model.Vits.NoiseScaleW);
        Assert.Equal(1.0f, config.Model.Vits.LengthScale);
        Assert.Equal("cpu", config.Model.Provider);
    }

    /// <summary>
    ///     Proves that <see cref="ArgumentException"/> is thrown for a null or empty installed
    ///     directory, rather than silently building an unusable configuration.
    /// </summary>
    [Fact]
    public void SherpaOnnxVitsLibriTtsEnglishSynthesisModel_CreateEngineConfig_EmptyDirectory_ThrowsArgumentException()
    {
        // Arrange
        ISynthesisModel model = new SherpaOnnxVitsLibriTtsEnglishSynthesisModel();

        // Act & Assert
        Assert.Throws<ArgumentException>(() => model.CreateEngineConfig(string.Empty));
    }

    /// <summary>
    ///     Proves that <see cref="ISynthesisModel.CapabilityProfile"/> resolves to the shared
    ///     <see cref="DefaultModelCapabilityProfile"/> instance, since this plain VITS/Piper model
    ///     needs no bespoke tag-rendering override.
    /// </summary>
    [Fact]
    public void SherpaOnnxVitsLibriTtsEnglishSynthesisModel_CapabilityProfile_IsDefaultProfile()
    {
        // Arrange
        ISynthesisModel model = new SherpaOnnxVitsLibriTtsEnglishSynthesisModel();

        // Act & Assert
        Assert.Same(DefaultModelCapabilityProfile.Instance, model.CapabilityProfile);
    }

    /// <summary>
    ///     Proves that <see cref="ISpeechModel.InstallAsync"/> extracts a synthetic <c>.tar.bz2</c>
    ///     archive (shaped like the real published archive: one top-level folder holding the
    ///     model's files) placed at this model's own declared relative install path, and removes
    ///     the archive afterward - never depending on any real network access or the real
    ///     ~78 MiB production download.
    /// </summary>
    [Fact]
    public async Task SherpaOnnxVitsLibriTtsEnglishSynthesisModel_InstallAsync_SyntheticArchive_ExtractsFilesAndRemovesArchive()
    {
        // Arrange
        var model = new SherpaOnnxVitsLibriTtsEnglishSynthesisModel();
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
