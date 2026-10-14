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

        // Act
        var engine = await factory.LoadAsync(model, null, TestContext.Current.CancellationToken);

        // Assert: a real engine composed from the installed directory of that model
        Assert.NotSame(UnavailableSpeechSynthesizerEngine.Instance, engine);
        Assert.True(engine.IsAvailable);
        Assert.Equal(installed, model.BackendDirectory);
        await engine.DisposeAsync();
    }

    /// <summary>
    ///     Proves that LoadAsync forwards the supplied parameter values so they reach the model
    ///     when speech is synthesized, using non-default values so a dropped bag is detectable.
    /// </summary>
    [Fact]
    public async Task SynthesizerSessionFactory_LoadAsync_SynthesisModelWithValues_ForwardsAppliedValues()
    {
        // Arrange: non-default values (gain default is 0.5, voice default is "a")
        var store = IsolatedStore();
        var model = new SynthesisModelDouble();
        Directory.CreateDirectory(store.GetCurrentDirectory(model.Id));
        var factory = new SynthesizerSessionFactory(store);
        IReadOnlyDictionary<string, object> values = new Dictionary<string, object>
        {
            ["gain"] = 0.8,
            ["voice"] = "b"
        };

        // Act
        var engine = await factory.LoadAsync(model, values, TestContext.Current.CancellationToken);
        await engine.SynthesizeAsync("Hello there.", TestContext.Current.CancellationToken);

        // Assert: the model resolved its speaker from the forwarded values and the backend used it
        Assert.True(engine.IsAvailable);
        Assert.NotNull(model.ReceivedValues);
        Assert.Equal(0.8, model.ReceivedValues["gain"]);
        Assert.Equal("b", model.ReceivedValues["voice"]);
        Assert.Equal(1, model.Backend?.LastSpeakerId);
        await engine.DisposeAsync();
    }

    /// <summary>
    ///     Proves that LoadAsync rejects a value of the wrong type for a declared parameter.
    /// </summary>
    [Fact]
    public async Task SynthesizerSessionFactory_LoadAsync_WrongTypeParameter_PropagatesArgumentException()
    {
        // Arrange
        var factory = FactoryWithInstalledDouble(out var model);
        IReadOnlyDictionary<string, object> values = new Dictionary<string, object> { ["gain"] = "loud" };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => factory.LoadAsync(model, values, TestContext.Current.CancellationToken));
    }

    /// <summary>
    ///     Proves that LoadAsync rejects a numeric value outside the declared range.
    /// </summary>
    [Fact]
    public async Task SynthesizerSessionFactory_LoadAsync_OutOfRangeParameter_PropagatesArgumentException()
    {
        // Arrange
        var factory = FactoryWithInstalledDouble(out var model);
        IReadOnlyDictionary<string, object> values = new Dictionary<string, object> { ["gain"] = 99.0 };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => factory.LoadAsync(model, values, TestContext.Current.CancellationToken));
    }

    /// <summary>
    ///     Proves that LoadAsync rejects a choice value the parameter does not offer.
    /// </summary>
    [Fact]
    public async Task SynthesizerSessionFactory_LoadAsync_UnofferedChoice_PropagatesArgumentException()
    {
        // Arrange
        var factory = FactoryWithInstalledDouble(out var model);
        IReadOnlyDictionary<string, object> values = new Dictionary<string, object> { ["voice"] = "zzz" };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => factory.LoadAsync(model, values, TestContext.Current.CancellationToken));
    }

    /// <summary>
    ///     Builds a factory over an isolated store in which the synthesis model double is installed.
    /// </summary>
    /// <param name="model">The installed model double.</param>
    /// <returns>The factory under test.</returns>
    private static SynthesizerSessionFactory FactoryWithInstalledDouble(out SynthesisModelDouble model)
    {
        var store = IsolatedStore();
        model = new SynthesisModelDouble();
        Directory.CreateDirectory(store.GetCurrentDirectory(model.Id));
        return new SynthesizerSessionFactory(store);
    }

    /// <summary>
    ///     Deterministic public synthesis-role model double declaring one numeric and one choice
    ///     parameter and recording what it is given.
    /// </summary>
    private sealed class SynthesisModelDouble : ISynthesisModel
    {
        private readonly FakeSpeechModel _inner = new("synthesis-double", "Synthesis Double");

        /// <summary>Gets the directory the backend was created for.</summary>
        public string? BackendDirectory { get; private set; }

        /// <summary>Gets the most recent backend created by this model.</summary>
        public BackendDouble? Backend { get; private set; }

        /// <summary>Gets the parameter value bag most recently passed to speaker resolution.</summary>
        public IReadOnlyDictionary<string, object>? ReceivedValues { get; private set; }

        /// <inheritdoc/>
        public string Id => _inner.Id;

        /// <inheritdoc/>
        public string DisplayName => _inner.DisplayName;

        /// <inheritdoc/>
        public SpeechModelRole Role => SpeechModelRole.Synthesis;

        /// <inheritdoc/>
        public IReadOnlyList<ISpeechModelParameter> Parameters { get; } =
        [
            new NumericParameter("gain", "Gain", "Output gain.", new NumericParameterBounds(0.0, 1.0, 0.1, 0.5)),
            new ChoiceParameter(
                "voice",
                "Voice",
                "Voice to speak with.",
                [new ChoiceParameterOption("a", "Voice A"), new ChoiceParameterOption("b", "Voice B")],
                "a")
        ];

        /// <inheritdoc/>
        public SpeechModelAudioTagSupport AudioTagSupport => _inner.AudioTagSupport;

        /// <inheritdoc/>
        public SpeechModelDownloadDescriptor DownloadDescriptor => _inner.DownloadDescriptor;

        /// <inheritdoc/>
        public AudioFormat PreferredAudioFormat => AudioFormat.Mono(22050);

        /// <inheritdoc/>
        public ISynthesisBackend CreateBackend(string installedModelDirectory)
        {
            BackendDirectory = installedModelDirectory;
            Backend = new BackendDouble();
            return Backend;
        }

        /// <inheritdoc/>
        public int ResolveSpeakerId(IReadOnlyDictionary<string, object>? parameterValues)
        {
            ReceivedValues = parameterValues;
            return parameterValues is not null && parameterValues.TryGetValue("voice", out var voice) &&
                   Equals(voice, "b")
                ? 1
                : 0;
        }
    }

    /// <summary>
    ///     Backend producing deterministic silence and recording the speaker it was asked for.
    /// </summary>
    private sealed class BackendDouble : ISynthesisBackend
    {
        /// <summary>Gets the speaker id of the most recent generation request, or null if none.</summary>
        public int? LastSpeakerId { get; private set; }

        /// <inheritdoc/>
        public int SampleRate => 22050;

        /// <inheritdoc/>
        public BackendAudio Generate(string text, float speed, int speakerId)
        {
            LastSpeakerId = speakerId;
            return new BackendAudio([0f], SampleRate);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
        }
    }
}
