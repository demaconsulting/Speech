using System.Collections.Concurrent;
using DemaConsulting.Speech.AudioSubsystem;

namespace DemaConsulting.Speech.Demo.RecognitionPanelSubsystem;

/// <summary>
///     TEMPORARY diagnostic instrumentation added to investigate a reported live-microphone bug:
///     a long mid-sentence pause during streaming recognition sometimes causes the first word(s)
///     spoken right after the pause to be dropped. Opt-in and off by default; this is internal
///     investigation tooling, not a permanent public feature - remove once the investigation
///     concludes.
/// </summary>
/// <remarks>
///     When enabled (see <see cref="EnvironmentVariableName"/>), this subscribes its own,
///     entirely independent handler to the same <see cref="IAudioCaptureDevice.FrameCaptured"/>
///     event the real recognizer subscribes to, and writes every captured frame - in the
///     capture device's own native sample rate and channel count, before any resampling the
///     recognizer applies - to a session-scoped <c>.wav</c> file. This lets a user reproduce the
///     bug live and hand over the exact audio that caused it for offline analysis, replaying it
///     through the real engine exactly as a recorded reproduction file would be.
///     <para>
///     Writing to disk happens on a dedicated writer thread via a producer/consumer queue so
///     a slow or failing disk can never block or throw into the audio capture callback; any
///     writer failure is caught, logged, and otherwise ignored so live recognition is unaffected.
///     </para>
///     <para>
///     The queue is bounded (<see cref="DefaultQueueCapacity"/> frames by default). When a slow
///     or stalled writer lets it fill, the capture callback never waits: the newest frame is
///     dropped and counted in <see cref="DroppedFrameCount"/> (reported once when the
///     recording is finalized), so memory use stays bounded and the recording simply has a
///     gap - consistent with the rule that this diagnostic can never affect live recognition.
///     </para>
/// </remarks>
internal sealed class CaptureDebugRecorder : IDisposable
{
    /// <summary>
    ///     The environment variable naming the directory to write capture debug <c>.wav</c> files
    ///     to. Recording is disabled entirely when this variable is unset or empty.
    /// </summary>
    public const string EnvironmentVariableName = "DEMASPEECH_CAPTURE_DEBUG_DIR";

    /// <summary>The capture device this recorder is independently listening to.</summary>
    private readonly IAudioCaptureDevice _device;

    /// <summary>
    ///     The default maximum number of captured frames waiting for the writer thread. Capture
    ///     frames are typically tens of milliseconds long, so this tolerates a writer stall of
    ///     tens of seconds before frames start being dropped.
    /// </summary>
    internal const int DefaultQueueCapacity = 2000;

    /// <summary>The bounded queue handing captured frames from the audio callback to the writer thread.</summary>
    private readonly BlockingCollection<IReadOnlyList<float>> _queue;

    /// <summary>The maximum time <see cref="Dispose"/> waits for the writer thread to drain.</summary>
    private readonly TimeSpan _drainTimeout;

    /// <summary>The number of frames discarded because the queue was full.</summary>
    private long _droppedFrames;

    /// <summary>The thread that owns the writer and performs all file I/O.</summary>
    private readonly Thread _writerThread;

    /// <summary>Appends one frame of samples to the output; used exclusively by the writer thread (or a timed-out <see cref="Dispose"/>).</summary>
    private readonly Action<IReadOnlyList<float>> _writeSamples;

    /// <summary>Finalizes and closes the output; called at most once, under <see cref="_writerLock"/>.</summary>
    private readonly Action _closeWriter;

    /// <summary>
    ///     Guards every access to the writer so <see cref="WriterThreadMain"/> and a
    ///     timed-out <see cref="Dispose"/> can never touch it concurrently - whichever of the two
    ///     acquires this lock first is the one that finalizes the file (see both methods' own
    ///     remarks).
    /// </summary>
    private readonly object _writerLock = new();

    /// <summary>
    ///     Set, under <see cref="_writerLock"/>, by whichever of <see cref="WriterThreadMain"/> or
    ///     <see cref="Dispose"/> disposes the writer first. Once set, the other backs
    ///     off instead of touching the now-disposed writer.
    /// </summary>
    private bool _writerFinalized;

    /// <summary>Guards <see cref="Dispose"/> against running more than once (1 once disposal has started).</summary>
    private int _disposed;

    /// <summary>
    ///     The maximum time <see cref="Dispose"/> waits for the writer thread to drain queued
    ///     frames before giving up. Generous because this is off the critical recognition path,
    ///     but bounded so a stalled disk cannot hang the caller (typically a UI-thread command)
    ///     indefinitely.
    /// </summary>
    private static readonly TimeSpan DefaultDrainTimeout = TimeSpan.FromSeconds(10);

    /// <summary>
    ///     Initializes a new instance of the <see cref="CaptureDebugRecorder"/> class and starts
    ///     its writer thread. Private: use <see cref="TryStart"/>, which owns the opt-in check
    ///     and failure handling this constructor assumes has already happened.
    /// </summary>
    /// <param name="device">The capture device to independently listen to.</param>
    /// <param name="writer">The already-created writer to append captured frames to.</param>
    private CaptureDebugRecorder(IAudioCaptureDevice device, WavFileWriter writer)
        : this(device, writer.WriteSamples, writer.Dispose)
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="CaptureDebugRecorder"/> class from write and close
    ///     delegates and starts its writer thread. Internal so tests can substitute a failing output.
    /// </summary>
    /// <param name="device">The capture device to independently listen to.</param>
    /// <param name="writeSamples">Appends one captured frame to the output.</param>
    /// <param name="closeWriter">Finalizes and closes the output; invoked once.</param>
    /// <param name="queueCapacity">The maximum number of frames waiting for the writer; must be positive.</param>
    /// <param name="drainTimeout">
    ///     The maximum time <see cref="Dispose"/> waits for the writer thread, or <see langword="null"/> for the
    ///     default.
    /// </param>
    internal CaptureDebugRecorder(
        IAudioCaptureDevice device,
        Action<IReadOnlyList<float>> writeSamples,
        Action closeWriter,
        int queueCapacity = DefaultQueueCapacity,
        TimeSpan? drainTimeout = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(queueCapacity);

        _queue = new BlockingCollection<IReadOnlyList<float>>(queueCapacity);
        _drainTimeout = drainTimeout ?? DefaultDrainTimeout;
        _device = device;
        _writeSamples = writeSamples;
        _closeWriter = closeWriter;

        _writerThread = new Thread(WriterThreadMain)
        {
            // A background thread: correctness no longer depends on the CLR waiting for this
            // thread at process exit, because Dispose() itself force-finalizes the header (see
            // its own remarks) if the writer thread hasn't drained within _drainTimeout. Keeping
            // this a background thread instead means a stalled disk can never block process
            // shutdown, regardless of how long the stall lasts.
            IsBackground = true,
            Name = "CaptureDebugRecorder"
        };
        _writerThread.Start();

        _device.FrameCaptured += OnFrameCaptured;
    }

    /// <summary>
    ///     Starts a new capture debug recording session for <paramref name="captureDevice"/> when
    ///     <see cref="EnvironmentVariableName"/> is set to a usable directory path, or does
    ///     nothing when it is unset or empty.
    /// </summary>
    /// <param name="captureDevice">
    ///     The same capture device instance the real recognizer is about to listen through. Must
    ///     not be <see langword="null"/>.
    /// </param>
    /// <returns>
    ///     A disposable recorder to stop and finalize the file when the caller stops listening,
    ///     or <see langword="null"/> when recording is disabled or could not be started (for
    ///     example, an inaccessible directory), in which case the failure is logged to the
    ///     console and live recognition proceeds unaffected.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="captureDevice"/> is <see langword="null"/>.</exception>
    public static CaptureDebugRecorder? TryStart(IAudioCaptureDevice captureDevice)
    {
        ArgumentNullException.ThrowIfNull(captureDevice);

        var directory = Environment.GetEnvironmentVariable(EnvironmentVariableName);
        if (string.IsNullOrWhiteSpace(directory))
        {
            return null;
        }

        try
        {
            Directory.CreateDirectory(directory);

            var suffix = Guid.NewGuid().ToString("N")[..6];
            var fileName = $"capture-{DateTime.Now:yyyyMMdd-HHmmss}-{suffix}.wav";
            var path = Path.Join(directory, fileName);

            var writer = new WavFileWriter(path, captureDevice.SampleRate, captureDevice.ChannelCount);

            Console.WriteLine($"[CaptureDebug] Recording raw capture audio to: {path}");

            return new CaptureDebugRecorder(captureDevice, writer);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            // A diagnostic recorder that cannot start must never prevent normal recognition from
            // starting - report the failure and continue as if recording had never been requested
            Console.WriteLine($"[CaptureDebug] Failed to start capture debug recording: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    ///     Gets the number of captured frames discarded because the writer could not keep up and
    ///     the bounded queue was full.
    /// </summary>
    internal long DroppedFrameCount => Interlocked.Read(ref _droppedFrames);

    /// <summary>
    ///     Enqueues one captured frame for the writer thread. Runs on the capture device's own
    ///     callback thread, so this must never block or throw.
    /// </summary>
    /// <param name="sender">The raising capture device. Unused.</param>
    /// <param name="e">The event carrying the captured samples.</param>
    private void OnFrameCaptured(object? sender, AudioCaptureFrameEventArgs e)
    {
        try
        {
            // Copy the samples: the event argument is not guaranteed to outlive this handler, and
            // the writer thread will read this list at an arbitrary later time
            // When the queue is full the frame is dropped and counted: the callback must never wait.
            if (_queue.Count >= _queue.BoundedCapacity || !_queue.TryAdd(e.Samples.ToArray()))
            {
                Interlocked.Increment(ref _droppedFrames);
            }
        }
        catch (InvalidOperationException)
        {
            // The queue has already been marked complete (by Dispose or a failed writer) or disposed
            // (ObjectDisposedException derives from InvalidOperationException) - a frame arriving
            // after that is simply discarded rather than treated as an error, never thrown into
            // the audio callback
        }
    }

    /// <summary>
    ///     Drains queued frames to the writer until <see cref="Dispose"/> completes the queue, or
    ///     until a timed-out <see cref="Dispose"/> force-finalizes the file first. Runs entirely
    ///     on <see cref="_writerThread"/> so file I/O never touches the audio callback thread.
    /// </summary>
    private void WriterThreadMain()
    {
        try
        {
            foreach (var frame in _queue.GetConsumingEnumerable())
            {
                lock (_writerLock)
                {
                    // Dispose() may have already force-finalized (and disposed) the writer after
                    // giving up waiting for this thread - any frame still arriving after that is
                    // discarded rather than written to a now-disposed writer.
                    if (_writerFinalized)
                    {
                        break;
                    }

                    _writeSamples(frame);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ObjectDisposedException
            or NotSupportedException or InvalidOperationException or ArgumentException)
        {
            // A failing disk (or an unexpectedly closed writer) during an in-progress recording must
            // not crash the writer thread or propagate anywhere near the audio pipeline - stop
            // accepting frames (so the queue cannot grow without bound) and report why
            Console.WriteLine($"[CaptureDebug] Capture debug write failed: {ex.Message}");
            CompleteQueueQuietly();
        }
        finally
        {
            lock (_writerLock)
            {
                if (!_writerFinalized)
                {
                    _writerFinalized = true;
                    CloseWriterQuietly();
                }
            }
        }
    }

    /// <summary>
    ///     Closes the output, containing any failure: this runs on a background thread (or in a
    ///     diagnostic Dispose), where an escaping exception would terminate the process.
    /// </summary>
    private void CloseWriterQuietly()
    {
        try
        {
            _closeWriter();
        }
#pragma warning disable CA1031 // Diagnostic tooling must never fault the host; the failure is reported instead.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            Console.WriteLine($"[CaptureDebug] Failed to finalize the capture debug file: {ex.Message}");
        }
    }

    /// <summary>Marks the queue complete so later frames are rejected, ignoring a queue that is already disposed.</summary>
    private void CompleteQueueQuietly()
    {
        try
        {
            _queue.CompleteAdding();
        }
        catch (ObjectDisposedException)
        {
            // Already disposed by Dispose; nothing left to complete
        }
    }

    /// <summary>
    ///     Stops listening for captured frames, waits (up to <see cref="_drainTimeout"/>) for the
    ///     writer thread to fully drain any queued frames, and finalizes the <c>.wav</c> file
    ///     header. Idempotent: a second call does nothing.
    /// </summary>
    /// <remarks>
    ///     This is diagnostic/debug instrumentation off the critical recognition path, and
    ///     <see cref="Dispose"/> is typically invoked synchronously from a UI-thread command, so
    ///     the wait below is bounded rather than infinite: <see cref="_queue"/> is completed just
    ///     above, guaranteeing <see cref="WriterThreadMain"/> will eventually observe
    ///     <see cref="BlockingCollection{T}.GetConsumingEnumerable()"/> ending and exit, but a
    ///     stalled disk (a disconnected network share, a failing drive, and so on) could otherwise
    ///     block that exit indefinitely and hang the caller. When the writer thread finishes
    ///     within the timeout, it has already disposed the writer itself (see
    ///     <see cref="WriterThreadMain"/>'s own remarks) and the now-idle queue is disposed here
    ///     too. When it does not finish in time, this method attempts to force-finalize the
    ///     header itself with whatever samples were already written - synchronized against
    ///     <see cref="WriterThreadMain"/> via <see cref="_writerLock"/> so the two can never
    ///     touch the writer concurrently - so the file is normally left as a valid,
    ///     playable <c>.wav</c> rather than one with a never-finalized header. That attempt itself
    ///     uses a short, separately-bounded <see cref="Monitor.TryEnter(object, TimeSpan)"/>
    ///     rather than a blocking <see langword="lock"/>, because the one case this cannot solve
    ///     is the writer thread being blocked *inside* a single <c>WriteSamples</c> call on a
    ///     genuinely stalled disk while already holding <see cref="_writerLock"/> - a blocking
    ///     <see langword="lock"/> here would wait for that call to return, which could once again
    ///     be indefinite. In that one pathological case, this method still returns promptly
    ///     (never waiting longer than <see cref="_drainTimeout"/> plus that short attempt) but the
    ///     header is left never finalized, the same fundamentally unavoidable trade-off as leaving a
    ///     synchronous file API call unable to be canceled. The queue itself is intentionally
    ///     left undisposed whenever the writer thread has not confirmed it is done, since it may
    ///     still be between <see cref="BlockingCollection{T}.GetConsumingEnumerable()"/>
    ///     iterations, and is instead left to the garbage collector.
    /// </remarks>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _device.FrameCaptured -= OnFrameCaptured;
        CompleteQueueQuietly();

        if (DroppedFrameCount > 0)
        {
            Console.WriteLine(
                $"[CaptureDebug] The writer could not keep up; {DroppedFrameCount} captured frame(s) " +
                "were dropped from the recording.");
        }

        if (_writerThread.Join(_drainTimeout))
        {
            _queue.Dispose();
            return;
        }

        if (!Monitor.TryEnter(_writerLock, TimeSpan.FromSeconds(1)))
        {
            Console.WriteLine(
                "[CaptureDebug] Writer thread did not drain within the timeout and is still " +
                "busy; leaving the file with a never-finalized header rather than blocking further.");
            return;
        }

        try
        {
            if (_writerFinalized)
            {
                return;
            }

            _writerFinalized = true;
            CloseWriterQuietly();
        }
        finally
        {
            Monitor.Exit(_writerLock);
        }

        Console.WriteLine(
            "[CaptureDebug] Writer thread did not drain within the timeout; finalized the file " +
            "with the samples captured so far and abandoned the remaining queued frames.");
    }
}
