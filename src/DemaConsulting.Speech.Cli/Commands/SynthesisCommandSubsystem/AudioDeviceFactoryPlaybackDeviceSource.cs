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

namespace DemaConsulting.Speech.Cli.Commands.SynthesisCommandSubsystem;

/// <summary>
///     Production <see cref="ICliPlaybackDeviceSource"/> implementation backed by a real
///     <see cref="AudioDeviceFactory"/>.
/// </summary>
/// <remarks>
///     This adapter forwards every call to the composed <see cref="AudioDeviceFactory"/>
///     unchanged - including its hardware-detection behavior - so <see cref="SpeakCommand.Run(Cli.Context)"/>'s
///     real playback-device resolution is exactly what it was before this seam was introduced.
///     It does not own the composed factory and does not dispose anything.
/// </remarks>
internal sealed class AudioDeviceFactoryPlaybackDeviceSource : ICliPlaybackDeviceSource
{
    /// <summary>The real factory every call is forwarded to.</summary>
    private readonly AudioDeviceFactory _factory;

    /// <summary>
    ///     Initializes a new instance of the <see cref="AudioDeviceFactoryPlaybackDeviceSource"/>
    ///     class.
    /// </summary>
    /// <param name="factory">The real factory to forward every call to. Must not be null.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="factory"/> is <see langword="null"/>.</exception>
    public AudioDeviceFactoryPlaybackDeviceSource(AudioDeviceFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        _factory = factory;
    }

    /// <inheritdoc/>
    public IAudioPlaybackDeviceProbe PlaybackProbe => _factory.PlaybackProbe;

    /// <inheritdoc/>
    public IAudioPlaybackDevice CreatePlaybackDevice(AudioDeviceSelection? selection) =>
        _factory.CreatePlaybackDevice(selection);
}
