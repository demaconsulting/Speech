using DemaConsulting.Speech.ModelManagementSubsystem;
using DemaConsulting.Speech.Onnx.NemotronStt.ModelManagementSubsystem;

namespace DemaConsulting.Speech.Onnx.NemotronStt.Tests;

/// <summary>
///     Unit tests for <see cref="SpeechModelCatalogNemotronSttExtensions.AddNemotronSttModels"/>,
///     proving it registers this package's one recognition model and supports fluent chaining.
/// </summary>
public sealed class SpeechModelCatalogNemotronSttExtensionsTests : IDisposable
{
    /// <summary>A scratch root directory unique to this test instance, cleaned up on dispose.</summary>
    private readonly string _testRoot;

    /// <summary>Initializes a fresh scratch directory so no test touches the real model store.</summary>
    public SpeechModelCatalogNemotronSttExtensionsTests()
    {
        _testRoot = Path.Join(Path.GetTempPath(), "DemaConsulting.Speech.Onnx.NemotronStt.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testRoot);
    }

    /// <summary>Deletes the scratch directory tree.</summary>
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
            // Best-effort cleanup only.
        }
    }

    /// <summary>Proves the call returns the same catalog instance.</summary>
    [Fact]
    public void AddNemotronSttModels_Called_ReturnsSameCatalogInstance()
    {
        using var catalog = new SpeechModelCatalog(NewStoreOptions());

        Assert.Same(catalog, catalog.AddNemotronSttModels());
    }

    /// <summary>Proves exactly the one Nemotron model is registered, as a recognition model, not downloaded.</summary>
    [Fact]
    public void AddNemotronSttModels_Called_RegistersOneRecognitionModel()
    {
        using var catalog = new SpeechModelCatalog(NewStoreOptions());

        var descriptor = Assert.Single(catalog.AddNemotronSttModels().Enumerate());

        Assert.Equal(OnnxNemotronMultilingualRecognitionModel.ModelId, descriptor.Id);
        Assert.Equal(SpeechModelRole.Recognition, descriptor.Role);
        Assert.Equal(SpeechModelState.NotDownloaded, descriptor.State);
    }

    /// <summary>Proves the optional execution-provider preference list is accepted.</summary>
    [Fact]
    public void AddNemotronSttModels_WithProviders_Registers()
    {
        using var catalog = new SpeechModelCatalog(NewStoreOptions());

        Assert.Single(catalog.AddNemotronSttModels(["CPU"]).Enumerate());
    }

    /// <summary>Builds store options rooted at the scratch directory.</summary>
    private SpeechModelStoreOptions NewStoreOptions() => new() { RootPathOverride = _testRoot };
}
