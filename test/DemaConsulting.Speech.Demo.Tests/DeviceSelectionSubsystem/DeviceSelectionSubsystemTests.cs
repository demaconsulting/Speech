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

        // Assert: the panel lists what the probes reported
        Assert.Equal([micA, micB], viewModel.CaptureDevices);
        Assert.Equal([speaker], viewModel.PlaybackDevices);
    }

    /// <summary>
    ///     Proves that the pickers preselect the first device the library's probes report in
    ///     each direction.
    /// </summary>
    [Fact]
    public void DeviceSelectionSubsystem_Composed_ProbesReportDevices_PickersPreselectFirstOfEachDirection()
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

        // Assert: the first device of each direction is the selection handed to the library
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
    ///     environment, which re-scans its device table.
    /// </summary>
    [Fact]
    public async Task DeviceSelectionSubsystem_Composed_Refresh_ReScansLibrary()
    {
        // Arrange: a factory over a deterministic PortAudio environment and explicit probes
        var micA = new AudioDeviceDescription("Mic A", AudioDeviceDirection.Capture, 1, 16000);
        var captureProbe = Substitute.For<IAudioCaptureDeviceProbe>();
        captureProbe.Enumerate().Returns([micA]);
        var speaker = new AudioDeviceDescription("Speaker", AudioDeviceDirection.Playback, 2, 48000);
        var playbackProbe = Substitute.For<IAudioPlaybackDeviceProbe>();
        playbackProbe.Enumerate().Returns([speaker]);
        var api = new FakePortAudioApi();
        var environment = new PortAudioEnvironment(api, OSPlatform.Windows);
        var service = new AudioDeviceService(new AudioDeviceFactory(captureProbe, playbackProbe, null, environment));
        var viewModel = new DeviceSelectionViewModel(service);

        // Act: refresh through the panel
        await viewModel.RefreshAsync();

        // Assert: the library re-scanned its device table and the lists are unchanged
        Assert.Equal(1, api.TerminateCallCount);
        Assert.Equal(2, api.InitializeCallCount);
        Assert.Equal([micA], viewModel.CaptureDevices);
        Assert.Equal([speaker], viewModel.PlaybackDevices);
    }

    /// <summary>
    ///     Proves that a refresh the library cannot complete leaves the composed panel's lists and
    ///     selections untouched.
    /// </summary>
    [Fact]
    public async Task DeviceSelectionSubsystem_Composed_RefreshFails_KeepsListsAndSelections()
    {
        // Arrange: a panel over an environment whose native teardown fails
        var (viewModel, micA, speaker) = ComposeWithFailingRefresh();
        var captureSelectionBefore = viewModel.CaptureSelection;
        var playbackSelectionBefore = viewModel.PlaybackSelection;

        // Act: refresh through the panel
        await viewModel.RefreshAsync();

        // Assert: nothing was re-enumerated or reselected, in either direction
        Assert.Equal([micA], viewModel.CaptureDevices);
        Assert.Equal([speaker], viewModel.PlaybackDevices);
        Assert.Equal(captureSelectionBefore, viewModel.CaptureSelection);
        Assert.Equal(playbackSelectionBefore, viewModel.PlaybackSelection);
    }

    /// <summary>
    ///     Proves that a refresh the library cannot complete is explained in both status texts.
    /// </summary>
    [Fact]
    public async Task DeviceSelectionSubsystem_Composed_RefreshFails_ExplainsInStatus()
    {
        // Arrange: a panel over an environment whose native teardown fails
        var (viewModel, _, _) = ComposeWithFailingRefresh();

        // Act: refresh through the panel
        await viewModel.RefreshAsync();

        // Assert: both statuses carry the failure message
        Assert.Equal("teardown failed", viewModel.CaptureStatus);
        Assert.Equal("teardown failed", viewModel.PlaybackStatus);
    }

    /// <summary>
    ///     Composes a panel over one capture and one playback device whose PortAudio teardown fails.
    /// </summary>
    /// <returns>The panel plus the capture and playback devices it lists.</returns>
    private static (DeviceSelectionViewModel ViewModel, AudioDeviceDescription Mic, AudioDeviceDescription Speaker)
        ComposeWithFailingRefresh()
    {
        var micA = new AudioDeviceDescription("Mic A", AudioDeviceDirection.Capture, 1, 16000);
        var captureProbe = Substitute.For<IAudioCaptureDeviceProbe>();
        captureProbe.Enumerate().Returns([micA]);
        var speaker = new AudioDeviceDescription("Speaker", AudioDeviceDirection.Playback, 2, 48000);
        var playbackProbe = Substitute.For<IAudioPlaybackDeviceProbe>();
        playbackProbe.Enumerate().Returns([speaker]);
        var api = new FakePortAudioApi { TerminateFailure = new InvalidOperationException("teardown failed") };
        var environment = new PortAudioEnvironment(api, OSPlatform.Windows);
        var service = new AudioDeviceService(new AudioDeviceFactory(captureProbe, playbackProbe, null, environment));
        return (new DeviceSelectionViewModel(service), micA, speaker);
    }
    /// <summary>
    ///     Proves that a refresh through the composed panel keeps the chosen device when the
    ///     library still reports it.
    /// </summary>
    [Fact]
    public async Task DeviceSelectionSubsystem_Composed_Refresh_DeviceStillReported_KeepsSelection()
    {
        // Arrange: a factory over a deterministic PortAudio environment and explicit probes
        var micA = new AudioDeviceDescription("Mic A", AudioDeviceDirection.Capture, 1, 16000);
        var micB = new AudioDeviceDescription("Mic B", AudioDeviceDirection.Capture, 2, 48000);
        var captureProbe = Substitute.For<IAudioCaptureDeviceProbe>();
        captureProbe.Enumerate().Returns([micA, micB]);
        var environment = new PortAudioEnvironment(new FakePortAudioApi(), OSPlatform.Windows);
        var service = new AudioDeviceService(new AudioDeviceFactory(captureProbe, null, null, environment));
        var viewModel = new DeviceSelectionViewModel(service);
        viewModel.SelectedCaptureDevice = micB;

        // Act: refresh through the panel
        await viewModel.RefreshAsync();

        // Assert: the chosen device stayed chosen
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

        /// <summary>Gets or sets an exception that <see cref="Terminate"/> throws, when set.</summary>
        internal Exception? TerminateFailure { get; set; }

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
            if (TerminateFailure is not null)
            {
                throw TerminateFailure;
            }
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
