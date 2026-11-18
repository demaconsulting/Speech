using DemaConsulting.Speech.ModelManagementSubsystem;
using DemaConsulting.Speech.Onnx.Piper.ModelManagementSubsystem;
using DemaConsulting.Speech.SynthesisSubsystem;

namespace DemaConsulting.Speech.Onnx.Piper.Tests.ModelManagementSubsystem;

/// <summary>
///     Behavior tests for <see cref="OnnxPiperLibriTtsEnSynthesisModel"/>: its catalog facts, the speaker parameter and
///     its resolution, the download descriptor, the installer that keeps only the graph and the configuration of the
///     release archive, and loading a backend from an installed directory (with a tiny stand-in graph, never the real
///     voice).
/// </summary>
public sealed class OnnxPiperLibriTtsEnSynthesisModelTests : IDisposable
{
    private const string ArchiveFolder = "vits-piper-en_US-libritts_r-medium/";

    private readonly string _root = Path.Join(Path.GetTempPath(), "DemaConsulting.Speech.Onnx.Piper.Tests", Guid.NewGuid().ToString("N"));

    /// <summary>Initializes a scratch directory that no test shares.</summary>
    public OnnxPiperLibriTtsEnSynthesisModelTests()
    {
        Directory.CreateDirectory(_root);
    }

    /// <summary>Deletes the scratch directory.</summary>
    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // Best-effort cleanup only.
        }
    }

    /// <summary>Proves the catalog facts: id, role, no audio tags, CC BY 4.0 with its link, the shared English text analyzer.</summary>
    [Fact]
    public void OnnxPiperLibriTtsEnSynthesisModel_CatalogFacts_AreDeclared()
    {
        // Arrange
        ISynthesisModel model = new OnnxPiperLibriTtsEnSynthesisModel();

        // Assert
        Assert.Equal("piper-onnx-libritts-r-en-us-medium", model.Id);
        Assert.Equal(SpeechModelRole.Synthesis, model.Role);
        Assert.Equal(SpeechModelAudioTagSupport.None, model.AudioTagSupport);
        Assert.Equal("CC BY 4.0", model.LicenseName);
        Assert.Equal("https://creativecommons.org/licenses/by/4.0/", model.LicenseUrl?.ToString());
        Assert.Equal(22050, model.PreferredAudioFormat.SampleRate);
        Assert.Equal(1, model.PreferredAudioFormat.ChannelCount);
        Assert.NotNull(model.TextAnalyzer);
        Assert.Same(model.TextAnalyzer, new OnnxPiperLibriTtsEnSynthesisModel().TextAnalyzer);
    }

    /// <summary>Proves the only parameter is an integer speaker selection from 0 to 903 whose default is 0.</summary>
    [Fact]
    public void OnnxPiperLibriTtsEnSynthesisModel_Parameters_AreTheSpeakerSelectionOnly()
    {
        // Arrange
        var model = new OnnxPiperLibriTtsEnSynthesisModel();

        // Act
        var parameter = Assert.IsType<NumericParameter>(Assert.Single(model.Parameters));

        // Assert
        Assert.Equal(OnnxPiperLibriTtsEnSynthesisModel.SpeakerParameterId, parameter.Id);
        Assert.True(parameter.IsInteger);
        Assert.Equal(0, parameter.Minimum);
        Assert.Equal(903, parameter.Maximum);
        Assert.Equal(0, parameter.Default);
    }

    /// <summary>Proves a speaker selection is resolved to its number, and that a missing, out-of-range or non-numeric value selects speaker 0.</summary>
    [Theory]
    [InlineData(null, 0)]
    [InlineData(7, 7)]
    [InlineData(7.0, 7)]
    [InlineData(7.4f, 7)]
    [InlineData(903, 903)]
    [InlineData(904, 0)]
    [InlineData(-1, 0)]
    [InlineData(double.NaN, 0)]
    [InlineData(double.PositiveInfinity, 0)]
    [InlineData("7", 0)]
    public void OnnxPiperLibriTtsEnSynthesisModel_ResolveSpeakerId_Value_ResolvesOrFallsBackToZero(object? value, int expected)
    {
        // Arrange
        ISynthesisModel model = new OnnxPiperLibriTtsEnSynthesisModel();
        IReadOnlyDictionary<string, object>? bag = value is null
            ? new Dictionary<string, object>()
            : new Dictionary<string, object> { [OnnxPiperLibriTtsEnSynthesisModel.SpeakerParameterId] = value };

        // Act & Assert
        Assert.Equal(expected, model.ResolveSpeakerId(bag));
        Assert.Equal(0, model.ResolveSpeakerId(null));
    }

    /// <summary>Proves the download descriptor names one pinned HTTPS file (a non-empty SHA-256) that the installer unpacks.</summary>
    [Fact]
    public void OnnxPiperLibriTtsEnSynthesisModel_DownloadDescriptor_PinsOneHttpsArchive()
    {
        // Arrange
        var model = new OnnxPiperLibriTtsEnSynthesisModel();

        // Act
        var file = Assert.Single(model.DownloadDescriptor.Files);

        // Assert
        Assert.Equal("https", file.Uri.Scheme);
        Assert.EndsWith(".tar.bz2", file.Uri.AbsolutePath, StringComparison.Ordinal);
        Assert.Equal("10dc268f3e371696d721486123e2705a9fc1faa113491979fde4d88dba1f1b1c", file.Sha256Checksum);
    }

    /// <summary>Proves the installer keeps only the graph and the configuration, flat in the installed directory, and removes the archive and everything else (the espeak-ng data, the token table).</summary>
    [Fact]
    public async Task OnnxPiperLibriTtsEnSynthesisModel_InstallAsync_ReleaseArchive_KeepsOnlyGraphAndConfiguration()
    {
        // Arrange
        var model = new OnnxPiperLibriTtsEnSynthesisModel();
        var archivePath = model.DownloadDescriptor.Files[0].ResolveStagedPath(_root);
        await File.WriteAllBytesAsync(archivePath, PiperTestFixtures.BuildArchive(
        [
            (ArchiveFolder + "en_US-libritts_r-medium.onnx", [1, 2, 3]),
            (ArchiveFolder + "en_US-libritts_r-medium.onnx.json", "{}"u8.ToArray()),
            (ArchiveFolder + "tokens.txt", "a 1"u8.ToArray()),
            (ArchiveFolder + "MODEL_CARD", "card"u8.ToArray()),
            (ArchiveFolder + "espeak-ng-data/en_dict", "dict"u8.ToArray()),
        ]), TestContext.Current.CancellationToken);

        // Act
        await model.InstallAsync(_root, TestContext.Current.CancellationToken);

        // Assert
        var installed = Directory.GetFileSystemEntries(_root).Select(Path.GetFileName).Order(StringComparer.Ordinal).ToList();
        Assert.Equal(["en_US-libritts_r-medium.onnx", "en_US-libritts_r-medium.onnx.json"], installed);
        Assert.Equal([1, 2, 3], await File.ReadAllBytesAsync(Path.Combine(_root, "en_US-libritts_r-medium.onnx"), TestContext.Current.CancellationToken));
    }

    /// <summary>Proves an entry path that tries to leave the installed directory still lands inside it, under its bare file name.</summary>
    [Fact]
    public async Task OnnxPiperLibriTtsEnSynthesisModel_InstallAsync_HostileEntryPath_StaysInsideInstalledDirectory()
    {
        // Arrange
        var staging = Path.Combine(_root, "staging");
        Directory.CreateDirectory(staging);
        var model = new OnnxPiperLibriTtsEnSynthesisModel();
        await File.WriteAllBytesAsync(model.DownloadDescriptor.Files[0].ResolveStagedPath(staging), PiperTestFixtures.BuildArchive(
        [
            ("../../evil.onnx", [9]),
            ("x/en_US-libritts_r-medium.onnx.json", "{}"u8.ToArray()),
        ]), TestContext.Current.CancellationToken);

        // Act
        await model.InstallAsync(staging, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(Directory.GetFiles(_root, "evil.onnx", SearchOption.TopDirectoryOnly));
        Assert.True(File.Exists(Path.Combine(staging, "evil.onnx")));
    }

    /// <summary>Proves an archive that lacks the graph or the configuration fails the install and is left in place for the downloader to discard.</summary>
    [Theory]
    [InlineData("en_US-libritts_r-medium.onnx")]
    [InlineData("en_US-libritts_r-medium.onnx.json")]
    [InlineData("tokens.txt")]
    public async Task OnnxPiperLibriTtsEnSynthesisModel_InstallAsync_ArchiveMissingAFile_Throws(string onlyEntry)
    {
        // Arrange
        var model = new OnnxPiperLibriTtsEnSynthesisModel();
        var archivePath = model.DownloadDescriptor.Files[0].ResolveStagedPath(_root);
        await File.WriteAllBytesAsync(archivePath, PiperTestFixtures.BuildArchive([(ArchiveFolder + onlyEntry, [1])]), TestContext.Current.CancellationToken);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidDataException>(() => model.InstallAsync(_root, TestContext.Current.CancellationToken));
        Assert.True(File.Exists(archivePath));
    }

    /// <summary>Proves a backend is created from an installed directory (with a stand-in graph) at the voice's 22,050 Hz.</summary>
    [Fact]
    public void OnnxPiperLibriTtsEnSynthesisModel_CreateBackend_InstalledDirectory_ReturnsBackendAtVoiceRate()
    {
        // Arrange
        PiperTestFixtures.WriteInstalledEchoVoice(_root);
        ISynthesisModel model = new OnnxPiperLibriTtsEnSynthesisModel();

        // Act
        using var backend = model.CreateBackend(_root);

        // Assert
        Assert.Equal(22050, backend.SampleRate);
        Assert.IsAssignableFrom<IAnnotatedSynthesisBackend>(backend);
    }

    /// <summary>Proves a preferred execution provider that is not available is skipped and the backend is created on the CPU provider.</summary>
    [Fact]
    public void OnnxPiperLibriTtsEnSynthesisModel_CreateBackend_UnavailablePreferredProvider_FallsBackToCpu()
    {
        // Arrange
        PiperTestFixtures.WriteInstalledEchoVoice(_root);
        ISynthesisModel model = new OnnxPiperLibriTtsEnSynthesisModel(["NotARealExecutionProvider"]);

        // Act
        using var backend = model.CreateBackend(_root);
        var echo = PiperTestFixtures.DecodeEcho(backend.Generate("Hello there.", 1.0f, 0));

        // Assert
        Assert.Equal(22050, backend.SampleRate);
        Assert.NotEmpty(echo.Ids);
    }

    /// <summary>Proves loading fails clearly for an unusable installation: a missing directory argument, a configuration for another voice, a configuration that is not usable, and a missing graph.</summary>
    [Fact]
    public void OnnxPiperLibriTtsEnSynthesisModel_CreateBackend_UnusableInstallation_Throws()
    {
        // Arrange
        ISynthesisModel model = new OnnxPiperLibriTtsEnSynthesisModel();
        var otherVoice = Path.Combine(_root, "other");
        PiperTestFixtures.WriteInstalledEchoVoice(otherVoice);
        File.WriteAllText(
            Path.Combine(otherVoice, "en_US-libritts_r-medium.onnx.json"),
            PiperTestFixtures.TrimmedConfigJson.Replace("\"num_speakers\": 904", "\"num_speakers\": 1", StringComparison.Ordinal));
        var broken = Path.Combine(_root, "broken");
        PiperTestFixtures.WriteInstalledEchoVoice(broken);
        File.WriteAllText(Path.Combine(broken, "en_US-libritts_r-medium.onnx.json"), "{}");
        var noGraph = Path.Combine(_root, "missing-graph");
        PiperTestFixtures.WriteInstalledEchoVoice(noGraph);
        File.Delete(Path.Combine(noGraph, "en_US-libritts_r-medium.onnx"));

        // Act & Assert
        Assert.Throws<ArgumentException>(() => model.CreateBackend(string.Empty));
        Assert.Throws<InvalidDataException>(() => model.CreateBackend(otherVoice));
        Assert.Throws<InvalidDataException>(() => model.CreateBackend(broken));
        Assert.ThrowsAny<Exception>(() => model.CreateBackend(noGraph));
    }
}
