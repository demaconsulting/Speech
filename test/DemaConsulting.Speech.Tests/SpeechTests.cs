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
    /// <summary>Upper bound for awaiting an asynchronous result in a test, so a regression fails instead of hanging.</summary>
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    /// <summary>
    ///     Advances a result enumerator, failing the test instead of hanging if nothing arrives.
    /// </summary>
    /// <typeparam name="T">The enumerated item type.</typeparam>
    /// <param name="enumerator">The enumerator to advance.</param>
    /// <param name="cancellationToken">The test cancellation token.</param>
    /// <returns><see langword="true"/> if an item is available; <see langword="false"/> at end of stream.</returns>
    private static async Task<bool> NextAsync<T>(IAsyncEnumerator<T> enumerator, CancellationToken cancellationToken)
    {
        var next = enumerator.MoveNextAsync();
        return await next.AsTask().WaitAsync(Timeout, cancellationToken);
    }

    /// <summary>
    ///     Best-effort scratch-directory cleanup: a locked file or a missing directory must never
    ///     mask the test's own assertion failure.
    /// </summary>
    /// <param name="path">The scratch directory to delete.</param>
    private static void TryDeleteDirectory(string path)
    {
        try
        {
            Directory.Delete(path, recursive: true);
        }
        catch (IOException)
        {
            // Best-effort cleanup only.
        }
        catch (UnauthorizedAccessException)
        {
            // Best-effort cleanup only.
        }
    }

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

        // Act & Assert: the factory consults its default capture probe, which reports no capture
        // devices, so it returns the honest unavailable fallback. Starting that device throws the
        // documented exception.
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
            TryDeleteDirectory(testRoot);
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
            TryDeleteDirectory(testRoot);
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
                scriptedResults: [new SpeechRecognitionResult("hello", IsFinal: false)],
                scriptedFlushResult: new SpeechRecognitionResult("hello world", IsFinal: true)));

            // Act: load the engine and create a session
            var cancellationToken = TestContext.Current.CancellationToken;
            await using var engine = await SpeechRecognizerFactory.LoadAsync(
                new FakeRecognitionModel(), installedModelDirectory, null, backendFactory,
                cancellationToken: cancellationToken);
            await using var session = await engine.CreateSessionAsync(captureDevice, cancellationToken);
            await using var results = session.GetResultsAsync(cancellationToken).GetAsyncEnumerator(cancellationToken);

            // Act: stream one captured block; the backend decodes only a provisional result for it
            await session.StartAsync(cancellationToken);
            captureDevice.FrameCaptured += Raise.Event<EventHandler<AudioCaptureFrameEventArgs>>(
                captureDevice,
                new AudioCaptureFrameEventArgs([0.1f, 0.2f, 0.3f]));

            // Act: consume the provisional result BEFORE stopping, so ordering never depends on
            // scheduling; stopping then flushes the backend's trailing final result
            Assert.True(await NextAsync(results, cancellationToken));
            var provisional = results.Current.Result;
            await session.StopAsync(cancellationToken);
            Assert.True(await NextAsync(results, cancellationToken));
            var final = results.Current.Result;
            var streamEnded = !await NextAsync(results, cancellationToken);

            // Assert: the system surfaced a provisional result followed by the final result, in
            // that order, and then completed the stream
            Assert.True(session.IsAvailable);
            Assert.False(provisional.IsFinal);
            Assert.Equal("hello", provisional.Text);
            Assert.True(final.IsFinal);
            Assert.Equal("hello world", final.Text);
            Assert.True(streamEnded);
        }
        finally
        {
            TryDeleteDirectory(installedModelDirectory);
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
            var synthesisEngine = new FakeSynthesisEngine(sampleRate: 16000);
            var backendFactory = new FakeSynthesisEngineFactory(synthesisEngine);

            // Act: load the engine, create a session, and speak one tagged utterance end to end
            var cancellationToken = TestContext.Current.CancellationToken;
            await using var engine = await SpeechSynthesizerFactory.LoadAsync(
                new FakeSynthesisModel(), installedModelDirectory, null, backendFactory,
                cancellationToken: cancellationToken);
            Assert.True(engine.IsAvailable);
            await using var session = await engine.CreateSessionAsync(playbackDevice, cancellationToken);
            await session.SpeakAsync("[fast] hello world", cancellationToken);

            // Assert: the system started the device, wrote at least one block of audio, and stopped it
            playbackDevice.Received(1).Start();
            playbackDevice.Received().Write(Arg.Any<IReadOnlyList<float>>());
            playbackDevice.Received(1).Stop();

            // Assert: the Layer 2 tag was rendered into a speed override and stripped from the text
            var call = Assert.Single(synthesisEngine.GenerateCalls);
            Assert.True(call.Speed > 1.0f);
            Assert.Equal("hello world", call.Text.Trim());
        }
        finally
        {
            TryDeleteDirectory(installedModelDirectory);
        }
    }

    /// <summary>
    ///     Proves the file-backed devices work as a system without audio hardware: speech
    ///     synthesized through a real <see cref="ISynthesisSession"/> is written to a WAV file by
    ///     <see cref="WavFileAudioPlaybackDevice"/>, and the same file then streams back through
    ///     <see cref="WavFileAudioCaptureDevice"/> as captured frames with the written format.
    /// </summary>
    [Fact]
    public async Task Speech_SystemIntegration_FileBackedDevices_SynthesizedSpeechRoundTripsThroughWavFile()
    {
        // Arrange: a scratch model directory, a WAV output path, and a deterministic backend
        var root = Path.Join(Path.GetTempPath(), "DemaConsulting.Speech.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var wavPath = Path.Join(root, "speech.wav");
        try
        {
            var backendFactory = new FakeSynthesisEngineFactory(new FakeSynthesisEngine(sampleRate: 16000));
            var cancellationToken = TestContext.Current.CancellationToken;

            // Act: synthesize into the file-backed playback device, then replay the file
            using (var playbackDevice = new WavFileAudioPlaybackDevice(wavPath, 16000, 1))
            {
                await using var engine = await SpeechSynthesizerFactory.LoadAsync(
                    new FakeSynthesisModel(), root, null, backendFactory, cancellationToken: cancellationToken);
                await using var session = await engine.CreateSessionAsync(playbackDevice, cancellationToken);
                await session.SpeakAsync("hello world", cancellationToken);
            }

            // The capture device is not IDisposable; Start() delivers the whole file synchronously
            // before returning, so every frame is counted before the assertions run. Stop() in the
            // finally releases any file handle before the scratch directory is deleted.
            var captureDevice = new WavFileAudioCaptureDevice(wavPath);
            try
            {
                var capturedSampleCount = 0;
                captureDevice.FrameCaptured += (_, args) => capturedSampleCount += args.Samples.Count;
                captureDevice.Start();

                // Assert: the file holds the synthesized audio in the written format
                Assert.True(File.Exists(wavPath));
                Assert.True(capturedSampleCount > 0);
                Assert.Equal(16000, captureDevice.SampleRate);
                Assert.Equal(1, captureDevice.ChannelCount);
            }
            finally
            {
                captureDevice.Stop();
            }
        }
        finally
        {
            TryDeleteDirectory(root);
        }
    }

    /// <summary>
    ///     Proves managed audio-file decoding works through the public API alone: a FLAC file and
    ///     its WAV twin read to identical clips, the container is detected by content rather
    ///     than extension, and a stereo clip converts to mono at a requested rate.
    /// </summary>
    [Fact]
    public void Speech_SystemIntegration_ManagedAudioFileDecoding_FlacAndWavReadIdenticallyByContent()
    {
        // Arrange: a FLAC fixture, its WAV twin, and a copy of the FLAC under an unrelated extension
        var directory = Path.Combine(AppContext.BaseDirectory, "TestData", "flac");
        var renamedPath = Path.Combine(Path.GetTempPath(), $"speech-{Guid.NewGuid():N}.dat");
        File.Copy(Path.Combine(directory, "stereo-16-48000.flac"), renamedPath);
        try
        {
            // Act
            var flac = AudioFileReader.Read(Path.Combine(directory, "stereo-16-48000.flac"));
            var wav = AudioFileReader.Read(Path.Combine(directory, "stereo-16-48000.wav"));
            var sniffed = AudioFileReader.Read(renamedPath);
            var (monoSamples, monoRate) = AudioFileReader.ReadMono(Path.Combine(directory, "stereo-16-48000.flac"));

            // Assert: identical decode, content-based detection, and mono conversion
            Assert.Equal(wav.Samples, flac.Samples);
            Assert.Equal(wav.SampleRate, flac.SampleRate);
            Assert.Equal(2, flac.ChannelCount);
            Assert.Equal(flac.Samples, sniffed.Samples);
            Assert.Equal(flac.Samples.Length / 2, monoSamples.Length);
            Assert.Equal(flac.SampleRate, monoRate);
        }
        finally
        {
            File.Delete(renamedPath);
        }
    }

    /// <summary>
    ///     Proves the closed audio-tag vocabulary is usable through the public parser alone: a
    ///     known tag becomes a tag span and an unrecognized bracketed phrase passes through as
    ///     plain narration text rather than being dropped or rejected.
    /// </summary>
    [Fact]
    public void Speech_SystemIntegration_AudioTagVocabulary_KnownTagParsedAndUnknownBracketPassesThrough()
    {
        // Act
        var spans = AudioTagParser.Parse("Hello [laughs] world [nonsense].");

        // Assert: exactly one tag span, and the unknown bracket survives in the narration text
        var tagSpan = Assert.Single(spans, span => span.Kind == TaggedTextSpanKind.Tag);
        Assert.Equal(NaturalLanguageAudioTag.Laughs, tagSpan.Tag);
        var narration = string.Concat(spans.Where(span => span.Kind == TaggedTextSpanKind.PlainText).Select(span => span.Text));
        Assert.Contains("[nonsense]", narration);
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
            CancellationToken cancellationToken)
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
