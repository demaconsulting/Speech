using DemaConsulting.Speech.AudioSubsystem;

namespace DemaConsulting.Speech.Tests.AudioSubsystem;

/// <summary>
///     Unit tests for the <see cref="AudioDeviceSelection"/> record.
/// </summary>
public class AudioDeviceSelectionTests
{
    /// <summary>
    ///     Proves that resolving a selection whose name matches an enumerated device returns
    ///     that device.
    /// </summary>
    [Fact]
    public void AudioDeviceSelection_Resolve_MatchingName_ReturnsMatchingDevice()
    {
        // Arrange: a selection naming a device present in the enumeration
        var selection = new AudioDeviceSelection("Microphone");
        var devices = new List<AudioDeviceDescription>
        {
            new("Microphone", AudioDeviceDirection.Capture, 1, 16000),
            new("Line In", AudioDeviceDirection.Capture, 2, 44100)
        };

        // Act: resolve the selection against the enumeration
        var resolved = selection.Resolve(devices);

        // Assert: the matching device is returned
        Assert.Equal(devices[0], resolved);
    }

    /// <summary>
    ///     Proves that resolving a selection whose name is no longer present in the enumeration
    ///     falls back to null (system default) rather than throwing.
    /// </summary>
    [Fact]
    public void AudioDeviceSelection_Resolve_NoLongerPresent_ReturnsNull()
    {
        // Arrange: a selection naming a device that has since disappeared
        var selection = new AudioDeviceSelection("Unplugged Microphone");
        var devices = new List<AudioDeviceDescription>
        {
            new("Line In", AudioDeviceDirection.Capture, 2, 44100)
        };

        // Act: resolve the stale selection
        var resolved = selection.Resolve(devices);

        // Assert: falls back to null (system default) instead of throwing
        Assert.Null(resolved);
    }

    /// <summary>
    ///     Proves that <see cref="AudioDeviceSelection.SystemDefault"/> always resolves to null,
    ///     regardless of what devices are enumerated.
    /// </summary>
    [Fact]
    public void AudioDeviceSelection_Resolve_SystemDefault_ReturnsNull()
    {
        // Arrange: the canonical system-default selection and a non-empty enumeration
        var selection = AudioDeviceSelection.SystemDefault;
        var devices = new List<AudioDeviceDescription>
        {
            new("Line In", AudioDeviceDirection.Capture, 2, 44100)
        };

        // Act: resolve the system-default selection
        var resolved = selection.Resolve(devices);

        // Assert: system default always resolves to null, meaning "let the host decide"
        Assert.Null(resolved);
    }

    /// <summary>
    ///     Proves that resolving against an empty device list never throws and falls back to
    ///     null, confirming "nothing throws at composition" holds even with no devices at all.
    /// </summary>
    [Fact]
    public void AudioDeviceSelection_Resolve_EmptyDeviceList_ReturnsNull()
    {
        // Arrange: a named selection but no devices enumerated at all
        var selection = new AudioDeviceSelection("Microphone");
        var devices = new List<AudioDeviceDescription>();

        // Act: resolve against the empty enumeration
        var resolved = selection.Resolve(devices);

        // Assert: falls back to null rather than throwing
        Assert.Null(resolved);
    }

    /// <summary>
    ///     Proves that <see cref="AudioDeviceSelection.Resolve"/> rejects a null device list with
    ///     <see cref="ArgumentNullException"/>, distinguishing "not asked" from "no devices".
    /// </summary>
    [Fact]
    public void AudioDeviceSelection_Resolve_NullDeviceList_ThrowsArgumentNullException()
    {
        // Arrange: a selection instance
        var selection = new AudioDeviceSelection("Microphone");

        // Act & Assert: a null device list is rejected explicitly
        Assert.Throws<ArgumentNullException>(() => selection.Resolve(null!));
    }
}
