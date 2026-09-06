namespace DemaConsulting.Speech.AudioSubsystem;

/// <summary>
///     Thrown when an operation is attempted on an audio device or probe that has honestly
///     reported itself as unavailable.
/// </summary>
/// <remarks>
///     Per architecture.md's "nothing throws at composition" decision, an <c>Unavailable*</c>
///     device never throws merely for existing - callers can always construct and hold one
///     safely. This exception is instead reserved for the first-use failure case: a caller that
///     ignored <c>IsAvailable == false</c> and invoked an operational member anyway has made a
///     programming error, and that error should surface immediately rather than silently no-op.
/// </remarks>
public sealed class AudioDeviceUnavailableException : Exception
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="AudioDeviceUnavailableException"/> class
    ///     with a default message.
    /// </summary>
    /// <remarks>
    ///     Provided for standard .NET exception-type conformance; callers should prefer
    ///     <see cref="AudioDeviceUnavailableException(string)"/> to describe which device or
    ///     operation was affected.
    /// </remarks>
    public AudioDeviceUnavailableException()
        : base("The audio device is unavailable.")
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="AudioDeviceUnavailableException"/> class
    ///     with a message describing which device or operation was affected.
    /// </summary>
    /// <param name="message">A message describing the unavailable device or attempted operation.</param>
    public AudioDeviceUnavailableException(string message)
        : base(message)
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="AudioDeviceUnavailableException"/> class
    ///     with a message and an inner exception describing the underlying cause.
    /// </summary>
    /// <param name="message">A message describing the unavailable device or attempted operation.</param>
    /// <param name="innerException">The exception that is the cause of this exception.</param>
    public AudioDeviceUnavailableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
