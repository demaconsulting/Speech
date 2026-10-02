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

using System.Threading.Channels;
using DemaConsulting.Speech.RecognitionSubsystem;

namespace DemaConsulting.Speech.Cli.Tests.Commands.RecognitionCommandSubsystem;

/// <summary>
///     Deterministic, in-memory fake <see cref="IRecognitionSession"/> used by <c>recognize</c>
///     and <c>ask</c> command unit tests, so no test depends on a real, native sherpa-onnx engine.
/// </summary>
/// <remarks>
///     Results are buffered on an unbounded <see cref="Channel{T}"/> rather than raised through a
///     background-thread event, mirroring the real <see cref="IRecognitionSession.GetResultsAsync"/>
///     single-consumer, fully <c>async</c> contract: a test calls <see cref="RaiseResult"/> to
///     enqueue a result, which a concurrently running <c>await foreach</c> over
///     <see cref="GetResultsAsync"/> then observes, with no real background thread involved.
/// </remarks>
internal sealed class FakeRecognitionSession : IRecognitionSession
{
    /// <summary>The channel backing <see cref="GetResultsAsync"/>; results are written via <see cref="RaiseResult"/>.</summary>
    private readonly Channel<SpeechRecognitionEvent> _channel = Channel.CreateUnbounded<SpeechRecognitionEvent>();

    /// <summary>Gets the number of times <see cref="StartAsync"/> was called.</summary>
    public int StartCallCount { get; private set; }

    /// <summary>Gets the number of times <see cref="StopAsync"/> was called.</summary>
    public int StopCallCount { get; private set; }

    /// <summary>Gets the number of times <see cref="DisposeAsync"/> was called.</summary>
    public int DisposeCallCount { get; private set; }

    /// <summary>Gets or sets an exception to throw from <see cref="StartAsync"/>, or <see langword="null"/> for none.</summary>
    public Exception? StartException { get; set; }

    /// <summary>
    ///     Gets or sets an action invoked synchronously from <see cref="StartAsync"/>, after
    ///     incrementing <see cref="StartCallCount"/> and transitioning <see cref="State"/> to
    ///     <see cref="RecognitionSessionState.Running"/>.
    /// </summary>
    /// <remarks>
    ///     Lets a test simulate a file-mode session by calling <see cref="RaiseResult"/> and/or
    ///     <see cref="StopAsync"/> reentrantly from within this callback.
    /// </remarks>
    public Action<FakeRecognitionSession>? OnStart { get; set; }

    /// <summary>
    ///     Gets or sets an action invoked synchronously from <see cref="StopAsync"/>, after
    ///     incrementing <see cref="StopCallCount"/> but before the results channel is completed.
    /// </summary>
    public Action<FakeRecognitionSession>? OnStop { get; set; }

    /// <inheritdoc/>
    public bool IsAvailable { get; set; } = true;

    /// <inheritdoc/>
    public RecognitionSessionState State { get; private set; } = RecognitionSessionState.Created;

    /// <inheritdoc/>
    public event EventHandler<SessionStateChangedEventArgs>? StateChanged;

    /// <summary>
    ///     Synthetically enqueues a result onto the results channel, letting a test simulate a
    ///     recognized result without a real engine. Safe to call from any thread, including
    ///     reentrantly from <see cref="OnStart"/>/<see cref="OnStop"/>.
    /// </summary>
    public void RaiseResult(string text, bool isFinal) =>
        _channel.Writer.TryWrite(new SpeechRecognitionEvent(new SpeechRecognitionResult(text, isFinal)));

    /// <inheritdoc/>
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        StartCallCount++;
        SetState(RecognitionSessionState.Running);

        if (StartException is not null)
        {
            SetState(RecognitionSessionState.Faulted);
            throw StartException;
        }

        OnStart?.Invoke(this);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        StopCallCount++;
        OnStop?.Invoke(this);
        SetState(RecognitionSessionState.Stopped);
        _channel.Writer.TryComplete();
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<SpeechRecognitionEvent> GetResultsAsync(CancellationToken cancellationToken = default) =>
        _channel.Reader.ReadAllAsync(cancellationToken);

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        DisposeCallCount++;
        _channel.Writer.TryComplete();
        return ValueTask.CompletedTask;
    }

    /// <summary>Updates <see cref="State"/> and raises <see cref="StateChanged"/>.</summary>
    private void SetState(RecognitionSessionState newState)
    {
        var previous = State;
        State = newState;
        StateChanged?.Invoke(this, new SessionStateChangedEventArgs(previous, newState));
    }
}
