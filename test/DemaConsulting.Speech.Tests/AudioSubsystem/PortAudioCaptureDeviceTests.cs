using System.Runtime.InteropServices;
using DemaConsulting.Speech.AudioSubsystem;
using DemaConsulting.Speech.AudioSubsystem.PortAudio;
using DemaConsulting.Speech.Diagnostics;
using NSubstitute;

namespace DemaConsulting.Speech.Tests.AudioSubsystem;

/// <summary>
///     Unit tests for <see cref="PortAudioCaptureDevice"/>.
/// </summary>
public class PortAudioCaptureDeviceTests
{
    /// <summary>
    ///     Proves that a stale named selection falls back to the host-API-scoped default device.
    /// </summary>
    [Fact]
    public void PortAudioCaptureDevice_Constructor_StaleSelection_FallsBackToDefaultDevice()
    {
        // Arrange: a fake runtime whose selected name is absent but whose host-API default exists
        var api = new FakePortAudioApi(
        [
            new PortAudioDeviceInfo("Default Mic", 5, 1, 0, 16000, 0.01, 0.0),
            new PortAudioDeviceInfo("Backup Mic", 5, 2, 0, 48000, 0.01, 0.0)
        ])
        {
            FindHostApiIndexResult = 5,
            HostApiInfo = new PortAudioHostApiInfo("Windows WASAPI", PortAudioHostApiType.Wasapi, 0, -1)
        };
        var diagnostics = Substitute.For<ISpeechDiagnostics>();
        var environment = new PortAudioEnvironment(api, OSPlatform.Windows);

        // Act: construct with a stale persisted selection
        var device = new PortAudioCaptureDevice(environment, new AudioDeviceSelection("Missing Mic"), diagnostics);

        // Assert: the real device remains available by falling back to the host-API default
        Assert.True(device.IsAvailable);
        diagnostics.Received().Report(
            SpeechDiagnosticLevel.Info,
            "AudioSubsystem",
            "Resolved PortAudio capture device 'Default Mic' via host API default.");
    }

    /// <summary>
    ///     Proves that a running device raises <see cref="IAudioCaptureDevice.FrameCaptured"/>
    ///     when the PortAudio seam delivers samples.
    /// </summary>
    [Fact]
    public void PortAudioCaptureDevice_Start_StreamCapturesSamples_RaisesFrameCaptured()
    {
        // Arrange: a fake runtime with one default microphone and a capture-stream fake
        var stream = new FakePortAudioStream();
        var api = new FakePortAudioApi([new PortAudioDeviceInfo("Mic", 5, 2, 0, 48000, 0.01, 0.0)])
        {
            FindHostApiIndexResult = 5,
            HostApiInfo = new PortAudioHostApiInfo("Windows WASAPI", PortAudioHostApiType.Wasapi, 0, -1),
            CaptureStream = stream
        };
        var environment = new PortAudioEnvironment(api, OSPlatform.Windows);
        var device = new PortAudioCaptureDevice(environment);
        IReadOnlyList<float>? receivedSamples = null;
        device.FrameCaptured += (_, args) => receivedSamples = args.Samples;

        // Act: start capture, simulate one callback block, then stop
        device.Start();
        stream.RaiseCapturedSamples([0.1f, -0.2f, 0.3f, -0.4f]);
        device.Stop();

        // Assert: the frame is surfaced through the managed event and the stream lifecycle is honored
        Assert.Equal([0.1f, -0.2f, 0.3f, -0.4f], receivedSamples);
        Assert.Equal(1, stream.StartCallCount);
        Assert.Equal(1, stream.StopCallCount);
        Assert.Equal(1, stream.DisposeCallCount);
    }

    /// <summary>
    ///     Proves that a seam open failure is surfaced as <see cref="AudioDeviceUnavailableException"/>.
    /// </summary>
    [Fact]
    public void PortAudioCaptureDevice_Start_OpenFails_ThrowsAudioDeviceUnavailableException()
    {
        // Arrange: a fake runtime whose capture-stream open attempt fails for the resolved default device
        var api = new FakePortAudioApi([new PortAudioDeviceInfo("Mic", 5, 1, 0, 16000, 0.01, 0.0)])
        {
            FindHostApiIndexResult = 5,
            HostApiInfo = new PortAudioHostApiInfo("Windows WASAPI", PortAudioHostApiType.Wasapi, 0, -1),
            OpenCaptureException = new InvalidOperationException("Native stream open failed.")
        };
        var diagnostics = Substitute.For<ISpeechDiagnostics>();
        var environment = new PortAudioEnvironment(api, OSPlatform.Windows);
        var device = new PortAudioCaptureDevice(environment, diagnostics: diagnostics);

        // Act / Assert: the device degrades a native open failure into the documented exception
        var exception = Assert.Throws<AudioDeviceUnavailableException>(device.Start);
        Assert.Equal("Failed to start capture on 'Mic'.", exception.Message);
        Assert.IsType<InvalidOperationException>(exception.InnerException);
        diagnostics.Received().Report(
            SpeechDiagnosticLevel.Error,
            "AudioSubsystem",
            "Failed to start PortAudio capture on 'Mic': Native stream open failed.");
    }

    /// <summary>
    ///     Proves that a runtime with no resolvable capture device honestly reports itself as unavailable.
    /// </summary>
    [Fact]
    public void PortAudioCaptureDevice_IsAvailable_NoResolvableDevice_ReturnsFalse()
    {
        // Arrange: a fake runtime whose preferred host API exists but has no capture-capable devices
        var api = new FakePortAudioApi([new PortAudioDeviceInfo("Speaker", 5, 0, 2, 48000, 0.0, 0.01)])
        {
            FindHostApiIndexResult = 5,
            HostApiInfo = new PortAudioHostApiInfo("Windows WASAPI", PortAudioHostApiType.Wasapi, -1, 0)
        };
        var environment = new PortAudioEnvironment(api, OSPlatform.Windows);

        // Act: construct the device against the empty capture catalog
        var device = new PortAudioCaptureDevice(environment);

        // Assert: the device is honestly unavailable and start throws the documented exception
        Assert.False(device.IsAvailable);
        Assert.Throws<AudioDeviceUnavailableException>(device.Start);
    }

    /// <summary>
    ///     Proves that a resolved capture device reports the resolved device's own channel count
    ///     and sample rate, so a consumer can downmix and resample its frames correctly.
    /// </summary>
    [Fact]
    public void PortAudioCaptureDevice_CaptureFormat_ResolvedDevice_ReflectsResolvedDeviceFormat()
    {
        // Arrange: a fake runtime whose host-API default microphone is stereo at 48 kHz
        var api = new FakePortAudioApi([new PortAudioDeviceInfo("Mic", 5, 2, 0, 48000, 0.01, 0.0)])
        {
            FindHostApiIndexResult = 5,
            HostApiInfo = new PortAudioHostApiInfo("Windows WASAPI", PortAudioHostApiType.Wasapi, 0, -1)
        };
        var environment = new PortAudioEnvironment(api, OSPlatform.Windows);

        // Act: construct the device against the resolvable capture catalog
        var device = new PortAudioCaptureDevice(environment);

        // Assert: the reported format matches the resolved device
        Assert.True(device.IsAvailable);
        Assert.Equal(2, device.ChannelCount);
        Assert.Equal(48000, device.SampleRate);
    }

    /// <summary>
    ///     Proves that an unresolved capture device reports a zero capture format, matching its
    ///     false availability flag instead of advertising a format it cannot deliver.
    /// </summary>
    [Fact]
    public void PortAudioCaptureDevice_CaptureFormat_NoResolvableDevice_ReturnsZeroRateAndChannelCount()
    {
        // Arrange: a fake runtime whose preferred host API has no capture-capable devices
        var api = new FakePortAudioApi([new PortAudioDeviceInfo("Speaker", 5, 0, 2, 48000, 0.0, 0.01)])
        {
            FindHostApiIndexResult = 5,
            HostApiInfo = new PortAudioHostApiInfo("Windows WASAPI", PortAudioHostApiType.Wasapi, -1, 0)
        };
        var environment = new PortAudioEnvironment(api, OSPlatform.Windows);

        // Act: construct the device against the empty capture catalog
        var device = new PortAudioCaptureDevice(environment);

        // Assert: both format values are zero
        Assert.False(device.IsAvailable);
        Assert.Equal(0, device.ChannelCount);
        Assert.Equal(0, device.SampleRate);
    }

    /// <summary>
    ///     Minimal fake PortAudio seam used by the capture-device tests.
    /// </summary>
    private sealed class FakePortAudioApi(IReadOnlyList<PortAudioDeviceInfo> devices) : IPortAudioApi
    {
        /// <summary>
        ///     Gets or sets the host-API index returned by <see cref="FindHostApiIndex"/>.
        /// </summary>
        internal int? FindHostApiIndexResult { get; init; }

        /// <summary>
        ///     Gets or sets the host-API info returned by <see cref="GetHostApiInfo"/>.
        /// </summary>
        internal PortAudioHostApiInfo HostApiInfo { get; init; } =
            new("Windows WASAPI", PortAudioHostApiType.Wasapi, -1, -1);

        /// <summary>
        ///     Gets or sets the capture stream instance returned by <see cref="OpenCaptureStream"/>.
        /// </summary>
        internal FakePortAudioStream CaptureStream { get; init; } = new();

        /// <summary>
        ///     Gets or sets the exception thrown by <see cref="OpenCaptureStream"/>.
        /// </summary>
        internal Exception? OpenCaptureException { get; init; }

        /// <inheritdoc/>
        public int HostApiCount => 1;

        /// <inheritdoc/>
        public int DeviceCount => devices.Count;

        /// <inheritdoc/>
        public void Initialize()
        {
        }

        /// <inheritdoc/>
        public int? FindHostApiIndex(PortAudioHostApiType hostApiType)
        {
            return FindHostApiIndexResult;
        }

        /// <inheritdoc/>
        public PortAudioHostApiInfo GetHostApiInfo(int hostApiIndex)
        {
            return HostApiInfo;
        }

        /// <inheritdoc/>
        public PortAudioDeviceInfo GetDeviceInfo(int deviceIndex)
        {
            return devices[deviceIndex];
        }

        /// <inheritdoc/>
        public IPortAudioStream OpenCaptureStream(
            int deviceIndex,
            int channelCount,
            int sampleRate,
            uint framesPerBuffer,
            Action<IReadOnlyList<float>> onSamplesCaptured)
        {
            if (OpenCaptureException is not null)
            {
                throw OpenCaptureException;
            }

            CaptureStream.OnSamplesCaptured = onSamplesCaptured;
            return CaptureStream;
        }

        /// <inheritdoc/>
        public IPortAudioStream OpenPlaybackStream(
            int deviceIndex,
            int channelCount,
            int sampleRate,
            uint framesPerBuffer,
            Func<int, IReadOnlyList<float>> provideSamples)
        {
            throw new NotSupportedException("Playback is outside this test scope.");
        }
    }

    /// <summary>
    ///     Minimal fake PortAudio stream used by the capture-device tests.
    /// </summary>
    private sealed class FakePortAudioStream : IPortAudioStream
    {
        /// <summary>
        ///     Gets or sets the capture callback to invoke when tests simulate input.
        /// </summary>
        internal Action<IReadOnlyList<float>>? OnSamplesCaptured { get; set; }

        /// <summary>
        ///     Gets the number of times <see cref="Start"/> has been called.
        /// </summary>
        internal int StartCallCount { get; private set; }

        /// <summary>
        ///     Gets the number of times <see cref="Stop"/> has been called.
        /// </summary>
        internal int StopCallCount { get; private set; }

        /// <summary>
        ///     Gets the number of times <see cref="Dispose"/> has been called.
        /// </summary>
        internal int DisposeCallCount { get; private set; }

        /// <inheritdoc/>
        public void Start()
        {
            StartCallCount++;
        }

        /// <inheritdoc/>
        public void Stop()
        {
            StopCallCount++;
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            DisposeCallCount++;
        }

        /// <summary>
        ///     Simulates one PortAudio capture callback delivering the provided samples.
        /// </summary>
        /// <param name="samples">The samples to surface through the managed event.</param>
        internal void RaiseCapturedSamples(IReadOnlyList<float> samples)
        {
            OnSamplesCaptured?.Invoke(samples);
        }
    }
}
