using DemaConsulting.Speech.AudioSubsystem;
using DemaConsulting.Speech.Diagnostics;
using DemaConsulting.Speech.ModelManagementSubsystem;

namespace DemaConsulting.Speech.RecognitionSubsystem;

/// <summary>
///     Real <see cref="ISpeechRecognizerEngine"/> implementation holding one loaded, "hot"
///     sherpa-onnx recognition backend and enforcing single-session exclusivity over it.
/// </summary>
/// <remarks>
///     Loading the backend into native memory is the expensive step <see cref="SpeechRecognizerFactory"/>
///     performs once; this engine then reuses that same backend across as many sequential
///     sessions as a host creates, which is why only one session may be active at a time
///     (<see cref="CreateSessionAsync"/> fails fast with <see cref="RecognitionEngineBusyException"/>
///     rather than queuing - Decision #2). The exclusivity lease spans the full life of a session,
///     through its own <see cref="IAsyncDisposable.DisposeAsync"/> completing, so a caller that
///     wants low-latency reuse should keep this engine instance around for as long as it may
///     recognize speech and create a fresh session per run, rather than reloading the backend per
///     turn.
/// </remarks>
internal sealed class SherpaOnnxSpeechRecognizerEngine : ISpeechRecognizerEngine
{
    /// <summary>The diagnostics category used for every event this engine reports.</summary>
    private const string DiagnosticsCategory = "RecognitionSubsystem";

    /// <summary>Guards access to <see cref="_currentSession"/> and <see cref="_isDisposed"/>.</summary>
    private readonly object _syncRoot = new();

    /// <summary>The loaded, shared backend this engine owns and disposes.</summary>
    private readonly IRecognitionBackend _backend;

    /// <summary>The recognition model owning <see cref="_backend"/>.</summary>
    private readonly IRecognitionModel _model;

    /// <summary>The sink for structural lifecycle and fault events.</summary>
    private readonly ISpeechDiagnostics _diagnostics;

    /// <summary>The single-session exclusivity lease over <see cref="_backend"/> (Decision #2).</summary>
    private readonly SemaphoreSlim _lease = new(1, 1);

    /// <summary>The currently active session, if any, so <see cref="DisposeAsync"/> can dispose it first.</summary>
    private IRecognitionSession? _currentSession;

    /// <summary>Whether this engine has already been disposed.</summary>
    private bool _isDisposed;

    /// <summary>
    ///     Initializes a new instance of the <see cref="SherpaOnnxSpeechRecognizerEngine"/> class
    ///     over an already-loaded backend.
    /// </summary>
    /// <param name="backend">The loaded recognition backend this engine owns and disposes. Must not be null.</param>
    /// <param name="model">The recognition model owning <paramref name="backend"/>. Must not be null.</param>
    /// <param name="diagnostics">The sink for structural lifecycle and fault events. Must not be null.</param>
    internal SherpaOnnxSpeechRecognizerEngine(
        IRecognitionBackend backend,
        IRecognitionModel model,
        ISpeechDiagnostics diagnostics)
    {
        ArgumentNullException.ThrowIfNull(backend);
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(diagnostics);

        _backend = backend;
        _model = model;
        _diagnostics = diagnostics;
    }

    /// <inheritdoc/>
    /// <remarks>
    ///     Always <see langword="true"/>: this type is only ever created by
    ///     <see cref="SpeechRecognizerFactory"/> after the backend loaded successfully.
    /// </remarks>
    public bool IsAvailable => true;

    /// <inheritdoc/>
    /// <exception cref="RecognitionEngineBusyException">
    ///     Thrown when a previously created session still holds this engine's exclusivity lease,
    ///     including while that session is still tearing down via its own
    ///     <see cref="IAsyncDisposable.DisposeAsync"/>.
    /// </exception>
    public Task<IRecognitionSession> CreateSessionAsync(
        IAudioCaptureDevice device,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(device);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_syncRoot)
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this);
        }

        // No microphone (or no working audio backend) is an ordinary machine state, exactly like
        // a model not being installed - there is nothing to stream, so return the honest fallback
        // rather than a session doomed to fail the instant it is started.
        if (!device.IsAvailable)
        {
            _diagnostics.Report(
                SpeechDiagnosticLevel.Warning,
                DiagnosticsCategory,
                "Cannot create a recognition session because the supplied capture device is unavailable.");
            return Task.FromResult<IRecognitionSession>(UnavailableRecognitionSession.Instance);
        }

        // Fail fast rather than queue: waiting here would make this call's latency depend on an
        // unrelated session's teardown, with no way to bound that wait distinctly from the
        // caller's own cancellationToken (Decision #2).
        if (!_lease.Wait(0, CancellationToken.None))
        {
            throw new RecognitionEngineBusyException(
                "Cannot create a recognition session: this engine's backend is already leased by another " +
                "active session. Dispose that session before creating a new one.");
        }

        var released = 0;
        void ReleaseLease()
        {
            // Idempotent: DisposeAsync's own best-effort teardown could in principle invoke this
            // more than once on some error paths, and the lease must only ever be released once.
            if (Interlocked.Exchange(ref released, 1) != 0)
            {
                return;
            }

            lock (_syncRoot)
            {
                _currentSession = null;
            }

            _lease.Release();
        }

        var session = new SherpaOnnxRecognitionSession(
            _backend,
            device,
            _model.AudioFormat.SampleRate,
            _model,
            ReleaseLease,
            _diagnostics,
            new DedicatedWorker(diagnostics: _diagnostics, diagnosticsCategory: DiagnosticsCategory));

        lock (_syncRoot)
        {
            _currentSession = session;
        }

        return Task.FromResult<IRecognitionSession>(session);
    }

    /// <summary>Releases resources held by this engine.</summary>
    /// <remarks>
    ///     Disposes the currently active session first (so its teardown completes and the lease
    ///     is released cleanly), then disposes the shared backend. Idempotent: a second call does
    ///     nothing.
    /// </remarks>
    public async ValueTask DisposeAsync()
    {
        IRecognitionSession? session;
        lock (_syncRoot)
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            session = _currentSession;
        }

        if (session is not null)
        {
            try
            {
                await session.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // Intentionally broad: the active session's own teardown must not prevent this
                // engine's backend from being disposed.
                _diagnostics.Report(
                    SpeechDiagnosticLevel.Error,
                    DiagnosticsCategory,
                    $"Failed to dispose the active recognition session while disposing its engine: {ex.Message}");
            }
        }

        _backend.Dispose();
        _lease.Dispose();
    }
}
