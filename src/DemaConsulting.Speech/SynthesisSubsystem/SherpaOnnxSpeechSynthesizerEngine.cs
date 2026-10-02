using DemaConsulting.Speech.AudioSubsystem;
using DemaConsulting.Speech.Diagnostics;
using DemaConsulting.Speech.ModelManagementSubsystem;

namespace DemaConsulting.Speech.SynthesisSubsystem;

/// <summary>
///     Real <see cref="ISpeechSynthesizerEngine"/> implementation holding one loaded
///     <see cref="ISynthesisBackend"/>, enforcing single-session exclusivity, and constructing
///     <see cref="SherpaOnnxSynthesisSession"/> instances bound to a caller-supplied playback
///     device.
/// </summary>
/// <remarks>
///     Exclusivity is enforced with a binary <see cref="SemaphoreSlim"/> acquired (fail-fast, no
///     waiting) in <see cref="CreateSessionAsync"/> and released only once the resulting session's
///     <see cref="IAsyncDisposable.DisposeAsync"/> has fully completed - not merely when an
///     operation stops - so the lease genuinely spans the session's entire life.
/// </remarks>
internal sealed class SherpaOnnxSpeechSynthesizerEngine : ISpeechSynthesizerEngine
{
    /// <summary>The diagnostics category used for every event this engine reports.</summary>
    private const string DiagnosticsCategory = "SynthesisSubsystem";

    /// <summary>The loaded synthesis backend this engine owns, lends to sessions, and disposes.</summary>
    private readonly ISynthesisBackend _backend;

    /// <summary>The model driving text normalization, tag rendering, and parameter conventions.</summary>
    private readonly ISynthesisModel _model;

    /// <summary>The session-level parameter value bag, or <see langword="null"/> when none was supplied.</summary>
    private readonly IReadOnlyDictionary<string, object>? _parameterValues;

    /// <summary>The sink for structural lifecycle and fault events.</summary>
    private readonly ISpeechDiagnostics _diagnostics;

    /// <summary>The exclusivity lease: at most one session may hold it at a time.</summary>
    private readonly SemaphoreSlim _lease = new(1, 1);

    /// <summary>Guards the create-versus-dispose transition and access to <see cref="_activeSession"/>.</summary>
    private readonly object _syncRoot = new();

    /// <summary>Whether <see cref="DisposeAsync"/> has already run.</summary>
    private bool _isDisposed;

    /// <summary>The currently leased session, if any, so <see cref="DisposeAsync"/> can dispose it first.</summary>
    private ISynthesisSession? _activeSession;

    /// <summary>
    ///     Initializes a new instance of the <see cref="SherpaOnnxSpeechSynthesizerEngine"/> class
    ///     over an already-loaded backend.
    /// </summary>
    /// <param name="backend">The loaded synthesis backend this engine owns and disposes. Must not be null.</param>
    /// <param name="model">The model to normalize text and render tags with. Must not be null.</param>
    /// <param name="parameterValues">The session-level parameter value bag, or <see langword="null"/>.</param>
    /// <param name="diagnostics">
    ///     The sink for structural lifecycle and fault events, or <see langword="null"/> to use
    ///     <see cref="NullSpeechDiagnostics.Instance"/>.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="backend"/> or <paramref name="model"/> is null.</exception>
    internal SherpaOnnxSpeechSynthesizerEngine(
        ISynthesisBackend backend,
        ISynthesisModel model,
        IReadOnlyDictionary<string, object>? parameterValues = null,
        ISpeechDiagnostics? diagnostics = null)
    {
        ArgumentNullException.ThrowIfNull(backend);
        ArgumentNullException.ThrowIfNull(model);

        _backend = backend;
        _model = model;
        _parameterValues = parameterValues;
        _diagnostics = diagnostics ?? NullSpeechDiagnostics.Instance;
    }

    /// <inheritdoc/>
    /// <remarks>
    ///     Always <see langword="true"/>: this type is only ever created by
    ///     <see cref="SpeechSynthesizerFactory"/> after the backend loaded successfully, so its
    ///     existence is itself the availability guarantee.
    /// </remarks>
    public bool IsAvailable => true;

    /// <inheritdoc/>
    /// <remarks>
    ///     The disposed check, lease acquisition, session construction, and registration as
    ///     <see cref="_activeSession"/> all happen inside one <see cref="_syncRoot"/> critical
    ///     section - this method does no awaiting, so holding the lock for its entire body is
    ///     safe and closes the race where a concurrent <see cref="DisposeAsync"/> could otherwise
    ///     pass the disposed check, dispose the lease/backend, and let this call continue on to
    ///     acquire a now-disposed lease or hand out a session backed by already-disposed native
    ///     state.
    /// </remarks>
    public Task<ISynthesisSession> CreateSessionAsync(IAudioPlaybackDevice device, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(device);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_syncRoot)
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this);

            if (!_lease.Wait(0, cancellationToken))
            {
                throw new SynthesisEngineBusyException(
                    "Cannot create a synthesis session: this engine's exclusivity lease is already held by another session.");
            }

            SherpaOnnxSynthesisSession session;
            try
            {
                session = new SherpaOnnxSynthesisSession(_backend, device, _model, _parameterValues, _diagnostics, ReleaseLease);
            }
            catch
            {
                // Construction failed before the session could ever release its own lease
                // itself; roll the acquired lease back so a construction failure cannot strand
                // this engine permanently busy.
                _lease.Release();
                throw;
            }

            _activeSession = session;
            return Task.FromResult<ISynthesisSession>(session);
        }
    }

    /// <inheritdoc/>
    public async Task SpeakAsync(IAudioPlaybackDevice device, string text, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(device);
        ArgumentNullException.ThrowIfNull(text);

        await using var session = await CreateSessionAsync(device, cancellationToken).ConfigureAwait(false);
        await session.SpeakAsync(text, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<SynthesizedSpeech>> SynthesizeAsync(string text, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(text);

        await using var session = await CreateSessionAsync(UnavailableAudioPlaybackDevice.Instance, cancellationToken)
            .ConfigureAwait(false);
        return await session.SynthesizeAsync(text, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Releases the exclusivity lease. Invoked once by a leased session's <see cref="IAsyncDisposable.DisposeAsync"/>.</summary>
    private void ReleaseLease()
    {
        lock (_syncRoot)
        {
            _activeSession = null;
        }

        _lease.Release();
    }

    /// <inheritdoc/>
    /// <remarks>
    ///     Disposes any still-active leased session first (best-effort), then disposes the owned
    ///     backend. Idempotent: a second call does nothing.
    /// </remarks>
    public async ValueTask DisposeAsync()
    {
        ISynthesisSession? activeSession;
        lock (_syncRoot)
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            activeSession = _activeSession;
        }

        if (activeSession is not null)
        {
            try
            {
                await activeSession.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // Intentionally broad: disposing an already-leased session during engine teardown
                // is best-effort, and a fault here must not prevent the backend itself from being
                // released.
                _diagnostics.Report(
                    SpeechDiagnosticLevel.Error,
                    DiagnosticsCategory,
                    $"Failed to dispose the active synthesis session while disposing the engine: {ex.Message}");
            }
        }

        _lease.Dispose();
        _backend.Dispose();
    }
}
