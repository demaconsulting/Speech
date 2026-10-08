using DemaConsulting.Speech.AudioSubsystem;
using DemaConsulting.Speech.Diagnostics;
using DemaConsulting.Speech.ModelManagementSubsystem;
using DemaConsulting.Speech.RecognitionSubsystem;
using DemaConsulting.Speech.Tests.ModelManagementSubsystem.Fakes;
using DemaConsulting.Speech.Tests.RecognitionSubsystem.Fakes;
using NSubstitute;

namespace DemaConsulting.Speech.Tests.RecognitionSubsystem;

/// <summary>
///     Unit tests for <see cref="SpeechRecognizerFactory"/>, proving that composition never
///     throws for an ordinary machine state and honestly degrades to
///     <see cref="UnavailableSpeechRecognizerEngine"/> instead.
/// </summary>
public sealed class SpeechRecognizerFactoryTests : IDisposable
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
    ///     Initializes a new instance of the <see cref="SpeechRecognizerFactoryTests"/> class,
    ///     creating the scratch installed-model directory the "installed" cases require.
    /// </summary>
    public SpeechRecognizerFactoryTests()
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
    public async Task SpeechRecognizerFactory_LoadAsync_ModelNotInstalled_ReturnsUnavailableEngine()
    {
        // Arrange: a directory that does not exist
        var backendFactory = new FakeRecognitionEngineFactory();
        var missingDirectory = Path.Join(_installedModelDirectory, "not-installed");

        // Act: compose against the missing model directory
        await using var engine = await SpeechRecognizerFactory.LoadAsync(new FakeRecognitionModel(), missingDirectory, null, backendFactory, cancellationToken: TestContext.Current.CancellationToken);

        // Assert: the honest fallback is returned and no backend was loaded
        Assert.Same(UnavailableSpeechRecognizerEngine.Instance, engine);
        Assert.Equal(0, backendFactory.CreateCallCount);
    }

    /// <summary>
    ///     Proves that a model declaring a non-recognition role composes to the honest unavailable
    ///     engine.
    /// </summary>
    [Fact]
    public async Task SpeechRecognizerFactory_LoadAsync_ModelRoleIsNotRecognition_ReturnsUnavailableEngine()
    {
        // Arrange: an installed model that declares the synthesis role
        var backendFactory = new FakeRecognitionEngineFactory();

        // Act: compose against the wrong-role model
        await using var engine = await SpeechRecognizerFactory.LoadAsync(new WrongRoleRecognitionModel(), _installedModelDirectory, null, backendFactory, cancellationToken: TestContext.Current.CancellationToken);

        // Assert: the honest fallback is returned and no backend was loaded
        Assert.Same(UnavailableSpeechRecognizerEngine.Instance, engine);
        Assert.Equal(0, backendFactory.CreateCallCount);
    }

    /// <summary>
    ///     Proves that a native-runtime or model-file load failure degrades to the honest
    ///     unavailable engine instead of faulting the returned task.
    /// </summary>
    [Fact]
    public async Task SpeechRecognizerFactory_LoadAsync_EngineLoadFails_ReturnsUnavailableEngineAndDoesNotFaultTask()
    {
        // Arrange: an installed model and a backend factory that faults
        var diagnostics = Substitute.For<ISpeechDiagnostics>();
        var backendFactory = new FakeRecognitionEngineFactory(
            createException: new DllNotFoundException("sherpa-onnx-c-api"));

        // Act: compose, capturing any exception that escapes
        ISpeechRecognizerEngine? engine = null;
        var exception = await Record.ExceptionAsync(async () => engine = await SpeechRecognizerFactory.LoadAsync(new FakeRecognitionModel(), _installedModelDirectory, diagnostics, backendFactory, cancellationToken: TestContext.Current.CancellationToken));

        // Assert: composition succeeded honestly and reported the fault as a structural fact
        Assert.Null(exception);
        Assert.Same(UnavailableSpeechRecognizerEngine.Instance, engine);
        diagnostics.Received().Report(
            SpeechDiagnosticLevel.Error,
            "RecognitionSubsystem",
            Arg.Is<string>(message => message.Contains("could not be loaded", StringComparison.Ordinal)));
    }

    /// <summary>
    ///     Proves that an installed recognition model composes a real engine wired to the
    ///     injected backend factory, with the installed-model directory passed through unchanged.
    /// </summary>
    [Fact]
    public async Task SpeechRecognizerFactory_LoadAsync_ModelInstalled_ReturnsRealEngine()
    {
        // Arrange: an installed model and a fake backend factory
        var backendFactory = new FakeRecognitionEngineFactory();
        var model = new FakeRecognitionModel();

        // Act: compose an engine
        await using var engine = await SpeechRecognizerFactory.LoadAsync(model, _installedModelDirectory, null, backendFactory, cancellationToken: TestContext.Current.CancellationToken);

        // Assert: a real engine was built from the injected backend, for the right model
        Assert.IsType<SpeechRecognizerEngine>(engine);
        Assert.True(engine.IsAvailable);
        Assert.Equal(1, backendFactory.CreateCallCount);
        Assert.Same(model, backendFactory.RequestedModel);
        Assert.Equal(_installedModelDirectory, backendFactory.RequestedInstalledModelDirectory);
    }

    /// <summary>
    ///     Proves that an unrecognized parameter id is silently ignored (never faults) and is
    ///     reported at <see cref="SpeechDiagnosticLevel.Info"/> when a diagnostics sink is
    ///     supplied - preserving this library's deliberate cross-model settings-dictionary-reuse
    ///     contract.
    /// </summary>
    [Fact]
    public async Task SpeechRecognizerFactory_LoadAsync_UnrecognizedParameterId_ComposesAndReportsInfo()
    {
        // Arrange
        var backendFactory = new FakeRecognitionEngineFactory();
        var model = new FakeRecognitionModel();
        var diagnostics = Substitute.For<ISpeechDiagnostics>();
        IReadOnlyDictionary<string, object> parameterValues = new Dictionary<string, object> { ["typo-id"] = 1 };

        // Act
        await using var engine = await SpeechRecognizerFactory.LoadAsync(model, _installedModelDirectory, diagnostics, backendFactory, parameterValues, TestContext.Current.CancellationToken);

        // Assert: still a real, working engine, plus the observability diagnostic
        Assert.IsType<SpeechRecognizerEngine>(engine);
        Assert.True(engine.IsAvailable);
        diagnostics.Received(1).Report(
            SpeechDiagnosticLevel.Info,
            "RecognitionSubsystem",
            "Parameter 'typo-id' is not declared by this model and was ignored.");
    }

    /// <summary>
    ///     Proves that an out-of-range value for a recognized numeric parameter faults the
    ///     returned task with <see cref="ArgumentException"/>, rather than silently clamping.
    /// </summary>
    [Fact]
    public async Task SpeechRecognizerFactory_LoadAsync_RecognizedNumericParameterOutOfRange_FaultsWithArgumentException()
    {
        // Arrange
        var backendFactory = new FakeRecognitionEngineFactory();
        var model = new FakeRecognitionModel();
        IReadOnlyDictionary<string, object> parameterValues = new Dictionary<string, object> { ["sensitivity"] = 5.0 };

        // Act / Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() => SpeechRecognizerFactory.LoadAsync(model, _installedModelDirectory, null, backendFactory, parameterValues, TestContext.Current.CancellationToken));
        Assert.Contains("sensitivity", exception.Message, StringComparison.Ordinal);
        Assert.Equal(0, backendFactory.CreateCallCount);
    }

    /// <summary>
    ///     Proves that a value not matching any declared choice option faults the returned task
    ///     with <see cref="ArgumentException"/>.
    /// </summary>
    [Fact]
    public async Task SpeechRecognizerFactory_LoadAsync_RecognizedChoiceParameterInvalidOption_FaultsWithArgumentException()
    {
        // Arrange
        var backendFactory = new FakeRecognitionEngineFactory();
        var model = new FakeRecognitionModel();
        IReadOnlyDictionary<string, object> parameterValues = new Dictionary<string, object> { ["language"] = "klingon" };

        // Act / Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() => SpeechRecognizerFactory.LoadAsync(model, _installedModelDirectory, null, backendFactory, parameterValues, TestContext.Current.CancellationToken));
        Assert.Contains("language", exception.Message, StringComparison.Ordinal);
        Assert.Contains("klingon", exception.Message, StringComparison.Ordinal);
        Assert.Equal(0, backendFactory.CreateCallCount);
    }

    /// <summary>
    ///     Proves that a non-boolean value for a recognized <see cref="BooleanParameter"/> faults
    ///     the returned task with <see cref="ArgumentException"/>.
    /// </summary>
    [Fact]
    public async Task SpeechRecognizerFactory_LoadAsync_RecognizedBooleanParameterWrongType_FaultsWithArgumentException()
    {
        // Arrange
        var backendFactory = new FakeRecognitionEngineFactory();
        var model = new FakeRecognitionModel();
        IReadOnlyDictionary<string, object> parameterValues = new Dictionary<string, object> { ["denoise"] = "yes" };

        // Act / Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() => SpeechRecognizerFactory.LoadAsync(model, _installedModelDirectory, null, backendFactory, parameterValues, TestContext.Current.CancellationToken));
        Assert.Contains("denoise", exception.Message, StringComparison.Ordinal);
        Assert.Equal(0, backendFactory.CreateCallCount);
    }

    /// <summary>
    ///     Proves that a wrong CLR type for a recognized numeric parameter faults the returned
    ///     task with <see cref="ArgumentException"/>.
    /// </summary>
    [Fact]
    public async Task SpeechRecognizerFactory_LoadAsync_RecognizedNumericParameterWrongType_FaultsWithArgumentException()
    {
        // Arrange
        var backendFactory = new FakeRecognitionEngineFactory();
        var model = new FakeRecognitionModel();
        IReadOnlyDictionary<string, object> parameterValues = new Dictionary<string, object> { ["sensitivity"] = "high" };

        // Act / Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() => SpeechRecognizerFactory.LoadAsync(model, _installedModelDirectory, null, backendFactory, parameterValues, TestContext.Current.CancellationToken));
        Assert.Contains("sensitivity", exception.Message, StringComparison.Ordinal);
        Assert.Equal(0, backendFactory.CreateCallCount);
    }

    /// <summary>
    ///     Proves that a supplied <c>parameterValues</c> bag genuinely reaches the model's own
    ///     two-argument <c>CreateBackend</c> override, not merely the backend factory.
    /// </summary>
    [Fact]
    public async Task SpeechRecognizerFactory_LoadAsync_ParameterValuesSupplied_ReachesModelCreateBackend()
    {
        // Arrange: an installed model whose CreateBackend override encodes the language value
        var backendFactory = new FakeRecognitionEngineFactory();
        var model = new ParameterCapturingRecognitionModel();
        IReadOnlyDictionary<string, object> parameterValues = new Dictionary<string, object> { ["language"] = "en-gb" };

        // Act: compose an engine, supplying parameterValues
        await using var engine = await SpeechRecognizerFactory.LoadAsync(model, _installedModelDirectory, null, backendFactory, parameterValues, TestContext.Current.CancellationToken);

        // Assert: the value reached the model's own CreateBackend override
        Assert.IsType<SpeechRecognizerEngine>(engine);
        Assert.Equal("en-gb", (backendFactory.RequestedBackend as ParameterCapturingRecognitionEngine)?.Language);
    }

    /// <summary>
    ///     Proves that the public composition overload faults the returned task with
    ///     <see cref="ArgumentNullException"/> for a null model, since a null argument is a
    ///     programming error rather than an ordinary machine state.
    /// </summary>
    [Fact]
    public async Task SpeechRecognizerFactory_LoadAsync_NullModel_FaultsWithArgumentNullException()
    {
        // Act & Assert: a null model is rejected
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => SpeechRecognizerFactory.LoadAsync(null!, _installedModelDirectory, cancellationToken: TestContext.Current.CancellationToken));
    }

    /// <summary>
    ///     Proves that a cancelled token faults the returned task with
    ///     <see cref="OperationCanceledException"/> rather than letting composition proceed.
    /// </summary>
    [Fact]
    public async Task SpeechRecognizerFactory_LoadAsync_CancelledToken_FaultsWithOperationCanceledException()
    {
        // Arrange: a token already cancelled before the call
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act & Assert: the cancellation is honored rather than silently ignored
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => SpeechRecognizerFactory.LoadAsync(
                new FakeRecognitionModel(), _installedModelDirectory, cancellationToken: cts.Token));
    }

    /// <summary>
    ///     Proves that a model not yet installed in the store composes to the honest unavailable
    ///     engine, and that the backend is never loaded.
    /// </summary>
    [Fact]
    public async Task SpeechRecognizerFactory_LoadAsync_WithStoreModelNotInstalled_ReturnsUnavailableEngine()
    {
        // Arrange: a store with no installed model directory
        var backendFactory = new FakeRecognitionEngineFactory();
        var model = new FakeRecognitionModel();

        // Act: compose against the store, which resolves to a directory that does not exist
        await using var engine = await SpeechRecognizerFactory.LoadAsync(model, _store, null, backendFactory, cancellationToken: TestContext.Current.CancellationToken);

        // Assert: the honest fallback is returned and no backend was loaded
        Assert.Same(UnavailableSpeechRecognizerEngine.Instance, engine);
        Assert.Equal(0, backendFactory.CreateCallCount);
    }

    /// <summary>
    ///     Proves that an installed recognition model composes a real engine wired to the
    ///     injected backend factory, with the directory resolved through the store rather than
    ///     hard-coded.
    /// </summary>
    [Fact]
    public async Task SpeechRecognizerFactory_LoadAsync_WithStoreModelInstalled_ReturnsRealEngine()
    {
        // Arrange: a model installed via the store, and a fake backend factory
        var backendFactory = new FakeRecognitionEngineFactory();
        var model = new FakeRecognitionModel();
        Directory.CreateDirectory(_store.GetCurrentDirectory(model.Id));

        // Act: compose an engine through the store overload
        await using var engine = await SpeechRecognizerFactory.LoadAsync(model, _store, null, backendFactory, cancellationToken: TestContext.Current.CancellationToken);

        // Assert: a real engine was built, and the directory was resolved through the store
        Assert.IsType<SpeechRecognizerEngine>(engine);
        Assert.Equal(1, backendFactory.CreateCallCount);
        Assert.Equal(_store.GetCurrentDirectory(model.Id), backendFactory.RequestedInstalledModelDirectory);
    }

    /// <summary>
    ///     Proves that a supplied <c>parameterValues</c> bag genuinely reaches the model's own
    ///     two-argument <c>CreateBackend</c> override when composed through the PUBLIC
    ///     store-based composition overload (with no injected <c>backendFactory</c>), so the real
    ///     production delegation chain (public store overload → public string overload →
    ///     internal string+backendFactory overload → real
    ///     <see cref="Fakes.FakeRecognitionEngineFactory"/>-free <c>DefaultRecognitionBackendFactory</c>)
    ///     is exercised end to end. The model throws from within its own
    ///     <c>CreateBackend</c> override, after recording the received parameter bag, so the
    ///     test never reaches a real native sherpa-onnx engine construction.
    /// </summary>
    [Fact]
    public async Task SpeechRecognizerFactory_LoadAsync_WithStoreParameterValuesSupplied_ReachesModelCreateBackend()
    {
        // Arrange: a throwing parameter-capturing model installed via the store, and a parameter bag
        var model = new ThrowingParameterCapturingRecognitionModel();
        Directory.CreateDirectory(_store.GetCurrentDirectory(model.Id));
        IReadOnlyDictionary<string, object> parameterValues = new Dictionary<string, object> { ["language"] = "en-gb" };

        // Act: compose an engine through the genuine public store overload, supplying
        // parameterValues, with no backendFactory argument so overload resolution can only bind
        // to the real public overload
        await using var engine = await SpeechRecognizerFactory.LoadAsync(model, _store, null, parameterValues, TestContext.Current.CancellationToken);

        // Assert: the value reached the model's own CreateBackend override via the real
        // production chain, and the model's throw was honestly swallowed into the unavailable
        // fallback rather than faulting the task
        Assert.Same(UnavailableSpeechRecognizerEngine.Instance, engine);
        Assert.Equal(parameterValues, model.RequestedParameterValues);
    }

    /// <summary>
    ///     Proves that the public store-based composition overload faults the returned task with
    ///     <see cref="ArgumentNullException"/> for a null model.
    /// </summary>
    [Fact]
    public async Task SpeechRecognizerFactory_LoadAsync_WithStoreNullModel_FaultsWithArgumentNullException()
    {
        // Act & Assert: a null model is rejected
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => SpeechRecognizerFactory.LoadAsync(null!, _store, cancellationToken: TestContext.Current.CancellationToken));
    }

    /// <summary>
    ///     Proves that the public store-based composition overload faults the returned task with
    ///     <see cref="ArgumentNullException"/> for a null store.
    /// </summary>
    [Fact]
    public async Task SpeechRecognizerFactory_LoadAsync_WithStoreNullStore_FaultsWithArgumentNullException()
    {
        // Act & Assert: a null store is rejected
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => SpeechRecognizerFactory.LoadAsync(new FakeRecognitionModel(), (SpeechModelStore)null!, cancellationToken: TestContext.Current.CancellationToken));
    }

    /// <summary>
    ///     Proves that a model not yet installed in the catalog's store composes to the honest
    ///     unavailable engine, and that the backend is never loaded.
    /// </summary>
    [Fact]
    public async Task SpeechRecognizerFactory_LoadAsync_WithCatalogModelNotInstalled_ReturnsUnavailableEngine()
    {
        // Arrange: a catalog whose store has no installed model directory
        var backendFactory = new FakeRecognitionEngineFactory();
        var model = new FakeRecognitionModel();

        // Act: compose against the catalog, which resolves to a directory that does not exist
        await using var engine = await SpeechRecognizerFactory.LoadAsync(model, _catalog, null, backendFactory, cancellationToken: TestContext.Current.CancellationToken);

        // Assert: the honest fallback is returned and no backend was loaded
        Assert.Same(UnavailableSpeechRecognizerEngine.Instance, engine);
        Assert.Equal(0, backendFactory.CreateCallCount);
    }

    /// <summary>
    ///     Proves that an installed recognition model composes a real engine wired to the
    ///     injected backend factory, with the directory resolved through the catalog's own store
    ///     rather than a second, disconnected store.
    /// </summary>
    [Fact]
    public async Task SpeechRecognizerFactory_LoadAsync_WithCatalogModelInstalled_ReturnsRealEngine()
    {
        // Arrange: a model installed via the catalog's store, and a fake backend factory
        var backendFactory = new FakeRecognitionEngineFactory();
        var model = new FakeRecognitionModel();
        Directory.CreateDirectory(_store.GetCurrentDirectory(model.Id));

        // Act: compose an engine through the catalog overload
        await using var engine = await SpeechRecognizerFactory.LoadAsync(model, _catalog, null, backendFactory, cancellationToken: TestContext.Current.CancellationToken);

        // Assert: a real engine was built, and the directory was resolved through the catalog's store
        Assert.IsType<SpeechRecognizerEngine>(engine);
        Assert.Equal(1, backendFactory.CreateCallCount);
        Assert.Equal(_catalog.Store.GetCurrentDirectory(model.Id), backendFactory.RequestedInstalledModelDirectory);
    }

    /// <summary>
    ///     Proves that a supplied <c>parameterValues</c> bag genuinely reaches the model's own
    ///     two-argument <c>CreateBackend</c> override when composed through the PUBLIC
    ///     catalog-based composition overload (with no injected <c>backendFactory</c>), so the real
    ///     production delegation chain (public catalog overload → public store overload → public
    ///     string overload → internal string+backendFactory overload → real
    ///     <c>DefaultRecognitionBackendFactory</c>) is exercised end to end. The model throws
    ///     from within its own <c>CreateBackend</c> override, after recording the received
    ///     parameter bag, so the test never reaches a real native sherpa-onnx engine construction.
    /// </summary>
    [Fact]
    public async Task SpeechRecognizerFactory_LoadAsync_WithCatalogParameterValuesSupplied_ReachesModelCreateBackend()
    {
        // Arrange: a throwing parameter-capturing model installed via the catalog's store, and a parameter bag
        var model = new ThrowingParameterCapturingRecognitionModel();
        Directory.CreateDirectory(_store.GetCurrentDirectory(model.Id));
        IReadOnlyDictionary<string, object> parameterValues = new Dictionary<string, object> { ["language"] = "en-gb" };

        // Act: compose an engine through the genuine public catalog overload, supplying
        // parameterValues, with no backendFactory argument so overload resolution can only bind
        // to the real public overload
        await using var engine = await SpeechRecognizerFactory.LoadAsync(model, _catalog, null, parameterValues, TestContext.Current.CancellationToken);

        // Assert: the value reached the model's own CreateBackend override via the real
        // production chain, and the model's throw was honestly swallowed into the unavailable
        // fallback rather than faulting the task
        Assert.Same(UnavailableSpeechRecognizerEngine.Instance, engine);
        Assert.Equal(parameterValues, model.RequestedParameterValues);
    }

    /// <summary>
    ///     Proves that the public catalog-based composition overload faults the returned task
    ///     with <see cref="ArgumentNullException"/> for a null model.
    /// </summary>
    [Fact]
    public async Task SpeechRecognizerFactory_LoadAsync_WithCatalogNullModel_FaultsWithArgumentNullException()
    {
        // Act & Assert: a null model is rejected
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => SpeechRecognizerFactory.LoadAsync(null!, _catalog, cancellationToken: TestContext.Current.CancellationToken));
    }

    /// <summary>
    ///     Proves that the public catalog-based composition overload faults the returned task
    ///     with <see cref="ArgumentNullException"/> for a null catalog.
    /// </summary>
    [Fact]
    public async Task SpeechRecognizerFactory_LoadAsync_WithCatalogNullCatalog_FaultsWithArgumentNullException()
    {
        // Act & Assert: a null catalog is rejected
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => SpeechRecognizerFactory.LoadAsync(new FakeRecognitionModel(), (SpeechModelCatalog)null!, cancellationToken: TestContext.Current.CancellationToken));
    }

    /// <summary>
    ///     Proves that when <see cref="IRecognitionBackendFactory.Create"/> ignores cancellation
    ///     but still finishes within the dedicated worker's abandon grace period - so the worker
    ///     genuinely completes rather than being abandoned - <see cref="SpeechRecognizerFactory.LoadAsync(IRecognitionModel,string,ISpeechDiagnostics?,IRecognitionBackendFactory,IReadOnlyDictionary{string,object}?,CancellationToken)"/>
    ///     still honors the cancellation request (finding 30) rather than reporting a loaded
    ///     engine, and disposes the backend that was created so it does not leak.
    /// </summary>
    [Fact(Timeout = 10000)]
    public async Task SpeechRecognizerFactory_LoadAsync_CancelledDuringGraceWindowCreateSucceeds_ThrowsAndDisposesBackend()
    {
        // Arrange: a backend factory whose Create blocks until released, then succeeds, ignoring
        // cancellation entirely - exactly like the real native binding, which has no in-flight
        // cancellation primitive of its own.
        using var createStarted = new SemaphoreSlim(0, 1);
        using var createRelease = new SemaphoreSlim(0, 1);
        var engine = new FakeRecognitionEngine();
        var backendFactory = new BlockingRecognitionEngineFactory(createStarted, createRelease, engine);
        using var cts = new CancellationTokenSource();

        // Act: start loading, wait until Create has begun, cancel, then let Create finish quickly
        // (well within the worker's default abandon timeout) so the worker is not abandoned
        var loadTask = SpeechRecognizerFactory.LoadAsync(
            new FakeRecognitionModel(), _installedModelDirectory, null, backendFactory, cancellationToken: cts.Token);
        await createStarted.WaitAsync(TestContext.Current.CancellationToken);
        await cts.CancelAsync();
        createRelease.Release();

        // Assert: the cancellation is still honored even though backend creation genuinely
        // succeeded, and the backend that was created is disposed rather than leaked
        await Assert.ThrowsAsync<OperationCanceledException>(() => loadTask);
        Assert.Equal(1, engine.DisposeCallCount);
    }

    /// <summary>
    ///     Test-only <see cref="IRecognitionBackendFactory"/> whose <see cref="Create"/> signals a
    ///     semaphore once called, then blocks until the test explicitly releases a second
    ///     semaphore before returning the pre-configured engine - simulating a native load call
    ///     that ignores cancellation but still finishes within the dedicated worker's abandon
    ///     grace period.
    /// </summary>
    private sealed class BlockingRecognitionEngineFactory(
        SemaphoreSlim createStarted,
        SemaphoreSlim createRelease,
        FakeRecognitionEngine engine) : IRecognitionBackendFactory
    {
        public IRecognitionBackend Create(
            IRecognitionModel model,
            string installedModelDirectory,
            IReadOnlyDictionary<string, object>? parameterValues = null)
        {
            createStarted.Release();

            // Bounded wait purely as a safety net so a failed/timed-out test cannot leave this
            // dedicated worker thread blocked forever; the behavior under test only relies on the
            // release happening promptly.
            createRelease.Wait(TimeSpan.FromSeconds(30));
            return engine;
        }
    }

    /// <summary>
    ///     Test-only recognition model whose declared role contradicts the recognition interface
    ///     it implements, used to prove the factory rejects it honestly.
    /// </summary>
    private sealed class WrongRoleRecognitionModel : IRecognitionModel
    {
        /// <inheritdoc/>
        public string Id => "wrong-role-model";

        /// <inheritdoc/>
        public string DisplayName => "Wrong Role Model";

        /// <inheritdoc/>
        public SpeechModelRole Role => SpeechModelRole.Synthesis;

        /// <inheritdoc/>
        public IReadOnlyList<ISpeechModelParameter> Parameters => [];

        /// <inheritdoc/>
        public SpeechModelAudioTagSupport AudioTagSupport => SpeechModelAudioTagSupport.None;

        /// <inheritdoc/>
        public SpeechModelDownloadDescriptor DownloadDescriptor =>
            FakeModelDescriptors.SingleFileDescriptor("wrong-role-model");

        /// <inheritdoc/>
        AudioFormat IRecognitionModel.AudioFormat => AudioFormat.Mono(16000);

        /// <inheritdoc/>
        IRecognitionBackend IRecognitionModel.CreateBackend(string installedModelDirectory) =>
            new FakeRecognitionEngine();
    }

    /// <summary>
    ///     Test-only recognition model whose two-argument <c>CreateBackend</c> override encodes a
    ///     supplied <c>"language"</c> parameter value into the returned
    ///     <see cref="FakeRecognitionEngine"/>, used to prove a <c>parameterValues</c> bag
    ///     supplied to <see cref="SpeechRecognizerFactory"/>'s <c>LoadAsync</c> overloads
    ///     genuinely reaches the model, not merely the backend factory.
    /// </summary>
    private sealed class ParameterCapturingRecognitionModel : IRecognitionModel
    {
        /// <inheritdoc/>
        public string Id => "parameter-capturing-recognition-model";

        /// <inheritdoc/>
        public string DisplayName => "Parameter Capturing Recognition Model";

        /// <inheritdoc/>
        public SpeechModelRole Role => SpeechModelRole.Recognition;

        /// <inheritdoc/>
        public IReadOnlyList<ISpeechModelParameter> Parameters => [];

        /// <inheritdoc/>
        public SpeechModelAudioTagSupport AudioTagSupport => SpeechModelAudioTagSupport.None;

        /// <inheritdoc/>
        public SpeechModelDownloadDescriptor DownloadDescriptor =>
            FakeModelDescriptors.SingleFileDescriptor("parameter-capturing-recognition-model");

        /// <inheritdoc/>
        AudioFormat IRecognitionModel.AudioFormat => AudioFormat.Mono(16000);

        /// <inheritdoc/>
        IRecognitionBackend IRecognitionModel.CreateBackend(string installedModelDirectory) =>
            new FakeRecognitionEngine();

        /// <summary>
        ///     Encodes a supplied <c>"language"</c> string value into the returned engine's
        ///     reported capability/metadata so a test can assert the value it passed as
        ///     <c>parameterValues</c> reached this method, not merely the backend factory that
        ///     called it.
        /// </summary>
        /// <inheritdoc/>
        IRecognitionBackend IRecognitionModel.CreateBackend(
            string installedModelDirectory,
            IReadOnlyDictionary<string, object>? parameterValues)
        {
            var language = parameterValues is not null &&
                parameterValues.TryGetValue("language", out var value) &&
                value is string languageValue
                    ? languageValue
                    : null;

            return new ParameterCapturingRecognitionEngine(language);
        }
    }

    /// <summary>
    ///     Minimal <see cref="IRecognitionBackend"/> that only exposes the <c>language</c> value
    ///     it was constructed with, so a test can assert a <c>parameterValues</c> bag genuinely
    ///     reached <see cref="IRecognitionModel.CreateBackend(string,IReadOnlyDictionary{string,object}?)"/>
    ///     as implemented by <see cref="ParameterCapturingRecognitionModel"/>.
    /// </summary>
    private sealed class ParameterCapturingRecognitionEngine(string? language) : IRecognitionBackend
    {
        /// <summary>Gets the <c>language</c> parameter value the owning model received, or <see langword="null"/>.</summary>
        public string? Language => language;

        /// <inheritdoc/>
        public void AcceptSamples(ReadOnlySpan<float> monoSamples)
        {
        }

        /// <inheritdoc/>
        public bool TryDecode(out SpeechRecognitionResult? result)
        {
            result = null;
            return false;
        }

        /// <inheritdoc/>
        public bool TryFlush(out SpeechRecognitionResult? result) => TryDecode(out result);

        /// <inheritdoc/>
        public void Reset()
        {
        }

        /// <inheritdoc/>
        public void Dispose()
        {
        }
    }

    /// <summary>
    ///     Test-only recognition model whose two-argument <c>CreateBackend</c> override records
    ///     the received <c>parameterValues</c> bag into <see cref="RequestedParameterValues"/>
    ///     and then throws, short-circuiting before any real native sherpa-onnx engine
    ///     construction could occur. Used to prove that a <c>parameterValues</c> bag supplied to
    ///     the genuine PUBLIC store/catalog composition overloads (with no injected
    ///     <c>backendFactory</c> test seam) reaches this model through the real production
    ///     delegation chain, without requiring a working native engine.
    /// </summary>
    private sealed class ThrowingParameterCapturingRecognitionModel : IRecognitionModel
    {
        /// <summary>
        ///     Gets the <c>parameterValues</c> bag most recently received by the two-argument
        ///     <see cref="IRecognitionModel.CreateBackend(string,IReadOnlyDictionary{string,object}?)"/>
        ///     override, or <see langword="null"/> if it has not yet been invoked.
        /// </summary>
        public IReadOnlyDictionary<string, object>? RequestedParameterValues { get; private set; }

        /// <inheritdoc/>
        public string Id => "throwing-parameter-capturing-recognition-model";

        /// <inheritdoc/>
        public string DisplayName => "Throwing Parameter Capturing Recognition Model";

        /// <inheritdoc/>
        public SpeechModelRole Role => SpeechModelRole.Recognition;

        /// <inheritdoc/>
        public IReadOnlyList<ISpeechModelParameter> Parameters => [];

        /// <inheritdoc/>
        public SpeechModelAudioTagSupport AudioTagSupport => SpeechModelAudioTagSupport.None;

        /// <inheritdoc/>
        public SpeechModelDownloadDescriptor DownloadDescriptor =>
            FakeModelDescriptors.SingleFileDescriptor("throwing-parameter-capturing-recognition-model");

        /// <inheritdoc/>
        AudioFormat IRecognitionModel.AudioFormat => AudioFormat.Mono(16000);

        /// <summary>
        ///     Never expected to be invoked by these tests, since <c>parameterValues</c> is
        ///     always supplied; throws <see cref="NotSupportedException"/> if it ever is.
        /// </summary>
        /// <inheritdoc/>
        IRecognitionBackend IRecognitionModel.CreateBackend(string installedModelDirectory) =>
            throw new NotSupportedException(
                "This test model always expects parameterValues to be supplied; the single-argument overload should never be invoked.");

        /// <summary>
        ///     Records the received <paramref name="parameterValues"/> bag into
        ///     <see cref="RequestedParameterValues"/>, then throws
        ///     <see cref="InvalidOperationException"/> to short-circuit before any real native
        ///     sherpa-onnx engine construction could occur.
        /// </summary>
        /// <inheritdoc/>
        IRecognitionBackend IRecognitionModel.CreateBackend(
            string installedModelDirectory,
            IReadOnlyDictionary<string, object>? parameterValues)
        {
            RequestedParameterValues = parameterValues;
            throw new InvalidOperationException(
                "Deliberate short-circuit: parameterValues has been recorded; no real backend is produced.");
        }
    }
}
