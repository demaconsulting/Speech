using DemaConsulting.Speech.ModelManagementSubsystem;
using DemaConsulting.Speech.Onnx.Piper.ModelManagementSubsystem;

namespace DemaConsulting.Speech.Onnx.Piper.Tests;

/// <summary>
///     Behavior tests for <see cref="SpeechModelCatalogPiperExtensions.AddPiperModels"/>: it registers this package's one
///     synthesis model, returns the same catalog for chaining and rejects a null catalog.
/// </summary>
public sealed class SpeechModelCatalogPiperExtensionsTests : IDisposable
{
    private readonly string _testRoot = Path.Join(Path.GetTempPath(), "DemaConsulting.Speech.Onnx.Piper.Tests", Guid.NewGuid().ToString("N"));

    /// <summary>Initializes a scratch store root so no test touches the real per-user model store.</summary>
    public SpeechModelCatalogPiperExtensionsTests()
    {
        Directory.CreateDirectory(_testRoot);
    }

    /// <summary>Deletes the scratch store root.</summary>
    public void Dispose()
    {
        try
        {
            Directory.Delete(_testRoot, recursive: true);
        }
        catch (IOException)
        {
            // Best-effort cleanup only.
        }
    }

    /// <summary>Proves the extension registers exactly the Piper synthesis model.</summary>
    [Fact]
    public void AddPiperModels_Called_RegistersExpectedSingleSynthesisModel()
    {
        // Arrange
        using var catalog = new SpeechModelCatalog(new SpeechModelStoreOptions { RootPathOverride = _testRoot });

        // Act
        catalog.AddPiperModels();
        var descriptor = Assert.Single(catalog.Enumerate());

        // Assert
        Assert.Equal(OnnxPiperLibriTtsEnSynthesisModel.ModelId, descriptor.Id);
        Assert.Equal(SpeechModelRole.Synthesis, descriptor.Role);
        Assert.IsType<OnnxPiperLibriTtsEnSynthesisModel>(descriptor.Model);
    }

    /// <summary>Proves the extension returns the catalog it was called on, so registrations can be chained.</summary>
    [Fact]
    public void AddPiperModels_Called_ReturnsSameCatalogInstance()
    {
        // Arrange
        using var catalog = new SpeechModelCatalog(new SpeechModelStoreOptions { RootPathOverride = _testRoot });

        // Act
        var result = catalog.AddPiperModels(["NotARealExecutionProvider"]);

        // Assert
        Assert.Same(catalog, result);
        Assert.Single(catalog.Enumerate());
    }

    /// <summary>Proves a null catalog is rejected with an argument exception.</summary>
    [Fact]
    public void AddPiperModels_NullCatalog_ThrowsArgumentNullException()
    {
        // Arrange
        SpeechModelCatalog catalog = null!;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => catalog.AddPiperModels());
    }
}
