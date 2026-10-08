using System.Runtime.InteropServices;
using System.Security.Cryptography;
using DemaConsulting.Speech.AudioSubsystem;
using DemaConsulting.Speech.AudioSubsystem.PortAudio;
using DemaConsulting.Speech.Diagnostics;
using DemaConsulting.Speech.ModelManagementSubsystem;
using DemaConsulting.Speech.RecognitionSubsystem;
using DemaConsulting.Speech.SynthesisSubsystem;
using DemaConsulting.Speech.Tests.ModelManagementSubsystem.Fakes;
using DemaConsulting.Speech.Tests.RecognitionSubsystem.Fakes;
using DemaConsulting.Speech.Tests.SynthesisSubsystem.Fakes;
using NSubstitute;

namespace DemaConsulting.Speech.Tests;

/// <summary>
///     System-level integration tests for the Speech system.
/// </summary>
public class SpeechTests
{
    /// <summary>
    ///     Proves that the composed system returns real PortAudio-backed device abstractions when PortAudio initializes.
    /// </summary>
    [Fact]
    public void Speech_SystemIntegration_PortAudioInitialized_FactoryReturnsRealDevices()
    {
        // Arrange: a fake PortAudio environment with one default capture device and one default playback device
        var environment = new PortAudioEnvironment(
            new FakePortAudioApi(
            [
                new PortAudioDeviceInfo("Mic", 5, 1, 0, 16000, 0.01, 0.0),
                new PortAudioDeviceInfo("Speaker", 5, 0, 2, 48000, 0.0, 0.01)
            ]),
            OSPlatform.Windows);
        var factory = new AudioDeviceFactory(null, null, null, environment);

        // Act: request both a capture and a playback device
        var captureDevice = factory.CreateCaptureDevice();
        var playbackDevice = factory.CreatePlaybackDevice();

        // Assert: the system composes real PortAudio-backed devices end to end
        Assert.IsType<PortAudioCaptureDevice>(captureDevice);
        Assert.IsType<PortAudioPlaybackDevice>(playbackDevice);
        Assert.True(captureDevice.IsAvailable);
        Assert.True(playbackDevice.IsAvailable);
    }

    /// <summary>
    ///     Proves that a host-supplied <see cref="ISpeechDiagnostics"/> sink receives structural events reported during audio-device composition.
    /// </summary>
    [Fact]
    public void Speech_SystemIntegration_DiagnosticsSink_ReceivesStructuralEvents()
    {
        // Arrange: a recording diagnostics sink and a deterministic PortAudio environment
        var diagnostics = Substitute.For<ISpeechDiagnostics>();
        var environment = new PortAudioEnvironment(
            new FakePortAudioApi(
            [
                new PortAudioDeviceInfo("Mic", 5, 1, 0, 16000, 0.01, 0.0)
            ]),
            OSPlatform.Windows);
        var factory = new AudioDeviceFactory(null, null, diagnostics, environment);

        // Act: trigger composition that resolves a real capture device
        factory.CreateCaptureDevice();

        // Assert: the sink observed the selection event
        diagnostics.Received().Report(
            SpeechDiagnosticLevel.Info,
            "AudioSubsystem",
            "Resolved PortAudio capture device 'Mic' via host API default.");
    }

    /// <summary>
    ///     Proves that omitting the diagnostics sink still composes safely through the default null implementation.
    /// </summary>
    [Fact]
    public void Speech_SystemIntegration_NoDiagnosticsSupplied_UsesNullSpeechDiagnosticsSafely()
    {
        // Arrange: a deterministic PortAudio environment and a factory with no diagnostics sink supplied
        var environment = new PortAudioEnvironment(
            new FakePortAudioApi(
            [
                new PortAudioDeviceInfo("Mic", 5, 1, 0, 16000, 0.01, 0.0)
            ]),
            OSPlatform.Windows);
        var factory = new AudioDeviceFactory(null, null, null, environment);

        // Act: exercise composition that reports diagnostics internally
        var exception = Record.Exception(() => factory.CreateCaptureDevice());

        // Assert: composition succeeds without any diagnostics-related failure
        Assert.Null(exception);
    }

    /// <summary>
    ///     Proves that a PortAudio initialization failure still composes safely by returning the unavailable fallback devices.
    /// </summary>
    [Fact]
    public void Speech_SystemIntegration_PortAudioInitializationFails_FactoryReturnsUnavailableDevices()
    {
        // Arrange: a deterministic PortAudio environment whose initialization fails
        var environment = new PortAudioEnvironment(
            new FakePortAudioApi([], new InvalidOperationException("PortAudio init failed.")),
            OSPlatform.Windows);
        var factory = new AudioDeviceFactory(null, null, null, environment);

        // Act: request both a capture and a playback device
        var captureDevice = factory.CreateCaptureDevice();
        var playbackDevice = factory.CreatePlaybackDevice();

        // Assert: the system honestly reports both devices as unavailable instead of throwing
        Assert.Same(UnavailableAudioCaptureDevice.Instance, captureDevice);
        Assert.Same(UnavailableAudioPlaybackDevice.Instance, playbackDevice);
    }

    /// <summary>
    ///     Proves that attempting to use a device the factory could not resolve from its own probe
    ///     surfaces the documented exception, consistent with the probe the factory was given.
    /// </summary>
    [Fact]
    public void Speech_SystemValidation_NoResolvableCaptureDevice_StartThrowsAudioDeviceUnavailableException()
    {
        // Arrange: a PortAudio-initialized environment whose preferred host API has no capture-capable devices
        var environment = new PortAudioEnvironment(
            new FakePortAudioApi(
            [
                new PortAudioDeviceInfo("Speaker", 5, 0, 2, 48000, 0.0, 0.01)
            ]),
            OSPlatform.Windows);
        var factory = new AudioDeviceFactory(null, null, null, environment);
        var device = factory.CreateCaptureDevice();

        // Act & Assert: the factory's own default probe reports no capture devices, so it returns
        // the honest unavailable fallback (consistent with AudioDeviceFactory_CreateCaptureDevice
        // now consulting CaptureProbe before constructing a real device) rather than a real,
        // resolved-but-unavailable PortAudioCaptureDevice - starting it still throws the same
        // documented exception either way.
        Assert.Same(UnavailableAudioCaptureDevice.Instance, device);
        Assert.False(device.IsAvailable);
        Assert.Throws<AudioDeviceUnavailableException>(device.Start);
    }

    /// <summary>
    ///     Proves that the system composes <see cref="SpeechModelStore"/> and
    ///     <see cref="SpeechModelDownloader"/> end to end: a declared file is fetched, its
    ///     SHA-256 checksum is verified, and it is atomically installed and reported installed.
    /// </summary>
    [Fact]
    public async Task Speech_SystemIntegration_ModelDownload_VerifiesAndAtomicallyInstallsModel()
    {
        // Arrange: a scratch store root and a fake download client serving a known payload
        var testRoot = Path.Join(Path.GetTempPath(), "DemaConsulting.Speech.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testRoot);
        try
        {
            var store = new SpeechModelStore(new SpeechModelStoreOptions { RootPathOverride = testRoot });
            var payload = "system-integration-model-payload"u8.ToArray();
            var checksum = Convert.ToHexStringLower(SHA256.HashData(payload));
            var descriptor = new SpeechModelDownloadDescriptor(
            [
                new SpeechModelDownloadFile(new Uri("https://example.test/model.bin"), checksum, "model.bin")
            ]);
            using var downloader = new SpeechModelDownloader(store, new FixedPayloadModelDownloadClient(payload));

            // Act: download, verify, and atomically install the model
            var result = await downloader.DownloadAsync(
                "system-integration-model", descriptor, cancellationToken: TestContext.Current.CancellationToken);

            // Assert: the system reports the model installed with its verified content in place
            Assert.Equal(SpeechModelDownloadOutcome.Installed, result.Outcome);
            Assert.True(store.IsInstalled("system-integration-model"));
            var installedBytes = await File.ReadAllBytesAsync(
                Path.Join(store.GetCurrentDirectory("system-integration-model"), "model.bin"),
                TestContext.Current.CancellationToken);
            Assert.Equal(payload, installedBytes);
        }
        finally
        {
            Directory.Delete(testRoot, recursive: true);
        }
    }

    /// <summary>
    ///     Proves that the system composes <see cref="SpeechModelCatalog"/> over an injected known
    ///     model and a real <see cref="SpeechModelStore"/>: the catalog honestly reports
    ///     <see cref="SpeechModelState.NotDownloaded"/> before any download, and
    ///     <see cref="SpeechModelState.Downloaded"/> once <see cref="SpeechModelCatalog.DownloadAsync"/>
    ///     verifies and atomically installs the model.
    /// </summary>
    [Fact]
    public async Task Speech_SystemIntegration_ModelCatalog_EnumeratesAndTracksDownloadState()
    {
        // Arrange: a scratch store root, one known fake model, and a fake download client
        var testRoot = Path.Join(Path.GetTempPath(), "DemaConsulting.Speech.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testRoot);
        try
        {
            var store = new SpeechModelStore(new SpeechModelStoreOptions { RootPathOverride = testRoot });
            var payload = "system-integration-catalog-payload"u8.ToArray();
            var checksum = Convert.ToHexStringLower(SHA256.HashData(payload));
            var descriptor = new SpeechModelDownloadDescriptor(
            [
                new SpeechModelDownloadFile(new Uri("https://example.test/catalog-model.bin"), checksum, "model.bin")
            ]);
            var model = new FakeCatalogModel("system-integration-catalog-model", descriptor);
            using var catalog = new SpeechModelCatalog(
                [model], store, new FixedPayloadModelDownloadClient(payload));

            // Act 1: enumerate before any download
            var beforeDescriptor = Assert.Single(catalog.Enumerate());

            // Assert 1: honestly reports not-downloaded
            Assert.Equal(SpeechModelState.NotDownloaded, beforeDescriptor.State);

            // Act 2: download, verify, and atomically install the model through the catalog
            var result = await catalog.DownloadAsync(
                "system-integration-catalog-model", cancellationToken: TestContext.Current.CancellationToken);

            // Assert 2: the system reports the model installed and the catalog reflects it
            Assert.Equal(SpeechModelDownloadOutcome.Installed, result.Outcome);
            var afterDescriptor = Assert.Single(catalog.Enumerate());
            Assert.Equal(SpeechModelState.Downloaded, afterDescriptor.State);
        }
        finally
        {
            Directory.Delete(testRoot, recursive: true);
        }
    }

    /// <summary>
    ///     Proves that the system composes the full streaming recognition pipeline end to end:
    ///     <see cref="SpeechRecognizerFactory"/> loads a real engine over an installed model, an
    ///     <see cref="IRecognitionSession"/> is bound to an available capture device, a captured
    ///     audio block flows through resampling into the backend, and the resulting recognition
    ///     text is surfaced through <see cref="IRecognitionSession.GetResultsAsync"/>.
    /// </summary>
    [Fact]
    public async Task Speech_SystemIntegration_StreamingRecognition_CapturedAudioProducesRecognitionResults()
    {
        // Arrange: a scratch installed-model directory, an available capture device, and a
        // deterministic backend standing in for the native sherpa-onnx runtime
        var installedModelDirectory = Path.Join(
            Path.GetTempPath(), "DemaConsulting.Speech.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(installedModelDirectory);
        try
        {
            var captureDevice = Substitute.For<IAudioCaptureDevice>();
            captureDevice.IsAvailable.Returns(true);
            captureDevice.SampleRate.Returns(16000);
            captureDevice.ChannelCount.Returns(1);
            var backendFactory = new FakeRecognitionEngineFactory(new FakeRecognitionEngine(
            [
                new SpeechRecognitionResult("hello", IsFinal: false),
                new SpeechRecognitionResult("hello world", IsFinal: true)
            ]));

            // Act: load the engine, create a session, stream one captured block, and drain the pipeline
            var cancellationToken = TestContext.Current.CancellationToken;
            await using var engine = await SpeechRecognizerFactory.LoadAsync(
                new FakeRecognitionModel(), installedModelDirectory, null, backendFactory,
                cancellationToken: cancellationToken);
            await using var session = await engine.CreateSessionAsync(captureDevice, cancellationToken);

            var received = new List<SpeechRecognitionResult>();
            var pump = Task.Run(async () =>
            {
                await foreach (var evt in session.GetResultsAsync(cancellationToken))
                {
                    received.Add(evt.Result);
                }
            }, cancellationToken);

            await session.StartAsync(cancellationToken);
            captureDevice.FrameCaptured += Raise.Event<EventHandler<AudioCaptureFrameEventArgs>>(
                captureDevice,
                new AudioCaptureFrameEventArgs([0.1f, 0.2f, 0.3f]));
            await session.StopAsync(cancellationToken);
            await pump;

            // Assert: the system produced the final recognition result from the captured audio.
            // The scripted provisional result ("hello") arrived before the consumer started
            // draining, and its own final ("hello world") was buffered before it was ever read,
            // so the provisional was superseded rather than delivered as a stale partial
            // transcript trailing its own final (Decision #5).
            Assert.True(session.IsAvailable);
            var result = Assert.Single(received);
            Assert.True(result.IsFinal);
            Assert.Equal("hello world", result.Text);
        }
        finally
        {
            Directory.Delete(installedModelDirectory, recursive: true);
        }
    }

    /// <summary>
    ///     Proves that the system composes the full chunked streaming synthesis pipeline end to
    ///     end: <see cref="SpeechSynthesizerFactory"/> loads a real engine over an installed
    ///     model, an <see cref="ISynthesisSession"/> is bound to an available playback device,
    ///     text flows through Layer 2 rendering and the synthesis backend, and the resulting
    ///     audio is written to the playback device in order.
    /// </summary>
    [Fact]
    public async Task Speech_SystemIntegration_StreamingSynthesis_TextProducesPlayedAudio()
    {
        // Arrange: a scratch installed-model directory, an available playback device, and a
        // deterministic backend standing in for the native sherpa-onnx runtime
        var installedModelDirectory = Path.Join(
            Path.GetTempPath(), "DemaConsulting.Speech.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(installedModelDirectory);
        try
        {
            var playbackDevice = Substitute.For<IAudioPlaybackDevice>();
            playbackDevice.IsAvailable.Returns(true);
            playbackDevice.SampleRate.Returns(16000);
            playbackDevice.ChannelCount.Returns(1);
            var backendFactory = new FakeSynthesisEngineFactory(new FakeSynthesisEngine(sampleRate: 16000));

            // Act: load the engine, create a session, and speak one plain-text utterance end to end
            var cancellationToken = TestContext.Current.CancellationToken;
            await using var engine = await SpeechSynthesizerFactory.LoadAsync(
                new FakeSynthesisModel(), installedModelDirectory, null, backendFactory,
                cancellationToken: cancellationToken);
            Assert.True(engine.IsAvailable);
            await using var session = await engine.CreateSessionAsync(playbackDevice, cancellationToken);
            await session.SpeakAsync("hello world", cancellationToken);

            // Assert: the system started the device, wrote at least one block of audio, and stopped it
            playbackDevice.Received(1).Start();
            playbackDevice.Received().Write(Arg.Any<IReadOnlyList<float>>());
            playbackDevice.Received(1).Stop();
        }
        finally
        {
            Directory.Delete(installedModelDirectory, recursive: true);
        }
    }

    /// <summary>
    ///     Minimal fake <see cref="ISpeechModel"/> used only by the system-level catalog
    ///     integration test, so that test does not depend on the wider test project's
    ///     <c>Fakes</c> helpers.
    /// </summary>
    private sealed class FakeCatalogModel(string id, SpeechModelDownloadDescriptor downloadDescriptor) : IRecognitionModel
    {
        /// <inheritdoc/>
        public string Id => id;

        /// <inheritdoc/>
        public string DisplayName => "Fake Catalog Model";

        /// <inheritdoc/>
        public SpeechModelRole Role => SpeechModelRole.Recognition;

        /// <inheritdoc/>
        public IReadOnlyList<ISpeechModelParameter> Parameters => [];

        /// <inheritdoc/>
        public SpeechModelAudioTagSupport AudioTagSupport => SpeechModelAudioTagSupport.None;

        /// <inheritdoc/>
        public SpeechModelDownloadDescriptor DownloadDescriptor => downloadDescriptor;

        /// <inheritdoc/>
        AudioFormat IRecognitionModel.AudioFormat => AudioFormat.Mono(16000);

        /// <inheritdoc/>
        IRecognitionBackend IRecognitionModel.CreateBackend(string installedModelDirectory) =>
            new FakeRecognitionEngine();
    }

    /// <summary>
    ///     Minimal fake <see cref="IModelDownloadClient"/> that writes a fixed in-memory payload,
    ///     used by the system-level model-download integration test.
    /// </summary>
    private sealed class FixedPayloadModelDownloadClient(byte[] payload) : IModelDownloadClient
    {
        /// <inheritdoc/>
        public async Task DownloadAsync(
            Uri sourceUri,
            Stream destination,
            IProgress<SpeechModelDownloadProgress>? progress,
            CancellationToken cancellationToken,
            DownloadMirror? mirrorAuth = null)
        {
            await destination.WriteAsync(payload, cancellationToken);
            progress?.Report(new SpeechModelDownloadProgress(0, 1, payload.Length, payload.Length));
        }
    }

    /// <summary>
    ///     Minimal fake PortAudio seam used by the system tests.
    /// </summary>
    private sealed class FakePortAudioApi(
        IReadOnlyList<PortAudioDeviceInfo> devices,
        Exception? initializeException = null) : IPortAudioApi
    {
        /// <inheritdoc/>
        public int HostApiCount => 1;

        /// <inheritdoc/>
        public int DeviceCount => devices.Count;

        /// <inheritdoc/>
        public void Initialize()
        {
            if (initializeException is not null)
            {
                throw initializeException;
            }
        }

        /// <inheritdoc/>
        public void Terminate()
        {
        }

        /// <inheritdoc/>
        public int? FindHostApiIndex(PortAudioHostApiType hostApiType)
        {
            return 5;
        }

        /// <inheritdoc/>
        public PortAudioHostApiInfo GetHostApiInfo(int hostApiIndex)
        {
            return new PortAudioHostApiInfo("Windows WASAPI", PortAudioHostApiType.Wasapi, 0, 1);
        }

        /// <inheritdoc/>
        public PortAudioDeviceInfo GetDeviceInfo(int deviceIndex)
        {
            return devices[deviceIndex];
        }

        /// <inheritdoc/>
        public bool IsCaptureFormatSupported(int deviceIndex, int channelCount, int sampleRate)
        {
            return true;
        }

        /// <inheritdoc/>
        public bool IsPlaybackFormatSupported(int deviceIndex, int channelCount, int sampleRate)
        {
            return true;
        }

        /// <inheritdoc/>
        public IPortAudioStream OpenCaptureStream(
            int deviceIndex,
            int channelCount,
            int sampleRate,
            uint framesPerBuffer,
            Action<IReadOnlyList<float>> onSamplesCaptured)
        {
            throw new NotSupportedException("Stream opening is outside this test scope.");
        }

        /// <inheritdoc/>
        public IPortAudioStream OpenPlaybackStream(
            int deviceIndex,
            int channelCount,
            int sampleRate,
            uint framesPerBuffer,
            Func<int, float[]> provideSamples)
        {
            throw new NotSupportedException("Stream opening is outside this test scope.");
        }
    }
}
