namespace DemaConsulting.Speech.SynthesisSubsystem;

/// <summary>
///     The lifecycle states an <see cref="ISynthesisSession"/> passes through.
/// </summary>
/// <remarks>
///     Unlike <c>RecognitionSubsystem.RecognitionSessionState</c>, which describes a single
///     continuous capture window, <see cref="Starting"/>, <see cref="Running"/>, and
///     <see cref="Stopping"/> here denote one discrete, in-flight
///     <see cref="ISynthesisSession.SpeakAsync"/> or <see cref="ISynthesisSession.SynthesizeAsync"/>
///     operation rather than a continuous stream: a session has no analogue of recognition's
///     continuous start/stop capture window, so it returns to <see cref="Stopped"/> after each
///     operation completes and is ready to accept another <see cref="ISynthesisSession.SpeakAsync"/>
///     or <see cref="ISynthesisSession.SynthesizeAsync"/> call - this repeatability is what lets a
///     host construct one session per model/device combination and reuse it across many calls for
///     low-latency, repeated synthesis, rather than recreating it per call.
///     <para>
///     <see cref="Faulted"/> is terminal: once reached (an unrequested abandonment of a native
///     call, or any other non-cancellation failure), the session can no longer perform operations
///     and must be disposed and replaced.
///     </para>
/// </remarks>
public enum SynthesisSessionState
{
    /// <summary>The session has been created but has not yet performed any operation.</summary>
    Created,

    /// <summary>A <c>SpeakAsync</c>/<c>SynthesizeAsync</c> call has begun but audio has not yet started generating.</summary>
    Starting,

    /// <summary>A <c>SpeakAsync</c>/<c>SynthesizeAsync</c> call is actively generating (and, for <c>SpeakAsync</c>, playing) audio.</summary>
    Running,

    /// <summary>The in-flight operation is winding down (draining playback or completing cancellation).</summary>
    Stopping,

    /// <summary>The most recent operation has completed; the session is ready to accept another call.</summary>
    Stopped,

    /// <summary><see cref="IAsyncDisposable.DisposeAsync"/> has begun but has not yet completed.</summary>
    Disposing,

    /// <summary>The session has been fully disposed and can no longer be used.</summary>
    Disposed,

    /// <summary>
    ///     An in-flight operation failed for a reason other than its own requested cancellation
    ///     (including an unrequested native-call abandonment). Terminal: the session must be
    ///     disposed and replaced.
    /// </summary>
    Faulted
}
