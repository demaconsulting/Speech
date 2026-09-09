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
using DemaConsulting.Speech.Cli.Commands.ConversationCommandSubsystem;

namespace DemaConsulting.Speech.Cli.Tests.Commands.ConversationCommandSubsystem;

/// <summary>
///     Deterministic, in-memory fake <see cref="ICliCaptureDeviceSource"/> used by <c>ask</c>
///     command unit tests, so no test depends on real PortAudio hardware being present on the
///     machine running the test (in particular, headless CI runners with no real audio capture
///     device at all).
/// </summary>
/// <remarks>
///     Unlike constructing a real <see cref="AudioDeviceFactory"/> with an injected
///     <see cref="IAudioCaptureDeviceProbe"/> - which still resolves to the honestly unavailable
///     fallback on a machine where <c>PortAudioEnvironment.Shared.IsInitialized</c> is
///     <see langword="false"/>, regardless of the injected probe - this fake never consults real
///     PortAudio state at all: <see cref="CreateCaptureDevice"/> resolves purely from the
///     in-memory device list a test configures, making every scenario deterministic everywhere.
/// </remarks>
internal sealed class FakeCaptureDeviceSource : ICliCaptureDeviceSource
{
    /// <summary>The device returned by <see cref="CreateCaptureDevice"/> for a known selection.</summary>
    private readonly IAudioCaptureDevice _device;

    /// <summary>
    ///     Initializes a new instance of the <see cref="FakeCaptureDeviceSource"/> class.
    /// </summary>
    /// <param name="captureProbe">The probe to expose via <see cref="CaptureProbe"/>.</param>
    /// <param name="device">
    ///     The device to return from <see cref="CreateCaptureDevice"/> when the requested
    ///     selection is known to <paramref name="captureProbe"/>, or <see langword="null"/> to
    ///     use a fake, available device.
    /// </param>
    public FakeCaptureDeviceSource(IAudioCaptureDeviceProbe captureProbe, IAudioCaptureDevice? device = null)
    {
        CaptureProbe = captureProbe;
        _device = device ?? new FakeAudioCaptureDevice();
    }

    /// <inheritdoc/>
    public IAudioCaptureDeviceProbe CaptureProbe { get; }

    /// <inheritdoc/>
    public IAudioCaptureDevice CreateCaptureDevice(AudioDeviceSelection? selection)
    {
        var knownDevices = CaptureProbe.Enumerate();
        if (selection?.DeviceName is { } deviceName)
        {
            return knownDevices.Any(device => string.Equals(device.Name, deviceName, StringComparison.Ordinal))
                ? _device
                : UnavailableAudioCaptureDevice.Instance;
        }

        return knownDevices.Count > 0 ? _device : UnavailableAudioCaptureDevice.Instance;
    }
}
