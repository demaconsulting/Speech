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

using DemaConsulting.Speech.RecognitionSubsystem;

namespace DemaConsulting.Speech.Cli.Tests.Commands.RecognitionCommandSubsystem;

/// <summary>
///     Deterministic, in-memory fake <see cref="ISpeechRecognizer"/> used by <c>recognize</c>
///     command unit tests, so no test depends on a real, native sherpa-onnx engine.
/// </summary>
internal sealed class FakeSpeechRecognizer : ISpeechRecognizer
{
    /// <summary>Gets the number of times <see cref="Start"/> was called.</summary>
    public int StartCallCount { get; private set; }

    /// <summary>Gets the number of times <see cref="Stop"/> was called.</summary>
    public int StopCallCount { get; private set; }

    /// <summary>Gets the number of times <see cref="Dispose"/> was called.</summary>
    public int DisposeCallCount { get; private set; }

    /// <summary>Gets or sets an exception to throw from <see cref="Start"/>, or <see langword="null"/> for none.</summary>
    public Exception? StartException { get; set; }

    /// <summary>Gets or sets an action invoked synchronously from <see cref="Start"/>, after incrementing <see cref="StartCallCount"/>.</summary>
    /// <remarks>
    ///     Lets a test simulate a file-mode <c>WavFileAudioCaptureDevice</c>-driven session by
    ///     raising <see cref="ResultReceived"/> events and/or calling <see cref="Stop"/>
    ///     reentrantly from within this callback, mirroring the real recognizer's own
    ///     synchronous, blocking <c>Start()</c> contract for file input.
    /// </remarks>
    public Action<FakeSpeechRecognizer>? OnStart { get; set; }

    /// <inheritdoc/>
    public bool IsAvailable { get; set; } = true;

    /// <inheritdoc/>
    public event EventHandler<SpeechRecognitionEvent>? ResultReceived;

    /// <summary>
    ///     Synthetically raises <see cref="ResultReceived"/> with the given text and finality,
    ///     letting a test simulate a recognized result without a real engine.
    /// </summary>
    public void RaiseResult(string text, bool isFinal) =>
        ResultReceived?.Invoke(this, new SpeechRecognitionEvent(new SpeechRecognitionResult(text, isFinal)));

    /// <inheritdoc/>
    public void Start()
    {
        StartCallCount++;

        if (StartException is not null)
        {
            throw StartException;
        }

        OnStart?.Invoke(this);
    }

    /// <inheritdoc/>
    public void Stop() => StopCallCount++;

    /// <inheritdoc/>
    public void Dispose() => DisposeCallCount++;
}
