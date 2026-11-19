using System.Text;
using DemaConsulting.Speech.ModelManagementSubsystem;
using DemaConsulting.Speech.Onnx.Piper.ModelManagementSubsystem;
using DemaConsulting.Speech.SynthesisSubsystem;

namespace DemaConsulting.Speech.Onnx.Piper.Tests.ModelManagementSubsystem;

/// <summary>
///     Subsystem-level tests of the Piper ModelManagementSubsystem: the units working together (the model, its archive
///     installer, the voice configuration, the phoneme pipeline and the synthesis engine they build) from a downloaded
///     archive to audio, with a tiny stand-in graph that echoes what it is fed and never the real voice.
/// </summary>
public sealed class ModelManagementSubsystemTests : IDisposable
{
    private const string ArchiveFolder = "vits-piper-en_US-libritts_r-medium/";

    private readonly string _root = Path.Join(Path.GetTempPath(), "DemaConsulting.Speech.Onnx.Piper.Tests", Guid.NewGuid().ToString("N"));

    /// <summary>Initializes a scratch directory that no test shares.</summary>
    public ModelManagementSubsystemTests()
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

    /// <summary>
    ///     Proves a downloaded release archive becomes a working voice: the installer keeps only the graph and the
    ///     configuration, deletes the archive, and a backend created from the installed directory speaks.
    /// </summary>
    [Fact]
    public async Task ModelManagementSubsystem_InstallThenCreateBackend_ArchiveBecomesAWorkingVoice()
    {
        // Arrange
        ISynthesisModel model = new OnnxPiperLibriTtsEnSynthesisModel();
        var archivePath = model.DownloadDescriptor.Files[0].ResolveStagedPath(_root);
        await File.WriteAllBytesAsync(archivePath, PiperTestFixtures.BuildArchive(
        [
            (ArchiveFolder + "en_US-libritts_r-medium.onnx", PiperTestFixtures.BuildEchoModel()),
            (ArchiveFolder + "en_US-libritts_r-medium.onnx.json", Encoding.UTF8.GetBytes(PiperTestFixtures.TrimmedConfigJson)),
            (ArchiveFolder + "tokens.txt", "a 1"u8.ToArray()),
            (ArchiveFolder + "espeak-ng-data/en_dict", "dict"u8.ToArray()),
        ]), TestContext.Current.CancellationToken);

        // Act
        await model.InstallAsync(_root, TestContext.Current.CancellationToken);
        using var backend = model.CreateBackend(_root);
        var echo = PiperTestFixtures.DecodeEcho(backend.Generate("Hello there.", 1.0f, 0));

        // Assert
        var installed = Directory.GetFileSystemEntries(_root).Select(Path.GetFileName).Order(StringComparer.Ordinal).ToList();
        Assert.Equal(["en_US-libritts_r-medium.onnx", "en_US-libritts_r-medium.onnx.json"], installed);
        Assert.Equal(22050, backend.SampleRate);
        Assert.Equal(1, echo.Ids[0]);
        Assert.Equal(2, echo.Ids[^1]);
        Assert.True(echo.Ids.Length > 10, "A sentence must produce more than the framing.");
    }

    /// <summary>
    ///     Proves the speaker the model resolves from a host's parameter values is the speaker the loaded voice is run
    ///     with, and that a value outside the voice's speakers runs speaker 0.
    /// </summary>
    [Fact]
    public void ModelManagementSubsystem_ResolvedSpeaker_ReachesTheLoadedVoice()
    {
        // Arrange
        PiperTestFixtures.WriteInstalledEchoVoice(_root);
        ISynthesisModel model = new OnnxPiperLibriTtsEnSynthesisModel();
        using var backend = model.CreateBackend(_root);
        var selected = new Dictionary<string, object> { [OnnxPiperLibriTtsEnSynthesisModel.SpeakerParameterId] = 7.0 };
        var outOfRange = new Dictionary<string, object> { [OnnxPiperLibriTtsEnSynthesisModel.SpeakerParameterId] = 5000.0 };

        // Act
        var selectedEcho = PiperTestFixtures.DecodeEcho(backend.Generate("Hello there.", 1.0f, model.ResolveSpeakerId(selected)));
        var outOfRangeEcho = PiperTestFixtures.DecodeEcho(backend.Generate("Hello there.", 1.0f, model.ResolveSpeakerId(outOfRange)));
        var defaultEcho = PiperTestFixtures.DecodeEcho(backend.Generate("Hello there.", 1.0f, model.ResolveSpeakerId(null)));

        // Assert
        Assert.Equal(7, selectedEcho.Speaker);
        Assert.Equal(0, outOfRangeEcho.Speaker);
        Assert.Equal(0, defaultEcho.Speaker);
    }

    /// <summary>
    ///     Proves a voice configuration that is unusable, or that describes another voice (one speaker, another sample
    ///     rate), fails with an invalid-data error even though the graph beside it is not loadable: the configuration is
    ///     checked before any inference session is created.
    /// </summary>
    /// <param name="find">The text of the trimmed configuration to replace.</param>
    /// <param name="replacement">The text to put in its place.</param>
    [Theory]
    [InlineData("\"num_speakers\": 904", "\"num_speakers\": 1")]
    [InlineData("\"sample_rate\": 22050", "\"sample_rate\": 16000")]
    [InlineData("\"phoneme_type\": \"espeak\"", "\"phoneme_type\": \"text\"")]
    [InlineData("\"phoneme_id_map\"", "\"phoneme_map\"")]
    public void ModelManagementSubsystem_CreateBackend_VoiceOfAnotherShapeOrBrokenConfiguration_FailsBeforeTheGraphIsLoaded(string find, string replacement)
    {
        // Arrange
        var configJson = PiperTestFixtures.TrimmedConfigJson;
        Assert.Contains(find, configJson, StringComparison.Ordinal);
        File.WriteAllText(Path.Combine(_root, "en_US-libritts_r-medium.onnx.json"), configJson.Replace(find, replacement, StringComparison.Ordinal));
        File.WriteAllBytes(Path.Combine(_root, "en_US-libritts_r-medium.onnx"), "this is not an onnx graph"u8.ToArray());
        ISynthesisModel model = new OnnxPiperLibriTtsEnSynthesisModel();

        // Act & Assert
        Assert.Throws<InvalidDataException>(() => model.CreateBackend(_root));
    }

    /// <summary>
    ///     Proves a preferred execution provider that is unavailable is skipped and the voice runs on the CPU provider,
    ///     for a model that was registered in a catalog with the provider name.
    /// </summary>
    [Fact]
    public void ModelManagementSubsystem_CreateBackend_UnavailablePreferredProvider_FallsBackToCpuAndSynthesizes()
    {
        // Arrange
        PiperTestFixtures.WriteInstalledEchoVoice(_root);
        using var catalog = new SpeechModelCatalog(new SpeechModelStoreOptions { RootPathOverride = Path.Combine(_root, "store") });
        var model = Assert.IsAssignableFrom<ISynthesisModel>(Assert.Single(catalog.AddPiperModels(["NotARealExecutionProvider"]).Enumerate()).Model);

        // Act
        using var backend = model.CreateBackend(_root);
        var echo = PiperTestFixtures.DecodeEcho(backend.Generate("Hello there.", 1.0f, 0));

        // Assert
        Assert.Equal(22050, backend.SampleRate);
        Assert.Equal(1, echo.Ids[0]);
    }
}
