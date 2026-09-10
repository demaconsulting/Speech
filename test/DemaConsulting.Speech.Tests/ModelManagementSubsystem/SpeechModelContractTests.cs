using DemaConsulting.Speech.AudioSubsystem;
using DemaConsulting.Speech.ModelManagementSubsystem;
using DemaConsulting.Speech.SynthesisSubsystem;
using DemaConsulting.Speech.Tests.ModelManagementSubsystem.Fakes;

namespace DemaConsulting.Speech.Tests.ModelManagementSubsystem;

/// <summary>
///     Unit tests for the <see cref="ISpeechModel"/>, <see cref="IRecognitionModel"/>, and
///     <see cref="ISynthesisModel"/> contract, exercised through the test-only
///     <see cref="FakeRecognitionModel"/>/<see cref="FakeSynthesisModel"/> implementations since
///     this pass ships no real, production model class.
/// </summary>
public class SpeechModelContractTests
{
    /// <summary>
    ///     Proves that a model implementing <see cref="IRecognitionModel"/> exposes
    ///     <see cref="SpeechModelRole.Recognition"/> and is also usable through the common
    ///     <see cref="ISpeechModel"/> contract.
    /// </summary>
    [Fact]
    public void IRecognitionModel_Role_IsRecognition()
    {
        // Arrange
        var model = new FakeRecognitionModel();

        // Act & Assert
        Assert.Equal(SpeechModelRole.Recognition, model.Role);
        Assert.IsType<IRecognitionModel>(model, exactMatch: false);
    }

    /// <summary>
    ///     Proves that a model implementing <see cref="ISynthesisModel"/> exposes
    ///     <see cref="SpeechModelRole.Synthesis"/> and is also usable through the common
    ///     <see cref="ISpeechModel"/> contract.
    /// </summary>
    [Fact]
    public void ISynthesisModel_Role_IsSynthesis()
    {
        // Arrange
        var model = new FakeSynthesisModel();

        // Act & Assert
        Assert.Equal(SpeechModelRole.Synthesis, model.Role);
        Assert.IsType<ISynthesisModel>(model, exactMatch: false);
    }

    /// <summary>
    ///     Proves that a recognition model exposes the audio format it declares, so the
    ///     recognition pipeline can resample captured audio to the format the model needs.
    /// </summary>
    [Fact]
    public void IRecognitionModel_AudioFormat_DeclaredByModel_IsExposed()
    {
        // Arrange: a fake recognition model declaring a non-default engine input rate
        var model = new FakeRecognitionModel(sampleRate: 8000);

        // Act
        var format = ((IRecognitionModel)model).AudioFormat;

        // Assert
        Assert.Equal(new AudioFormat(8000, 1), format);
    }

    /// <summary>
    ///     Proves that a recognition model builds an engine configuration resolved against the
    ///     directory its files were installed into, and that the configuration carries the same
    ///     rate the model declares.
    /// </summary>
    [Fact]
    public void IRecognitionModel_CreateEngineConfig_InstalledDirectory_ResolvesPathsAndSampleRate()
    {
        // Arrange: a fake recognition model and an installed-model directory path
        var model = new FakeRecognitionModel();
        var installedModelDirectory = Path.Join(Path.GetTempPath(), "fake-installed-model");

        // Act
        var config = ((IRecognitionModel)model).CreateEngineConfig(installedModelDirectory);

        // Assert: the declared rate and the resolved token path are both present
        Assert.Equal(((IRecognitionModel)model).AudioFormat.SampleRate, config.FeatConfig.SampleRate);
        Assert.Equal(Path.Join(installedModelDirectory, "tokens.txt"), config.ModelConfig.Tokens);
    }

    /// <summary>
    ///     Proves that a recognition model rejects an empty installed-model directory, since no
    ///     engine file path could be resolved from it.
    /// </summary>
    [Fact]
    public void IRecognitionModel_CreateEngineConfig_EmptyDirectory_ThrowsArgumentException()
    {
        // Arrange
        var model = new FakeRecognitionModel();

        // Act & Assert
        Assert.Throws<ArgumentException>(() => ((IRecognitionModel)model).CreateEngineConfig(string.Empty));
    }

    /// <summary>
    ///     Proves that a synthesis model builds an engine configuration resolved against the
    ///     directory its files were installed into.
    /// </summary>
    [Fact]
    public void ISynthesisModel_CreateEngineConfig_InstalledDirectory_ResolvesPaths()
    {
        // Arrange: a fake synthesis model and an installed-model directory path
        var model = new FakeSynthesisModel();
        var installedModelDirectory = Path.Join(Path.GetTempPath(), "fake-installed-synthesis-model");

        // Act
        var config = ((ISynthesisModel)model).CreateEngineConfig(installedModelDirectory);

        // Assert: the model's own file paths are resolved against the supplied directory
        Assert.Equal(Path.Join(installedModelDirectory, "model.onnx"), config.Model.Vits.Model);
        Assert.Equal(Path.Join(installedModelDirectory, "tokens.txt"), config.Model.Vits.Tokens);
    }

    /// <summary>
    ///     Proves that a synthesis model exposes a preferred audio-format hint without exposing
    ///     any native engine type.
    /// </summary>
    [Fact]
    public void ISynthesisModel_PreferredAudioFormat_DeclaredByModel_IsExposed()
    {
        // Arrange
        var model = new FakeSynthesisModel();

        // Act
        var preferredFormat = ((ISynthesisModel)model).PreferredAudioFormat;

        // Assert
        Assert.Equal(new AudioFormat(24000, 1), preferredFormat);
    }

    /// <summary>
    ///     Proves that a synthesis model rejects an empty installed-model directory, since no
    ///     engine file path could be resolved from it.
    /// </summary>
    [Fact]
    public void ISynthesisModel_CreateEngineConfig_EmptyDirectory_ThrowsArgumentException()
    {
        // Arrange
        var model = new FakeSynthesisModel();

        // Act & Assert
        Assert.Throws<ArgumentException>(() => ((ISynthesisModel)model).CreateEngineConfig(string.Empty));
    }

    /// <summary>
    ///     Proves that a synthesis model's default <see cref="ISynthesisModel.CapabilityProfile"/>
    ///     hook returns the shared generically-correct profile, so a model needs zero code to get
    ///     correct Natural Language Audio Tag rendering.
    /// </summary>
    [Fact]
    public void ISynthesisModel_CapabilityProfile_DefaultImplementation_ReturnsDefaultProfile()
    {
        // Arrange
        var model = new FakeSynthesisModel();

        // Act
        var profile = ((ISynthesisModel)model).CapabilityProfile;

        // Assert
        Assert.Same(DefaultModelCapabilityProfile.Instance, profile);
    }

    /// <summary>
    ///     Proves that a model's declared <see cref="ISpeechModel.Parameters"/> list is
    ///     non-null, non-empty, and contains every descriptor kind a host UI must render.
    /// </summary>
    [Fact]
    public void ISpeechModel_Parameters_DeclaresEveryDescriptorKind()
    {
        // Arrange
        var model = new FakeRecognitionModel();

        // Act
        var parameters = model.Parameters;

        // Assert
        Assert.NotEmpty(parameters);
        Assert.Contains(parameters, p => p is NumericParameter);
        Assert.Contains(parameters, p => p is ChoiceParameter);
        Assert.Contains(parameters, p => p is BooleanParameter);
    }

    /// <summary>
    ///     Proves that a model's declared <see cref="ISpeechModel.AudioTagSupport"/> and
    ///     <see cref="ISpeechModel.DownloadDescriptor"/> are exposed without throwing.
    /// </summary>
    [Fact]
    public void ISpeechModel_AudioTagSupportAndDownloadDescriptor_AreExposed()
    {
        // Arrange
        var model = new FakeSynthesisModel();

        // Act & Assert
        Assert.Equal(SpeechModelAudioTagSupport.ParameterMapped, model.AudioTagSupport);
        Assert.NotEmpty(model.DownloadDescriptor.Files);
    }

    /// <summary>
    ///     Proves that the default <see cref="ISpeechModel.InstallAsync"/> implementation
    ///     completes without modifying a staging directory's contents at all, matching the
    ///     common single-file-model case that needs no unpacking.
    /// </summary>
    [Fact]
    public async Task ISpeechModel_InstallAsync_DefaultImplementation_CompletesWithoutModifyingStagingDirectory()
    {
        // Arrange: a scratch directory with a file in it, standing in for a verified staging
        // directory containing a single downloaded, already-usable file.
        var model = new FakeSynthesisModel();
        var stagingDirectory = Path.Join(Path.GetTempPath(), "DemaConsulting.Speech.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(stagingDirectory);
        try
        {
            var filePath = Path.Join(stagingDirectory, "model.bin");
            await File.WriteAllBytesAsync(filePath, "unchanged"u8.ToArray(), TestContext.Current.CancellationToken);
            var beforeBytes = await File.ReadAllBytesAsync(filePath, TestContext.Current.CancellationToken);

            // Act
            await ((ISpeechModel)model).InstallAsync(stagingDirectory, TestContext.Current.CancellationToken);

            // Assert: the directory's single file is byte-identical, and no other file appeared.
            var afterBytes = await File.ReadAllBytesAsync(filePath, TestContext.Current.CancellationToken);
            Assert.Equal(beforeBytes, afterBytes);
            Assert.Single(Directory.EnumerateFileSystemEntries(stagingDirectory));
        }
        finally
        {
            Directory.Delete(stagingDirectory, recursive: true);
        }
    }

    /// <summary>
    ///     Proves that the default <see cref="ISpeechModel.NormalizeText"/> implementation
    ///     returns its input unchanged.
    /// </summary>
    [Fact]
    public void ISpeechModel_NormalizeText_DefaultImplementation_ReturnsInputUnchanged()
    {
        // Arrange
        var model = new FakeSynthesisModel();
        const string text = "Hello, world! <break/>";

        // Act
        var normalized = ((ISpeechModel)model).NormalizeText(text);

        // Assert
        Assert.Equal(text, normalized);
    }

    /// <summary>
    ///     Proves that the default <see cref="ISpeechModel.LicenseName"/>/<see cref="ISpeechModel.LicenseUrl"/>
    ///     implementations report an explicit <c>"Unknown"</c> name and a null URL, so a model
    ///     that declares no license still reports a discoverable, honest value rather than
    ///     throwing or silently claiming a specific license.
    /// </summary>
    [Fact]
    public void ISpeechModel_LicenseName_DefaultImplementation_ReturnsUnknownAndNullLicenseUrl()
    {
        // Arrange
        var model = new FakeSynthesisModel();

        // Act & Assert
        Assert.Equal("Unknown", ((ISpeechModel)model).LicenseName);
        Assert.Null(((ISpeechModel)model).LicenseUrl);
    }

    /// <summary>
    ///     Proves that <see cref="FakeRecognitionModel"/> constructed with
    ///     <c>useZipArchivePayload: true</c> extracts its declared zip archive's entries into the
    ///     staging directory and removes the archive file, when its <see cref="ISpeechModel.InstallAsync"/>
    ///     override is invoked directly.
    /// </summary>
    [Fact]
    public async Task FakeRecognitionModel_InstallAsync_WithZipArchivePayload_ExtractsEntriesAndRemovesArchive()
    {
        // Arrange: populate a temp staging directory with the fake's declared zip archive, as if
        // it had already been downloaded and checksum-verified.
        var model = new FakeRecognitionModel(useZipArchivePayload: true);
        var stagingDirectory = Path.Join(Path.GetTempPath(), "DemaConsulting.Speech.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(stagingDirectory);
        try
        {
            var archivePath = Path.Join(stagingDirectory, FakeModelDescriptors.ZipArchiveRelativeInstallPath);
            await File.WriteAllBytesAsync(archivePath, FakeModelDescriptors.ZipArchiveBytes, TestContext.Current.CancellationToken);

            // Act
            await ((ISpeechModel)model).InstallAsync(stagingDirectory, TestContext.Current.CancellationToken);

            // Assert: the extracted entry exists with its expected content, and the zip is gone.
            var extractedPath = Path.Join(stagingDirectory, FakeModelDescriptors.ZipArchiveEntryName);
            Assert.True(File.Exists(extractedPath));
            var extractedBytes = await File.ReadAllBytesAsync(extractedPath, TestContext.Current.CancellationToken);
            Assert.Equal(FakeModelDescriptors.ZipArchiveEntryContent, extractedBytes);
            Assert.False(File.Exists(archivePath));
        }
        finally
        {
            Directory.Delete(stagingDirectory, recursive: true);
        }
    }

    /// <summary>
    ///     Proves that the default <see cref="IRecognitionModel.NormalizeText(string,bool)"/>
    ///     hook forwards to the inherited <see cref="ISpeechModel.NormalizeText(string)"/>
    ///     identity default for both a finalized and a provisional result, since
    ///     <see cref="FakeRecognitionModel"/> does not override either member.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void IRecognitionModel_NormalizeText_DefaultImplementation_ReturnsInputUnchanged(bool isFinal)
    {
        // Arrange
        var model = new FakeRecognitionModel();
        const string text = "HELLO WORLD";

        // Act
        var normalized = ((IRecognitionModel)model).NormalizeText(text, isFinal);

        // Assert
        Assert.Equal(text, normalized);
    }
}
