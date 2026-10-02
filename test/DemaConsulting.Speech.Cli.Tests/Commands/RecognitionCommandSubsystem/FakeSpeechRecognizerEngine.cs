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

using DemaConsulting.Speech.AudioSubsystem;
using DemaConsulting.Speech.RecognitionSubsystem;

namespace DemaConsulting.Speech.Cli.Tests.Commands.RecognitionCommandSubsystem;

/// <summary>
///     Deterministic, in-memory fake <see cref="ISpeechRecognizerEngine"/> used by
///     <c>recognize</c>/<c>ask</c> command unit tests, wrapping a single pre-configured
///     <see cref="FakeRecognitionSession"/> that <see cref="CreateSessionAsync"/> returns.
/// </summary>
internal sealed class FakeSpeechRecognizerEngine : ISpeechRecognizerEngine
{
    /// <summary>The session <see cref="CreateSessionAsync"/> returns.</summary>
    private readonly FakeRecognitionSession _session;

    /// <summary>
    ///     Initializes a new instance of the <see cref="FakeSpeechRecognizerEngine"/> class.
    /// </summary>
    /// <param name="session">The session <see cref="CreateSessionAsync"/> returns. Must not be null.</param>
    public FakeSpeechRecognizerEngine(FakeRecognitionSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        _session = session;
    }

    /// <summary>Gets the number of times <see cref="CreateSessionAsync"/> was called.</summary>
    public int CreateSessionAsyncCallCount { get; private set; }

    /// <summary>Gets the number of times <see cref="DisposeAsync"/> was called.</summary>
    public int DisposeCallCount { get; private set; }

    /// <summary>Gets the capture device passed to the most recent <see cref="CreateSessionAsync"/> call.</summary>
    public IAudioCaptureDevice? LastDevice { get; private set; }

    /// <summary>Gets or sets an exception to throw from <see cref="CreateSessionAsync"/>, or <see langword="null"/> for none.</summary>
    public Exception? CreateSessionException { get; set; }

    /// <inheritdoc/>
    public bool IsAvailable { get; set; } = true;

    /// <inheritdoc/>
    public Task<IRecognitionSession> CreateSessionAsync(
        IAudioCaptureDevice device,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(device);

        CreateSessionAsyncCallCount++;
        LastDevice = device;
        cancellationToken.ThrowIfCancellationRequested();

        if (CreateSessionException is not null)
        {
            throw CreateSessionException;
        }

        return Task.FromResult<IRecognitionSession>(_session);
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        DisposeCallCount++;
        return ValueTask.CompletedTask;
    }
}
