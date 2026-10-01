using DemaConsulting.Speech.Demo.RecognitionPanelSubsystem;
using DemaConsulting.Speech.Demo.Tests.Fakes;
using DemaConsulting.Speech.ModelManagementSubsystem;
using DemaConsulting.Speech.RecognitionSubsystem;

namespace DemaConsulting.Speech.Demo.Tests.RecognitionPanelSubsystem;

/// <summary>
///     Unit tests for <see cref="RecognizerSessionFactory"/>.
/// </summary>
/// <remarks>
///     The "correct role composes a working engine" path delegates to the library's own
///     <see cref="SpeechRecognizerFactory"/>, which requires an <see cref="IRecognitionModel"/> -
///     an interface only the library's own assemblies can implement (see the type's remarks).
///     That path is therefore outside this test project's reach and remains covered by the
///     library's own recognition-subsystem tests; these tests cover every path this project can
///     reach: argument validation and the honest "wrong role" outcome.
/// </remarks>
public class RecognizerSessionFactoryTests
{
    /// <summary>
    ///     Builds a store rooted inside this test run's own output directory.
    /// </summary>
    /// <returns>A store isolated from any developer's real installed-model directory.</returns>
    private static SpeechModelStore IsolatedStore() => new(new SpeechModelStoreOptions
    {
        RootPathOverride = Path.Join(
            AppContext.BaseDirectory, "RecognizerSessionFactoryTests", Guid.NewGuid().ToString("N"))
    });

    /// <summary>
    ///     Proves that the factory rejects a missing store.
    /// </summary>
    [Fact]
    public void RecognizerSessionFactory_Constructor_NullStore_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new RecognizerSessionFactory(null!));
    }

    /// <summary>
    ///     Proves that LoadAsync rejects a missing model.
    /// </summary>
    [Fact]
    public async Task RecognizerSessionFactory_LoadAsync_NullModel_ThrowsArgumentNullException()
    {
        // Arrange
        var factory = new RecognizerSessionFactory(IsolatedStore());

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => factory.LoadAsync(null!, TestContext.Current.CancellationToken));
    }

    /// <summary>
    ///     Proves that LoadAsync honestly reports a model that does not implement the library's
    ///     recognition role as an unavailable engine, exactly like a model that is not installed,
    ///     rather than throwing.
    /// </summary>
    [Fact]
    public async Task RecognizerSessionFactory_LoadAsync_ModelNotRecognitionRole_ReturnsUnavailableEngine()
    {
        // Arrange: a fake model that only ever implements the public ISpeechModel contract
        var factory = new RecognizerSessionFactory(IsolatedStore());
        var model = new FakeSpeechModel(role: SpeechModelRole.Recognition);

        // Act
        var engine = await factory.LoadAsync(model, TestContext.Current.CancellationToken);

        // Assert: the honest unavailable fallback, not a real engine or an exception
        Assert.Same(UnavailableSpeechRecognizerEngine.Instance, engine);
        Assert.False(engine.IsAvailable);
    }
}
