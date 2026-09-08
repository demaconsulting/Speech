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
///     CLI-owned seam over real-playback-device resolution, letting <see cref="SpeakCommand"/>'s
///     own device-dispatch logic be unit tested against a controlled, in-memory fake instead of
///     the library's real, sealed <see cref="AudioDeviceFactory"/>.
/// </summary>
/// <remarks>
///     Mirrors the same "CLI-owned seam over a sealed library type" pattern
///     <c>ICliModelCatalog</c> already establishes for the model catalog (see
///     _SpeechCli ModelCommandsSubsystem Design_). This seam exists because
///     <see cref="AudioDeviceFactory"/>'s own real-hardware detection
///     (<c>PortAudioEnvironment.Shared.IsInitialized</c>) makes its
///     <see cref="AudioDeviceFactory.CreatePlaybackDevice"/> method return the honest
///     unavailable fallback on any machine with no real playback hardware - including headless
///     CI runners - regardless of any playback probe injected into the factory's constructor,
///     and its internal, hardware-injectable constructor is deliberately not reachable from
///     <c>DemaConsulting.Speech.Cli.Tests</c> (that assembly is not granted
///     <c>InternalsVisibleTo</c> access, by design - see
///     _SpeechCli SynthesisCommandSubsystem Design_ for the full rationale). The production
///     implementation (<see cref="AudioDeviceFactoryPlaybackDeviceSource"/>) forwards every call
///     unchanged to a real <see cref="AudioDeviceFactory"/>; it adds no public API to
///     <c>DemaConsulting.Speech</c> and does not change <see cref="AudioDeviceFactory"/>'s own
///     hardware-detection behavior in any way.
/// </remarks>
internal interface ICliPlaybackDeviceSource
{
    /// <summary>
    ///     Gets the probe used to enumerate available playback devices.
    /// </summary>
    IAudioPlaybackDeviceProbe PlaybackProbe { get; }

    /// <summary>
    ///     Creates a playback device for the given selection.
    /// </summary>
    /// <param name="selection">
    ///     The requested device selection, or <see langword="null"/> to request the system
    ///     default output device.
    /// </param>
    /// <returns>
    ///     A real, available playback device when one is known to <see cref="PlaybackProbe"/>;
    ///     otherwise an honestly unavailable device (<see cref="IAudioPlaybackDevice.IsAvailable"/>
    ///     is <see langword="false"/>).
    /// </returns>
    IAudioPlaybackDevice CreatePlaybackDevice(AudioDeviceSelection? selection);
}
