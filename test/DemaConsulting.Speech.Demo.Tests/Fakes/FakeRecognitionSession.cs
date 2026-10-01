using System.Runtime.CompilerServices;
using DemaConsulting.Speech.RecognitionSubsystem;

namespace DemaConsulting.Speech.Demo.Tests.Fakes;

/// <summary>
///     Hand-written test double for <see cref="IRecognitionSession"/>, used instead of a mocking
///     library because the interface's streaming <see cref="GetResultsAsync"/> contract needs
///     real, test-controlled asynchronous suspension/resumption rather than a canned return
///     value.
/// </summary>
/// <remarks>
///     Every signaling member below (<see cref="PushResult"/>, <see cref="Complete"/>,
///     <see cref="Fault"/>) completes synchronously on the calling thread: <see cref="GetResultsAsync"/>'s
///     enumerator suspends on an unconfigured <see cref="TaskCompletionSource{TResult}"/>, whose
///     default continuation behavior runs inline on whichever thread signals it - so a test can
///     push a result and immediately assert the ViewModel observed it, exactly like the old
///     <c>Raise.Event</c> pattern this double replaces.
/// </remarks>
public sealed class FakeRecognitionSession : IRecognitionSession
{
    /// <summary>The gate guarding every mutable field below.</summary>
    private readonly object _gate = new();

    /// <summary>Results pushed by a test but not yet consumed by an active enumeration.</summary>
    private readonly List<SpeechRecognitionEvent> _pending = [];

    /// <summary>The signal an idle enumerator is awaiting, if one is suspended.</summary>
    private TaskCompletionSource<bool>? _signal;

    /// <summary>Set once <see cref="Complete"/> has been called.</summary>
    private bool _completed;

    /// <summary>Set once <see cref="Fault"/> has been called.</summary>
    private Exception? _fault;

    /// <inheritdoc/>
    public bool IsAvailable { get; set; } = true;

    /// <inheritdoc/>
    public RecognitionSessionState State { get; private set; } = RecognitionSessionState.Created;

    /// <inheritdoc/>
    public event EventHandler<SessionStateChangedEventArgs>? StateChanged;

    /// <summary>Gets the number of times <see cref="StartAsync"/> was called.</summary>
    public int StartCallCount { get; private set; }

    /// <summary>Gets the number of times <see cref="StopAsync"/> was called.</summary>
    public int StopCallCount { get; private set; }

    /// <summary>Gets the number of times <see cref="DisposeAsync"/> was called.</summary>
    public int DisposeCallCount { get; private set; }

    /// <summary>Gets or sets the exception <see cref="StartAsync"/> throws, if any.</summary>
    public Exception? StartException { get; set; }

    /// <summary>
    ///     Gets or sets a callback invoked at the start of <see cref="StopAsync"/>, letting a
    ///     test observe exactly when a Stop was requested (for example, to flip a "still
    ///     listening" flag a fake device service checks).
    /// </summary>
    public Action? OnStopRequested { get; set; }

    /// <summary>
    ///     Raises <see cref="StateChanged"/>, moving <see cref="State"/> to <paramref name="current"/>.
    /// </summary>
    /// <param name="current">The state to transition to.</param>
    public void RaiseStateChanged(RecognitionSessionState current)
    {
        var previous = State;
        State = current;
        StateChanged?.Invoke(this, new SessionStateChangedEventArgs(previous, current));
    }

    /// <summary>
    ///     Pushes one result for a suspended or future <see cref="GetResultsAsync"/> enumeration
    ///     to observe.
    /// </summary>
    /// <param name="result">The result to push.</param>
    public void PushResult(SpeechRecognitionResult result)
    {
        lock (_gate)
        {
            _pending.Add(new SpeechRecognitionEvent(result));
            var signal = _signal;
            _signal = null;
            signal?.TrySetResult(true);
        }
    }

    /// <summary>Ends a <see cref="GetResultsAsync"/> enumeration cleanly, as a real Stop would.</summary>
    public void Complete()
    {
        lock (_gate)
        {
            _completed = true;
            var signal = _signal;
            _signal = null;
            signal?.TrySetResult(true);
        }
    }

    /// <summary>Faults a <see cref="GetResultsAsync"/> enumeration with the given exception.</summary>
    /// <param name="exception">The exception the enumerator should throw.</param>
    public void Fault(Exception exception)
    {
        lock (_gate)
        {
            _fault = exception;
            var signal = _signal;
            _signal = null;
            signal?.TrySetResult(true);
        }
    }

    /// <inheritdoc/>
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        StartCallCount++;

        if (StartException is not null)
        {
            throw StartException;
        }

        RaiseStateChanged(RecognitionSessionState.Running);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        StopCallCount++;
        OnStopRequested?.Invoke();
        RaiseStateChanged(RecognitionSessionState.Stopped);
        Complete();
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<SpeechRecognitionEvent> GetResultsAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (!IsAvailable)
        {
            throw new SpeechRecognizerUnavailableException("The fake recognition session is unavailable.");
        }

        while (true)
        {
            SpeechRecognitionEvent? next;
            Exception? fault;
            bool completed;
            Task<bool>? wait;

            lock (_gate)
            {
                next = _pending.Count > 0 ? _pending[0] : null;
                if (next is not null)
                {
                    _pending.RemoveAt(0);
                }

                fault = _fault;
                completed = _completed;
                wait = null;

                if (next is null && fault is null && !completed)
                {
                    _signal = new TaskCompletionSource<bool>();
                    wait = _signal.Task;
                }
            }

            if (next is not null)
            {
                yield return next;
                continue;
            }

            if (fault is not null)
            {
                throw fault;
            }

            if (completed)
            {
                yield break;
            }

            await wait!.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        DisposeCallCount++;
        return ValueTask.CompletedTask;
    }
}
