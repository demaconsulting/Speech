using System.Runtime.InteropServices;
using DemaConsulting.Speech.AudioSubsystem;
using DemaConsulting.Speech.AudioSubsystem.PortAudio;
using DemaConsulting.Speech.Demo.DeviceSelectionSubsystem;
using NSubstitute;

namespace DemaConsulting.Speech.Demo.Tests.DeviceSelectionSubsystem;

/// <summary>
///     Subsystem tests for the DeviceSelectionSubsystem, composing a real
///     <see cref="DeviceSelectionViewModel"/> over a real <see cref="AudioDeviceService"/> and
///     <see cref="AudioDeviceFactory"/> with substitute probes.
/// </summary>
public class DeviceSelectionSubsystemTests
{
    /// <summary>
    ///     Proves that the pickers list exactly the devices the library's probes report.
    /// </summary>
    [Fact]
    public void DeviceSelectionSubsystem_Composed_ProbesReportDevices_PickersListThem()
    {
        // Arrange: a factory whose probes report two capture devices and one playback device
        var micA = new AudioDeviceDescription("Mic A", AudioDeviceDirection.Capture, 1, 16000);
        var micB = new AudioDeviceDescription("Mic B", AudioDeviceDirection.Capture, 2, 48000);
        var speaker = new AudioDeviceDescription("Speaker", AudioDeviceDirection.Playback, 2, 48000);
        var captureProbe = Substitute.For<IAudioCaptureDeviceProbe>();
        captureProbe.Enumerate().Returns([micA, micB]);
        var playbackProbe = Substitute.For<IAudioPlaybackDeviceProbe>();
        playbackProbe.Enumerate().Returns([speaker]);
        var service = new AudioDeviceService(new AudioDeviceFactory(captureProbe, playbackProbe, null));

        // Act: compose the panel over the real adapter
        var viewModel = new DeviceSelectionViewModel(service);

        // Assert: the panel lists what the probes reported and preselects the first of each
        Assert.Equal([micA, micB], viewModel.CaptureDevices);
        Assert.Equal([speaker], viewModel.PlaybackDevices);
        Assert.Equal(new AudioDeviceSelection("Mic A"), viewModel.CaptureSelection);
        Assert.Equal(new AudioDeviceSelection("Speaker"), viewModel.PlaybackSelection);
    }

    /// <summary>
    ///     Proves that probes reporting nothing produce empty pickers with an explanation and the
    ///     system-default selections.
    /// </summary>
    [Fact]
    public void DeviceSelectionSubsystem_Composed_ProbesReportNothing_PickersExplainAndDefault()
    {
        // Arrange: a factory whose probes report no devices
        var captureProbe = Substitute.For<IAudioCaptureDeviceProbe>();
        captureProbe.Enumerate().Returns([]);
        var playbackProbe = Substitute.For<IAudioPlaybackDeviceProbe>();
        playbackProbe.Enumerate().Returns([]);
        var service = new AudioDeviceService(new AudioDeviceFactory(captureProbe, playbackProbe, null));

        // Act: compose the panel over the real adapter
        var viewModel = new DeviceSelectionViewModel(service);

        // Assert: both pickers are empty, explained, and fall back to the system default
        Assert.False(viewModel.HasCaptureDevices);
        Assert.False(viewModel.HasPlaybackDevices);
        Assert.Contains("audio backend may be unavailable", viewModel.CaptureStatus, StringComparison.Ordinal);
        Assert.Contains("audio backend may be unavailable", viewModel.PlaybackStatus, StringComparison.Ordinal);
        Assert.Equal(AudioDeviceSelection.SystemDefault, viewModel.CaptureSelection);
        Assert.Equal(AudioDeviceSelection.SystemDefault, viewModel.PlaybackSelection);
    }

    /// <summary>
    ///     Proves that a refresh through the composed panel reaches the library's PortAudio
    ///     environment and keeps the current selection when the device is still reported.
    /// </summary>
    [Fact]
    public async Task DeviceSelectionSubsystem_Composed_Refresh_ReScansLibraryAndKeepsSelection()
    {
        // Arrange: a factory over a deterministic PortAudio environment and explicit probes
        var micA = new AudioDeviceDescription("Mic A", AudioDeviceDirection.Capture, 1, 16000);
        var micB = new AudioDeviceDescription("Mic B", AudioDeviceDirection.Capture, 2, 48000);
        var captureProbe = Substitute.For<IAudioCaptureDeviceProbe>();
        captureProbe.Enumerate().Returns([micA, micB]);
        var api = new FakePortAudioApi();
        var environment = new PortAudioEnvironment(api, OSPlatform.Windows);
        var service = new AudioDeviceService(new AudioDeviceFactory(captureProbe, null, null, environment));
        var viewModel = new DeviceSelectionViewModel(service);
        viewModel.SelectedCaptureDevice = micB;

        // Act: refresh through the panel
        await viewModel.Refresh();

        // Assert: the library re-scanned its device table and the chosen device stayed chosen
        Assert.Equal(1, api.TerminateCallCount);
        Assert.Equal(2, api.InitializeCallCount);
        Assert.Same(micB, viewModel.SelectedCaptureDevice);
    }

    /// <summary>
    ///     Minimal fake PortAudio seam used only to observe the library re-scan.
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
