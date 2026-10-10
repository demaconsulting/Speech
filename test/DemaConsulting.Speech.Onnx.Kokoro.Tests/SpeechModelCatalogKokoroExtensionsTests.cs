using DemaConsulting.Speech.ModelManagementSubsystem;
using DemaConsulting.Speech.Onnx.Kokoro.ModelManagementSubsystem;

namespace DemaConsulting.Speech.Onnx.Kokoro.Tests;

/// <summary>
///     Unit tests for <see cref="SpeechModelCatalogKokoroExtensions.AddKokoroModels"/>, proving it
///     registers exactly this package's one shipped offline synthesis model and supports fluent
///     call chaining.
/// </summary>
public sealed class SpeechModelCatalogKokoroExtensionsTests : IDisposable
{
    /// <summary>A scratch root directory unique to this test instance, cleaned up on dispose.</summary>
    private readonly string _testRoot;

    /// <summary>
    ///     Initializes a fresh, unique scratch directory for each test, so no test touches the
    ///     real, shared, per-user model store.
    /// </summary>
    public SpeechModelCatalogKokoroExtensionsTests()
    {
        _testRoot = Path.Join(Path.GetTempPath(), "DemaConsulting.Speech.Onnx.Kokoro.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testRoot);
    }

    /// <summary>Deletes the scratch directory tree created for this test instance.</summary>
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
    ///     Proves that <see cref="SpeechModelCatalogKokoroExtensions.AddKokoroModels"/> returns
    ///     the exact same catalog instance it was called on, supporting fluent call chaining
    ///     (for example <c>new SpeechModelCatalog().AddKokoroModels()</c> at a composition root).
    /// </summary>
    [Fact]
    public void AddKokoroModels_Called_ReturnsSameCatalogInstance()
    {
        // Arrange
        using var catalog = new SpeechModelCatalog(NewStoreOptions());

        // Act
        var result = catalog.AddKokoroModels();

        // Assert
        Assert.Same(catalog, result);
    }

    /// <summary>
    ///     Proves that <see cref="SpeechModelCatalogKokoroExtensions.AddKokoroModels"/> registers
    ///     exactly this package's one shipped offline synthesis model, by its expected id and
    ///     role.
    /// </summary>
    [Fact]
    public void AddKokoroModels_Called_RegistersExpectedSingleSynthesisModel()
    {
        // Arrange
        using var catalog = new SpeechModelCatalog(NewStoreOptions());

        // Act
        catalog.AddKokoroModels();
        var descriptors = catalog.Enumerate();

        // Assert
        var descriptor = Assert.Single(descriptors);
        Assert.Equal(OnnxKokoroEnglishSynthesisModel.ModelId, descriptor.Id);
        Assert.Equal(SpeechModelRole.Synthesis, descriptor.Role);
    }

    /// <summary>
    ///     Proves that <see cref="SpeechModelCatalogKokoroExtensions.AddKokoroModels"/> throws
    ///     <see cref="ArgumentNullException"/> for a null catalog, rather than a confusing
    ///     <see cref="NullReferenceException"/>.
    /// </summary>
    [Fact]
    public void AddKokoroModels_NullCatalog_ThrowsArgumentNullException()
    {
        // Arrange
        SpeechModelCatalog catalog = null!;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => catalog.AddKokoroModels());
    }

    /// <summary>
    ///     Builds store options rooted at this test's scratch directory, so the catalog never
    ///     touches the real, shared, per-user model store.
    /// </summary>
    private SpeechModelStoreOptions NewStoreOptions() => new() { RootPathOverride = _testRoot };
}
