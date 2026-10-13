using System.Runtime.CompilerServices;

namespace DemaConsulting.Speech.RecognitionSubsystem;

/// <summary>
///     Internal two-tier backpressure buffer feeding <see cref="RecognitionSession.GetResultsAsync"/>:
///     one overwritable "latest provisional" slot plus an unbounded FIFO of final results.
/// </summary>
/// <remarks>
///     A provisional result that arrives before the consumer has read the previous one simply
///     overwrites it (coalescing is the documented common case), while final results are queued
///     and are never coalesced or dropped. A provisional is also superseded (never delivered)
///     once its own final is buffered, so a consumer never sees a stale partial transcript
///     trailing the final result for the same utterance. Neither tier ever blocks the pump
///     thread that calls <see cref="AddResult"/>: the decode/flush loop must never wait on a
///     slow consumer. Final results are small and arrive at utterance boundaries, so retaining
///     all of them is bounded in practice by the session length.
/// </remarks>
internal sealed class RecognitionResultBuffer
{
    /// <summary>Guards every field below.</summary>
    private readonly object _lock = new();

    /// <summary>The FIFO of buffered final results; never trimmed.</summary>
    private readonly Queue<SpeechRecognitionEvent> _finals = new();

    /// <summary>The single overwritable "latest provisional" slot, or <see langword="null"/> when empty.</summary>
    private SpeechRecognitionEvent? _provisional;

    /// <summary>Whether no further results will ever be added.</summary>
    private bool _completed;

    /// <summary>The cause of a session fault surfaced to an active enumerator, if any.</summary>
    private Exception? _fault;

    /// <summary>Signaled whenever new data, completion, or a fault becomes available to a waiting consumer.</summary>
    private TaskCompletionSource<bool> _signal = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    ///     Adds one recognition result to the buffer: a provisional result overwrites any
    ///     previously unread provisional result, while a final result is queued and also
    ///     supersedes (clears) any not-yet-consumed provisional result still buffered.
    /// </summary>
    /// <param name="result">The result to buffer. Must not be null.</param>
    internal void AddResult(SpeechRecognitionEvent result)
    {
        ArgumentNullException.ThrowIfNull(result);

        TaskCompletionSource<bool> toSignal;
        lock (_lock)
        {
            if (_completed || _fault is not null)
            {
                return;
            }

            if (result.Result.IsFinal)
            {
                // A final result supersedes any not-yet-consumed provisional: DequeueNext()
                // always drains queued finals before the provisional slot, so an unconsumed
                // provisional left in place here would otherwise be delivered after its own
                // final - a stale, already-superseded partial transcript trailing the final
                // result for the same utterance. Clearing it here means a consumer only ever
                // sees the provisional if it reads before the final arrives, never after.
                _provisional = null;
                _finals.Enqueue(result);
            }
            else
            {
                _provisional = result;
            }

            toSignal = _signal;
            _signal = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        toSignal.TrySetResult(true);
    }

    /// <summary>
    ///     Marks the buffer complete: every already-buffered result is still delivered, but no
    ///     further result will ever be added, and an active enumeration ends once it is drained.
    /// </summary>
    internal void Complete()
    {
        TaskCompletionSource<bool> toSignal;
        lock (_lock)
        {
            _completed = true;
            toSignal = _signal;
        }

        toSignal.TrySetResult(true);
    }

    /// <summary>
    ///     Marks the buffer faulted: an active or future enumeration throws
    ///     <see cref="RecognitionSessionFaultedException"/> wrapping <paramref name="cause"/>
    ///     once every already-buffered result has been delivered.
    /// </summary>
    /// <param name="cause">The exception describing why the owning session faulted. Must not be null.</param>
    internal void Fault(Exception cause)
    {
        ArgumentNullException.ThrowIfNull(cause);

        TaskCompletionSource<bool> toSignal;
        lock (_lock)
        {
            _fault ??= cause;
            toSignal = _signal;
        }

        toSignal.TrySetResult(true);
    }

    /// <summary>
    ///     Streams every buffered result in order, waiting for new data as needed, until the
    ///     buffer is completed or faulted.
    /// </summary>
    /// <param name="cancellationToken">A token that ends only this enumeration when canceled.</param>
    internal async IAsyncEnumerable<SpeechRecognitionEvent> ReadAllAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        while (true)
        {
            SpeechRecognitionEvent? next;
            bool completed;
            Exception? fault;
            Task waitTask;
            lock (_lock)
            {
                next = DequeueNext();
                completed = _completed;
                fault = _fault;
                waitTask = _signal.Task;
            }

            if (next is not null)
            {
                yield return next;
                continue;
            }

            if (fault is not null)
            {
                throw new RecognitionSessionFaultedException(
                    "The recognition session faulted while its results were being enumerated.",
                    fault);
            }

            if (completed)
            {
                yield break;
            }

            await waitTask.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Removes and returns the next buffered result (a queued final takes priority over the
    ///     provisional slot), or <see langword="null"/> when nothing is buffered. Must be called
    ///     under <see cref="_lock"/>.
    /// </summary>
    private SpeechRecognitionEvent? DequeueNext()
    {
        if (_finals.Count > 0)
        {
            return _finals.Dequeue();
        }

        if (_provisional is not null)
        {
            var next = _provisional;
            _provisional = null;
            return next;
        }

        return null;
    }
}
