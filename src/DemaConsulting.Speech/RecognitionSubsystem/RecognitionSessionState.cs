namespace DemaConsulting.Speech.RecognitionSubsystem;

/// <summary>
///     The lifecycle states an <see cref="IRecognitionSession"/> moves through.
/// </summary>
/// <remarks>
///     The forward-only progression is <see cref="Created"/> → <see cref="Starting"/> →
///     <see cref="Running"/> → <see cref="Stopping"/> → <see cref="Stopped"/> →
///     <see cref="Disposing"/> → <see cref="Disposed"/>, with <see cref="Faulted"/> reachable as
///     a terminal state from <see cref="Starting"/>, <see cref="Running"/>, or
///     <see cref="Stopping"/>. A session is single-use: once it reaches <see cref="Stopped"/>,
///     <see cref="IRecognitionSession.StartAsync"/> throws rather than permitting a back-edge to
///     <see cref="Starting"/> again - see <see cref="IRecognitionSession"/>'s remarks.
/// </remarks>
public enum RecognitionSessionState
{
    /// <summary>The session has been created but <see cref="IRecognitionSession.StartAsync"/> has not yet been called.</summary>
    Created,

    /// <summary>The session is subscribing to and starting its bound capture device.</summary>
    Starting,

    /// <summary>The session is streaming captured audio through the recognition backend.</summary>
    Running,

    /// <summary>The session is draining buffered audio and tearing down its capture subscription.</summary>
    Stopping,

    /// <summary>The session has stopped; it will never run again.</summary>
    Stopped,

    /// <summary>The session is releasing its resources and the engine's exclusivity lease.</summary>
    Disposing,

    /// <summary>The session has been fully disposed.</summary>
    Disposed,

    /// <summary>
    ///     The session encountered an unrecoverable error while <see cref="Starting"/>,
    ///     <see cref="Running"/>, or <see cref="Stopping"/> and can no longer be used.
    /// </summary>
    Faulted
}
