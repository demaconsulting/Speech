using DemaConsulting.Speech.AudioSubsystem;

namespace DemaConsulting.Speech.Demo.DeviceSelectionSubsystem;

/// <summary>
///     Production <see cref="IAudioDeviceService"/> implementation backed by the library's
///     <see cref="AudioDeviceFactory"/>.
/// </summary>
/// <remarks>
///     This adapter is deliberately behavior-free: it forwards each call to the corresponding
///     library probe and returns the result unchanged, so the demo's device list is exactly what
///     the library reports and nothing the demo invented. Because the library's probes never
///     throw and degrade to empty enumerations when the audio backend is unavailable, this
///     adapter inherits that contract without needing any error handling of its own.
///     Instances are stateless apart from the injected factory and are safe to share.
/// </remarks>
public sealed class AudioDeviceService : IAudioDeviceService
{
    /// <summary>The library factory whose probes supply every enumerated device.</summary>
    private readonly AudioDeviceFactory _factory;

    /// <summary>
    ///     Initializes a new instance of the <see cref="AudioDeviceService"/> class.
    /// </summary>
    /// <param name="factory">
    ///     The library audio-device factory to enumerate through. Must not be <see langword="null"/>.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when <paramref name="factory"/> is <see langword="null"/>.
    /// </exception>
    public AudioDeviceService(AudioDeviceFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        _factory = factory;
    }

    /// <inheritdoc/>
    public IReadOnlyList<AudioDeviceDescription> EnumerateCaptureDevices() =>
        _factory.CaptureProbe.Enumerate();

    /// <inheritdoc/>
    public IReadOnlyList<AudioDeviceDescription> EnumeratePlaybackDevices() =>
        _factory.PlaybackProbe.Enumerate();

    /// <inheritdoc/>
    public IAudioCaptureDevice CreateCaptureDevice(AudioDeviceSelection? selection) =>
        _factory.CreateCaptureDevice(selection);

    /// <inheritdoc/>
    public IAudioPlaybackDevice CreatePlaybackDevice(AudioDeviceSelection? selection) =>
        _factory.CreatePlaybackDevice(selection);
}
