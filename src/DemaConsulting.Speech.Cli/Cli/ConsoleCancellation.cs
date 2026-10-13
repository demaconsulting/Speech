// Copyright (c) DEMA Consulting
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

namespace DemaConsulting.Speech.Cli.Cli;

/// <summary>
///     Wires <c>Ctrl+C</c> (<see cref="Console.CancelKeyPress"/>) to cooperative cancellation for
///     the duration of a command, replacing the subscribe/unsubscribe pattern every long-running
///     subcommand would otherwise repeat.
/// </summary>
/// <remarks>
///     Each registration subscribes one handler and unsubscribes it again when the returned
///     <see cref="Registration"/> is disposed, so a command holds the subscription with a single
///     <c>using</c> declaration around its blocking call.
/// </remarks>
internal static class ConsoleCancellation
{
    /// <summary>
    ///     Subscribes a <c>Ctrl+C</c> handler that suppresses the runtime's default process
    ///     termination, cancels <paramref name="source"/>, and then invokes the optional
    ///     <paramref name="onCancel"/> callback.
    /// </summary>
    /// <param name="source">The cancellation source to cancel. Must not be null.</param>
    /// <param name="onCancel">
    ///     An optional synchronous callback for command-specific work (for example, stopping a live
    ///     session). It runs on the console's signal thread, after <paramref name="source"/> has
    ///     been canceled, and must not block.
    /// </param>
    /// <returns>A registration that unsubscribes the handler when disposed.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> is <see langword="null"/>.</exception>
    public static Registration Register(CancellationTokenSource source, Action? onCancel = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        return Subscribe(suppressTermination: true, () =>
        {
            source.Cancel();
            onCancel?.Invoke();
        });
    }

    /// <summary>
    ///     Subscribes a <c>Ctrl+C</c> handler that only invokes <paramref name="onCancel"/>, leaving
    ///     the termination decision to any other handler (such as one registered by an enclosing
    ///     <see cref="Register"/> call).
    /// </summary>
    /// <param name="onCancel">A synchronous callback that must not block. Must not be null.</param>
    /// <returns>A registration that unsubscribes the handler when disposed.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="onCancel"/> is <see langword="null"/>.</exception>
    public static Registration Observe(Action onCancel)
    {
        ArgumentNullException.ThrowIfNull(onCancel);
        return Subscribe(suppressTermination: false, onCancel);
    }

    /// <summary>Creates a registration and subscribes its handler to <see cref="Console.CancelKeyPress"/>.</summary>
    /// <param name="suppressTermination">Whether the handler suppresses default process termination.</param>
    /// <param name="action">The cancellation work the handler runs.</param>
    /// <returns>The live registration; disposing it unsubscribes the handler.</returns>
    private static Registration Subscribe(bool suppressTermination, Action action)
    {
        var registration = new Registration(suppressTermination, action);
        Console.CancelKeyPress += registration.Handler;
        registration.MarkSubscribed();
        return registration;
    }

    /// <summary>A live <c>Ctrl+C</c> subscription; disposing it unsubscribes the handler.</summary>
    internal sealed class Registration : IDisposable
    {
        /// <summary>Whether the handler sets <see cref="ConsoleCancelEventArgs.Cancel"/> to keep the process alive.</summary>
        private readonly bool _suppressTermination;

        /// <summary>The cancellation work to run when <c>Ctrl+C</c> is pressed.</summary>
        private readonly Action _action;

        /// <summary>Non-zero while the handler is subscribed to the console event.</summary>
        private int _subscribed;

        /// <summary>Initializes a new instance of the <see cref="Registration"/> class.</summary>
        /// <param name="suppressTermination">Whether to suppress default process termination.</param>
        /// <param name="action">The cancellation work to run.</param>
        internal Registration(bool suppressTermination, Action action)
        {
            _suppressTermination = suppressTermination;
            _action = action;
            Handler = OnCancelKeyPress;
        }

        /// <summary>Gets the delegate subscribed to <see cref="Console.CancelKeyPress"/>.</summary>
        internal ConsoleCancelEventHandler Handler { get; }

        /// <summary>
        ///     Runs the cancellation work as the console signal would, for tests that cannot raise
        ///     <see cref="Console.CancelKeyPress"/> (its event arguments have no public constructor).
        /// </summary>
        /// <returns>Whether the handler would suppress default process termination.</returns>
        internal bool Raise()
        {
            _action();
            return _suppressTermination;
        }

        /// <summary>
        ///     Gets a value indicating whether the handler is currently subscribed to
        ///     <see cref="Console.CancelKeyPress"/>; <see langword="false"/> once disposed.
        /// </summary>
        internal bool IsSubscribed => Volatile.Read(ref _subscribed) != 0;

        /// <summary>Records that the handler has been subscribed to <see cref="Console.CancelKeyPress"/>.</summary>
        internal void MarkSubscribed()
        {
            Volatile.Write(ref _subscribed, 1);
        }

        /// <summary>Unsubscribes the handler; safe to call more than once.</summary>
        public void Dispose()
        {
            Console.CancelKeyPress -= Handler;
            Volatile.Write(ref _subscribed, 0);
        }

        /// <summary>
        ///     The <see cref="Console.CancelKeyPress"/> handler: marks the event canceled when this
        ///     registration suppresses termination, then runs the cancellation work.
        /// </summary>
        /// <param name="sender">The event sender (unused).</param>
        /// <param name="e">The event arguments whose <see cref="ConsoleCancelEventArgs.Cancel"/> is set when termination is suppressed.</param>
        private void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs e)
        {
            if (_suppressTermination)
            {
                e.Cancel = true;
            }

            _action();
        }
    }
}
