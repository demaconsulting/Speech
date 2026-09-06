namespace DemaConsulting.Speech.Demo.Tests.Fakes;

/// <summary>
///     Test-only <see cref="SynchronizationContext"/> that runs every posted callback inline on
///     the calling thread.
/// </summary>
/// <remarks>
///     ViewModels report download progress through <see cref="Progress{T}"/>, which captures the
///     ambient synchronization context so a UI-thread ViewModel is never mutated from a
///     background thread. With no context installed, <see cref="Progress{T}"/> falls back to
///     queueing callbacks on the thread pool, which would make progress assertions racy. This
///     context makes those callbacks deterministic without changing the production code's
///     UI-thread-correct behavior.
/// </remarks>
public sealed class InlineSynchronizationContext : SynchronizationContext
{
    /// <inheritdoc/>
    public override void Post(SendOrPostCallback d, object? state)
    {
        ArgumentNullException.ThrowIfNull(d);

        d(state);
    }

    /// <inheritdoc/>
    public override void Send(SendOrPostCallback d, object? state) => Post(d, state);

    /// <summary>
    ///     Installs an <see cref="InlineSynchronizationContext"/> on the calling thread for the
    ///     lifetime of the returned scope, restoring the previous context on disposal.
    /// </summary>
    /// <returns>A scope that restores the previous synchronization context when disposed.</returns>
    public static IDisposable Install() => new Scope();

    /// <summary>
    ///     Restores the previously installed synchronization context on disposal.
    /// </summary>
    private sealed class Scope : IDisposable
    {
        /// <summary>The context that was installed before this scope replaced it.</summary>
        private readonly SynchronizationContext? _previous = Current;

        /// <summary>
        ///     Initializes a new instance of the <see cref="Scope"/> class, installing the inline
        ///     context.
        /// </summary>
        public Scope() => SetSynchronizationContext(new InlineSynchronizationContext());

        /// <inheritdoc/>
        public void Dispose() => SetSynchronizationContext(_previous);
    }
}
