using DemaConsulting.Speech.Demo.SynthesisPanelSubsystem;
using DemaConsulting.Speech.Demo.Tests.Fakes;
using DemaConsulting.Speech.ModelManagementSubsystem;
using DemaConsulting.Speech.SynthesisSubsystem;

namespace DemaConsulting.Speech.Demo.Tests.SynthesisPanelSubsystem;

/// <summary>
///     Unit tests for <see cref="SynthesizerSessionFactory"/>.
/// </summary>
/// <remarks>
///     The "correct role composes a working engine" path delegates to the library's own
///     <see cref="SpeechSynthesizerFactory"/>, which requires an <see cref="ISynthesisModel"/> -
///     an interface only the library's own assemblies can implement (see the type's remarks).
///     That path is therefore outside this test project's reach and remains covered by the
///     library's own synthesis-subsystem tests; these tests cover every path this project can
///     reach: argument validation and the honest "wrong role" outcome.
/// </remarks>
public class SynthesizerSessionFactoryTests
{
    /// <summary>
    ///     Builds a store rooted inside this test run's own output directory.
    /// </summary>
    /// <returns>A store isolated from any developer's real installed-model directory.</returns>
    private static SpeechModelStore IsolatedStore() => new(new SpeechModelStoreOptions
    {
        RootPathOverride = Path.Join(
            AppContext.BaseDirectory, "SynthesizerSessionFactoryTests", Guid.NewGuid().ToString("N"))
    });

    /// <summary>
    ///     Proves that the factory rejects a missing store.
    /// </summary>
    [Fact]
    public void SynthesizerSessionFactory_Constructor_NullStore_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new SynthesizerSessionFactory(null!));
    }

    /// <summary>
    ///     Proves that LoadAsync rejects a missing model.
    /// </summary>
    [Fact]
    public async Task SynthesizerSessionFactory_LoadAsync_NullModel_ThrowsArgumentNullException()
    {
        // Arrange
        var factory = new SynthesizerSessionFactory(IsolatedStore());

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => factory.LoadAsync(null!, null, TestContext.Current.CancellationToken));
    }

    /// <summary>
    ///     Proves that LoadAsync honestly reports a model that does not implement the library's
    ///     synthesis role as an unavailable engine, exactly like a model that is not installed,
    ///     rather than throwing.
    /// </summary>
    [Fact]
    public async Task SynthesizerSessionFactory_LoadAsync_ModelNotSynthesisRole_ReturnsUnavailableEngine()
    {
        // Arrange: a fake model that only ever implements the public ISpeechModel contract
        var factory = new SynthesizerSessionFactory(IsolatedStore());
        var model = new FakeSpeechModel(role: SpeechModelRole.Synthesis);

        // Act
        var engine = await factory.LoadAsync(model, null, TestContext.Current.CancellationToken);

        // Assert: the honest unavailable fallback, not a real engine or an exception
        Assert.Same(UnavailableSpeechSynthesizerEngine.Instance, engine);
        Assert.False(engine.IsAvailable);
    }

    /// <summary>
    ///     Proves that LoadAsync honestly reports a wrong-role model as unavailable even when an
    ///     optional <c>parameterValues</c> bag is supplied, since the role check happens before
    ///     any parameter is consulted.
    /// </summary>
    [Fact]
    public async Task SynthesizerSessionFactory_LoadAsync_ModelNotSynthesisRoleWithParameterValues_ReturnsUnavailableEngine()
    {
        // Arrange
        var factory = new SynthesizerSessionFactory(IsolatedStore());
        var model = new FakeSpeechModel(role: SpeechModelRole.Synthesis);
        IReadOnlyDictionary<string, object> parameterValues = new Dictionary<string, object> { ["voice"] = "af" };

        // Act
        var engine = await factory.LoadAsync(model, parameterValues, TestContext.Current.CancellationToken);

        // Assert
        Assert.Same(UnavailableSpeechSynthesizerEngine.Instance, engine);
        Assert.False(engine.IsAvailable);
    }
}
