using DemaConsulting.Speech.AudioSubsystem;
using DemaConsulting.Speech.Diagnostics;
using DemaConsulting.Speech.ModelManagementSubsystem;
using DemaConsulting.Speech.SynthesisSubsystem;
using DemaConsulting.Speech.Tests.ModelManagementSubsystem.Fakes;
using DemaConsulting.Speech.Tests.SynthesisSubsystem.Fakes;
using NSubstitute;

namespace DemaConsulting.Speech.Tests.SynthesisSubsystem;

/// <summary>
///     Unit tests for <see cref="SpeechSynthesizerFactory"/>, proving that composition never
///     throws for an ordinary machine state and honestly degrades to
///     <see cref="UnavailableSpeechSynthesizerEngine"/> instead.
/// </summary>
public sealed class SpeechSynthesizerFactoryTests : IDisposable
{
    /// <summary>
    ///     A scratch directory standing in for a model's installed <c>current/</c> directory,
    ///     created per test instance and removed on disposal.
    /// </summary>
    private readonly string _installedModelDirectory = Path.Join(
        Path.GetTempPath(),
        "DemaConsulting.Speech.Tests",
        Guid.NewGuid().ToString("N"));

    /// <summary>
    ///     A scratch root directory for a real <see cref="SpeechModelStore"/>, created per test
    ///     instance and removed on disposal.
    /// </summary>
    private readonly string _storeRoot = Path.Join(
        Path.GetTempPath(),
        "DemaConsulting.Speech.Tests",
        Guid.NewGuid().ToString("N"));

    /// <summary>
    ///     A real <see cref="SpeechModelStore"/> rooted at <see cref="_storeRoot"/>, used to
    ///     exercise store-based directory resolution rather than a mock.
    /// </summary>
    private readonly SpeechModelStore _store;

    /// <summary>
    ///     A catalog wrapping <see cref="_store"/>, used to exercise catalog-based directory
    ///     resolution via <see cref="SpeechModelCatalog.Store"/> rather than a mock.
    /// </summary>
    private readonly SpeechModelCatalog _catalog;

    /// <summary>
    ///     Initializes a new instance of the <see cref="SpeechSynthesizerFactoryTests"/> class,
    ///     creating the scratch installed-model directory the "installed" cases require.
    /// </summary>
    public SpeechSynthesizerFactoryTests()
    {
        Directory.CreateDirectory(_installedModelDirectory);
        _store = new SpeechModelStore(new SpeechModelStoreOptions { RootPathOverride = _storeRoot });
        _catalog = new SpeechModelCatalog([], _store, null);
    }

    /// <summary>Removes the scratch installed-model directory.</summary>
    public void Dispose()
    {
        _catalog.Dispose();

        try
        {
            if (Directory.Exists(_installedModelDirectory))
            {
                Directory.Delete(_installedModelDirectory, recursive: true);
            }

            if (Directory.Exists(_storeRoot))
            {
                Directory.Delete(_storeRoot, recursive: true);
            }
        }
        catch (IOException)
        {
            // Best-effort cleanup only; a leftover temp directory does not fail the test.
        }
    }

    /// <summary>
    ///     Proves that a model whose files are not installed composes to the honest unavailable
    ///     engine, and that the backend is never loaded.
    /// </summary>
    [Fact]
    public async Task SpeechSynthesizerFactory_LoadAsync_ModelNotInstalled_ReturnsUnavailableEngine()
    {
        // Arrange: a directory that does not exist
        var backendFactory = new FakeSynthesisEngineFactory();
        var missingDirectory = Path.Join(_installedModelDirectory, "not-installed");

        // Act: compose against the missing model directory
        var engine = await SpeechSynthesizerFactory.LoadAsync(
            new FakeSynthesisModel(), missingDirectory, null, backendFactory, null, TestContext.Current.CancellationToken);

        // Assert: the honest fallback is returned and no backend was loaded
        Assert.Same(UnavailableSpeechSynthesizerEngine.Instance, engine);
        Assert.Equal(0, backendFactory.CreateCallCount);
    }

    /// <summary>
    ///     Proves that a model declaring a non-synthesis role composes to the honest unavailable
    ///     engine.
    /// </summary>
    [Fact]
    public async Task SpeechSynthesizerFactory_LoadAsync_ModelRoleIsNotSynthesis_ReturnsUnavailableEngine()
    {
        // Arrange: an installed model that declares the recognition role
        var backendFactory = new FakeSynthesisEngineFactory();

        // Act: compose against the wrong-role model
        var engine = await SpeechSynthesizerFactory.LoadAsync(
            new WrongRoleSynthesisModel(), _installedModelDirectory, null, backendFactory, null, TestContext.Current.CancellationToken);

        // Assert: the honest fallback is returned and no backend was loaded
        Assert.Same(UnavailableSpeechSynthesizerEngine.Instance, engine);
        Assert.Equal(0, backendFactory.CreateCallCount);
    }

    /// <summary>
    ///     Proves that a native-runtime or model-file load failure degrades to the honest
    ///     unavailable engine instead of propagating out of composition.
    /// </summary>
    [Fact]
    public async Task SpeechSynthesizerFactory_LoadAsync_EngineLoadFails_ReturnsUnavailableEngineAndDoesNotThrow()
    {
        // Arrange: an installed model and a backend factory that faults
        var diagnostics = Substitute.For<ISpeechDiagnostics>();
        var backendFactory = new FakeSynthesisEngineFactory(
            createException: new DllNotFoundException("sherpa-onnx-c-api"));

        // Act: compose, capturing any exception that escapes
        ISpeechSynthesizerEngine? engine = null;
        var exception = await Record.ExceptionAsync(async () => engine = await SpeechSynthesizerFactory.LoadAsync(
            new FakeSynthesisModel(), _installedModelDirectory, diagnostics, backendFactory, null, TestContext.Current.CancellationToken));

        // Assert: composition succeeded honestly and reported the fault as a structural fact
        Assert.Null(exception);
        Assert.Same(UnavailableSpeechSynthesizerEngine.Instance, engine);
        diagnostics.Received().Report(
            SpeechDiagnosticLevel.Error,
            "SynthesisSubsystem",
            Arg.Is<string>(message => message.Contains("could not be loaded", StringComparison.Ordinal)));
    }

    /// <summary>
    ///     Proves that an installed synthesis model composes a real engine wired to the injected
    ///     backend factory, with the installed-model directory passed through unchanged.
    /// </summary>
    [Fact]
    public async Task SpeechSynthesizerFactory_LoadAsync_ModelInstalled_ReturnsRealEngine()
    {
        // Arrange: an installed model and a fake backend factory
        var backendFactory = new FakeSynthesisEngineFactory();
        var model = new FakeSynthesisModel();

        // Act: compose an engine
        await using var engine = await SpeechSynthesizerFactory.LoadAsync(
            model, _installedModelDirectory, null, backendFactory, null, TestContext.Current.CancellationToken);

        // Assert: a real engine was built from the injected backend, for the right model
        Assert.True(engine.IsAvailable);
        Assert.Equal(1, backendFactory.CreateCallCount);
        Assert.Same(model, backendFactory.RequestedModel);
        Assert.Equal(_installedModelDirectory, backendFactory.RequestedInstalledModelDirectory);
    }

    /// <summary>
    ///     Proves that the public composition overload rejects a null model, since a null
    ///     argument is a programming error rather than an ordinary machine state.
    /// </summary>
    [Fact]
    public async Task SpeechSynthesizerFactory_LoadAsync_NullModel_ThrowsArgumentNullException()
    {
        // Act & Assert: a null model is rejected
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => SpeechSynthesizerFactory.LoadAsync(null!, _installedModelDirectory, cancellationToken: TestContext.Current.CancellationToken));
    }

    /// <summary>
    ///     Proves that an already-cancelled token throws <see cref="OperationCanceledException"/>
    ///     synchronously from composition, rather than composing anyway.
    /// </summary>
    [Fact]
    public async Task SpeechSynthesizerFactory_LoadAsync_CancelledToken_ThrowsOperationCanceledException()
    {
        // Arrange
        var backendFactory = new FakeSynthesisEngineFactory();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => SpeechSynthesizerFactory.LoadAsync(
            new FakeSynthesisModel(), _installedModelDirectory, null, backendFactory, null, cts.Token));
    }

    /// <summary>
    ///     Proves that <c>parameterValues</c> passed to
    ///     <see cref="SpeechSynthesizerFactory.LoadAsync(ISynthesisModel,string,ISpeechDiagnostics,ISynthesisBackendFactory,IReadOnlyDictionary{string,object}?,CancellationToken)"/>
    ///     reaches the constructed engine's synthesis calls, by round-tripping it through a fake
    ///     model's <c>ResolveSpeakerId</c> hook into the fake engine's captured speaker id.
    /// </summary>
    [Fact]
    public async Task SpeechSynthesizerFactory_LoadAsync_ParameterValuesSupplied_ForwardedToEngine()
    {
        // Arrange
        var backendFactory = new FakeSynthesisEngineFactory();
        var model = new FakeSynthesisModel(
            resolveSpeakerId: values => values is not null && values.TryGetValue("voice", out var value) && value is int id ? id : 0);
        IReadOnlyDictionary<string, object> parameterValues = new Dictionary<string, object> { ["voice"] = 4 };

        // Act
        await using var engine = await SpeechSynthesizerFactory.LoadAsync(
            model, _installedModelDirectory, null, backendFactory, parameterValues, TestContext.Current.CancellationToken);
        await engine.SynthesizeAsync("Hello.", TestContext.Current.CancellationToken);

        // Assert
        var call = Assert.Single(backendFactory.Engine.GenerateCalls);
        Assert.Equal(4, call.SpeakerId);
    }

    /// <summary>
    ///     Proves that an unrecognized parameter id is silently ignored (never throws) and is
    ///     reported at <see cref="SpeechDiagnosticLevel.Info"/> when a diagnostics sink is
    ///     supplied.
    /// </summary>
    [Fact]
    public async Task SpeechSynthesizerFactory_LoadAsync_UnrecognizedParameterId_ComposesAndReportsInfo()
    {
        // Arrange
        var backendFactory = new FakeSynthesisEngineFactory();
        var model = new FakeSynthesisModel();
        var diagnostics = Substitute.For<ISpeechDiagnostics>();
        IReadOnlyDictionary<string, object> parameterValues = new Dictionary<string, object> { ["typo-id"] = 1 };

        // Act
        await using var engine = await SpeechSynthesizerFactory.LoadAsync(
            model, _installedModelDirectory, diagnostics, backendFactory, parameterValues, TestContext.Current.CancellationToken);

        // Assert: still a real, working engine, plus the observability diagnostic
        Assert.True(engine.IsAvailable);
        diagnostics.Received(1).Report(
            SpeechDiagnosticLevel.Info,
            "SynthesisSubsystem",
            "Parameter 'typo-id' is not declared by this model and was ignored.");
    }

    /// <summary>
    ///     Proves that an out-of-range value for a recognized numeric parameter throws
    ///     <see cref="ArgumentException"/> synchronously from <c>LoadAsync</c>, rather than
    ///     silently defaulting, and that the backend is never loaded.
    /// </summary>
    [Fact]
    public async Task SpeechSynthesizerFactory_LoadAsync_RecognizedNumericParameterOutOfRange_Throws()
    {
        // Arrange: FakeSynthesisModel declares "tempo" bounded to [0.5, 2.0]
        var backendFactory = new FakeSynthesisEngineFactory();
        var model = new FakeSynthesisModel();
        IReadOnlyDictionary<string, object> parameterValues = new Dictionary<string, object> { ["tempo"] = 5.0 };

        // Act / Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() => SpeechSynthesizerFactory.LoadAsync(
            model, _installedModelDirectory, null, backendFactory, parameterValues, TestContext.Current.CancellationToken));
        Assert.Contains("tempo", exception.Message, StringComparison.Ordinal);
        Assert.Equal(0, backendFactory.CreateCallCount);
    }

    /// <summary>
    ///     Proves that a wrong CLR type for a recognized numeric parameter throws
    ///     <see cref="ArgumentException"/> synchronously from <c>LoadAsync</c>.
    /// </summary>
    [Fact]
    public async Task SpeechSynthesizerFactory_LoadAsync_RecognizedNumericParameterWrongType_Throws()
    {
        // Arrange
        var backendFactory = new FakeSynthesisEngineFactory();
        var model = new FakeSynthesisModel();
        IReadOnlyDictionary<string, object> parameterValues = new Dictionary<string, object> { ["tempo"] = "fast" };

        // Act / Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() => SpeechSynthesizerFactory.LoadAsync(
            model, _installedModelDirectory, null, backendFactory, parameterValues, TestContext.Current.CancellationToken));
        Assert.Contains("tempo", exception.Message, StringComparison.Ordinal);
        Assert.Equal(0, backendFactory.CreateCallCount);
    }

    /// <summary>
    ///     Proves that a non-integral value for the real, shipped
    ///     <see cref="SherpaOnnxVitsLibriTtsEnglishSynthesisModel"/>'s integer-only
    ///     <c>speaker</c> parameter throws <see cref="ArgumentException"/> synchronously from
    ///     <c>LoadAsync</c>, rather than silently rounding it (this library's previous,
    ///     deliberately superseded, behavior for this exact case).
    /// </summary>
    [Fact]
    public async Task SpeechSynthesizerFactory_LoadAsync_RecognizedIntegerParameterFractionalValue_Throws()
    {
        // Arrange
        var backendFactory = new FakeSynthesisEngineFactory();
        var model = new SherpaOnnxVitsLibriTtsEnglishSynthesisModel();
        IReadOnlyDictionary<string, object> parameterValues = new Dictionary<string, object>
        {
            [SherpaOnnxVitsLibriTtsEnglishSynthesisModel.SpeakerParameterId] = 12.4,
        };

        // Act / Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() => SpeechSynthesizerFactory.LoadAsync(
            model, _installedModelDirectory, null, backendFactory, parameterValues, TestContext.Current.CancellationToken));
        Assert.Contains(SherpaOnnxVitsLibriTtsEnglishSynthesisModel.SpeakerParameterId, exception.Message, StringComparison.Ordinal);
        Assert.Contains("whole number", exception.Message, StringComparison.Ordinal);
        Assert.Equal(0, backendFactory.CreateCallCount);
    }

    /// <summary>
    ///     Proves that a value not matching any declared voice for the real, shipped
    ///     <see cref="SherpaOnnxKokoroEnglishSynthesisModel"/>'s <c>ChoiceParameter</c> throws
    ///     <see cref="ArgumentException"/> synchronously from <c>LoadAsync</c>.
    /// </summary>
    [Fact]
    public async Task SpeechSynthesizerFactory_LoadAsync_RecognizedChoiceParameterInvalidOption_Throws()
    {
        // Arrange
        var backendFactory = new FakeSynthesisEngineFactory();
        var model = new SherpaOnnxKokoroEnglishSynthesisModel();
        IReadOnlyDictionary<string, object> parameterValues = new Dictionary<string, object>
        {
            [SherpaOnnxKokoroEnglishSynthesisModel.VoiceParameterId] = "not-a-declared-voice",
        };

        // Act / Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() => SpeechSynthesizerFactory.LoadAsync(
            model, _installedModelDirectory, null, backendFactory, parameterValues, TestContext.Current.CancellationToken));
        Assert.Contains("not-a-declared-voice", exception.Message, StringComparison.Ordinal);
        Assert.Equal(0, backendFactory.CreateCallCount);
    }

    /// <summary>
    ///     Proves that a model not yet installed in the store composes to the honest unavailable
    ///     engine, and that the backend is never loaded.
    /// </summary>
    [Fact]
    public async Task SpeechSynthesizerFactory_LoadAsync_WithStoreModelNotInstalled_ReturnsUnavailableEngine()
    {
        // Arrange: a store with no installed model directory
        var backendFactory = new FakeSynthesisEngineFactory();
        var model = new FakeSynthesisModel();

        // Act: compose against the store, which resolves to a directory that does not exist
        var engine = await SpeechSynthesizerFactory.LoadAsync(
            model, _store, null, backendFactory, null, TestContext.Current.CancellationToken);

        // Assert: the honest fallback is returned and no backend was loaded
        Assert.Same(UnavailableSpeechSynthesizerEngine.Instance, engine);
        Assert.Equal(0, backendFactory.CreateCallCount);
    }

    /// <summary>
    ///     Proves that an installed synthesis model composes a real engine wired to the injected
    ///     backend factory, with the directory resolved through the store rather than hard-coded.
    /// </summary>
    [Fact]
    public async Task SpeechSynthesizerFactory_LoadAsync_WithStoreModelInstalled_ReturnsRealEngine()
    {
        // Arrange: a model installed via the store, and a fake backend factory
        var backendFactory = new FakeSynthesisEngineFactory();
        var model = new FakeSynthesisModel();
        Directory.CreateDirectory(_store.GetCurrentDirectory(model.Id));

        // Act: compose an engine through the store overload
        await using var engine = await SpeechSynthesizerFactory.LoadAsync(
            model, _store, null, backendFactory, null, TestContext.Current.CancellationToken);

        // Assert: a real engine was built, and the directory was resolved through the store
        Assert.True(engine.IsAvailable);
        Assert.Equal(1, backendFactory.CreateCallCount);
        Assert.Equal(_store.GetCurrentDirectory(model.Id), backendFactory.RequestedInstalledModelDirectory);
    }

    /// <summary>
    ///     Proves that the public store-based composition overload rejects a null model.
    /// </summary>
    [Fact]
    public async Task SpeechSynthesizerFactory_LoadAsync_WithStoreNullModel_ThrowsArgumentNullException()
    {
        // Act & Assert: a null model is rejected
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => SpeechSynthesizerFactory.LoadAsync(null!, _store, cancellationToken: TestContext.Current.CancellationToken));
    }

    /// <summary>
    ///     Proves that the public store-based composition overload rejects a null store.
    /// </summary>
    [Fact]
    public async Task SpeechSynthesizerFactory_LoadAsync_WithStoreNullStore_ThrowsArgumentNullException()
    {
        // Act & Assert: a null store is rejected
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => SpeechSynthesizerFactory.LoadAsync(new FakeSynthesisModel(), (SpeechModelStore)null!, cancellationToken: TestContext.Current.CancellationToken));
    }

    /// <summary>
    ///     Proves that a model not yet installed in the catalog's store composes to the honest
    ///     unavailable engine, and that the backend is never loaded.
    /// </summary>
    [Fact]
    public async Task SpeechSynthesizerFactory_LoadAsync_WithCatalogModelNotInstalled_ReturnsUnavailableEngine()
    {
        // Arrange: a catalog whose store has no installed model directory
        var backendFactory = new FakeSynthesisEngineFactory();
        var model = new FakeSynthesisModel();

        // Act: compose against the catalog, which resolves to a directory that does not exist
        var engine = await SpeechSynthesizerFactory.LoadAsync(
            model, _catalog, null, backendFactory, null, TestContext.Current.CancellationToken);

        // Assert: the honest fallback is returned and no backend was loaded
        Assert.Same(UnavailableSpeechSynthesizerEngine.Instance, engine);
        Assert.Equal(0, backendFactory.CreateCallCount);
    }

    /// <summary>
    ///     Proves that an installed synthesis model composes a real engine wired to the injected
    ///     backend factory, with the directory resolved through the catalog's own store rather
    ///     than a second, disconnected store.
    /// </summary>
    [Fact]
    public async Task SpeechSynthesizerFactory_LoadAsync_WithCatalogModelInstalled_ReturnsRealEngine()
    {
        // Arrange: a model installed via the catalog's store, and a fake backend factory
        var backendFactory = new FakeSynthesisEngineFactory();
        var model = new FakeSynthesisModel();
        Directory.CreateDirectory(_store.GetCurrentDirectory(model.Id));

        // Act: compose an engine through the catalog overload
        await using var engine = await SpeechSynthesizerFactory.LoadAsync(
            model, _catalog, null, backendFactory, null, TestContext.Current.CancellationToken);

        // Assert: a real engine was built, and the directory was resolved through the catalog's store
        Assert.True(engine.IsAvailable);
        Assert.Equal(1, backendFactory.CreateCallCount);
        Assert.Equal(_catalog.Store.GetCurrentDirectory(model.Id), backendFactory.RequestedInstalledModelDirectory);
    }

    /// <summary>
    ///     Proves that the public catalog-based composition overload rejects a null model.
    /// </summary>
    [Fact]
    public async Task SpeechSynthesizerFactory_LoadAsync_WithCatalogNullModel_ThrowsArgumentNullException()
    {
        // Act & Assert: a null model is rejected
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => SpeechSynthesizerFactory.LoadAsync(null!, _catalog, cancellationToken: TestContext.Current.CancellationToken));
    }

    /// <summary>
    ///     Proves that the public catalog-based composition overload rejects a null catalog.
    /// </summary>
    [Fact]
    public async Task SpeechSynthesizerFactory_LoadAsync_WithCatalogNullCatalog_ThrowsArgumentNullException()
    {
        // Act & Assert: a null catalog is rejected
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => SpeechSynthesizerFactory.LoadAsync(new FakeSynthesisModel(), (SpeechModelCatalog)null!, cancellationToken: TestContext.Current.CancellationToken));
    }

    /// <summary>
    ///     Test-only synthesis model whose declared role contradicts the synthesis interface it
    ///     implements, used to prove the factory rejects it honestly.
    /// </summary>
    private sealed class WrongRoleSynthesisModel : ISynthesisModel
    {
        /// <inheritdoc/>
        public string Id => "wrong-role-model";

        /// <inheritdoc/>
        public string DisplayName => "Wrong Role Model";

        /// <inheritdoc/>
        public SpeechModelRole Role => SpeechModelRole.Recognition;

        /// <inheritdoc/>
        public IReadOnlyList<ISpeechModelParameter> Parameters => [];

        /// <inheritdoc/>
        public SpeechModelAudioTagSupport AudioTagSupport => SpeechModelAudioTagSupport.None;

        /// <inheritdoc/>
        public SpeechModelDownloadDescriptor DownloadDescriptor =>
            FakeModelDescriptors.SingleFileDescriptor("wrong-role-model");

        /// <inheritdoc/>
        public AudioFormat PreferredAudioFormat => AudioFormat.Mono(24000);

        /// <inheritdoc/>
        SherpaOnnx.OfflineTtsConfig ISynthesisModel.CreateEngineConfig(string installedModelDirectory) =>
            new();
    }
}
