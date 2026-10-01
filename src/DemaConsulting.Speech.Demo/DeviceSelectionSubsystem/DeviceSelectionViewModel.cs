using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DemaConsulting.Speech.AudioSubsystem;

namespace DemaConsulting.Speech.Demo.DeviceSelectionSubsystem;

/// <summary>
///     Presentation state for the demo's audio capture/playback device pickers.
/// </summary>
/// <remarks>
///     This ViewModel exists so the demo can prove, without any speech logic of its own, that a
///     host application can discover and choose audio devices through the library's public
///     surface alone. It never touches the audio backend directly - every device comes from the
///     injected <see cref="IAudioDeviceService"/> seam - which is what makes it unit-testable
///     with no microphone or speaker present.
///     <para>
///     A machine with no audio device is an ordinary state, not an error, so an empty
///     enumeration produces an explanatory status message and a disabled picker rather than an
///     exception or a silently blank list. This mirrors the library's own "honest unavailable
///     state" contract.
///     </para>
///     <para>
///     Not thread-safe: like all Avalonia ViewModels it is expected to be used from the UI thread.
///     </para>
/// </remarks>
public sealed partial class DeviceSelectionViewModel : ObservableObject
{
    /// <summary>The status message shown when no device of a given direction could be enumerated.</summary>
    private const string NoDevicesMessage =
        "No device was reported. The audio backend may be unavailable on this machine, " +
        "or no device of this kind is connected.";

    /// <summary>The seam supplying every enumerated device.</summary>
    private readonly IAudioDeviceService _deviceService;

    /// <summary>
    ///     The pre-refresh hooks registered by other panels sharing this device-selection state.
    /// </summary>
    /// <remarks>
    ///     A list of delegates is used instead of a plain multicast event because
    ///     <see cref="Refresh"/> must genuinely <c>await</c> each hook to completion, in order,
    ///     before calling <see cref="IAudioDeviceService.RefreshDevices"/>; a standard C# event
    ///     cannot be meaningfully awaited (invoking a multicast delegate only returns the last
    ///     subscriber's return value, discarding the others), so an explicit, awaitable
    ///     registration list is used instead.
    /// </remarks>
    private readonly List<Func<Task>> _preRefreshHooks = [];

    /// <summary>
    ///     Gets the capture (input) devices currently offered to the user.
    /// </summary>
    public ObservableCollection<AudioDeviceDescription> CaptureDevices { get; } = [];

    /// <summary>
    ///     Gets the playback (output) devices currently offered to the user.
    /// </summary>
    public ObservableCollection<AudioDeviceDescription> PlaybackDevices { get; } = [];

    /// <summary>
    ///     Gets or sets the capture device the user has chosen, or <see langword="null"/> when no
    ///     capture device is available or none has been chosen yet.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CaptureSelection))]
    public partial AudioDeviceDescription? SelectedCaptureDevice { get; set; }

    /// <summary>
    ///     Gets or sets the playback device the user has chosen, or <see langword="null"/> when no
    ///     playback device is available or none has been chosen yet.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PlaybackSelection))]
    public partial AudioDeviceDescription? SelectedPlaybackDevice { get; set; }

    /// <summary>
    ///     Gets or sets the message describing the capture-device list's current state.
    /// </summary>
    [ObservableProperty]
    public partial string CaptureStatus { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets the message describing the playback-device list's current state.
    /// </summary>
    [ObservableProperty]
    public partial string PlaybackStatus { get; set; } = string.Empty;

    /// <summary>
    ///     Gets a value indicating whether at least one capture device is available to choose.
    /// </summary>
    public bool HasCaptureDevices => CaptureDevices.Count > 0;

    /// <summary>
    ///     Gets a value indicating whether at least one playback device is available to choose.
    /// </summary>
    public bool HasPlaybackDevices => PlaybackDevices.Count > 0;

    /// <summary>
    ///     Gets the library selection value representing the chosen capture device.
    /// </summary>
    /// <remarks>
    ///     Falls back to <see cref="AudioDeviceSelection.SystemDefault"/> when nothing is chosen,
    ///     so a caller always has a usable selection to hand to the library.
    /// </remarks>
    public AudioDeviceSelection CaptureSelection => ToSelection(SelectedCaptureDevice);

    /// <summary>
    ///     Gets the library selection value representing the chosen playback device.
    /// </summary>
    /// <remarks>
    ///     Falls back to <see cref="AudioDeviceSelection.SystemDefault"/> when nothing is chosen,
    ///     so a caller always has a usable selection to hand to the library.
    /// </remarks>
    public AudioDeviceSelection PlaybackSelection => ToSelection(SelectedPlaybackDevice);

    /// <summary>
    ///     Initializes a new instance of the <see cref="DeviceSelectionViewModel"/> class and
    ///     populates both device lists.
    /// </summary>
    /// <param name="deviceService">
    ///     The device-enumeration seam to read from. Must not be <see langword="null"/>.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when <paramref name="deviceService"/> is <see langword="null"/>.
    /// </exception>
    /// <remarks>
    ///     Enumerating during construction means the window shows real device names the instant it
    ///     opens rather than an empty list the user must manually refresh.
    /// </remarks>
    public DeviceSelectionViewModel(IAudioDeviceService deviceService)
    {
        ArgumentNullException.ThrowIfNull(deviceService);

        _deviceService = deviceService;

        // Calls RefreshCore() directly (not the async Refresh() command) so construction-time
        // population remains synchronous: no pre-refresh hook can be registered yet at this
        // point (every hook owner receives this already-constructed instance via its own
        // constructor, so registration can only happen after this constructor returns), and
        // calling a Task-returning method without awaiting it here would otherwise be a
        // CS4014 warning-as-error.
        RefreshCore();
    }

    /// <summary>
    ///     Registers a hook to be invoked and awaited by <see cref="Refresh"/> before it forces
    ///     the backend to re-scan its device table.
    /// </summary>
    /// <param name="hook">
    ///     The asynchronous hook to register. Must not be <see langword="null"/>. Should return a
    ///     completed or near-instantly-completing task once it has no more active work to
    ///     protect (a hook with nothing active should be a fast no-op).
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="hook"/> is <see langword="null"/>.</exception>
    /// <remarks>
    ///     Intended for panels sharing this device-selection state (e.g. recognition or synthesis
    ///     panels) to deterministically stop their own active session - and confirm the
    ///     underlying device is actually closed - before a device refresh is attempted, so the
    ///     <see cref="AudioDeviceInUseException"/> fallback below becomes a rare defensive path
    ///     rather than the primary way a refresh succeeds.
    /// </remarks>
    internal void RegisterPreRefreshHook(Func<Task> hook)
    {
        ArgumentNullException.ThrowIfNull(hook);
        _preRefreshHooks.Add(hook);
    }

    /// <summary>
    ///     Unregisters a hook previously registered with <see cref="RegisterPreRefreshHook"/>.
    /// </summary>
    /// <param name="hook">The exact hook instance previously registered. Must not be <see langword="null"/>.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="hook"/> is <see langword="null"/>.</exception>
    /// <remarks>
    ///     A safe no-op when <paramref name="hook"/> was never registered or was already
    ///     unregistered, matching the tolerant unsubscribe pattern used elsewhere in this demo.
    /// </remarks>
    internal void UnregisterPreRefreshHook(Func<Task> hook)
    {
        ArgumentNullException.ThrowIfNull(hook);
        _preRefreshHooks.Remove(hook);
    }

    /// <summary>
    ///     Invokes and awaits every registered pre-refresh hook, in registration order, then
    ///     re-reads both device lists from the audio backend, preserving the user's current
    ///     choices where those devices are still present.
    /// </summary>
    /// <remarks>
    ///     Exposed as a command because audio devices are hot-pluggable: a user who connects a
    ///     headset after the window opened must be able to see it without restarting the demo.
    ///     Each registered hook (see <see cref="RegisterPreRefreshHook"/>) is awaited to
    ///     completion before the next one runs and before the backend re-scan is attempted, so a
    ///     panel with an active session gets a deterministic chance to stop it - and confirm the
    ///     device is closed - first. The existing <see cref="AudioDeviceInUseException"/> handling
    ///     in <see cref="RefreshCore"/> remains as a defensive fallback for any session the hooks
    ///     could not stop.
    /// </remarks>
    [RelayCommand]
    public async Task Refresh()
    {
        foreach (var hook in _preRefreshHooks.ToArray())
        {
            await hook().ConfigureAwait(true);
        }

        RefreshCore();
    }

    /// <summary>
    ///     Forces the backend to re-scan its device table and re-enumerates both device lists.
    /// </summary>
    /// <remarks>
    ///     Split out of <see cref="Refresh"/> so the constructor can populate both lists
    ///     synchronously at construction time without awaiting anything. First forces the backend
    ///     to re-scan its device table via <see cref="IAudioDeviceService.RefreshDevices"/>; when
    ///     that is refused because a device is currently in use, both lists and selections are
    ///     left untouched and the refusal's message is surfaced through the existing status-text
    ///     pattern instead of re-enumerating or crashing.
    ///     <para>
    ///         Any other exception raised by <see cref="IAudioDeviceService.RefreshDevices"/> (for
    ///         example, a native PortAudio teardown/reinitialization failure) is handled the same
    ///         way: both lists and selections are left untouched and the exception's message is
    ///         surfaced through the same status-text pattern instead of propagating and crashing
    ///         the UI.
    ///     </para>
    /// </remarks>
    private void RefreshCore()
    {
        try
        {
            _deviceService.RefreshDevices();
        }
        catch (AudioDeviceInUseException ex)
        {
            CaptureStatus = ex.Message;
            PlaybackStatus = ex.Message;
            return;
        }
        catch (Exception ex)
        {
            CaptureStatus = ex.Message;
            PlaybackStatus = ex.Message;
            return;
        }

        // Capture the current choices by name so a device that survived the refresh stays
        // selected; name is the library's only stable device identity.
        var previousCaptureName = SelectedCaptureDevice?.Name;
        var previousPlaybackName = SelectedPlaybackDevice?.Name;

        // Re-enumerate both directions. The seam never throws, so no defensive handling is needed.
        Replace(CaptureDevices, _deviceService.EnumerateCaptureDevices());
        Replace(PlaybackDevices, _deviceService.EnumeratePlaybackDevices());

        // Restore each selection, falling back to the first available device so the picker is
        // never left blank while devices exist.
        SelectedCaptureDevice = Reselect(CaptureDevices, previousCaptureName);
        SelectedPlaybackDevice = Reselect(PlaybackDevices, previousPlaybackName);

        // Report the honest state of each list, including the "nothing available" case.
        CaptureStatus = DescribeState(CaptureDevices.Count, "capture");
        PlaybackStatus = DescribeState(PlaybackDevices.Count, "playback");

        OnPropertyChanged(nameof(HasCaptureDevices));
        OnPropertyChanged(nameof(HasPlaybackDevices));
    }

    /// <summary>
    ///     Replaces a bound collection's contents in place with a fresh enumeration.
    /// </summary>
    /// <param name="target">The bound collection to update.</param>
    /// <param name="devices">The devices to place into <paramref name="target"/>.</param>
    /// <remarks>
    ///     Updating in place (rather than assigning a new collection) keeps the existing binding
    ///     intact so the view does not need to re-subscribe on every refresh.
    /// </remarks>
    private static void Replace(
        ObservableCollection<AudioDeviceDescription> target,
        IReadOnlyList<AudioDeviceDescription> devices)
    {
        target.Clear();
        foreach (var device in devices)
        {
            target.Add(device);
        }
    }

    /// <summary>
    ///     Chooses which device should be selected after a refresh.
    /// </summary>
    /// <param name="devices">The freshly enumerated devices.</param>
    /// <param name="previousName">The name of the previously selected device, if any.</param>
    /// <returns>
    ///     The device matching <paramref name="previousName"/> when it is still present;
    ///     otherwise the first available device; otherwise <see langword="null"/>.
    /// </returns>
    private static AudioDeviceDescription? Reselect(
        ObservableCollection<AudioDeviceDescription> devices,
        string? previousName)
    {
        if (previousName is not null)
        {
            var match = devices.FirstOrDefault(
                device => string.Equals(device.Name, previousName, StringComparison.Ordinal));
            if (match is not null)
            {
                return match;
            }
        }

        return devices.FirstOrDefault();
    }

    /// <summary>
    ///     Builds the status message describing one device list's state.
    /// </summary>
    /// <param name="count">The number of devices enumerated.</param>
    /// <param name="direction">The human-readable device direction, e.g. <c>"capture"</c>.</param>
    /// <returns>An explanatory message for an empty list, or a plain count for a populated one.</returns>
    private static string DescribeState(int count, string direction) =>
        count == 0
            ? NoDevicesMessage
            : $"{count} {direction} device(s) reported.";

    /// <summary>
    ///     Converts a chosen device into the library's persistable selection value.
    /// </summary>
    /// <param name="device">The chosen device, or <see langword="null"/> when none is chosen.</param>
    /// <returns>
    ///     A name-based selection for <paramref name="device"/>, or
    ///     <see cref="AudioDeviceSelection.SystemDefault"/> when nothing is chosen.
    /// </returns>
    private static AudioDeviceSelection ToSelection(AudioDeviceDescription? device) =>
        device is null ? AudioDeviceSelection.SystemDefault : new AudioDeviceSelection(device.Name);
}
