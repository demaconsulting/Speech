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
///     Writing to disk happens on a dedicated background thread via a producer/consumer queue so
///     a slow or failing disk can never block or throw into the audio capture callback; any
///     writer failure is caught, logged, and otherwise ignored so live recognition is unaffected.
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

    /// <summary>The queue handing captured frames from the audio callback to the writer thread.</summary>
    private readonly BlockingCollection<IReadOnlyList<float>> _queue = [];

    /// <summary>The background thread that owns the writer and performs all file I/O.</summary>
    private readonly Thread _writerThread;

    /// <summary>The writer used exclusively by <see cref="_writerThread"/>.</summary>
    private readonly WavFileWriter _writer;

    /// <summary>
    ///     The maximum time <see cref="Dispose"/> waits for the writer thread to drain queued
    ///     frames before giving up. Generous because this is off the critical recognition path,
    ///     but bounded so a stalled disk cannot hang the caller (typically a UI-thread command)
    ///     indefinitely.
    /// </summary>
    private static readonly TimeSpan DrainTimeout = TimeSpan.FromSeconds(10);

    /// <summary>
    ///     Initializes a new instance of the <see cref="CaptureDebugRecorder"/> class and starts
    ///     its background writer thread. Private: use <see cref="TryStart"/>, which owns the
    ///     opt-in check and failure handling this constructor assumes has already happened.
    /// </summary>
    /// <param name="device">The capture device to independently listen to.</param>
    /// <param name="writer">The already-created writer to append captured frames to.</param>
    private CaptureDebugRecorder(IAudioCaptureDevice device, WavFileWriter writer)
    {
        _device = device;
        _writer = writer;

        _writerThread = new Thread(WriterThreadMain)
        {
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
    ///     Enqueues one captured frame for the background writer thread. Runs on the capture
    ///     device's own callback thread, so this must never block or throw.
    /// </summary>
    /// <param name="sender">The raising capture device. Unused.</param>
    /// <param name="e">The event carrying the captured samples.</param>
    private void OnFrameCaptured(object? sender, AudioCaptureFrameEventArgs e)
    {
        try
        {
            // Copy the samples: the event argument is not guaranteed to outlive this handler, and
            // the writer thread will read this list at an arbitrary later time
            _queue.Add(e.Samples.ToArray());
        }
        catch (InvalidOperationException)
        {
            // The queue has already been marked complete by Dispose - a frame arriving after Stop
            // was requested is simply discarded rather than treated as an error
        }
    }

    /// <summary>
    ///     Drains queued frames to the writer until <see cref="Dispose"/> completes the queue.
    ///     Runs entirely on <see cref="_writerThread"/> so file I/O never touches the audio
    ///     callback thread.
    /// </summary>
    private void WriterThreadMain()
    {
        try
        {
            foreach (var frame in _queue.GetConsumingEnumerable())
            {
                _writer.WriteSamples(frame);
            }
        }
        catch (IOException ex)
        {
            // A failing disk during an in-progress recording must not crash the writer thread or
            // propagate anywhere near the audio pipeline - just stop writing and report why
            Console.WriteLine($"[CaptureDebug] Capture debug write failed: {ex.Message}");
        }
        finally
        {
            // Dispose the writer here, on the thread that exclusively owns it, rather than in
            // Dispose(): WavFileWriter only patches its RIFF/data chunk sizes on Dispose, so if
            // Dispose() gave up waiting (see DrainTimeout below) and never disposed the writer,
            // the file's header would never be finalized. Disposing here guarantees the header
            // is always finalized once this thread's queue is drained, whether or not the caller
            // was still waiting for it.
            _writer.Dispose();
        }
    }

    /// <summary>
    ///     Stops listening for captured frames, waits (up to <see cref="DrainTimeout"/>) for the
    ///     background writer thread to fully drain any queued frames, and finalizes the
    ///     <c>.wav</c> file header.
    /// </summary>
    /// <remarks>
    ///     This is diagnostic/debug instrumentation off the critical recognition path, and
    ///     <see cref="Dispose"/> is typically invoked synchronously from a UI-thread command, so
    ///     the wait below is bounded rather than infinite: <see cref="_queue"/> is completed just
    ///     above, guaranteeing <see cref="WriterThreadMain"/> will eventually observe
    ///     <see cref="BlockingCollection{T}.GetConsumingEnumerable()"/> ending and exit, but a
    ///     stalled disk (a disconnected network share, a failing drive, and so on) could otherwise
    ///     block that exit indefinitely and hang the caller. The writer itself is always disposed
    ///     (and its <c>.wav</c> header finalized) by <see cref="WriterThreadMain"/>'s own
    ///     <see langword="finally"/> block, not here, so the file is finalized correctly whether
    ///     or not this method's wait times out. When the writer thread finishes within the
    ///     timeout, the now-idle queue is also disposed here. When it does not, disposing the
    ///     queue here would race with the writer thread's still-in-flight
    ///     <see cref="BlockingCollection{T}.GetConsumingEnumerable()"/> enumeration, so instead
    ///     this logs a diagnostic and intentionally leaves the queue to be finalized by the
    ///     garbage collector, at the cost of a leaked queue handle in that rare pathological case.
    /// </remarks>
    public void Dispose()
    {
        _device.FrameCaptured -= OnFrameCaptured;
        _queue.CompleteAdding();

        if (_writerThread.Join(DrainTimeout))
        {
            _queue.Dispose();
        }
        else
        {
            Console.WriteLine(
                "[CaptureDebug] Writer thread did not drain within the timeout; leaving it to " +
                "finish and finalize the file in the background.");
        }
    }
}
