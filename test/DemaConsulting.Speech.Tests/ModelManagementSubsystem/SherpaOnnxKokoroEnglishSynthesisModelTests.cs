using DemaConsulting.Speech.AudioSubsystem;
using DemaConsulting.Speech.ModelManagementSubsystem;
using DemaConsulting.Speech.SynthesisSubsystem;
using DemaConsulting.Speech.Tests.ModelManagementSubsystem.Fakes;

namespace DemaConsulting.Speech.Tests.ModelManagementSubsystem;

/// <summary>
///     Unit tests for <see cref="SherpaOnnxKokoroEnglishSynthesisModel"/>, proving its declared
///     catalog metadata, download descriptor shape, archive install behavior (against a small
///     synthetic <c>.tar.bz2</c> fixture, never the real ~103 MiB production download),
///     engine-configuration wiring, and speaker-id resolution - all deterministically and without
///     any real network access or native sherpa-onnx runtime.
/// </summary>
public sealed class SherpaOnnxKokoroEnglishSynthesisModelTests : IDisposable
{
    /// <summary>The archive's own top-level folder name, matching the real published archive.</summary>
    private const string ExtractedFolderName = "kokoro-int8-en-v0_19";

    /// <summary>
    ///     The confirmed <c>id2speaker</c> voice ordering (index = speaker id), reproduced from
    ///     <c>k2-fsa/sherpa-onnx</c>'s own generation script and independently confirmed against
    ///     the real archive's <c>voices.bin</c> byte count and a live model load - see the
    ///     production class's type-level remarks for the full provenance.
    /// </summary>
    private static readonly (string Voice, int SpeakerId)[] ExpectedVoiceIds =
    [
        ("af", 0),
        ("af_bella", 1),
        ("af_nicole", 2),
        ("af_sarah", 3),
        ("af_sky", 4),
        ("am_adam", 5),
        ("am_michael", 6),
        ("bf_emma", 7),
        ("bf_isabella", 8),
        ("bm_george", 9),
        ("bm_lewis", 10),
    ];

    /// <summary>A scratch root directory unique to this test instance, cleaned up on dispose.</summary>
    private readonly string _testRoot;

    /// <summary>
    ///     Initializes a fresh, unique scratch directory for each test.
    /// </summary>
    public SherpaOnnxKokoroEnglishSynthesisModelTests()
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
    ///     Proves this model declares its expected, stable catalog identity, and its one
    ///     voice-selection <see cref="ChoiceParameter"/> with 11 options.
    /// </summary>
    [Fact]
    public void SherpaOnnxKokoroEnglishSynthesisModel_Identity_DeclaresExpectedValues()
    {
        // Arrange
        var model = new SherpaOnnxKokoroEnglishSynthesisModel();

        // Act & Assert
        Assert.Equal("kokoro-int8-en-v0_19", model.Id);
        Assert.Equal(SherpaOnnxKokoroEnglishSynthesisModel.ModelId, model.Id);
        Assert.Equal("Kokoro English (int8, 11 voices)", model.DisplayName);
        Assert.Equal(SpeechModelRole.Synthesis, model.Role);
        Assert.Equal(SpeechModelAudioTagSupport.None, model.AudioTagSupport);
        Assert.Equal("Apache-2.0", model.LicenseName);
        Assert.Equal(new Uri("https://www.apache.org/licenses/LICENSE-2.0"), model.LicenseUrl);

        var parameter = Assert.Single(model.Parameters);
        var choice = Assert.IsType<ChoiceParameter>(parameter);
        Assert.Equal(SherpaOnnxKokoroEnglishSynthesisModel.VoiceParameterId, choice.Id);
        Assert.Equal(11, choice.Options.Count);
        Assert.Equal("af", choice.Default);
        Assert.Equal(
            ExpectedVoiceIds.Select(pair => pair.Voice),
            choice.Options.Select(option => option.Value));
    }

    /// <summary>
    ///     Proves that this model declares exactly one HTTPS download file, whose checksum is a
    ///     well-formed 64-character hexadecimal SHA-256 digest and whose relative install path
    ///     names a <c>.tar.bz2</c> archive, fetched directly from sherpa-onnx's official GitHub
    ///     Releases mirror.
    /// </summary>
    [Fact]
    public void SherpaOnnxKokoroEnglishSynthesisModel_DownloadDescriptor_DeclaresSingleValidatedArchiveFile()
    {
        // Arrange
        var model = new SherpaOnnxKokoroEnglishSynthesisModel();

        // Act
        var file = Assert.Single(model.DownloadDescriptor.Files);

        // Assert
        Assert.Equal("https", file.Uri.Scheme);
        Assert.Equal("github.com", file.Uri.Host);
        Assert.Equal(
            "https://github.com/k2-fsa/sherpa-onnx/releases/download/tts-models/kokoro-int8-en-v0_19.tar.bz2",
            file.Uri.ToString());
        Assert.Equal(64, file.Sha256Checksum.Length);
        Assert.True(file.Sha256Checksum.All(char.IsAsciiHexDigit));
        Assert.EndsWith(".tar.bz2", file.RelativeInstallPath, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves that this model exposes its best-effort preferred mono playback format.
    /// </summary>
    [Fact]
    public void SherpaOnnxKokoroEnglishSynthesisModel_PreferredAudioFormat_IsMono24000()
    {
        // Arrange
        ISynthesisModel model = new SherpaOnnxKokoroEnglishSynthesisModel();

        // Act
        var preferredFormat = model.PreferredAudioFormat;

        // Assert
        Assert.Equal(new AudioFormat(24000, 1), preferredFormat);
    }

    /// <summary>
    ///     Proves that <see cref="ISynthesisModel.CreateEngineConfig"/> resolves the Kokoro
    ///     model/voices/tokens/data-dir paths against the archive's extracted top-level folder.
    /// </summary>
    [Fact]
    public void SherpaOnnxKokoroEnglishSynthesisModel_CreateEngineConfig_ResolvesKokoroFilesAndConfig()
    {
        // Arrange
        ISynthesisModel model = new SherpaOnnxKokoroEnglishSynthesisModel();
        var installedDirectory = _testRoot;

        // Act
        var config = model.CreateEngineConfig(installedDirectory);

        // Assert
        var modelFolder = Path.Combine(installedDirectory, ExtractedFolderName);
        Assert.Equal(Path.Combine(modelFolder, "model.int8.onnx"), config.Model.Kokoro.Model);
        Assert.Equal(Path.Combine(modelFolder, "voices.bin"), config.Model.Kokoro.Voices);
        Assert.Equal(Path.Combine(modelFolder, "tokens.txt"), config.Model.Kokoro.Tokens);
        Assert.Equal(Path.Combine(modelFolder, "espeak-ng-data"), config.Model.Kokoro.DataDir);
        Assert.Equal(1.0f, config.Model.Kokoro.LengthScale);
        Assert.True(string.IsNullOrEmpty(config.Model.Kokoro.DictDir));
        Assert.True(string.IsNullOrEmpty(config.Model.Kokoro.Lexicon));
        Assert.True(string.IsNullOrEmpty(config.Model.Kokoro.Lang));
        Assert.Equal("cpu", config.Model.Provider);
    }

    /// <summary>
    ///     Proves that <see cref="ArgumentException"/> is thrown for a null or empty installed
    ///     directory, rather than silently building an unusable configuration.
    /// </summary>
    [Fact]
    public void SherpaOnnxKokoroEnglishSynthesisModel_CreateEngineConfig_EmptyDirectory_ThrowsArgumentException()
    {
        // Arrange
        ISynthesisModel model = new SherpaOnnxKokoroEnglishSynthesisModel();

        // Act & Assert
        Assert.Throws<ArgumentException>(() => model.CreateEngineConfig(string.Empty));
    }

    /// <summary>
    ///     Proves that <see cref="ISynthesisModel.CapabilityProfile"/> resolves to the shared
    ///     <see cref="DefaultModelCapabilityProfile"/> instance, since Kokoro has no native inline
    ///     tag support and needs no bespoke tag-rendering override.
    /// </summary>
    [Fact]
    public void SherpaOnnxKokoroEnglishSynthesisModel_CapabilityProfile_IsDefaultProfile()
    {
        // Arrange
        ISynthesisModel model = new SherpaOnnxKokoroEnglishSynthesisModel();

        // Act & Assert
        Assert.Same(DefaultModelCapabilityProfile.Instance, model.CapabilityProfile);
    }

    /// <summary>
    ///     Proves that <see cref="ISpeechModel.InstallAsync"/> extracts a synthetic <c>.tar.bz2</c>
    ///     archive (shaped like the real published archive: one top-level folder holding the
    ///     model's files) placed at this model's own declared relative install path, and removes
    ///     the archive afterward - never depending on any real network access or the real
    ///     ~103 MiB production download.
    /// </summary>
    [Fact]
    public async Task SherpaOnnxKokoroEnglishSynthesisModel_InstallAsync_SyntheticArchive_ExtractsFilesAndRemovesArchive()
    {
        // Arrange
        var model = new SherpaOnnxKokoroEnglishSynthesisModel();
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
    ///     Proves that <see cref="ISynthesisModel.ResolveSpeakerId"/> resolves every one of the
    ///     11 declared voice values to its documented, confirmed speaker id.
    /// </summary>
    [Theory]
    [MemberData(nameof(VoiceIdTestCases))]
    public void SherpaOnnxKokoroEnglishSynthesisModel_ResolveSpeakerId_KnownVoice_ReturnsConfirmedId(string voice, int expectedId)
    {
        // Arrange
        ISynthesisModel model = new SherpaOnnxKokoroEnglishSynthesisModel();
        IReadOnlyDictionary<string, object> parameterValues = new Dictionary<string, object>
        {
            [SherpaOnnxKokoroEnglishSynthesisModel.VoiceParameterId] = voice,
        };

        // Act
        var speakerId = model.ResolveSpeakerId(parameterValues);

        // Assert
        Assert.Equal(expectedId, speakerId);
    }

    /// <summary>Supplies every declared voice/expected-speaker-id pair for the theory above.</summary>
    public static IEnumerable<object[]> VoiceIdTestCases() =>
        ExpectedVoiceIds.Select(pair => new object[] { pair.Voice, pair.SpeakerId });

    /// <summary>
    ///     Proves that an unrecognized voice value falls back to the default voice's speaker id
    ///     rather than throwing.
    /// </summary>
    [Fact]
    public void SherpaOnnxKokoroEnglishSynthesisModel_ResolveSpeakerId_UnknownVoice_ReturnsDefaultId()
    {
        // Arrange
        ISynthesisModel model = new SherpaOnnxKokoroEnglishSynthesisModel();
        IReadOnlyDictionary<string, object> parameterValues = new Dictionary<string, object>
        {
            [SherpaOnnxKokoroEnglishSynthesisModel.VoiceParameterId] = "not-a-real-voice",
        };

        // Act
        var speakerId = model.ResolveSpeakerId(parameterValues);

        // Assert: "af" (id 0) is the declared default
        Assert.Equal(0, speakerId);
    }

    /// <summary>
    ///     Proves that a <see langword="null"/> parameter value bag falls back to the default
    ///     voice's speaker id rather than throwing.
    /// </summary>
    [Fact]
    public void SherpaOnnxKokoroEnglishSynthesisModel_ResolveSpeakerId_NullBag_ReturnsDefaultId()
    {
        // Arrange
        ISynthesisModel model = new SherpaOnnxKokoroEnglishSynthesisModel();

        // Act
        var speakerId = model.ResolveSpeakerId(null);

        // Assert
        Assert.Equal(0, speakerId);
    }

    /// <summary>
    ///     Proves that a bag missing the declared voice key falls back to the default voice's
    ///     speaker id rather than throwing.
    /// </summary>
    [Fact]
    public void SherpaOnnxKokoroEnglishSynthesisModel_ResolveSpeakerId_MissingKey_ReturnsDefaultId()
    {
        // Arrange
        ISynthesisModel model = new SherpaOnnxKokoroEnglishSynthesisModel();
        IReadOnlyDictionary<string, object> parameterValues = new Dictionary<string, object>
        {
            ["some-other-parameter"] = "value",
        };

        // Act
        var speakerId = model.ResolveSpeakerId(parameterValues);

        // Assert
        Assert.Equal(0, speakerId);
    }
}
