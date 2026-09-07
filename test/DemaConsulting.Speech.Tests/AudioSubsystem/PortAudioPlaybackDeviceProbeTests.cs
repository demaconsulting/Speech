using System.Runtime.InteropServices;
using DemaConsulting.Speech.AudioSubsystem;
using DemaConsulting.Speech.AudioSubsystem.PortAudio;

namespace DemaConsulting.Speech.Tests.AudioSubsystem;

/// <summary>
///     Unit tests for <see cref="PortAudioPlaybackDeviceProbe"/>.
/// </summary>
public class PortAudioPlaybackDeviceProbeTests
{
    /// <summary>
    ///     Proves that enumeration returns only output-capable devices from the preferred host API.
    /// </summary>
    [Fact]
    public void PortAudioPlaybackDeviceProbe_Enumerate_MixedHostApis_ReturnsOnlyPreferredOutputDevices()
    {
        // Arrange: a fake runtime exposing three devices across two host APIs
        var api = new FakePortAudioApi(
        [
            new PortAudioDeviceInfo("Windows Mic", 5, 2, 0, 48000, 0.01, 0.0),
            new PortAudioDeviceInfo("Windows Speaker", 5, 0, 2, 48000, 0.0, 0.01),
            new PortAudioDeviceInfo("Other Host Speaker", 8, 0, 2, 16000, 0.0, 0.01)
        ])
        {
            FindHostApiIndexResult = 5,
            HostApiInfo = new PortAudioHostApiInfo("Windows WASAPI", PortAudioHostApiType.Wasapi, 0, 1)
        };
        var environment = new PortAudioEnvironment(api, OSPlatform.Windows);
        var probe = new PortAudioPlaybackDeviceProbe(environment);

        // Act: enumerate the preferred-host playback devices
        var devices = probe.Enumerate();

        // Assert: only the preferred-host output device is returned
        var device = Assert.Single(devices);
        Assert.Equal("Windows Speaker", device.Name);
        Assert.Equal(AudioDeviceDirection.Playback, device.Direction);
        Assert.Equal(2, device.ChannelCount);
        Assert.Equal(48000, device.SampleRate);
    }

    /// <summary>
    ///     Proves that a missing preferred host API degrades to an empty enumeration.
    /// </summary>
    [Fact]
    public void PortAudioPlaybackDeviceProbe_Enumerate_PreferredHostApiMissing_ReturnsEmptyList()
    {
        // Arrange: a fake runtime that initializes but exposes no preferred host API mapping
        var api = new FakePortAudioApi([]) { FindHostApiIndexResult = null };
        var environment = new PortAudioEnvironment(api, OSPlatform.Windows);
        var probe = new PortAudioPlaybackDeviceProbe(environment);

        // Act: enumerate playback devices
        var devices = probe.Enumerate();

        // Assert: the probe reports no devices rather than throwing
        Assert.Empty(devices);
    }

    /// <summary>
    ///     Proves that a runtime with zero devices degrades to an empty enumeration.
    /// </summary>
    [Fact]
    public void PortAudioPlaybackDeviceProbe_Enumerate_NoDevices_ReturnsEmptyList()
    {
        // Arrange: a fake runtime that resolves the preferred host API but has no devices
        var api = new FakePortAudioApi([])
        {
            FindHostApiIndexResult = 5,
            HostApiInfo = new PortAudioHostApiInfo("Windows WASAPI", PortAudioHostApiType.Wasapi, -1, -1)
        };
        var environment = new PortAudioEnvironment(api, OSPlatform.Windows);
        var probe = new PortAudioPlaybackDeviceProbe(environment);

        // Act: enumerate playback devices
        var devices = probe.Enumerate();

        // Assert: the probe reports no devices rather than throwing
        Assert.Empty(devices);
    }

    /// <summary>
    ///     Minimal fake PortAudio seam used by the probe tests.
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
            throw new NotSupportedException("Capture is outside this test scope.");
        }

        /// <inheritdoc/>
        public IPortAudioStream OpenPlaybackStream(
            int deviceIndex,
            int channelCount,
            int sampleRate,
            uint framesPerBuffer,
            Func<int, IReadOnlyList<float>> provideSamples)
        {
            throw new NotSupportedException("Stream opening is outside this test scope.");
        }
    }
}
