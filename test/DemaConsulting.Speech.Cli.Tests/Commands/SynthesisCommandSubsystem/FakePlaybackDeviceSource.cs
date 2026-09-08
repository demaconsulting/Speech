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
using DemaConsulting.Speech.Cli.Commands.SynthesisCommandSubsystem;

namespace DemaConsulting.Speech.Cli.Tests.Commands.SynthesisCommandSubsystem;

/// <summary>
///     Deterministic, in-memory fake <see cref="ICliPlaybackDeviceSource"/> used by <c>speak</c>
///     command unit tests, so no test depends on real PortAudio hardware being present on the
///     machine running the test (in particular, headless CI runners with no real audio playback
///     device at all).
/// </summary>
/// <remarks>
///     Unlike constructing a real <see cref="AudioDeviceFactory"/> with an injected
///     <see cref="IAudioPlaybackDeviceProbe"/> - which still resolves to the honestly unavailable
///     fallback on a machine where <c>PortAudioEnvironment.Shared.IsInitialized</c> is
///     <see langword="false"/>, regardless of the injected probe - this fake never consults real
///     PortAudio state at all: <see cref="CreatePlaybackDevice"/> resolves purely from the
///     in-memory device list a test configures, making every scenario deterministic everywhere.
/// </remarks>
internal sealed class FakePlaybackDeviceSource : ICliPlaybackDeviceSource
{
    /// <summary>The device returned by <see cref="CreatePlaybackDevice"/> for a known selection.</summary>
    private readonly IAudioPlaybackDevice _device;

    /// <summary>
    ///     Initializes a new instance of the <see cref="FakePlaybackDeviceSource"/> class.
    /// </summary>
    /// <param name="playbackProbe">The probe to expose via <see cref="PlaybackProbe"/>.</param>
    /// <param name="device">
    ///     The device to return from <see cref="CreatePlaybackDevice"/> when the requested
    ///     selection is known to <paramref name="playbackProbe"/>, or <see langword="null"/> to
    ///     use a fake, available device.
    /// </param>
    public FakePlaybackDeviceSource(IAudioPlaybackDeviceProbe playbackProbe, IAudioPlaybackDevice? device = null)
    {
        PlaybackProbe = playbackProbe;
        _device = device ?? new FakeAudioPlaybackDevice();
    }

    /// <inheritdoc/>
    public IAudioPlaybackDeviceProbe PlaybackProbe { get; }

    /// <inheritdoc/>
    public IAudioPlaybackDevice CreatePlaybackDevice(AudioDeviceSelection? selection)
    {
        var knownDevices = PlaybackProbe.Enumerate();
        if (selection?.DeviceName is { } deviceName)
        {
            return knownDevices.Any(device => string.Equals(device.Name, deviceName, StringComparison.Ordinal))
                ? _device
                : UnavailableAudioPlaybackDevice.Instance;
        }

        return knownDevices.Count > 0 ? _device : UnavailableAudioPlaybackDevice.Instance;
    }
}
