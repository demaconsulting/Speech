using DemaConsulting.Speech.AudioSubsystem;
using DemaConsulting.Speech.Demo.RecognitionPanelSubsystem;
using DemaConsulting.Speech.Demo.Tests.Fakes;
using DemaConsulting.Speech.ModelManagementSubsystem;
using DemaConsulting.Speech.RecognitionSubsystem;
using NSubstitute;

namespace DemaConsulting.Speech.Demo.Tests.RecognitionPanelSubsystem;

/// <summary>
///     Unit tests for <see cref="RecognizerSessionFactory"/>.
/// </summary>
/// <remarks>
///     The "correct role composes a working recognizer" path delegates to the library's own
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
        RootPathOverride = Path.Combine(
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
    ///     Proves that Create rejects a missing model.
    /// </summary>
    [Fact]
    public void RecognizerSessionFactory_Create_NullModel_ThrowsArgumentNullException()
    {
        // Arrange
        var factory = new RecognizerSessionFactory(IsolatedStore());
        var device = Substitute.For<IAudioCaptureDevice>();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => factory.Create(null!, device));
    }

    /// <summary>
    ///     Proves that Create rejects a missing capture device.
    /// </summary>
    [Fact]
    public void RecognizerSessionFactory_Create_NullDevice_ThrowsArgumentNullException()
    {
        // Arrange
        var factory = new RecognizerSessionFactory(IsolatedStore());

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => factory.Create(new FakeSpeechModel(), null!));
    }

    /// <summary>
    ///     Proves that Create honestly reports a model that does not implement the library's
    ///     recognition role as an unavailable recognizer, exactly like a model that is not
    ///     installed, rather than throwing.
    /// </summary>
    [Fact]
    public void RecognizerSessionFactory_Create_ModelNotRecognitionRole_ReturnsUnavailableRecognizer()
    {
        // Arrange: a fake model that only ever implements the public ISpeechModel contract
        var factory = new RecognizerSessionFactory(IsolatedStore());
        var device = Substitute.For<IAudioCaptureDevice>();
        var model = new FakeSpeechModel(role: SpeechModelRole.Recognition);

        // Act
        var recognizer = factory.Create(model, device);

        // Assert: the honest unavailable fallback, not a real recognizer or an exception
        Assert.Same(UnavailableSpeechRecognizer.Instance, recognizer);
        Assert.False(recognizer.IsAvailable);
    }
}
