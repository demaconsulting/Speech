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

using DemaConsulting.Speech.SynthesisSubsystem;

namespace DemaConsulting.Speech.Cli.Tests.Commands.SynthesisCommandSubsystem;

/// <summary>
///     Deterministic, in-memory fake <see cref="ISynthesisSession"/> used by <c>speak</c>/
///     <c>ask</c> command unit tests, so no test depends on a real, native sherpa-onnx engine.
/// </summary>
internal sealed class FakeSynthesisSession : ISynthesisSession
{
    /// <summary>Gets the ordered list of text values passed to <see cref="SpeakAsync"/>.</summary>
    public List<string> SpeakAsyncCalls { get; } = [];

    /// <summary>Gets the number of times <see cref="StopAsync"/> was called.</summary>
    public int StopCallCount { get; private set; }

    /// <summary>Gets the number of times <see cref="DisposeAsync"/> was called.</summary>
    public int DisposeCallCount { get; private set; }

    /// <summary>Gets or sets an exception to throw from <see cref="SpeakAsync"/>, or <see langword="null"/> for none.</summary>
    public Exception? SpeakAsyncException { get; set; }

    /// <summary>
    ///     Gets or sets an optional awaiter invoked from <see cref="SpeakAsync"/>, after recording
    ///     the call and the exception check, letting a test hold Phase 1's playback wait "in
    ///     flight" deterministically (for example, to prove a concurrently pre-warmed recognizer
    ///     is constructed before playback completes, not after). Purely additive: a test that
    ///     never sets this completes <see cref="SpeakAsync"/> synchronously, exactly as before.
    /// </summary>
    public Func<Task>? SpeakAsyncAwaiter { get; set; }

    /// <inheritdoc/>
    public bool IsAvailable { get; set; } = true;

    /// <inheritdoc/>
    public SynthesisSessionState State { get; private set; } = SynthesisSessionState.Created;

    /// <inheritdoc/>
    public event EventHandler<SessionStateChangedEventArgs>? StateChanged;

    /// <inheritdoc/>
    public Task<IReadOnlyList<SynthesizedSpeech>> SynthesizeAsync(string text, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException($"{nameof(FakeSynthesisSession)} only supports {nameof(SpeakAsync)}.");

    /// <inheritdoc/>
    public async Task SpeakAsync(string text, CancellationToken cancellationToken = default)
    {
        SpeakAsyncCalls.Add(text);
        cancellationToken.ThrowIfCancellationRequested();

        SetState(SynthesisSessionState.Running);

        if (SpeakAsyncException is not null)
        {
            SetState(SynthesisSessionState.Faulted);
            throw SpeakAsyncException;
        }

        if (SpeakAsyncAwaiter is not null)
        {
            await SpeakAsyncAwaiter().ConfigureAwait(false);
        }

        SetState(SynthesisSessionState.Stopped);
    }

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        StopCallCount++;
        SetState(SynthesisSessionState.Stopped);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        DisposeCallCount++;
        return ValueTask.CompletedTask;
    }

    /// <summary>Updates <see cref="State"/> and raises <see cref="StateChanged"/>.</summary>
    private void SetState(SynthesisSessionState newState)
    {
        var previous = State;
        State = newState;
        StateChanged?.Invoke(this, new SessionStateChangedEventArgs(previous, newState));
    }
}
