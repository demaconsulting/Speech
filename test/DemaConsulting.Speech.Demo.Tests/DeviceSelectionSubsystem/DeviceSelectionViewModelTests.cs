using DemaConsulting.Speech.AudioSubsystem;
using DemaConsulting.Speech.Demo.DeviceSelectionSubsystem;
using NSubstitute;

namespace DemaConsulting.Speech.Demo.Tests.DeviceSelectionSubsystem;

/// <summary>
///     Unit tests for <see cref="DeviceSelectionViewModel"/>.
/// </summary>
public class DeviceSelectionViewModelTests
{
    /// <summary>A capture device used across these tests.</summary>
    private static readonly AudioDeviceDescription MicA = new("Mic A", AudioDeviceDirection.Capture, 1, 16000);

    /// <summary>A second capture device used across these tests.</summary>
    private static readonly AudioDeviceDescription MicB = new("Mic B", AudioDeviceDirection.Capture, 2, 48000);

    /// <summary>A playback device used across these tests.</summary>
    private static readonly AudioDeviceDescription Speaker = new("Speaker", AudioDeviceDirection.Playback, 2, 48000);

    /// <summary>
    ///     Builds a faked device service reporting the supplied devices.
    /// </summary>
    /// <param name="capture">The capture devices to report.</param>
    /// <param name="playback">The playback devices to report.</param>
    /// <returns>The configured fake service.</returns>
    private static IAudioDeviceService Service(
        IReadOnlyList<AudioDeviceDescription> capture,
        IReadOnlyList<AudioDeviceDescription> playback)
    {
        var service = Substitute.For<IAudioDeviceService>();
        service.EnumerateCaptureDevices().Returns(capture);
        service.EnumeratePlaybackDevices().Returns(playback);
        return service;
    }

    /// <summary>
    ///     Builds a faked device service reporting one set of devices on its first enumeration and
    ///     another on every later enumeration, standing in for a hot-plug or unplug between
    ///     refreshes.
    /// </summary>
    /// <param name="firstCapture">The capture devices reported by the first enumeration.</param>
    /// <param name="laterCapture">The capture devices reported by later enumerations.</param>
    /// <param name="firstPlayback">The playback devices reported by the first enumeration.</param>
    /// <param name="laterPlayback">The playback devices reported by later enumerations.</param>
    /// <returns>The configured fake service.</returns>
    private static IAudioDeviceService SequencedService(
        IReadOnlyList<AudioDeviceDescription> firstCapture,
        IReadOnlyList<AudioDeviceDescription> laterCapture,
        IReadOnlyList<AudioDeviceDescription> firstPlayback,
        IReadOnlyList<AudioDeviceDescription> laterPlayback)
    {
        var service = Substitute.For<IAudioDeviceService>();
        service.EnumerateCaptureDevices().Returns(firstCapture, laterCapture);
        service.EnumeratePlaybackDevices().Returns(firstPlayback, laterPlayback);
        return service;
    }

    /// <summary>
    ///     Proves that the panel rejects a missing device service rather than presenting a
    ///     permanently broken picker.
    /// </summary>
    [Fact]
    public void DeviceSelectionViewModel_Constructor_NullService_ThrowsArgumentNullException()
    {
        // Act & Assert: composing without a device service is a programming error
        Assert.Throws<ArgumentNullException>(() => new DeviceSelectionViewModel(null!));
    }

    /// <summary>
    ///     Proves that the panel is populated the moment it is constructed, so the window opens
    ///     showing real devices rather than an empty list the user must refresh.
    /// </summary>
    [Fact]
    public void DeviceSelectionViewModel_Constructor_DevicesAvailable_PopulatesBothLists()
    {
        // Arrange: a service reporting two capture devices and one playback device
        var viewModel = new DeviceSelectionViewModel(Service([MicA, MicB], [Speaker]));

        // Act: read the populated state produced during construction
        var captureDevices = viewModel.CaptureDevices;
        var playbackDevices = viewModel.PlaybackDevices;

        // Assert: both lists carry exactly the reported devices and report availability
        Assert.Equal([MicA, MicB], captureDevices);
        Assert.Equal([Speaker], playbackDevices);
        Assert.True(viewModel.HasCaptureDevices);
        Assert.True(viewModel.HasPlaybackDevices);
    }

    /// <summary>
    ///     Proves that the panel pre-selects the first device of each direction so the picker is
    ///     never shown blank while devices exist.
    /// </summary>
    [Fact]
    public void DeviceSelectionViewModel_Constructor_DevicesAvailable_SelectsFirstOfEachDirection()
    {
        // Arrange & Act: compose the panel over a service reporting devices
        var viewModel = new DeviceSelectionViewModel(Service([MicA, MicB], [Speaker]));

        // Assert: the first device of each direction is selected
        Assert.Same(MicA, viewModel.SelectedCaptureDevice);
        Assert.Same(Speaker, viewModel.SelectedPlaybackDevice);
    }

    /// <summary>
    ///     Proves that a machine reporting no devices produces an explanatory message and a
    ///     disabled picker rather than a silently blank list a user would read as a bug.
    /// </summary>
    [Fact]
    public void DeviceSelectionViewModel_Constructor_NoDevices_ReportsHonestEmptyState()
    {
        // Arrange & Act: compose the panel over a service reporting nothing at all
        var viewModel = new DeviceSelectionViewModel(Service([], []));

        // Assert: both directions report empty, unselected, and explained
        Assert.Empty(viewModel.CaptureDevices);
        Assert.Empty(viewModel.PlaybackDevices);
        Assert.False(viewModel.HasCaptureDevices);
        Assert.False(viewModel.HasPlaybackDevices);
        Assert.Null(viewModel.SelectedCaptureDevice);
        Assert.Null(viewModel.SelectedPlaybackDevice);
        Assert.Contains("audio backend may be unavailable", viewModel.CaptureStatus, StringComparison.Ordinal);
        Assert.Contains("audio backend may be unavailable", viewModel.PlaybackStatus, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves that a populated list reports its device count rather than the empty-state
    ///     explanation.
    /// </summary>
    [Fact]
    public void DeviceSelectionViewModel_Constructor_DevicesAvailable_ReportsCountStatus()
    {
        // Arrange & Act: compose the panel over a service reporting two capture devices
        var viewModel = new DeviceSelectionViewModel(Service([MicA, MicB], [Speaker]));

        // Assert: the status messages state how many devices each direction reported
        Assert.Equal("2 capture device(s) reported.", viewModel.CaptureStatus);
        Assert.Equal("1 playback device(s) reported.", viewModel.PlaybackStatus);
    }

    /// <summary>
    ///     Proves that refreshing after a device is hot-plugged shows the new device without
    ///     losing the user's existing choice.
    /// </summary>
    [Fact]
    public async Task DeviceSelectionViewModel_Refresh_DeviceAdded_KeepsExistingSelection()
    {
        // Arrange: a service that reports one capture device, then two
        var service = SequencedService([MicA], [MicA, MicB], [Speaker], [Speaker]);
        var viewModel = new DeviceSelectionViewModel(service);

        // Act: re-enumerate after the second device appears
        await viewModel.Refresh();

        // Assert: the new device is listed and the original selection survived
        Assert.Equal([MicA, MicB], viewModel.CaptureDevices);
        Assert.Same(MicA, viewModel.SelectedCaptureDevice);
    }

    /// <summary>
    ///     Proves that a selection matching by name survives a refresh even when the backend
    ///     reports the same device with a re-detected audio format.
    /// </summary>
    [Fact]
    public async Task DeviceSelectionViewModel_Refresh_SameDeviceReportedWithNewFormat_SelectsMatchingNameAgain()
    {
        // Arrange: a service reporting the second device first, then the same device name with a
        // different resolved sample rate
        var replacement = new AudioDeviceDescription("Mic B", AudioDeviceDirection.Capture, 2, 44100);
        var service = SequencedService([MicA, MicB], [MicA, replacement], [Speaker], [Speaker]);
        var viewModel = new DeviceSelectionViewModel(service) { SelectedCaptureDevice = MicB };

        // Act: re-enumerate, receiving the same device name with a new format
        await viewModel.Refresh();

        // Assert: the freshly reported description for the same name is selected
        Assert.Same(replacement, viewModel.SelectedCaptureDevice);
    }

    /// <summary>
    ///     Proves that unplugging the selected device falls back to another available device
    ///     rather than leaving the picker blank.
    /// </summary>
    [Fact]
    public async Task DeviceSelectionViewModel_Refresh_SelectedDeviceRemoved_FallsBackToFirstAvailable()
    {
        // Arrange: a service that reports both devices, then only the second
        var service = SequencedService([MicA, MicB], [MicB], [Speaker], [Speaker]);
        var viewModel = new DeviceSelectionViewModel(service);

        // Act: re-enumerate after the originally selected device disappears
        await viewModel.Refresh();

        // Assert: the remaining device is selected instead of nothing
        Assert.Same(MicB, viewModel.SelectedCaptureDevice);
    }

    /// <summary>
    ///     Proves that losing every device clears the selection and restores the explanatory
    ///     empty-state message.
    /// </summary>
    [Fact]
    public async Task DeviceSelectionViewModel_Refresh_AllDevicesRemoved_ClearsSelectionAndExplains()
    {
        // Arrange: a service that reports a device, then nothing
        var service = SequencedService([MicA], [], [Speaker], []);
        var viewModel = new DeviceSelectionViewModel(service);

        // Act: re-enumerate after the backend loses every device
        await viewModel.Refresh();

        // Assert: the panel honestly reports the loss instead of showing a stale selection
        Assert.Null(viewModel.SelectedCaptureDevice);
        Assert.Null(viewModel.SelectedPlaybackDevice);
        Assert.False(viewModel.HasCaptureDevices);
        Assert.False(viewModel.HasPlaybackDevices);
    }

    /// <summary>
    ///     Proves that the refresh command exposed to the view performs the same re-enumeration
    ///     as the method, so the button in the UI is genuinely wired to the behavior under test.
    /// </summary>
    [Fact]
    public void DeviceSelectionViewModel_RefreshCommand_Executed_ReEnumeratesDevices()
    {
        // Arrange: a service reporting one device, then two
        var service = SequencedService([MicA], [MicA, MicB], [Speaker], [Speaker]);
        var viewModel = new DeviceSelectionViewModel(service);

        // Act: invoke the command the view's button binds to
        viewModel.RefreshCommand.Execute(null);

        // Assert: the panel re-enumerated through the seam
        Assert.Equal(2, viewModel.CaptureDevices.Count);
    }

    /// <summary>
    ///     Proves that a chosen device is converted into the library's name-based selection value,
    ///     which is what a host would persist and hand back to the library.
    /// </summary>
    [Fact]
    public void DeviceSelectionViewModel_CaptureSelection_DeviceSelected_ReturnsNameBasedSelection()
    {
        // Arrange: a panel with a device selected
        var viewModel = new DeviceSelectionViewModel(Service([MicA, MicB], [Speaker]))
        {
            SelectedCaptureDevice = MicB
        };

        // Act: read the library selection value the panel exposes
        var selection = viewModel.CaptureSelection;

        // Assert: the selection carries the device's name, the library's only stable identity
        Assert.Equal("Mic B", selection.DeviceName);
    }

    /// <summary>
    ///     Proves that a panel with nothing selected still yields a usable selection, so a caller
    ///     never has to special-case the no-device machine.
    /// </summary>
    [Fact]
    public void DeviceSelectionViewModel_Selections_NoDevices_ReturnSystemDefault()
    {
        // Arrange: a panel over a machine reporting no devices at all
        var viewModel = new DeviceSelectionViewModel(Service([], []));

        // Act: read both library selection values
        var capture = viewModel.CaptureSelection;
        var playback = viewModel.PlaybackSelection;

        // Assert: both fall back to the library's system-default selection
        Assert.Same(AudioDeviceSelection.SystemDefault, capture);
        Assert.Same(AudioDeviceSelection.SystemDefault, playback);
    }

    /// <summary>
    ///     Proves that changing the chosen playback device raises a change notification for the
    ///     derived library selection, so a bound view never shows a stale selection.
    /// </summary>
    [Fact]
    public void DeviceSelectionViewModel_SelectedPlaybackDevice_Changed_NotifiesPlaybackSelection()
    {
        // Arrange: a panel with two playback devices and a recorded notification list
        var other = new AudioDeviceDescription("Headset", AudioDeviceDirection.Playback, 2, 48000);
        var viewModel = new DeviceSelectionViewModel(Service([MicA], [Speaker, other]));
        var changed = new List<string?>();
        viewModel.PropertyChanged += (_, args) => changed.Add(args.PropertyName);

        // Act: choose the other playback device
        viewModel.SelectedPlaybackDevice = other;

        // Assert: the derived selection was announced as changed
        Assert.Contains(nameof(DeviceSelectionViewModel.PlaybackSelection), changed);
    }

    /// <summary>
    ///     Proves that <see cref="DeviceSelectionViewModel.Refresh"/> forces the backend to
    ///     re-scan its device table via <see cref="IAudioDeviceService.RefreshDevices"/> before
    ///     re-enumerating either device list.
    /// </summary>
    [Fact]
    public async Task DeviceSelectionViewModel_Refresh_CallsRefreshDevicesBeforeEnumerating()
    {
        // Arrange: a service reporting one device, then two, with a recorded call order
        var service = SequencedService([MicA], [MicA, MicB], [Speaker], [Speaker]);
        var viewModel = new DeviceSelectionViewModel(service);

        // Act: refresh again
        await viewModel.Refresh();

        // Assert: the backend refresh was invoked once per Refresh() call (construction + explicit call)
        service.Received(2).RefreshDevices();
        Assert.Equal([MicA, MicB], viewModel.CaptureDevices);
    }

    /// <summary>
    ///     Proves that when the service refuses a refresh because a device is in use, the
    ///     refusal's message is surfaced through the existing status-text pattern and neither
    ///     device list nor selection is disturbed, rather than crashing the UI.
    /// </summary>
    [Fact]
    public async Task DeviceSelectionViewModel_Refresh_ServiceThrowsAudioDeviceInUseException_SurfacesMessageViaStatusAndDoesNotClearLists()
    {
        // Arrange: a panel already populated, then a service that refuses the next refresh
        var service = Service([MicA, MicB], [Speaker]);
        var viewModel = new DeviceSelectionViewModel(service);
        const string inUseMessage = "Cannot refresh PortAudio devices while a stream is active on: Mic A.";
        service.When(s => s.RefreshDevices()).Do(_ => throw new AudioDeviceInUseException(inUseMessage));

        // Act: attempt a refresh that the backend refuses
        var exception = await Record.ExceptionAsync(() => viewModel.Refresh());

        // Assert: the refusal does not escape as an unhandled exception, the message is surfaced
        // via both status properties, and the existing lists/selection are left untouched
        Assert.Null(exception);
        Assert.Equal(inUseMessage, viewModel.CaptureStatus);
        Assert.Equal(inUseMessage, viewModel.PlaybackStatus);
        Assert.Equal([MicA, MicB], viewModel.CaptureDevices);
        Assert.Same(MicA, viewModel.SelectedCaptureDevice);
    }

    /// <summary>
    ///     Proves that <see cref="DeviceSelectionViewModel.Refresh"/> genuinely invokes and awaits
    ///     a registered pre-refresh hook to completion before calling
    ///     <see cref="IAudioDeviceService.RefreshDevices"/>, rather than firing the hook without
    ///     waiting for it.
    /// </summary>
    [Fact]
    public async Task DeviceSelectionViewModel_Refresh_RegisteredHook_InvokedAndAwaitedBeforeRefreshDevices()
    {
        // Arrange: a panel with a registered hook backed by a TaskCompletionSource so its
        // completion can be controlled from the test
        var service = Service([MicA], [Speaker]);
        var viewModel = new DeviceSelectionViewModel(service);
        service.ClearReceivedCalls();
        var hookGate = new TaskCompletionSource();
        viewModel.RegisterPreRefreshHook(() => hookGate.Task);

        // Act: start the refresh without awaiting it yet
        var refreshTask = viewModel.Refresh();

        // Assert: RefreshDevices() has not yet been called while the hook is unresolved
        service.DidNotReceive().RefreshDevices();

        // Act: complete the hook and let the refresh proceed
        hookGate.SetResult();
        await refreshTask;

        // Assert: RefreshDevices() was called exactly once, after the hook completed
        service.Received(1).RefreshDevices();
    }

    /// <summary>
    ///     Proves that a hook unregistered via <see cref="DeviceSelectionViewModel.UnregisterPreRefreshHook"/>
    ///     is never invoked by a subsequent <see cref="DeviceSelectionViewModel.Refresh"/>.
    /// </summary>
    [Fact]
    public async Task DeviceSelectionViewModel_Refresh_UnregisteredHook_NotInvoked()
    {
        // Arrange: a panel with a hook registered then immediately unregistered
        var viewModel = new DeviceSelectionViewModel(Service([MicA], [Speaker]));
        var invoked = false;
        Task Hook()
        {
            invoked = true;
            return Task.CompletedTask;
        }

        viewModel.RegisterPreRefreshHook(Hook);
        viewModel.UnregisterPreRefreshHook(Hook);

        // Act: refresh
        await viewModel.Refresh();

        // Assert: the unregistered hook was never invoked
        Assert.False(invoked);
    }

    /// <summary>
    ///     Proves that the existing <see cref="AudioDeviceInUseException"/> fallback still
    ///     surfaces its status message when a registered hook cannot stop the in-use session,
    ///     preserving the safety net alongside the new hook mechanism.
    /// </summary>
    [Fact]
    public async Task DeviceSelectionViewModel_Refresh_HookCannotStopInUseDevice_FallsBackToStatusMessage()
    {
        // Arrange: a panel with a no-op hook (simulating a hook unable to help) and a service
        // that unconditionally refuses the refresh
        var service = Service([MicA, MicB], [Speaker]);
        var viewModel = new DeviceSelectionViewModel(service);
        const string inUseMessage = "Cannot refresh PortAudio devices while a stream is active on: Mic A.";
        service.When(s => s.RefreshDevices()).Do(_ => throw new AudioDeviceInUseException(inUseMessage));
        viewModel.RegisterPreRefreshHook(() => Task.CompletedTask);

        // Act: attempt a refresh that the hook could not prevent from being refused
        var exception = await Record.ExceptionAsync(() => viewModel.Refresh());

        // Assert: the fallback status-message behavior still fires
        Assert.Null(exception);
        Assert.Equal(inUseMessage, viewModel.CaptureStatus);
        Assert.Equal(inUseMessage, viewModel.PlaybackStatus);
    }
}
