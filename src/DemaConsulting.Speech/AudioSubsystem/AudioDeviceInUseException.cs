namespace DemaConsulting.Speech.AudioSubsystem;

/// <summary>
///     Thrown when <see cref="AudioDeviceFactory.RefreshDevices"/> is refused because one or more
///     capture/playback devices created from the environment currently have an open/started
///     stream.
/// </summary>
/// <remarks>
///     <para>
///         This is the one deliberate exception to this library's "nothing throws at composition"
///         policy. Refreshing PortAudio's device table requires tearing down and reinitializing
///         the native runtime (<c>Pa_Terminate()</c> followed by <c>Pa_Initialize()</c>), which
///         would invalidate any stream currently open against it. Rather than silently tearing
///         down a live stream, the refresh is refused outright: this is caller-driven, recoverable
///         misuse, not an honest composition-time unavailability like
///         <see cref="AudioDeviceUnavailableException"/>.
///     </para>
///     <para>
///         Resolving this exception means stopping the active capture/playback session(s) that
///         are keeping a stream open - via <see cref="IAudioCaptureDevice.Stop"/> and/or
///         <see cref="IAudioPlaybackDevice.Stop"/> - before retrying
///         <see cref="AudioDeviceFactory.RefreshDevices"/>. This exception does not expose a
///         structured list of, or property identifying, the in-use device(s); any identifying
///         detail is only ever present as free-form text in the exception's
///         <see cref="Exception.Message"/>.
///     </para>
/// </remarks>
public sealed class AudioDeviceInUseException : Exception
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="AudioDeviceInUseException"/> class with a
    ///     default message.
    /// </summary>
    /// <remarks>
    ///     Provided for standard .NET exception-type conformance; callers should prefer
    ///     <see cref="AudioDeviceInUseException(string)"/> to describe which device(s) are in use.
    /// </remarks>
    public AudioDeviceInUseException()
        : base("The audio device refresh was refused because a device is currently in use.")
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="AudioDeviceInUseException"/> class with a
    ///     message describing which device(s) are currently in use.
    /// </summary>
    /// <param name="message">A message describing the in-use device(s) that refused the refresh.</param>
    public AudioDeviceInUseException(string message)
        : base(message)
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="AudioDeviceInUseException"/> class with a
    ///     message and an inner exception describing the underlying cause.
    /// </summary>
    /// <param name="message">A message describing the in-use device(s) that refused the refresh.</param>
    /// <param name="innerException">The exception that is the cause of this exception.</param>
    public AudioDeviceInUseException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
