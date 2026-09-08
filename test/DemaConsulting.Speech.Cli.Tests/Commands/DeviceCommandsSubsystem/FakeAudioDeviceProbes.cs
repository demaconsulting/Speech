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

namespace DemaConsulting.Speech.Cli.Tests.Commands.DeviceCommandsSubsystem;

/// <summary>
///     Deterministic, in-memory fake <see cref="IAudioCaptureDeviceProbe"/> used by
///     device-command unit tests, so no test depends on real audio hardware.
/// </summary>
internal sealed class FakeAudioCaptureDeviceProbe : IAudioCaptureDeviceProbe
{
    /// <summary>The devices this fake enumerates.</summary>
    private readonly IReadOnlyList<AudioDeviceDescription> _devices;

    /// <summary>
    ///     Initializes a new instance of the <see cref="FakeAudioCaptureDeviceProbe"/> class.
    /// </summary>
    /// <param name="devices">The devices to enumerate, or <see langword="null"/> for an empty list.</param>
    public FakeAudioCaptureDeviceProbe(IReadOnlyList<AudioDeviceDescription>? devices = null)
    {
        _devices = devices ?? [];
    }

    /// <inheritdoc/>
    public IReadOnlyList<AudioDeviceDescription> Enumerate() => _devices;
}

/// <summary>
///     Deterministic, in-memory fake <see cref="IAudioPlaybackDeviceProbe"/> used by
///     device-command unit tests, so no test depends on real audio hardware.
/// </summary>
internal sealed class FakeAudioPlaybackDeviceProbe : IAudioPlaybackDeviceProbe
{
    /// <summary>The devices this fake enumerates.</summary>
    private readonly IReadOnlyList<AudioDeviceDescription> _devices;

    /// <summary>
    ///     Initializes a new instance of the <see cref="FakeAudioPlaybackDeviceProbe"/> class.
    /// </summary>
    /// <param name="devices">The devices to enumerate, or <see langword="null"/> for an empty list.</param>
    public FakeAudioPlaybackDeviceProbe(IReadOnlyList<AudioDeviceDescription>? devices = null)
    {
        _devices = devices ?? [];
    }

    /// <inheritdoc/>
    public IReadOnlyList<AudioDeviceDescription> Enumerate() => _devices;
}
