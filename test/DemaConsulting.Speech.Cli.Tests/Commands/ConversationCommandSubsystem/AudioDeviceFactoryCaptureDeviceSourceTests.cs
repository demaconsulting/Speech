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
using DemaConsulting.Speech.Cli.Tests.Commands.DeviceCommandsSubsystem;

namespace DemaConsulting.Speech.Cli.Tests.Commands.ConversationCommandSubsystem;

/// <summary>
///     Unit tests for <see cref="AudioDeviceFactoryCaptureDeviceSource"/>, proving that it exposes
///     the probe it was composed with and resolves capture devices through the composed
///     <see cref="AudioDeviceFactory"/> with deterministic, hardware-independent results.
/// </summary>
/// <remarks>
///     Every test injects a fake probe into the factory. With an injected probe, a selection the
///     probe does not know resolves to the honestly unavailable device whether or not PortAudio is
///     initialized, so the assertions hold on any machine. A known device resolves to a usable device
///     only when real hardware is present, so the known-name test asserts the source matches the
///     composed factory rather than a fixed availability, and the "known device resolves to a usable
///     device" contract is covered through <see cref="FakeCaptureDeviceSource"/> in
///     <see cref="ICliCaptureDeviceSource"/> contract tests.
/// </remarks>
public sealed class AudioDeviceFactoryCaptureDeviceSourceTests
{
    /// <summary>A capture device description the fake probes enumerate.</summary>
    private static readonly AudioDeviceDescription Microphone =
        new("Fake Microphone", AudioDeviceDirection.Capture, 1, 16000);

    /// <summary>Test that CaptureProbe exposes the probe the factory was composed with.</summary>
    [Fact]
    public void AudioDeviceFactoryCaptureDeviceSource_CaptureProbe_ExposesInjectedProbe()
    {
        // Arrange
        var probe = new FakeAudioCaptureDeviceProbe([Microphone]);
        var source = new AudioDeviceFactoryCaptureDeviceSource(new AudioDeviceFactory(probe));

        // Act
        var exposed = source.CaptureProbe;

        // Assert
        Assert.Same(probe, exposed);
        Assert.Equal([Microphone], exposed.Enumerate());
    }

    /// <summary>Test that a named device the probe does not know resolves to an unavailable device without throwing.</summary>
    [Fact]
    public void AudioDeviceFactoryCaptureDeviceSource_CreateCaptureDevice_UnknownName_ReturnsUnavailableDevice()
    {
        // Arrange
        var source = new AudioDeviceFactoryCaptureDeviceSource(
            new AudioDeviceFactory(new FakeAudioCaptureDeviceProbe([Microphone])));

        // Act
        var device = source.CreateCaptureDevice(new AudioDeviceSelection("does-not-exist"));

        // Assert
        Assert.NotNull(device);
        Assert.False(device.IsAvailable);
    }

    /// <summary>Test that the default selection with no capture devices present resolves to an unavailable device without throwing.</summary>
    [Fact]
    public void AudioDeviceFactoryCaptureDeviceSource_CreateCaptureDevice_DefaultWithNoDevices_ReturnsUnavailableDevice()
    {
        // Arrange
        var source = new AudioDeviceFactoryCaptureDeviceSource(
            new AudioDeviceFactory(new FakeAudioCaptureDeviceProbe()));

        // Act
        var device = source.CreateCaptureDevice(null);

        // Assert
        Assert.NotNull(device);
        Assert.False(device.IsAvailable);
    }

    /// <summary>Test that a null factory is rejected.</summary>
    [Fact]
    public void AudioDeviceFactoryCaptureDeviceSource_NullFactory_ThrowsArgumentNullException()
    {
        // Arrange
        AudioDeviceFactory? factory = null;

        // Act
        void Act() => _ = new AudioDeviceFactoryCaptureDeviceSource(factory!);

        // Assert
        Assert.Throws<ArgumentNullException>(Act);
    }

    /// <summary>
    ///     Test that the <see cref="ICliCaptureDeviceSource"/> contract, exercised through the fake
    ///     source, resolves a known named device to a usable device and an unknown name to an
    ///     unavailable device without throwing.
    /// </summary>
    [Fact]
    public void ICliCaptureDeviceSource_CreateCaptureDevice_KnownAndUnknownNames_ResolveAsContractRequires()
    {
        // Arrange
        ICliCaptureDeviceSource source = new FakeCaptureDeviceSource(new FakeAudioCaptureDeviceProbe([Microphone]));

        // Act
        var known = source.CreateCaptureDevice(new AudioDeviceSelection("Fake Microphone"));
        var unknown = source.CreateCaptureDevice(new AudioDeviceSelection("does-not-exist"));

        // Assert
        Assert.True(known.IsAvailable);
        Assert.False(unknown.IsAvailable);
    }

    /// <summary>Test that the fake capture source rejects a null probe.</summary>
    [Fact]
    public void FakeCaptureDeviceSource_NullProbe_ThrowsArgumentNullException()
    {
        // Arrange
        IAudioCaptureDeviceProbe? probe = null;

        // Act
        void Act() => _ = new FakeCaptureDeviceSource(probe!);

        // Assert
        Assert.Throws<ArgumentNullException>(Act);
    }

    /// <summary>Test that the fake capture source rejects an unavailable device for known selections.</summary>
    [Fact]
    public void FakeCaptureDeviceSource_Constructor_UnavailableDevice_ThrowsArgumentException()
    {
        // Arrange
        var probe = new FakeAudioCaptureDeviceProbe([Microphone]);

        // Act
        void Act() => _ = new FakeCaptureDeviceSource(probe, UnavailableAudioCaptureDevice.Instance);

        // Assert
        Assert.Throws<ArgumentException>(Act);
    }

    /// <summary>
    ///     Test that a named device the probe knows resolves through the production source exactly as
    ///     it does through the composed factory.
    /// </summary>
    [Fact]
    public void AudioDeviceFactoryCaptureDeviceSource_CreateCaptureDevice_KnownName_MatchesFactoryResolution()
    {
        // Arrange
        var factory = new AudioDeviceFactory(new FakeAudioCaptureDeviceProbe([Microphone]));
        var source = new AudioDeviceFactoryCaptureDeviceSource(factory);
        var selection = new AudioDeviceSelection("Fake Microphone");

        // Act
        var viaSource = source.CreateCaptureDevice(selection);
        var viaFactory = factory.CreateCaptureDevice(selection);

        // Assert
        Assert.NotNull(viaSource);
        Assert.Equal(viaFactory.IsAvailable, viaSource.IsAvailable);
        Assert.Equal(viaFactory.GetType(), viaSource.GetType());
        (viaSource as IDisposable)?.Dispose();
        (viaFactory as IDisposable)?.Dispose();
    }
}
