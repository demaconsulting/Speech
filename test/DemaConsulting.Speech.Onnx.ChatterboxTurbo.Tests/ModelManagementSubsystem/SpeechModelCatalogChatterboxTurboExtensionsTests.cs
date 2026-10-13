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
        var registered = catalog.Enumerate();

        // Assert
        Assert.Same(catalog, result);
        var model = Assert.Single(registered);
        Assert.Equal("chatterbox-turbo-onnx-en", model.Id);
    }

    /// <summary>Proves a null catalog is rejected.</summary>
    [Fact]
    public void SpeechModelCatalogChatterboxTurboExtensions_AddChatterboxTurboModels_NullCatalog_ThrowsArgumentNullException()
    {
        // Arrange
        SpeechModelCatalog? catalog = null;

        // Act
        var exception = Record.Exception(() => catalog!.AddChatterboxTurboModels());

        // Assert
        Assert.IsType<ArgumentNullException>(exception);
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
        var registered = catalog.Enumerate();

        // Assert
        var model = Assert.IsType<OnnxChatterboxTurboEnglishSynthesisModel>(Assert.Single(registered).Model);
        var choice = Assert.IsType<ChoiceParameter>(Assert.Single(model.Parameters));
        Assert.Equal(["mine"], choice.Options.Select(option => option.Value));
        Assert.Null(options.ReferenceVoiceLibrary);
    }

    /// <summary>Proves registering leaves the caller's options object unchanged.</summary>
    [Fact]
    public void SpeechModelCatalogChatterboxTurboExtensions_AddChatterboxTurboModels_OptionsObject_IsNotMutated()
    {
        // Arrange
        using var catalog = new SpeechModelCatalog(new SpeechModelStoreOptions { RootPathOverride = Path.Combine(_root, "store") });
        var options = new ChatterboxTurboModelOptions();

        // Act
        catalog.AddChatterboxTurboModels(options);

        // Assert
        Assert.Null(options.ReferenceVoiceLibrary);
        Assert.Empty(options.ReferenceVoices);
    }

    /// <summary>Proves the registered model carries the selected precision variants' id and file set.</summary>
    [Fact]
    public void SpeechModelCatalogChatterboxTurboExtensions_AddChatterboxTurboModels_Variants_AreComposed()
    {
        // Arrange
        using var catalog = new SpeechModelCatalog(new SpeechModelStoreOptions { RootPathOverride = Path.Combine(_root, "store") });
        var options = new ChatterboxTurboModelOptions
        {
            LanguageModelVariant = ChatterboxTurboLanguageModelVariant.Fp32,
            AuxiliaryModelVariant = ChatterboxTurboAuxiliaryModelVariant.Fp16,
            IncludeStoreVoices = false,
        };

        // Act
        catalog.AddChatterboxTurboModels(options);
        var registered = Assert.Single(catalog.Enumerate());

        // Assert
        var model = Assert.IsType<OnnxChatterboxTurboEnglishSynthesisModel>(registered.Model);
        Assert.Equal("chatterbox-turbo-onnx-en-lm-fp32-aux-fp16", registered.Id);
        Assert.Equal(8, model.DownloadDescriptor.Files.Count);
    }

    /// <summary>Proves the registered model offers the explicitly supplied voices.</summary>
    [Fact]
    public void SpeechModelCatalogChatterboxTurboExtensions_AddChatterboxTurboModels_ExplicitVoices_AreComposed()
    {
        // Arrange
        using var catalog = new SpeechModelCatalog(new SpeechModelStoreOptions { RootPathOverride = Path.Combine(_root, "store") });
        var options = new ChatterboxTurboModelOptions
        {
            ReferenceVoices = [Voice("a"), Voice("b")],
            IncludeStoreVoices = false,
        };

        // Act
        catalog.AddChatterboxTurboModels(options);
        var registered = Assert.Single(catalog.Enumerate());

        // Assert
        var model = Assert.IsType<OnnxChatterboxTurboEnglishSynthesisModel>(registered.Model);
        var choice = Assert.IsType<ChoiceParameter>(Assert.Single(model.Parameters));
        Assert.Equal(["a", "b"], choice.Options.Select(option => option.Value));
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
