using System.Runtime.InteropServices;
using DemaConsulting.Speech.AudioSubsystem;
using DemaConsulting.Speech.AudioSubsystem.PortAudio;
using DemaConsulting.Speech.Demo.DeviceSelectionSubsystem;
using NSubstitute;

namespace DemaConsulting.Speech.Demo.Tests.DeviceSelectionSubsystem;

/// <summary>
///     Unit tests for <see cref="AudioDeviceService"/>.
/// </summary>
public class AudioDeviceServiceTests
{
    /// <summary>
    ///     Proves that the adapter rejects a missing factory rather than deferring the failure to
    ///     the first enumeration.
    /// </summary>
    [Fact]
    public void AudioDeviceService_Constructor_NullFactory_ThrowsArgumentNullException()
    {
        // Act & Assert: composing without a factory is a programming error surfaced immediately
        Assert.Throws<ArgumentNullException>(() => new AudioDeviceService(null!));
    }

    /// <summary>
    ///     Proves that capture enumeration returns exactly what the library's capture probe
    ///     reported, without filtering, reordering, or supplementing it.
    /// </summary>
    [Fact]
    public void AudioDeviceService_EnumerateCaptureDevices_ProbeReportsDevices_ReturnsProbeDevices()
    {
        // Arrange: a factory whose injected capture probe reports two devices
        var expected = new List<AudioDeviceDescription>
        {
            new("Mic A", AudioDeviceDirection.Capture, 1, 16000),
            new("Mic B", AudioDeviceDirection.Capture, 2, 48000)
        };
        var captureProbe = Substitute.For<IAudioCaptureDeviceProbe>();
        captureProbe.Enumerate().Returns(expected);
        var service = new AudioDeviceService(new AudioDeviceFactory(captureProbe, null, null));

        // Act: enumerate capture devices through the demo's seam
        var actual = service.EnumerateCaptureDevices();

        // Assert: the library's list is passed through unchanged
        Assert.Equal(expected, actual);
    }

    /// <summary>
    ///     Proves that playback enumeration returns exactly what the library's playback probe
    ///     reported.
    /// </summary>
    [Fact]
    public void AudioDeviceService_EnumeratePlaybackDevices_ProbeReportsDevices_ReturnsProbeDevices()
    {
        // Arrange: a factory whose injected playback probe reports one device
        var expected = new List<AudioDeviceDescription>
        {
            new("Speaker", AudioDeviceDirection.Playback, 2, 48000)
        };
        var playbackProbe = Substitute.For<IAudioPlaybackDeviceProbe>();
        playbackProbe.Enumerate().Returns(expected);
        var service = new AudioDeviceService(new AudioDeviceFactory(null, playbackProbe, null));

        // Act: enumerate playback devices through the demo's seam
        var actual = service.EnumeratePlaybackDevices();

        // Assert: the library's list is passed through unchanged
        Assert.Equal(expected, actual);
    }

    /// <summary>
    ///     Proves that a machine whose audio backend reports nothing produces an empty list rather
    ///     than an exception or invented placeholder devices.
    /// </summary>
    [Fact]
    public void AudioDeviceService_Enumerate_ProbesReportNothing_ReturnsEmptyLists()
    {
        // Arrange: a factory whose injected probes both report no devices at all
        var captureProbe = Substitute.For<IAudioCaptureDeviceProbe>();
        captureProbe.Enumerate().Returns([]);
        var playbackProbe = Substitute.For<IAudioPlaybackDeviceProbe>();
        playbackProbe.Enumerate().Returns([]);
        var service = new AudioDeviceService(new AudioDeviceFactory(captureProbe, playbackProbe, null));

        // Act: enumerate both directions
        var capture = service.EnumerateCaptureDevices();
        var playback = service.EnumeratePlaybackDevices();

        // Assert: both are honestly empty
        Assert.Empty(capture);
        Assert.Empty(playback);
    }

    /// <summary>
    ///     Proves that <see cref="AudioDeviceService.RefreshDevices"/> delegates to
    ///     <see cref="AudioDeviceFactory.RefreshDevices"/>, so the demo's refresh button actually
    ///     forces the backend to re-scan its device table.
    /// </summary>
    [Fact]
    public void AudioDeviceService_RefreshDevices_Always_DelegatesToFactoryRefreshDevices()
    {
        // Arrange: a factory over a deterministic PortAudio environment so the delegation can be
        // observed without touching a real audio backend
        var api = new FakePortAudioApi();
        var environment = new PortAudioEnvironment(api, OSPlatform.Windows);
        var factory = new AudioDeviceFactory(null, null, null, environment);
        var service = new AudioDeviceService(factory);

        // Act: refresh through the demo's seam
        service.RefreshDevices();

        // Assert: the factory's own refresh was performed (proved by the environment's runtime
        // being terminated and reinitialized)
        Assert.Equal(1, api.TerminateCallCount);
        Assert.Equal(2, api.InitializeCallCount);
    }

    /// <summary>
    ///     Minimal fake PortAudio seam used only by the refresh delegation test.
    /// </summary>
    private sealed class FakePortAudioApi : IPortAudioApi
    {
        /// <summary>Gets the number of times <see cref="Initialize"/> has been called.</summary>
        internal int InitializeCallCount { get; private set; }

        /// <summary>Gets the number of times <see cref="Terminate"/> has been called.</summary>
        internal int TerminateCallCount { get; private set; }

        /// <inheritdoc/>
        public int HostApiCount => 0;

        /// <inheritdoc/>
        public int DeviceCount => 0;

        /// <inheritdoc/>
        public void Initialize()
        {
            InitializeCallCount++;
        }

        /// <inheritdoc/>
        public void Terminate()
        {
            TerminateCallCount++;
        }

        /// <inheritdoc/>
        public int? FindHostApiIndex(PortAudioHostApiType hostApiType)
        {
            return null;
        }

        /// <inheritdoc/>
        public PortAudioHostApiInfo GetHostApiInfo(int hostApiIndex)
        {
            throw new NotSupportedException("Host API metadata is outside this test scope.");
        }

        /// <inheritdoc/>
        public PortAudioDeviceInfo GetDeviceInfo(int deviceIndex)
        {
            throw new NotSupportedException("Device enumeration is outside this test scope.");
        }

        /// <inheritdoc/>
        public bool IsCaptureFormatSupported(int deviceIndex, int channelCount, int sampleRate)
        {
            throw new NotSupportedException("Format negotiation is outside this test scope.");
        }

        /// <inheritdoc/>
        public bool IsPlaybackFormatSupported(int deviceIndex, int channelCount, int sampleRate)
        {
            throw new NotSupportedException("Format negotiation is outside this test scope.");
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
