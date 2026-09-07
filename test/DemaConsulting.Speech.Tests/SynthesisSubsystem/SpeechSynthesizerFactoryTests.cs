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
///     <see cref="UnavailableSpeechSynthesizer"/> instead.
/// </summary>
public sealed class SpeechSynthesizerFactoryTests : IDisposable
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
    ///     Initializes a new instance of the <see cref="SpeechSynthesizerFactoryTests"/> class,
    ///     creating the scratch installed-model directory the "installed" cases require.
    /// </summary>
    public SpeechSynthesizerFactoryTests()
    {
        Directory.CreateDirectory(_installedModelDirectory);
        _store = new SpeechModelStore(new SpeechModelStoreOptions { RootPathOverride = _storeRoot });
    }

    /// <summary>Removes the scratch installed-model directory.</summary>
    public void Dispose()
    {
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
    ///     synthesizer, and that the engine is never loaded.
    /// </summary>
    [Fact]
    public void SpeechSynthesizerFactory_Create_ModelNotInstalled_ReturnsUnavailableSynthesizer()
    {
        // Arrange: an available playback device but a directory that does not exist
        var playbackDevice = CreateAvailablePlaybackDevice();
        var engineFactory = new FakeSynthesisEngineFactory();
        var missingDirectory = Path.Combine(_installedModelDirectory, "not-installed");

        // Act: compose against the missing model directory
        var synthesizer = SpeechSynthesizerFactory.Create(
            new FakeSynthesisModel(), missingDirectory, playbackDevice, null, engineFactory);

        // Assert: the honest fallback is returned and no engine was loaded
        Assert.Same(UnavailableSpeechSynthesizer.Instance, synthesizer);
        Assert.Equal(0, engineFactory.CreateCallCount);
    }

    /// <summary>
    ///     Proves that a machine with no usable playback device composes to the honest
    ///     unavailable synthesizer rather than loading a model that could never be heard.
    /// </summary>
    [Fact]
    public void SpeechSynthesizerFactory_Create_PlaybackDeviceUnavailable_ReturnsUnavailableSynthesizer()
    {
        // Arrange: an installed model but the shared unavailable playback device
        var engineFactory = new FakeSynthesisEngineFactory();

        // Act: compose against the unavailable device
        var synthesizer = SpeechSynthesizerFactory.Create(
            new FakeSynthesisModel(),
            _installedModelDirectory,
            UnavailableAudioPlaybackDevice.Instance,
            null,
            engineFactory);

        // Assert: the honest fallback is returned and no engine was loaded
        Assert.Same(UnavailableSpeechSynthesizer.Instance, synthesizer);
        Assert.Equal(0, engineFactory.CreateCallCount);
    }

    /// <summary>
    ///     Proves that a model declaring a non-synthesis role composes to the honest unavailable
    ///     synthesizer.
    /// </summary>
    [Fact]
    public void SpeechSynthesizerFactory_Create_ModelRoleIsNotSynthesis_ReturnsUnavailableSynthesizer()
    {
        // Arrange: an installed model that declares the recognition role, and an available device
        var playbackDevice = CreateAvailablePlaybackDevice();
        var engineFactory = new FakeSynthesisEngineFactory();

        // Act: compose against the wrong-role model
        var synthesizer = SpeechSynthesizerFactory.Create(
            new WrongRoleSynthesisModel(), _installedModelDirectory, playbackDevice, null, engineFactory);

        // Assert: the honest fallback is returned and no engine was loaded
        Assert.Same(UnavailableSpeechSynthesizer.Instance, synthesizer);
        Assert.Equal(0, engineFactory.CreateCallCount);
    }

    /// <summary>
    ///     Proves that a native-runtime or model-file load failure degrades to the honest
    ///     unavailable synthesizer instead of propagating out of composition.
    /// </summary>
    [Fact]
    public void SpeechSynthesizerFactory_Create_EngineLoadFails_ReturnsUnavailableSynthesizerAndDoesNotThrow()
    {
        // Arrange: an installed model, an available device, and an engine factory that faults
        var playbackDevice = CreateAvailablePlaybackDevice();
        var diagnostics = Substitute.For<ISpeechDiagnostics>();
        var engineFactory = new FakeSynthesisEngineFactory(
            createException: new DllNotFoundException("sherpa-onnx-c-api"));

        // Act: compose, capturing any exception that escapes
        ISpeechSynthesizer? synthesizer = null;
        var exception = Record.Exception(() => synthesizer = SpeechSynthesizerFactory.Create(
            new FakeSynthesisModel(), _installedModelDirectory, playbackDevice, diagnostics, engineFactory));

        // Assert: composition succeeded honestly and reported the fault as a structural fact
        Assert.Null(exception);
        Assert.Same(UnavailableSpeechSynthesizer.Instance, synthesizer);
        diagnostics.Received().Report(
            SpeechDiagnosticLevel.Error,
            "SynthesisSubsystem",
            Arg.Is<string>(message => message.Contains("could not be loaded", StringComparison.Ordinal)));
    }

    /// <summary>
    ///     Proves that an installed synthesis model plus an available playback device composes a
    ///     real synthesizer wired to the injected engine factory, with the installed-model
    ///     directory passed through unchanged.
    /// </summary>
    [Fact]
    public void SpeechSynthesizerFactory_Create_ModelInstalledAndDeviceAvailable_ReturnsRealSynthesizer()
    {
        // Arrange: an installed model, an available device, and a fake engine factory
        var playbackDevice = CreateAvailablePlaybackDevice();
        var engineFactory = new FakeSynthesisEngineFactory();
        var model = new FakeSynthesisModel();

        // Act: compose a synthesizer
        using var synthesizer = SpeechSynthesizerFactory.Create(
            model, _installedModelDirectory, playbackDevice, null, engineFactory);

        // Assert: a real synthesizer was built from the injected engine, for the right model
        Assert.IsType<SherpaOnnxSpeechSynthesizer>(synthesizer);
        Assert.True(synthesizer.IsAvailable);
        Assert.Equal(1, engineFactory.CreateCallCount);
        Assert.Same(model, engineFactory.RequestedModel);
        Assert.Equal(_installedModelDirectory, engineFactory.RequestedInstalledModelDirectory);
    }

    /// <summary>
    ///     Proves that the public composition overload rejects a null model, since a null
    ///     argument is a programming error rather than an ordinary machine state.
    /// </summary>
    [Fact]
    public void SpeechSynthesizerFactory_Create_NullModel_ThrowsArgumentNullException()
    {
        // Arrange: an available playback device
        var playbackDevice = CreateAvailablePlaybackDevice();

        // Act & Assert: a null model is rejected
        Assert.Throws<ArgumentNullException>(
            () => SpeechSynthesizerFactory.Create(null!, _installedModelDirectory, playbackDevice));
    }

    /// <summary>
    ///     Proves that the public composition overload rejects a null playback device.
    /// </summary>
    [Fact]
    public void SpeechSynthesizerFactory_Create_NullPlaybackDevice_ThrowsArgumentNullException()
    {
        // Act & Assert: a null playback device is rejected
        Assert.Throws<ArgumentNullException>(
            () => SpeechSynthesizerFactory.Create(new FakeSynthesisModel(), _installedModelDirectory, null!));
    }

    /// <summary>
    ///     Proves that <c>parameterValues</c> passed to <see cref="SpeechSynthesizerFactory.Create(ISynthesisModel,string,IAudioPlaybackDevice,ISpeechDiagnostics,ISynthesisEngineFactory,IReadOnlyDictionary{string,object}?)"/>
    ///     reaches the constructed synthesizer's synthesis calls, by round-tripping it through a
    ///     fake model's <c>ResolveSpeakerId</c> hook into the fake engine's captured speaker id.
    /// </summary>
    [Fact]
    public async Task SpeechSynthesizerFactory_Create_ParameterValuesSupplied_ForwardedToSynthesizer()
    {
        // Arrange
        var playbackDevice = CreateAvailablePlaybackDevice();
        var engineFactory = new FakeSynthesisEngineFactory();
        var model = new FakeSynthesisModel(
            resolveSpeakerId: values => values is not null && values.TryGetValue("voice", out var value) && value is int id ? id : 0);
        IReadOnlyDictionary<string, object> parameterValues = new Dictionary<string, object> { ["voice"] = 4 };

        // Act
        using var synthesizer = SpeechSynthesizerFactory.Create(
            model, _installedModelDirectory, playbackDevice, null, engineFactory, parameterValues);
        await foreach (var _ in synthesizer.SynthesizeStreamAsync("Hello.", TestContext.Current.CancellationToken))
        {
            // Draining is enough to trigger the engine call.
        }

        // Assert
        var call = Assert.Single(engineFactory.Engine.GenerateCalls);
        Assert.Equal(4, call.SpeakerId);
    }

    /// <summary>
    ///     Proves that a model not yet installed in the store composes to the honest unavailable
    ///     synthesizer, and that the engine is never loaded.
    /// </summary>
    [Fact]
    public void SpeechSynthesizerFactory_Create_WithStoreModelNotInstalled_ReturnsUnavailableSynthesizer()
    {
        // Arrange: an available playback device and a store with no installed model directory
        var playbackDevice = CreateAvailablePlaybackDevice();
        var engineFactory = new FakeSynthesisEngineFactory();
        var model = new FakeSynthesisModel();

        // Act: compose against the store, which resolves to a directory that does not exist
        var synthesizer = SpeechSynthesizerFactory.Create(model, _store, playbackDevice, null, engineFactory);

        // Assert: the honest fallback is returned and no engine was loaded
        Assert.Same(UnavailableSpeechSynthesizer.Instance, synthesizer);
        Assert.Equal(0, engineFactory.CreateCallCount);
    }

    /// <summary>
    ///     Proves that an installed synthesis model plus an available playback device composes a
    ///     real synthesizer wired to the injected engine factory, with the directory resolved
    ///     through the store rather than hard-coded.
    /// </summary>
    [Fact]
    public void SpeechSynthesizerFactory_Create_WithStoreModelInstalledAndDeviceAvailable_ReturnsRealSynthesizer()
    {
        // Arrange: a model installed via the store, an available device, and a fake engine factory
        var playbackDevice = CreateAvailablePlaybackDevice();
        var engineFactory = new FakeSynthesisEngineFactory();
        var model = new FakeSynthesisModel();
        Directory.CreateDirectory(_store.GetCurrentDirectory(model.Id));

        // Act: compose a synthesizer through the store overload
        using var synthesizer = SpeechSynthesizerFactory.Create(model, _store, playbackDevice, null, engineFactory);

        // Assert: a real synthesizer was built, and the directory was resolved through the store
        Assert.IsType<SherpaOnnxSpeechSynthesizer>(synthesizer);
        Assert.Equal(1, engineFactory.CreateCallCount);
        Assert.Equal(_store.GetCurrentDirectory(model.Id), engineFactory.RequestedInstalledModelDirectory);
    }

    /// <summary>
    ///     Proves that the public store-based composition overload rejects a null model.
    /// </summary>
    [Fact]
    public void SpeechSynthesizerFactory_Create_WithStoreNullModel_ThrowsArgumentNullException()
    {
        // Arrange: an available playback device
        var playbackDevice = CreateAvailablePlaybackDevice();

        // Act & Assert: a null model is rejected
        Assert.Throws<ArgumentNullException>(
            () => SpeechSynthesizerFactory.Create(null!, _store, playbackDevice));
    }

    /// <summary>
    ///     Proves that the public store-based composition overload rejects a null store.
    /// </summary>
    [Fact]
    public void SpeechSynthesizerFactory_Create_WithStoreNullStore_ThrowsArgumentNullException()
    {
        // Arrange: an available playback device
        var playbackDevice = CreateAvailablePlaybackDevice();

        // Act & Assert: a null store is rejected
        Assert.Throws<ArgumentNullException>(
            () => SpeechSynthesizerFactory.Create(new FakeSynthesisModel(), (SpeechModelStore)null!, playbackDevice));
    }

    /// <summary>
    ///     Proves that the public store-based composition overload rejects a null playback
    ///     device, confirming the delegation still reaches the string-overload's own null check.
    /// </summary>
    [Fact]
    public void SpeechSynthesizerFactory_Create_WithStoreNullPlaybackDevice_ThrowsArgumentNullException()
    {
        // Act & Assert: a null playback device is rejected
        Assert.Throws<ArgumentNullException>(
            () => SpeechSynthesizerFactory.Create(new FakeSynthesisModel(), _store, null!));
    }

    /// <summary>
    ///     Builds a substitute playback device that reports itself available with a realistic
    ///     stereo 48 kHz playback format.
    /// </summary>
    /// <returns>The configured substitute playback device.</returns>
    private static IAudioPlaybackDevice CreateAvailablePlaybackDevice()
    {
        var playbackDevice = Substitute.For<IAudioPlaybackDevice>();
        playbackDevice.IsAvailable.Returns(true);
        playbackDevice.SampleRate.Returns(48000);
        playbackDevice.ChannelCount.Returns(2);
        return playbackDevice;
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
