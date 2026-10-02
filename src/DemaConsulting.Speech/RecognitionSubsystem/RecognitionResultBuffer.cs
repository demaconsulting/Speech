using System.Runtime.CompilerServices;
using DemaConsulting.Speech.Diagnostics;

namespace DemaConsulting.Speech.RecognitionSubsystem;

/// <summary>
///     Internal two-tier backpressure buffer feeding <see cref="SherpaOnnxRecognitionSession.GetResultsAsync"/>:
///     one overwritable "latest provisional" slot plus a byte-capped FIFO of final results.
/// </summary>
/// <remarks>
///     Implements Decision #5 of the engine/session async redesign: a provisional result that
///     arrives before the consumer has read the previous one simply overwrites it (coalescing is
///     the documented common case and costs no allocation beyond the record itself), while final
///     results are queued and never silently coalesced - they are capped by total UTF-16 byte size
///     of their buffered <see cref="SpeechRecognitionResult.Text"/> (64 KiB) rather than by count,
///     and the oldest unread final is evicted only as a last-resort safety valve, reported through
///     <see cref="ISpeechDiagnostics"/> at <see cref="SpeechDiagnosticLevel.Warning"/>. A
///     provisional is also superseded (never delivered) once its own final is buffered, so a
///     consumer never sees a stale partial transcript trailing the final result for the same
///     utterance. Neither tier ever blocks the pump thread that calls <see cref="AddResult"/>: the
///     native decode/flush loop must never wait on a slow consumer.
/// </remarks>
internal sealed class RecognitionResultBuffer
{
    /// <summary>The maximum total UTF-16 byte size of buffered final results' <see cref="SpeechRecognitionResult.Text"/>.</summary>
    private const long MaxFinalResultBytes = 64 * 1024;

    /// <summary>Guards every field below.</summary>
    private readonly object _lock = new();

    /// <summary>The FIFO of buffered final results, capped by <see cref="MaxFinalResultBytes"/>.</summary>
    private readonly Queue<SpeechRecognitionEvent> _finals = new();

    /// <summary>The sink to report a last-resort final-result eviction to.</summary>
    private readonly ISpeechDiagnostics _diagnostics;

    /// <summary>The diagnostics category used for reported events.</summary>
    private readonly string _diagnosticsCategory;

    /// <summary>The total UTF-16 byte size of every result currently queued in <see cref="_finals"/>.</summary>
    private long _finalBytes;

    /// <summary>The single overwritable "latest provisional" slot, or <see langword="null"/> when empty.</summary>
    private SpeechRecognitionEvent? _provisional;

    /// <summary>Whether no further results will ever be added.</summary>
    private bool _completed;

    /// <summary>The cause of a session fault surfaced to an active enumerator, if any.</summary>
    private Exception? _fault;

    /// <summary>Signaled whenever new data, completion, or a fault becomes available to a waiting consumer.</summary>
    private TaskCompletionSource<bool> _signal = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    ///     Initializes a new instance of the <see cref="RecognitionResultBuffer"/> class.
    /// </summary>
    /// <param name="diagnostics">The sink to report last-resort eviction to. Must not be null.</param>
    /// <param name="diagnosticsCategory">The diagnostics category used for reported events.</param>
    internal RecognitionResultBuffer(ISpeechDiagnostics diagnostics, string diagnosticsCategory)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);

        _diagnostics = diagnostics;
        _diagnosticsCategory = diagnosticsCategory;
    }

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
                EnqueueFinal(result);
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
    /// <param name="cancellationToken">A token that ends only this enumeration when cancelled.</param>
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
            var next = _finals.Dequeue();
            _finalBytes -= ByteSize(next);
            return next;
        }

        if (_provisional is not null)
        {
            var next = _provisional;
            _provisional = null;
            return next;
        }

        return null;
    }

    /// <summary>
    ///     Queues a final result, evicting the oldest buffered final as a last resort if doing so
    ///     is the only way to respect <see cref="MaxFinalResultBytes"/>. Must be called under
    ///     <see cref="_lock"/>.
    /// </summary>
    private void EnqueueFinal(SpeechRecognitionEvent result)
    {
        _finals.Enqueue(result);
        _finalBytes += ByteSize(result);

        while (_finalBytes > MaxFinalResultBytes && _finals.Count > 1)
        {
            var dropped = _finals.Dequeue();
            _finalBytes -= ByteSize(dropped);

            _diagnostics.Report(
                SpeechDiagnosticLevel.Warning,
                _diagnosticsCategory,
                "A buffered final recognition result was evicted because the consumer of GetResultsAsync has " +
                "not kept up and the final-result backlog exceeded its byte cap; this is a last-resort safety " +
                "valve, not a routine occurrence.");
        }
    }

    /// <summary>Computes the UTF-16 byte size of a result's <see cref="SpeechRecognitionResult.Text"/>.</summary>
    private static long ByteSize(SpeechRecognitionEvent result) => result.Result.Text.Length * 2L;
}
