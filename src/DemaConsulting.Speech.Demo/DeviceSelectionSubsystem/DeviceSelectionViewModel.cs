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
        Refresh();
    }

    /// <summary>
    ///     Re-reads both device lists from the audio backend, preserving the user's current
    ///     choices where those devices are still present.
    /// </summary>
    /// <remarks>
    ///     Exposed as a command because audio devices are hot-pluggable: a user who connects a
    ///     headset after the window opened must be able to see it without restarting the demo.
    /// </remarks>
    [RelayCommand]
    public void Refresh()
    {
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
