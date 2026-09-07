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
///     <see cref="UnavailableSpeechRecognizer"/> instead.
/// </summary>
public sealed class SpeechRecognizerFactoryTests : IDisposable
{
    /// <summary>
    ///     A scratch directory standing in for a model's installed <c>current/</c> directory,
    ///     created per test instance and removed on disposal.
    /// </summary>
    private readonly string _installedModelDirectory = Path.Combine(
        Path.GetTempPath(),
        "DemaConsulting.Speech.Tests",
        Guid.NewGuid().ToString("N"));

    /// <summary>
    ///     A scratch root directory for a real <see cref="SpeechModelStore"/>, created per test
    ///     instance and removed on disposal.
    /// </summary>
    private readonly string _storeRoot = Path.Combine(
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
    ///     recognizer, and that the engine is never loaded.
    /// </summary>
    [Fact]
    public void SpeechRecognizerFactory_Create_ModelNotInstalled_ReturnsUnavailableRecognizer()
    {
        // Arrange: an available capture device but a directory that does not exist
        var captureDevice = CreateAvailableCaptureDevice();
        var engineFactory = new FakeRecognitionEngineFactory();
        var missingDirectory = Path.Combine(_installedModelDirectory, "not-installed");

        // Act: compose against the missing model directory
        var recognizer = SpeechRecognizerFactory.Create(
            new FakeRecognitionModel(), missingDirectory, captureDevice, null, engineFactory);

        // Assert: the honest fallback is returned and no engine was loaded
        Assert.Same(UnavailableSpeechRecognizer.Instance, recognizer);
        Assert.Equal(0, engineFactory.CreateCallCount);
    }

    /// <summary>
    ///     Proves that a machine with no usable capture device composes to the honest unavailable
    ///     recognizer rather than loading a model that could never be fed.
    /// </summary>
    [Fact]
    public void SpeechRecognizerFactory_Create_CaptureDeviceUnavailable_ReturnsUnavailableRecognizer()
    {
        // Arrange: an installed model but the shared unavailable capture device
        var engineFactory = new FakeRecognitionEngineFactory();

        // Act: compose against the unavailable device
        var recognizer = SpeechRecognizerFactory.Create(
            new FakeRecognitionModel(),
            _installedModelDirectory,
            UnavailableAudioCaptureDevice.Instance,
            null,
            engineFactory);

        // Assert: the honest fallback is returned and no engine was loaded
        Assert.Same(UnavailableSpeechRecognizer.Instance, recognizer);
        Assert.Equal(0, engineFactory.CreateCallCount);
    }

    /// <summary>
    ///     Proves that a model declaring a non-recognition role composes to the honest unavailable
    ///     recognizer.
    /// </summary>
    [Fact]
    public void SpeechRecognizerFactory_Create_ModelRoleIsNotRecognition_ReturnsUnavailableRecognizer()
    {
        // Arrange: an installed model that declares the synthesis role, and an available device
        var captureDevice = CreateAvailableCaptureDevice();
        var engineFactory = new FakeRecognitionEngineFactory();

        // Act: compose against the wrong-role model
        var recognizer = SpeechRecognizerFactory.Create(
            new WrongRoleRecognitionModel(), _installedModelDirectory, captureDevice, null, engineFactory);

        // Assert: the honest fallback is returned and no engine was loaded
        Assert.Same(UnavailableSpeechRecognizer.Instance, recognizer);
        Assert.Equal(0, engineFactory.CreateCallCount);
    }

    /// <summary>
    ///     Proves that a native-runtime or model-file load failure degrades to the honest
    ///     unavailable recognizer instead of propagating out of composition.
    /// </summary>
    [Fact]
    public void SpeechRecognizerFactory_Create_EngineLoadFails_ReturnsUnavailableRecognizerAndDoesNotThrow()
    {
        // Arrange: an installed model, an available device, and an engine factory that faults
        var captureDevice = CreateAvailableCaptureDevice();
        var diagnostics = Substitute.For<ISpeechDiagnostics>();
        var engineFactory = new FakeRecognitionEngineFactory(
            createException: new DllNotFoundException("sherpa-onnx-c-api"));

        // Act: compose, capturing any exception that escapes
        ISpeechRecognizer? recognizer = null;
        var exception = Record.Exception(() => recognizer = SpeechRecognizerFactory.Create(
            new FakeRecognitionModel(), _installedModelDirectory, captureDevice, diagnostics, engineFactory));

        // Assert: composition succeeded honestly and reported the fault as a structural fact
        Assert.Null(exception);
        Assert.Same(UnavailableSpeechRecognizer.Instance, recognizer);
        diagnostics.Received().Report(
            SpeechDiagnosticLevel.Error,
            "RecognitionSubsystem",
            Arg.Is<string>(message => message.Contains("could not be loaded", StringComparison.Ordinal)));
    }

    /// <summary>
    ///     Proves that an installed recognition model plus an available capture device composes a
    ///     real recognizer wired to the injected engine factory, with the installed-model
    ///     directory passed through unchanged.
    /// </summary>
    [Fact]
    public void SpeechRecognizerFactory_Create_ModelInstalledAndDeviceAvailable_ReturnsRealRecognizer()
    {
        // Arrange: an installed model, an available device, and a fake engine factory
        var captureDevice = CreateAvailableCaptureDevice();
        var engineFactory = new FakeRecognitionEngineFactory();
        var model = new FakeRecognitionModel();

        // Act: compose a recognizer
        using var recognizer = SpeechRecognizerFactory.Create(
            model, _installedModelDirectory, captureDevice, null, engineFactory);

        // Assert: a real recognizer was built from the injected engine, for the right model
        Assert.IsType<SherpaOnnxSpeechRecognizer>(recognizer);
        Assert.True(recognizer.IsAvailable);
        Assert.Equal(1, engineFactory.CreateCallCount);
        Assert.Same(model, engineFactory.RequestedModel);
        Assert.Equal(_installedModelDirectory, engineFactory.RequestedInstalledModelDirectory);
    }

    /// <summary>
    ///     Proves that the public composition overload rejects a null model, since a null
    ///     argument is a programming error rather than an ordinary machine state.
    /// </summary>
    [Fact]
    public void SpeechRecognizerFactory_Create_NullModel_ThrowsArgumentNullException()
    {
        // Arrange: an available capture device
        var captureDevice = CreateAvailableCaptureDevice();

        // Act & Assert: a null model is rejected
        Assert.Throws<ArgumentNullException>(
            () => SpeechRecognizerFactory.Create(null!, _installedModelDirectory, captureDevice));
    }

    /// <summary>
    ///     Proves that the public composition overload rejects a null capture device.
    /// </summary>
    [Fact]
    public void SpeechRecognizerFactory_Create_NullCaptureDevice_ThrowsArgumentNullException()
    {
        // Act & Assert: a null capture device is rejected
        Assert.Throws<ArgumentNullException>(
            () => SpeechRecognizerFactory.Create(new FakeRecognitionModel(), _installedModelDirectory, null!));
    }

    /// <summary>
    ///     Proves that a model not yet installed in the store composes to the honest unavailable
    ///     recognizer, and that the engine is never loaded.
    /// </summary>
    [Fact]
    public void SpeechRecognizerFactory_Create_WithStoreModelNotInstalled_ReturnsUnavailableRecognizer()
    {
        // Arrange: an available capture device and a store with no installed model directory
        var captureDevice = CreateAvailableCaptureDevice();
        var engineFactory = new FakeRecognitionEngineFactory();
        var model = new FakeRecognitionModel();

        // Act: compose against the store, which resolves to a directory that does not exist
        var recognizer = SpeechRecognizerFactory.Create(model, _store, captureDevice, null, engineFactory);

        // Assert: the honest fallback is returned and no engine was loaded
        Assert.Same(UnavailableSpeechRecognizer.Instance, recognizer);
        Assert.Equal(0, engineFactory.CreateCallCount);
    }

    /// <summary>
    ///     Proves that an installed recognition model plus an available capture device composes a
    ///     real recognizer wired to the injected engine factory, with the directory resolved
    ///     through the store rather than hard-coded.
    /// </summary>
    [Fact]
    public void SpeechRecognizerFactory_Create_WithStoreModelInstalledAndDeviceAvailable_ReturnsRealRecognizer()
    {
        // Arrange: a model installed via the store, an available device, and a fake engine factory
        var captureDevice = CreateAvailableCaptureDevice();
        var engineFactory = new FakeRecognitionEngineFactory();
        var model = new FakeRecognitionModel();
        Directory.CreateDirectory(_store.GetCurrentDirectory(model.Id));

        // Act: compose a recognizer through the store overload
        using var recognizer = SpeechRecognizerFactory.Create(model, _store, captureDevice, null, engineFactory);

        // Assert: a real recognizer was built, and the directory was resolved through the store
        Assert.IsType<SherpaOnnxSpeechRecognizer>(recognizer);
        Assert.Equal(1, engineFactory.CreateCallCount);
        Assert.Equal(_store.GetCurrentDirectory(model.Id), engineFactory.RequestedInstalledModelDirectory);
    }

    /// <summary>
    ///     Proves that the public store-based composition overload rejects a null model.
    /// </summary>
    [Fact]
    public void SpeechRecognizerFactory_Create_WithStoreNullModel_ThrowsArgumentNullException()
    {
        // Arrange: an available capture device
        var captureDevice = CreateAvailableCaptureDevice();

        // Act & Assert: a null model is rejected
        Assert.Throws<ArgumentNullException>(
            () => SpeechRecognizerFactory.Create(null!, _store, captureDevice));
    }

    /// <summary>
    ///     Proves that the public store-based composition overload rejects a null store.
    /// </summary>
    [Fact]
    public void SpeechRecognizerFactory_Create_WithStoreNullStore_ThrowsArgumentNullException()
    {
        // Arrange: an available capture device
        var captureDevice = CreateAvailableCaptureDevice();

        // Act & Assert: a null store is rejected
        Assert.Throws<ArgumentNullException>(
            () => SpeechRecognizerFactory.Create(new FakeRecognitionModel(), (SpeechModelStore)null!, captureDevice));
    }

    /// <summary>
    ///     Proves that the public store-based composition overload rejects a null capture device,
    ///     confirming the delegation still reaches the string-overload's own null check.
    /// </summary>
    [Fact]
    public void SpeechRecognizerFactory_Create_WithStoreNullCaptureDevice_ThrowsArgumentNullException()
    {
        // Act & Assert: a null capture device is rejected
        Assert.Throws<ArgumentNullException>(
            () => SpeechRecognizerFactory.Create(new FakeRecognitionModel(), _store, null!));
    }

    /// <summary>
    ///     Proves that a model not yet installed in the catalog's store composes to the honest
    ///     unavailable recognizer, and that the engine is never loaded.
    /// </summary>
    [Fact]
    public void SpeechRecognizerFactory_Create_WithCatalogModelNotInstalled_ReturnsUnavailableRecognizer()
    {
        // Arrange: an available capture device and a catalog whose store has no installed model directory
        var captureDevice = CreateAvailableCaptureDevice();
        var engineFactory = new FakeRecognitionEngineFactory();
        var model = new FakeRecognitionModel();

        // Act: compose against the catalog, which resolves to a directory that does not exist
        var recognizer = SpeechRecognizerFactory.Create(model, _catalog, captureDevice, null, engineFactory);

        // Assert: the honest fallback is returned and no engine was loaded
        Assert.Same(UnavailableSpeechRecognizer.Instance, recognizer);
        Assert.Equal(0, engineFactory.CreateCallCount);
    }

    /// <summary>
    ///     Proves that an installed recognition model plus an available capture device composes a
    ///     real recognizer wired to the injected engine factory, with the directory resolved
    ///     through the catalog's own store rather than a second, disconnected store.
    /// </summary>
    [Fact]
    public void SpeechRecognizerFactory_Create_WithCatalogModelInstalledAndDeviceAvailable_ReturnsRealRecognizer()
    {
        // Arrange: a model installed via the catalog's store, an available device, and a fake engine factory
        var captureDevice = CreateAvailableCaptureDevice();
        var engineFactory = new FakeRecognitionEngineFactory();
        var model = new FakeRecognitionModel();
        Directory.CreateDirectory(_store.GetCurrentDirectory(model.Id));

        // Act: compose a recognizer through the catalog overload
        using var recognizer = SpeechRecognizerFactory.Create(model, _catalog, captureDevice, null, engineFactory);

        // Assert: a real recognizer was built, and the directory was resolved through the catalog's store
        Assert.IsType<SherpaOnnxSpeechRecognizer>(recognizer);
        Assert.Equal(1, engineFactory.CreateCallCount);
        Assert.Equal(_catalog.Store.GetCurrentDirectory(model.Id), engineFactory.RequestedInstalledModelDirectory);
    }

    /// <summary>
    ///     Proves that the public catalog-based composition overload rejects a null model.
    /// </summary>
    [Fact]
    public void SpeechRecognizerFactory_Create_WithCatalogNullModel_ThrowsArgumentNullException()
    {
        // Arrange: an available capture device
        var captureDevice = CreateAvailableCaptureDevice();

        // Act & Assert: a null model is rejected
        Assert.Throws<ArgumentNullException>(
            () => SpeechRecognizerFactory.Create(null!, _catalog, captureDevice));
    }

    /// <summary>
    ///     Proves that the public catalog-based composition overload rejects a null catalog.
    /// </summary>
    [Fact]
    public void SpeechRecognizerFactory_Create_WithCatalogNullCatalog_ThrowsArgumentNullException()
    {
        // Arrange: an available capture device
        var captureDevice = CreateAvailableCaptureDevice();

        // Act & Assert: a null catalog is rejected
        Assert.Throws<ArgumentNullException>(
            () => SpeechRecognizerFactory.Create(new FakeRecognitionModel(), (SpeechModelCatalog)null!, captureDevice));
    }

    /// <summary>
    ///     Proves that the public catalog-based composition overload rejects a null capture
    ///     device, confirming the delegation still reaches the store-overload's own null check.
    /// </summary>
    [Fact]
    public void SpeechRecognizerFactory_Create_WithCatalogNullCaptureDevice_ThrowsArgumentNullException()
    {
        // Act & Assert: a null capture device is rejected
        Assert.Throws<ArgumentNullException>(
            () => SpeechRecognizerFactory.Create(new FakeRecognitionModel(), _catalog, null!));
    }

    /// <summary>
    ///     Builds a substitute capture device that reports itself available with a realistic
    ///     stereo 48 kHz capture format.
    /// </summary>
    /// <returns>The configured substitute capture device.</returns>
    private static IAudioCaptureDevice CreateAvailableCaptureDevice()
    {
        var captureDevice = Substitute.For<IAudioCaptureDevice>();
        captureDevice.IsAvailable.Returns(true);
        captureDevice.SampleRate.Returns(48000);
        captureDevice.ChannelCount.Returns(2);
        return captureDevice;
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
        SherpaOnnx.OnlineRecognizerConfig IRecognitionModel.CreateEngineConfig(string installedModelDirectory) =>
            new();
    }
}
