namespace DemaConsulting.Speech.SynthesisSubsystem;

/// <summary>
///     Thrown when <see cref="ISpeechSynthesizerEngine.CreateSessionAsync"/> is called while this
///     engine's exclusivity lease is already held by another session.
/// </summary>
/// <remarks>
///     Per the engine-exclusivity design decision, at most one <see cref="ISynthesisSession"/> may
///     be leased from a given <see cref="ISpeechSynthesizerEngine"/> at a time, with the lease held
///     for the session's entire life through <see cref="IAsyncDisposable.DisposeAsync"/>
///     completion. A concurrent <see cref="ISpeechSynthesizerEngine.CreateSessionAsync"/> call
///     while that lease is held fails fast with this exception rather than queueing or awaiting
///     release, since waiting would make this call's latency depend on an unrelated session's
///     teardown with no caller-visible way to bound that wait. A caller that needs to wait should
///     implement its own retry/backoff.
/// </remarks>
public sealed class SynthesisEngineBusyException : Exception
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="SynthesisEngineBusyException"/> class with
    ///     a default message.
    /// </summary>
    /// <remarks>
    ///     Provided for standard .NET exception-type conformance; callers should prefer
    ///     <see cref="SynthesisEngineBusyException(string)"/> to describe which engine was busy.
    /// </remarks>
    public SynthesisEngineBusyException()
        : base("The synthesis engine's exclusivity lease is already held by another session.")
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="SynthesisEngineBusyException"/> class with
    ///     a message describing which engine was busy.
    /// </summary>
    /// <param name="message">A message describing the busy engine.</param>
    public SynthesisEngineBusyException(string message)
        : base(message)
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="SynthesisEngineBusyException"/> class with
    ///     a message and an inner exception describing the underlying cause.
    /// </summary>
    /// <param name="message">A message describing the busy engine.</param>
    /// <param name="innerException">The exception that is the cause of this exception.</param>
    public SynthesisEngineBusyException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
