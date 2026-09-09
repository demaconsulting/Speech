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

namespace DemaConsulting.Speech.Cli.Tests.Commands.ConversationCommandSubsystem;

/// <summary>
///     Deterministic, in-memory fake <see cref="IAudioCaptureDevice"/> used by <c>ask</c> command
///     unit tests, so no test depends on a real, native PortAudio capture stream.
/// </summary>
internal sealed class FakeAudioCaptureDevice : IAudioCaptureDevice
{
    /// <summary>Backing field for <see cref="FrameCaptured"/>. Never invoked by this fake.</summary>
    private EventHandler<AudioCaptureFrameEventArgs>? _frameCaptured;

    /// <inheritdoc/>
    public bool IsAvailable => true;

    /// <inheritdoc/>
    public int ChannelCount => 1;

    /// <inheritdoc/>
    public int SampleRate => 16000;

    /// <inheritdoc/>
    public event EventHandler<AudioCaptureFrameEventArgs>? FrameCaptured
    {
        add => _frameCaptured += value;
        remove => _frameCaptured -= value;
    }

    /// <inheritdoc/>
    public void Start()
    {
    }

    /// <inheritdoc/>
    public void Stop()
    {
    }
}
