using System.Runtime.InteropServices;
using DemaConsulting.Speech.AudioSubsystem;
using DemaConsulting.Speech.AudioSubsystem.PortAudio;
using DemaConsulting.Speech.Diagnostics;
using NSubstitute;

namespace DemaConsulting.Speech.Tests.AudioSubsystem;

/// <summary>
///     Unit tests for the <see cref="AudioDeviceFactory"/> class.
/// </summary>
public class AudioDeviceFactoryTests
{
    /// <summary>
    ///     Proves that constructing a factory with a deterministic PortAudio environment never throws.
    /// </summary>
    [Fact]
    public void AudioDeviceFactory_Constructor_CustomEnvironment_DoesNotThrow()
    {
        // Arrange: a fake PortAudio environment that initializes successfully
        var environment = new PortAudioEnvironment(new FakePortAudioApi([]), OSPlatform.Windows);

        // Act: construct with the deterministic environment
        var exception = Record.Exception(() => new AudioDeviceFactory(null, null, null, environment));

        // Assert: construction never throws
        Assert.Null(exception);
    }

    /// <summary>
    ///     Proves that a successful PortAudio initialization exposes the real PortAudio-backed probes.
    /// </summary>
    [Fact]
    public void AudioDeviceFactory_Constructor_PortAudioInitialized_ExposesRealProbes()
    {
        // Arrange: a fake PortAudio environment that initializes successfully
        var environment = new PortAudioEnvironment(new FakePortAudioApi([]), OSPlatform.Windows);

        // Act: construct with default probes
        var factory = new AudioDeviceFactory(null, null, null, environment);

        // Assert: the default probes are the real PortAudio-backed implementations
        Assert.IsType<PortAudioCaptureDeviceProbe>(factory.CaptureProbe);
        Assert.IsType<PortAudioPlaybackDeviceProbe>(factory.PlaybackProbe);
    }

    /// <summary>
    ///     Proves that an initialization failure degrades the default probes to the shared unavailable fallbacks.
    /// </summary>
    [Fact]
    public void AudioDeviceFactory_Constructor_PortAudioInitializationFails_ExposesUnavailableProbes()
    {
        // Arrange: a fake PortAudio environment whose initialization fails
        var diagnostics = Substitute.For<ISpeechDiagnostics>();
        var environment = new PortAudioEnvironment(
            new FakePortAudioApi([], new InvalidOperationException("PortAudio init failed.")),
            OSPlatform.Windows);

        // Act: construct with default probes
        var factory = new AudioDeviceFactory(null, null, diagnostics, environment);

        // Assert: both probes fall back to the shared unavailable instances and diagnostics report the failure
        Assert.Same(UnavailableAudioCaptureDeviceProbe.Instance, factory.CaptureProbe);
        Assert.Same(UnavailableAudioPlaybackDeviceProbe.Instance, factory.PlaybackProbe);
        diagnostics.Received().Report(
            SpeechDiagnosticLevel.Warning,
            "AudioSubsystem",
            "PortAudio initialization failed; using unavailable audio fallbacks. PortAudio init failed.");
    }

    /// <summary>
    ///     Proves that injected probes still override the PortAudio-backed defaults.
    /// </summary>
    [Fact]
    public void AudioDeviceFactory_Constructor_InjectedProbes_ExposesInjectedProbes()
    {
        // Arrange: injected probes and a successful PortAudio environment
        var captureProbe = Substitute.For<IAudioCaptureDeviceProbe>();
        var playbackProbe = Substitute.For<IAudioPlaybackDeviceProbe>();
        var environment = new PortAudioEnvironment(new FakePortAudioApi([]), OSPlatform.Windows);

        // Act: construct the factory with the mocks injected
        var factory = new AudioDeviceFactory(captureProbe, playbackProbe, null, environment);

        // Assert: the factory exposes exactly the injected mock instances
        Assert.Same(captureProbe, factory.CaptureProbe);
        Assert.Same(playbackProbe, factory.PlaybackProbe);
    }

    /// <summary>
    ///     Proves that a successful PortAudio initialization returns a real capture-device implementation.
    /// </summary>
    [Fact]
    public void AudioDeviceFactory_CreateCaptureDevice_PortAudioInitialized_ReturnsRealDevice()
    {
        // Arrange: a fake PortAudio environment with one default capture device
        var environment = new PortAudioEnvironment(
            new FakePortAudioApi(
            [
                new PortAudioDeviceInfo("Mic", 5, 1, 0, 16000, 0.01, 0.0)
            ])
            {
                FindHostApiIndexResult = 5,
                HostApiInfo = new PortAudioHostApiInfo("Windows WASAPI", PortAudioHostApiType.Wasapi, 0, -1)
            },
            OSPlatform.Windows);
        var factory = new AudioDeviceFactory(null, null, null, environment);

        // Act: request a capture device
        var device = factory.CreateCaptureDevice();

        // Assert: a real PortAudio-backed device is returned and is available
        var typedDevice = Assert.IsType<PortAudioCaptureDevice>(device);
        Assert.True(typedDevice.IsAvailable);
    }

    /// <summary>
    ///     Proves that a preferred capture format is forwarded to the constructed device.
    /// </summary>
    [Fact]
    public void AudioDeviceFactory_CreateCaptureDevice_PreferredFormatSupplied_ForwardsPreferredFormat()
    {
        // Arrange: a fake PortAudio environment with a higher-capability default capture device
        var environment = new PortAudioEnvironment(
            new FakePortAudioApi(
            [
                new PortAudioDeviceInfo("Mic", 5, 4, 0, 48000, 0.01, 0.0)
            ])
            {
                FindHostApiIndexResult = 5,
                HostApiInfo = new PortAudioHostApiInfo("Windows WASAPI", PortAudioHostApiType.Wasapi, 0, -1)
            },
            OSPlatform.Windows);
        var factory = new AudioDeviceFactory(null, null, null, environment);
        var preferredFormat = new AudioFormat(16000, 2);

        // Act
        var device = factory.CreateCaptureDevice(preferredFormat: preferredFormat);

        // Assert
        Assert.Equal(16000, device.SampleRate);
        Assert.Equal(2, device.ChannelCount);
    }

    /// <summary>
    ///     Proves that a successful PortAudio initialization returns a real playback-device implementation.
    /// </summary>
    [Fact]
    public void AudioDeviceFactory_CreatePlaybackDevice_PortAudioInitialized_ReturnsRealDevice()
    {
        // Arrange: a fake PortAudio environment with one default playback device
        var environment = new PortAudioEnvironment(
            new FakePortAudioApi(
            [
                new PortAudioDeviceInfo("Speaker", 5, 0, 2, 48000, 0.0, 0.01)
            ])
            {
                FindHostApiIndexResult = 5,
                HostApiInfo = new PortAudioHostApiInfo("Windows WASAPI", PortAudioHostApiType.Wasapi, -1, 0)
            },
            OSPlatform.Windows);
        var factory = new AudioDeviceFactory(null, null, null, environment);

        // Act: request a playback device
        var device = factory.CreatePlaybackDevice();

        // Assert: a real PortAudio-backed device is returned and is available
        var typedDevice = Assert.IsType<PortAudioPlaybackDevice>(device);
        Assert.True(typedDevice.IsAvailable);
    }

    /// <summary>
    ///     Proves that a preferred playback format is forwarded to the constructed device.
    /// </summary>
    [Fact]
    public void AudioDeviceFactory_CreatePlaybackDevice_PreferredFormatSupplied_ForwardsPreferredFormat()
    {
        // Arrange: a fake PortAudio environment with a higher-capability default playback device
        var environment = new PortAudioEnvironment(
            new FakePortAudioApi(
            [
                new PortAudioDeviceInfo("Speaker", 5, 0, 4, 48000, 0.0, 0.01)
            ])
            {
                FindHostApiIndexResult = 5,
                HostApiInfo = new PortAudioHostApiInfo("Windows WASAPI", PortAudioHostApiType.Wasapi, -1, 0)
            },
            OSPlatform.Windows);
        var factory = new AudioDeviceFactory(null, null, null, environment);
        var preferredFormat = new AudioFormat(24000, 2);

        // Act
        var device = factory.CreatePlaybackDevice(preferredFormat: preferredFormat);

        // Assert
        Assert.Equal(24000, device.SampleRate);
        Assert.Equal(2, device.ChannelCount);
    }

    /// <summary>
    ///     Proves that omitting a preferred format preserves the device-native defaults.
    /// </summary>
    [Fact]
    public void AudioDeviceFactory_CreateDevices_PreferredFormatOmitted_PreservesDefaultFormatBehavior()
    {
        // Arrange: fake default devices with their own native format values
        var environment = new PortAudioEnvironment(
            new FakePortAudioApi(
            [
                new PortAudioDeviceInfo("Mic", 5, 3, 0, 44100, 0.01, 0.0),
                new PortAudioDeviceInfo("Speaker", 5, 0, 2, 48000, 0.0, 0.01),
            ])
            {
                FindHostApiIndexResult = 5,
                HostApiInfo = new PortAudioHostApiInfo("Windows WASAPI", PortAudioHostApiType.Wasapi, 0, 1)
            },
            OSPlatform.Windows);
        var factory = new AudioDeviceFactory(null, null, null, environment);

        // Act
        var captureDevice = factory.CreateCaptureDevice();
        var playbackDevice = factory.CreatePlaybackDevice();

        // Assert
        Assert.Equal(44100, captureDevice.SampleRate);
        Assert.Equal(3, captureDevice.ChannelCount);
        Assert.Equal(48000, playbackDevice.SampleRate);
        Assert.Equal(2, playbackDevice.ChannelCount);
    }

    /// <summary>
    ///     Proves that an injected capture probe reporting zero known devices is genuinely
    ///     consulted: <see cref="AudioDeviceFactory.CreateCaptureDevice"/> returns the honest
    ///     unavailable fallback even though the real (fake) PortAudio environment has a device
    ///     that an unfiltered resolution would otherwise find.
    /// </summary>
    [Fact]
    public void AudioDeviceFactory_CreateCaptureDevice_InjectedProbeReportsNoDevices_ReturnsUnavailableDevice()
    {
        // Arrange: a fake PortAudio environment with one real capture device, but an injected
        // probe that (unlike the real environment) reports no known capture devices.
        var environment = new PortAudioEnvironment(
            new FakePortAudioApi(
            [
                new PortAudioDeviceInfo("Mic", 5, 1, 0, 16000, 0.01, 0.0)
            ])
            {
                FindHostApiIndexResult = 5,
                HostApiInfo = new PortAudioHostApiInfo("Windows WASAPI", PortAudioHostApiType.Wasapi, 0, -1)
            },
            OSPlatform.Windows);
        var captureProbe = Substitute.For<IAudioCaptureDeviceProbe>();
        captureProbe.Enumerate().Returns([]);
        var factory = new AudioDeviceFactory(captureProbe, null, null, environment);

        // Act: request the default capture device
        var device = factory.CreateCaptureDevice();

        // Assert: the injected probe's empty enumeration is honored, not the real environment scan
        Assert.Same(UnavailableAudioCaptureDevice.Instance, device);
    }

    /// <summary>
    ///     Proves that an injected capture probe reporting a different device name than requested
    ///     causes <see cref="AudioDeviceFactory.CreateCaptureDevice"/> to return the honest
    ///     unavailable fallback, rather than silently falling back to a real device resolved
    ///     independently of the probe.
    /// </summary>
    [Fact]
    public void AudioDeviceFactory_CreateCaptureDevice_InjectedProbeDoesNotKnowRequestedDevice_ReturnsUnavailableDevice()
    {
        // Arrange: a fake PortAudio environment with a real "Mic" device, but an injected probe
        // that only knows about an unrelated device name.
        var environment = new PortAudioEnvironment(
            new FakePortAudioApi(
            [
                new PortAudioDeviceInfo("Mic", 5, 1, 0, 16000, 0.01, 0.0)
            ])
            {
                FindHostApiIndexResult = 5,
                HostApiInfo = new PortAudioHostApiInfo("Windows WASAPI", PortAudioHostApiType.Wasapi, 0, -1)
            },
            OSPlatform.Windows);
        var captureProbe = Substitute.For<IAudioCaptureDeviceProbe>();
        captureProbe.Enumerate().Returns(
        [
            new AudioDeviceDescription("Some Other Mic", AudioDeviceDirection.Capture, 1, 16000)
        ]);
        var factory = new AudioDeviceFactory(captureProbe, null, null, environment);

        // Act: request a device the injected probe does not know about, but the real
        // environment would otherwise be able to resolve by name.
        var device = factory.CreateCaptureDevice(new AudioDeviceSelection("Mic"));

        // Assert: unavailable, because the injected probe never reported "Mic"
        Assert.Same(UnavailableAudioCaptureDevice.Instance, device);
    }

    /// <summary>
    ///     Proves that an injected playback probe reporting zero known devices is genuinely
    ///     consulted: <see cref="AudioDeviceFactory.CreatePlaybackDevice"/> returns the honest
    ///     unavailable fallback even though the real (fake) PortAudio environment has a device
    ///     that an unfiltered resolution would otherwise find.
    /// </summary>
    [Fact]
    public void AudioDeviceFactory_CreatePlaybackDevice_InjectedProbeReportsNoDevices_ReturnsUnavailableDevice()
    {
        // Arrange: a fake PortAudio environment with one real playback device, but an injected
        // probe that reports no known playback devices.
        var environment = new PortAudioEnvironment(
            new FakePortAudioApi(
            [
                new PortAudioDeviceInfo("Speaker", 5, 0, 2, 48000, 0.0, 0.01)
            ])
            {
                FindHostApiIndexResult = 5,
                HostApiInfo = new PortAudioHostApiInfo("Windows WASAPI", PortAudioHostApiType.Wasapi, -1, 0)
            },
            OSPlatform.Windows);
        var playbackProbe = Substitute.For<IAudioPlaybackDeviceProbe>();
        playbackProbe.Enumerate().Returns([]);
        var factory = new AudioDeviceFactory(null, playbackProbe, null, environment);

        // Act: request the default playback device
        var device = factory.CreatePlaybackDevice();

        // Assert: the injected probe's empty enumeration is honored, not the real environment scan
        Assert.Same(UnavailableAudioPlaybackDevice.Instance, device);
    }

    /// <summary>
    ///     Proves that an injected playback probe reporting the requested device name allows
    ///     <see cref="AudioDeviceFactory.CreatePlaybackDevice"/> to proceed to real resolution,
    ///     confirming the fix does not regress the common case where the injected probe agrees
    ///     with the real environment.
    /// </summary>
    [Fact]
    public void AudioDeviceFactory_CreatePlaybackDevice_InjectedProbeKnowsRequestedDevice_ReturnsRealDevice()
    {
        // Arrange
        var environment = new PortAudioEnvironment(
            new FakePortAudioApi(
            [
                new PortAudioDeviceInfo("Speaker", 5, 0, 2, 48000, 0.0, 0.01)
            ])
            {
                FindHostApiIndexResult = 5,
                HostApiInfo = new PortAudioHostApiInfo("Windows WASAPI", PortAudioHostApiType.Wasapi, -1, 0)
            },
            OSPlatform.Windows);
        var playbackProbe = Substitute.For<IAudioPlaybackDeviceProbe>();
        playbackProbe.Enumerate().Returns(
        [
            new AudioDeviceDescription("Speaker", AudioDeviceDirection.Playback, 2, 48000)
        ]);
        var factory = new AudioDeviceFactory(null, playbackProbe, null, environment);

        // Act
        var device = factory.CreatePlaybackDevice(new AudioDeviceSelection("Speaker"));

        // Assert
        var typedDevice = Assert.IsType<PortAudioPlaybackDevice>(device);
        Assert.True(typedDevice.IsAvailable);
    }

    /// <summary>
    ///     Proves that a selection with an empty-string device name is treated the same as a
    ///     <see langword="null"/> selection - falling back to "any known device is acceptable" -
    ///     matching <see cref="AudioDeviceSelection.Resolve"/>, which can never exactly match an
    ///     empty name and so always falls back to the system default for it. Without this fix, an
    ///     empty-string selection would spuriously require an (impossible) exact match and return
    ///     the unavailable fallback even though the probe reports real devices.
    /// </summary>
    [Fact]
    public void AudioDeviceFactory_CreateCaptureDevice_SelectionHasEmptyDeviceName_ReturnsRealDevice()
    {
        // Arrange: a fake PortAudio environment with one real capture device, and a probe
        // reporting that same device, matching what the real environment would enumerate.
        var environment = new PortAudioEnvironment(
            new FakePortAudioApi(
            [
                new PortAudioDeviceInfo("Mic", 5, 1, 0, 16000, 0.01, 0.0)
            ])
            {
                FindHostApiIndexResult = 5,
                HostApiInfo = new PortAudioHostApiInfo("Windows WASAPI", PortAudioHostApiType.Wasapi, 0, -1)
            },
            OSPlatform.Windows);
        var captureProbe = Substitute.For<IAudioCaptureDeviceProbe>();
        captureProbe.Enumerate().Returns(
        [
            new AudioDeviceDescription("Mic", AudioDeviceDirection.Capture, 1, 16000)
        ]);
        var factory = new AudioDeviceFactory(captureProbe, null, null, environment);

        // Act: request a device using a selection with an empty-string device name
        var device = factory.CreateCaptureDevice(new AudioDeviceSelection(string.Empty));

        // Assert: resolved as a real device, exactly as a null selection would be, rather than
        // spuriously falling back to unavailable
        var typedDevice = Assert.IsType<PortAudioCaptureDevice>(device);
        Assert.True(typedDevice.IsAvailable);
    }

    /// <summary>
    ///     Proves that a PortAudio initialization failure returns the honest unavailable fallback device.
    /// </summary>
    [Fact]
    public void AudioDeviceFactory_CreateDevices_PortAudioInitializationFails_ReturnUnavailableDevices()
    {
        // Arrange: a fake PortAudio environment whose initialization fails
        var environment = new PortAudioEnvironment(
            new FakePortAudioApi([], new InvalidOperationException("PortAudio init failed.")),
            OSPlatform.Windows);
        var factory = new AudioDeviceFactory(null, null, null, environment);

        // Act: request both device kinds
        var captureDevice = factory.CreateCaptureDevice();
        var playbackDevice = factory.CreatePlaybackDevice();

        // Assert: both devices degrade to the shared unavailable implementations
        Assert.Same(UnavailableAudioCaptureDevice.Instance, captureDevice);
        Assert.Same(UnavailableAudioPlaybackDevice.Instance, playbackDevice);
    }

    /// <summary>
    ///     Minimal fake PortAudio seam used by the factory tests.
    /// </summary>
    private sealed class FakePortAudioApi(
        IReadOnlyList<PortAudioDeviceInfo> devices,
        Exception? initializeException = null) : IPortAudioApi
    {
        /// <summary>
        ///     Gets or sets the host-API index returned by <see cref="FindHostApiIndex"/>.
        /// </summary>
        internal int? FindHostApiIndexResult { get; init; } = 5;

        /// <summary>
        ///     Gets or sets the host-API info returned by <see cref="GetHostApiInfo"/>.
        /// </summary>
        internal PortAudioHostApiInfo HostApiInfo { get; init; } =
            new("Windows WASAPI", PortAudioHostApiType.Wasapi, -1, -1);

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
