using DemaConsulting.Speech.AudioSubsystem;
using DemaConsulting.Speech.Demo.RecognitionPanelSubsystem;
using DemaConsulting.Speech.Demo.Tests.Fakes;
using DemaConsulting.Speech.ModelManagementSubsystem;
using DemaConsulting.Speech.RecognitionSubsystem;

namespace DemaConsulting.Speech.Demo.Tests.RecognitionPanelSubsystem;

/// <summary>
///     Unit tests for <see cref="RecognizerSessionFactory"/>.
/// </summary>
/// <remarks>
///     The correct-role path uses a deterministic public <see cref="IRecognitionModel"/> test
///     double and a fake backend, so no downloaded model or native runtime is needed.
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

    /// <summary>
    ///     Proves that LoadAsync returns a real engine, built from the installed model files, for
    ///     a model that implements the recognition role.
    /// </summary>
    [Fact]
    public async Task RecognizerSessionFactory_LoadAsync_RecognitionModel_ReturnsEngineForThatModel()
    {
        // Arrange
        var store = IsolatedStore();
        var model = new RecognitionModelDouble();
        var installed = store.GetCurrentDirectory(model.Id);
        Directory.CreateDirectory(installed);
        var factory = new RecognizerSessionFactory(store);

        // Act
        var engine = await factory.LoadAsync(model, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotSame(UnavailableSpeechRecognizerEngine.Instance, engine);
        Assert.True(engine.IsAvailable);
        Assert.Equal(installed, model.BackendDirectory);
        await engine.DisposeAsync();
    }

    /// <summary>
    ///     Deterministic public recognition-role model double.
    /// </summary>
    private sealed class RecognitionModelDouble : IRecognitionModel
    {
        private readonly FakeSpeechModel _inner = new("recognition-double", "Recognition Double");

        /// <summary>Gets the directory the backend was created for.</summary>
        public string? BackendDirectory { get; private set; }

        public string Id => _inner.Id;

        public string DisplayName => _inner.DisplayName;

        public SpeechModelRole Role => SpeechModelRole.Recognition;

        public IReadOnlyList<ISpeechModelParameter> Parameters => _inner.Parameters;

        public SpeechModelAudioTagSupport AudioTagSupport => _inner.AudioTagSupport;

        public SpeechModelDownloadDescriptor DownloadDescriptor => _inner.DownloadDescriptor;

        public AudioFormat AudioFormat => AudioFormat.Mono(16000);

        public IRecognitionBackend CreateBackend(string installedModelDirectory)
        {
            BackendDirectory = installedModelDirectory;
            return new BackendDouble();
        }
    }

    /// <summary>
    ///     Backend that never recognizes anything.
    /// </summary>
    private sealed class BackendDouble : IRecognitionBackend
    {
        public void AcceptSamples(ReadOnlySpan<float> monoSamples)
        {
        }

        public bool TryDecode(out SpeechRecognitionResult? result)
        {
            result = null;
            return false;
        }

        public bool TryFlush(out SpeechRecognitionResult? result) => TryDecode(out result);

        public void Reset()
        {
        }

        public void Dispose()
        {
        }
    }
}