using DemaConsulting.Speech.ModelManagementSubsystem;
using DemaConsulting.Speech.Onnx.ChatterboxTurbo.ModelManagementSubsystem;
using DemaConsulting.Speech.SynthesisSubsystem;

namespace DemaConsulting.Speech.Onnx.ChatterboxTurbo.Tests.ModelManagementSubsystem;

/// <summary>
///     Unit tests for <see cref="SpeechModelCatalogChatterboxTurboExtensions"/>.
/// </summary>
public sealed class SpeechModelCatalogChatterboxTurboExtensionsTests : IDisposable
{
    /// <summary>A scratch directory unique to this test instance.</summary>
    private readonly string _root;

    /// <summary>Initializes a fresh scratch directory.</summary>
    public SpeechModelCatalogChatterboxTurboExtensionsTests()
    {
        _root = Path.Join(Path.GetTempPath(), "DemaConsulting.Speech.Onnx.ChatterboxTurbo.Tests", Guid.NewGuid().ToString("N"));
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
        catch (UnauthorizedAccessException)
        {
            // Best-effort cleanup only.
        }
    }

    /// <summary>Proves the extension method registers the model and returns the same catalog.</summary>
    [Fact]
    public void SpeechModelCatalogChatterboxTurboExtensions_AddChatterboxTurboModels_RegistersModelAndReturnsCatalog()
    {
        // Arrange
        using var catalog = new SpeechModelCatalog(new SpeechModelStoreOptions { RootPathOverride = Path.Combine(_root, "store") });

        // Act
        var result = catalog.AddChatterboxTurboModels(new ChatterboxTurboModelOptions { ReferenceVoices = [Voice("a")] });

        // Assert
        Assert.Same(catalog, result);
        var model = Assert.Single(catalog.Enumerate());
        Assert.Equal("chatterbox-turbo-onnx-en", model.Id);
        Assert.Throws<ArgumentNullException>(() => SpeechModelCatalogChatterboxTurboExtensions.AddChatterboxTurboModels(null!));
    }

    /// <summary>Proves the catalog extension offers voices from the catalog store's library by default.</summary>
    [Fact]
    public void SpeechModelCatalogChatterboxTurboExtensions_AddChatterboxTurboModels_DefaultsToCatalogStoreVoices()
    {
        // Arrange
        using var catalog = new SpeechModelCatalog(new SpeechModelStoreOptions { RootPathOverride = Path.Combine(_root, "store") });
        using (var library = new ReferenceVoiceLibrary(catalog.Store))
        {
            WriteWav(Path.Combine(library.EnsureUserDirectory(), "mine.wav"));
        }

        var options = new ChatterboxTurboModelOptions();

        // Act
        catalog.AddChatterboxTurboModels(options);

        // Assert
        var model = Assert.IsType<OnnxChatterboxTurboEnglishSynthesisModel>(Assert.Single(catalog.Enumerate()).Model);
        var choice = Assert.IsType<ChoiceParameter>(Assert.Single(model.Parameters));
        Assert.Equal(["mine"], choice.Options.Select(option => option.Value));
        Assert.Null(options.ReferenceVoiceLibrary);
    }

    /// <summary>Creates a one-second voice.</summary>
    private static ChatterboxTurboReferenceVoice Voice(string id) =>
        ChatterboxTurboReferenceVoice.FromSamples(id, new float[24000], 24000);

    /// <summary>Writes a one-second silent WAV file.</summary>
    private static void WriteWav(string path)
    {
        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream);
        const int Samples = 24000;
        writer.Write("RIFF"u8.ToArray());
        writer.Write(36 + (Samples * 2));
        writer.Write("WAVEfmt "u8.ToArray());
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)1);
        writer.Write(24000);
        writer.Write(48000);
        writer.Write((short)2);
        writer.Write((short)16);
        writer.Write("data"u8.ToArray());
        writer.Write(Samples * 2);
        writer.Write(new byte[Samples * 2]);
    }
}
