using DemaConsulting.Speech.AudioSubsystem;
using DemaConsulting.Speech.Demo.SynthesisPanelSubsystem;
using DemaConsulting.Speech.Demo.Tests.Fakes;
using DemaConsulting.Speech.ModelManagementSubsystem;
using DemaConsulting.Speech.SynthesisSubsystem;

namespace DemaConsulting.Speech.Demo.Tests.SynthesisPanelSubsystem;

/// <summary>
///     Unit tests for <see cref="SynthesizerSessionFactory"/>.
/// </summary>
/// <remarks>
///     The correct-role path uses a deterministic public <see cref="ISynthesisModel"/> test
///     double and a fake backend, so no downloaded model or native runtime is needed.
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
        var model = new FakeSpeechModel(role: SpeechModelRole.Recognition);

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
        var model = new FakeSpeechModel(role: SpeechModelRole.Recognition);
        IReadOnlyDictionary<string, object> parameterValues = new Dictionary<string, object> { ["voice"] = "af" };

        // Act
        var engine = await factory.LoadAsync(model, parameterValues, TestContext.Current.CancellationToken);

        // Assert
        Assert.Same(UnavailableSpeechSynthesizerEngine.Instance, engine);
        Assert.False(engine.IsAvailable);
    }

    /// <summary>
    ///     Proves that LoadAsync returns a real synthesizer, built from the installed model files,
    ///     for a model that implements the synthesis role.
    /// </summary>
    [Fact]
    public async Task SynthesizerSessionFactory_LoadAsync_SynthesisModel_ReturnsEngineForThatModel()
    {
        // Arrange
        var store = IsolatedStore();
        var model = new SynthesisModelDouble();
        var installed = store.GetCurrentDirectory(model.Id);
        Directory.CreateDirectory(installed);
        var factory = new SynthesizerSessionFactory(store);
        IReadOnlyDictionary<string, object> values = new Dictionary<string, object> { ["gain"] = 0.5 };

        // Act
        var engine = await factory.LoadAsync(model, values, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotSame(UnavailableSpeechSynthesizerEngine.Instance, engine);
        Assert.True(engine.IsAvailable);
        Assert.Equal(installed, model.BackendDirectory);
        await engine.DisposeAsync();
    }

    /// <summary>
    ///     Proves that LoadAsync rejects an invalid value for a declared parameter.
    /// </summary>
    [Fact]
    public async Task SynthesizerSessionFactory_LoadAsync_InvalidParameters_PropagatesArgumentException()
    {
        // Arrange
        var store = IsolatedStore();
        var model = new SynthesisModelDouble();
        Directory.CreateDirectory(store.GetCurrentDirectory(model.Id));
        var factory = new SynthesizerSessionFactory(store);
        IReadOnlyDictionary<string, object> values = new Dictionary<string, object> { ["gain"] = 99.0 };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => factory.LoadAsync(model, values, TestContext.Current.CancellationToken));
    }

    /// <summary>
    ///     Deterministic public synthesis-role model double declaring one numeric parameter.
    /// </summary>
    private sealed class SynthesisModelDouble : ISynthesisModel
    {
        private readonly FakeSpeechModel _inner = new("synthesis-double", "Synthesis Double");

        /// <summary>Gets the directory the backend was created for.</summary>
        public string? BackendDirectory { get; private set; }

        public string Id => _inner.Id;

        public string DisplayName => _inner.DisplayName;

        public SpeechModelRole Role => SpeechModelRole.Synthesis;

        public IReadOnlyList<ISpeechModelParameter> Parameters { get; } =
        [
            new NumericParameter("gain", "Gain", "Output gain.", new NumericParameterBounds(0.0, 1.0, 0.1, 0.5))
        ];

        public SpeechModelAudioTagSupport AudioTagSupport => _inner.AudioTagSupport;

        public SpeechModelDownloadDescriptor DownloadDescriptor => _inner.DownloadDescriptor;

        public AudioFormat PreferredAudioFormat => AudioFormat.Mono(22050);

        public ISynthesisBackend CreateBackend(string installedModelDirectory)
        {
            BackendDirectory = installedModelDirectory;
            return new BackendDouble();
        }
    }

    /// <summary>
    ///     Backend producing silence.
    /// </summary>
    private sealed class BackendDouble : ISynthesisBackend
    {
        public int SampleRate => 22050;

        public BackendAudio Generate(string text, float speed, int speakerId) => new([0f], SampleRate);

        public void Dispose()
        {
        }
    }
}
