using DemaConsulting.Speech.AudioSubsystem;
using DemaConsulting.Speech.Demo.SynthesisPanelSubsystem;
using DemaConsulting.Speech.Demo.Tests.Fakes;
using DemaConsulting.Speech.ModelManagementSubsystem;
using DemaConsulting.Speech.SynthesisSubsystem;
using NSubstitute;

namespace DemaConsulting.Speech.Demo.Tests.SynthesisPanelSubsystem;

/// <summary>
///     Unit tests for <see cref="SynthesizerSessionFactory"/>.
/// </summary>
/// <remarks>
///     The "correct role composes a working synthesizer" path delegates to the library's own
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
    ///     Proves that Create rejects a missing model.
    /// </summary>
    [Fact]
    public void SynthesizerSessionFactory_Create_NullModel_ThrowsArgumentNullException()
    {
        // Arrange
        var factory = new SynthesizerSessionFactory(IsolatedStore());
        var device = Substitute.For<IAudioPlaybackDevice>();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => factory.Create(null!, device));
    }

    /// <summary>
    ///     Proves that Create rejects a missing playback device.
    /// </summary>
    [Fact]
    public void SynthesizerSessionFactory_Create_NullDevice_ThrowsArgumentNullException()
    {
        // Arrange
        var factory = new SynthesizerSessionFactory(IsolatedStore());

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => factory.Create(new FakeSpeechModel(), null!));
    }

    /// <summary>
    ///     Proves that Create honestly reports a model that does not implement the library's
    ///     synthesis role as an unavailable synthesizer, exactly like a model that is not
    ///     installed, rather than throwing.
    /// </summary>
    [Fact]
    public void SynthesizerSessionFactory_Create_ModelNotSynthesisRole_ReturnsUnavailableSynthesizer()
    {
        // Arrange: a fake model that only ever implements the public ISpeechModel contract
        var factory = new SynthesizerSessionFactory(IsolatedStore());
        var device = Substitute.For<IAudioPlaybackDevice>();
        var model = new FakeSpeechModel(role: SpeechModelRole.Synthesis);

        // Act
        var synthesizer = factory.Create(model, device);

        // Assert: the honest unavailable fallback, not a real synthesizer or an exception
        Assert.Same(UnavailableSpeechSynthesizer.Instance, synthesizer);
        Assert.False(synthesizer.IsAvailable);
    }

    /// <summary>
    ///     Proves that Create honestly reports a wrong-role model as unavailable even when an
    ///     optional <c>parameterValues</c> bag is supplied, since the role check happens before
    ///     any parameter is consulted.
    /// </summary>
    [Fact]
    public void SynthesizerSessionFactory_Create_ModelNotSynthesisRoleWithParameterValues_ReturnsUnavailableSynthesizer()
    {
        // Arrange
        var factory = new SynthesizerSessionFactory(IsolatedStore());
        var device = Substitute.For<IAudioPlaybackDevice>();
        var model = new FakeSpeechModel(role: SpeechModelRole.Synthesis);
        IReadOnlyDictionary<string, object> parameterValues = new Dictionary<string, object> { ["voice"] = "af" };

        // Act
        var synthesizer = factory.Create(model, device, parameterValues);

        // Assert
        Assert.Same(UnavailableSpeechSynthesizer.Instance, synthesizer);
        Assert.False(synthesizer.IsAvailable);
    }
}
