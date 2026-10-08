using DemaConsulting.Speech.ModelManagementSubsystem;
using DemaConsulting.Speech.Sherpa;
using DemaConsulting.Speech.Sherpa.ModelManagementSubsystem;

namespace DemaConsulting.Speech.Sherpa.Tests.ModelManagementSubsystem;

/// <summary>
///     Unit tests for <see cref="SpeechModelCatalogSherpaExtensions.AddSherpaModels"/>, proving it
///     registers exactly this package's four shipped models and supports fluent call chaining -
///     the direct replacement for the removed
///     <c>SpeechModelCatalog_KnownModels_ContainsAllFourRealModels</c> test that used to assert
///     the same facts against core's now-removed <c>SpeechModelCatalog.KnownModels</c>.
/// </summary>
public sealed class SpeechModelCatalogSherpaExtensionsTests : IDisposable
{
    /// <summary>A scratch root directory unique to this test instance, cleaned up on dispose.</summary>
    private readonly string _testRoot;

    /// <summary>
    ///     Initializes a fresh, unique scratch directory for each test, so no test touches the
    ///     real, shared, per-user model store.
    /// </summary>
    public SpeechModelCatalogSherpaExtensionsTests()
    {
        _testRoot = Path.Join(Path.GetTempPath(), "DemaConsulting.Speech.Sherpa.Tests", Guid.NewGuid().ToString("N"));
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
    ///     Proves that <see cref="SpeechModelCatalogSherpaExtensions.AddSherpaModels"/> returns
    ///     the exact same catalog instance it was called on, supporting fluent call chaining
    ///     (for example <c>new SpeechModelCatalog().AddSherpaModels()</c> at a composition root).
    /// </summary>
    [Fact]
    public void AddSherpaModels_Called_ReturnsSameCatalogInstance()
    {
        // Arrange
        using var catalog = new SpeechModelCatalog(NewStoreOptions());

        // Act
        var result = catalog.AddSherpaModels();

        // Assert
        Assert.Same(catalog, result);
    }

    /// <summary>
    ///     Proves that <see cref="SpeechModelCatalogSherpaExtensions.AddSherpaModels"/> registers
    ///     exactly the four models this package ships - two streaming recognition models and two
    ///     offline synthesis models - by their expected ids and roles.
    /// </summary>
    [Fact]
    public void AddSherpaModels_Called_RegistersExpectedFourModelsWithExpectedRoles()
    {
        // Arrange
        using var catalog = new SpeechModelCatalog(NewStoreOptions());

        // Act
        catalog.AddSherpaModels();
        var descriptors = catalog.Enumerate();

        // Assert
        var expectedIds = new[]
        {
            SherpaOnnxZipformerEnRecognitionModel.ModelId,
            SherpaOnnxNemotronStreamingEnRecognitionModel.ModelId,
            SherpaOnnxVitsLibriTtsEnglishSynthesisModel.ModelId,
            SherpaOnnxKokoroEnglishSynthesisModel.ModelId,
        };
        Assert.Equal(expectedIds, descriptors.Select(descriptor => descriptor.Id));

        var recognitionIds = new[]
        {
            SherpaOnnxZipformerEnRecognitionModel.ModelId,
            SherpaOnnxNemotronStreamingEnRecognitionModel.ModelId,
        };
        var synthesisIds = new[]
        {
            SherpaOnnxVitsLibriTtsEnglishSynthesisModel.ModelId,
            SherpaOnnxKokoroEnglishSynthesisModel.ModelId,
        };
        Assert.All(
            descriptors.Where(descriptor => recognitionIds.Contains(descriptor.Id)),
            descriptor => Assert.Equal(SpeechModelRole.Recognition, descriptor.Role));
        Assert.All(
            descriptors.Where(descriptor => synthesisIds.Contains(descriptor.Id)),
            descriptor => Assert.Equal(SpeechModelRole.Synthesis, descriptor.Role));
    }

    /// <summary>
    ///     Builds store options rooted at this test's scratch directory, so the catalog never
    ///     touches the real, shared, per-user model store.
    /// </summary>
    private SpeechModelStoreOptions NewStoreOptions() => new() { RootPathOverride = _testRoot };
}
