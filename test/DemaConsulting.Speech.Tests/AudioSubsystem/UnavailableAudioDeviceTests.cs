using DemaConsulting.Speech.AudioSubsystem;

namespace DemaConsulting.Speech.Tests.AudioSubsystem;

/// <summary>
///     Unit tests for the four <c>Unavailable*</c> fallback classes: <see cref="UnavailableAudioCaptureDevice"/>,
///     <see cref="UnavailableAudioPlaybackDevice"/>, <see cref="UnavailableAudioCaptureDeviceProbe"/>, and
///     <see cref="UnavailableAudioPlaybackDeviceProbe"/>.
/// </summary>
public class UnavailableAudioDeviceTests
{
    /// <summary>
    ///     Proves that <see cref="UnavailableAudioCaptureDevice.Instance"/> reports itself as
    ///     unavailable.
    /// </summary>
    [Fact]
    public void UnavailableAudioCaptureDevice_IsAvailable_Read_ReturnsFalse()
    {
        // Arrange & Act: read the availability flag from the shared instance
        var isAvailable = UnavailableAudioCaptureDevice.Instance.IsAvailable;

        // Assert: the device honestly reports itself as unavailable
        Assert.False(isAvailable);
    }

    /// <summary>
    ///     Proves that calling <see cref="IAudioCaptureDevice.Start"/> on the unavailable capture
    ///     device throws <see cref="AudioDeviceUnavailableException"/>.
    /// </summary>
    [Fact]
    public void UnavailableAudioCaptureDevice_Start_Always_ThrowsAudioDeviceUnavailableException()
    {
        // Arrange: the shared unavailable capture device
        var device = UnavailableAudioCaptureDevice.Instance;

        // Act & Assert: starting an unavailable device throws the documented exception
        Assert.Throws<AudioDeviceUnavailableException>(device.Start);
    }

    /// <summary>
    ///     Proves that calling <see cref="IAudioCaptureDevice.Stop"/> on the unavailable capture
    ///     device throws <see cref="AudioDeviceUnavailableException"/>.
    /// </summary>
    [Fact]
    public void UnavailableAudioCaptureDevice_Stop_Always_ThrowsAudioDeviceUnavailableException()
    {
        // Arrange: the shared unavailable capture device
        var device = UnavailableAudioCaptureDevice.Instance;

        // Act & Assert: stopping an unavailable device throws the documented exception
        Assert.Throws<AudioDeviceUnavailableException>(device.Stop);
    }

    /// <summary>
    ///     Proves that subscribing and unsubscribing from <see cref="IAudioCaptureDevice.FrameCaptured"/>
    ///     on the unavailable capture device is a safe no-op that never throws.
    /// </summary>
    [Fact]
    public void UnavailableAudioCaptureDevice_FrameCapturedSubscription_Always_DoesNotThrow()
    {
        // Arrange: the shared unavailable capture device and a handler to (un)subscribe
        var device = UnavailableAudioCaptureDevice.Instance;
        EventHandler<AudioCaptureFrameEventArgs> handler = (_, _) => { };

        // Act: subscribe then unsubscribe
        var exception = Record.Exception(() =>
        {
            device.FrameCaptured += handler;
            device.FrameCaptured -= handler;
        });

        // Assert: no exception is thrown
        Assert.Null(exception);
    }

    /// <summary>
    ///     Proves that the unavailable capture device reports a zero capture format rather than a
    ///     plausible-looking default, keeping the fallback honest for consumers that must
    ///     resample or downmix.
    /// </summary>
    [Fact]
    public void UnavailableAudioCaptureDevice_CaptureFormat_Read_ReturnsZeroRateAndChannelCount()
    {
        // Arrange: the shared unavailable capture device
        var device = UnavailableAudioCaptureDevice.Instance;

        // Act: read the reported capture format
        var sampleRate = device.SampleRate;
        var channelCount = device.ChannelCount;

        // Assert: both are zero, matching the false availability flag
        Assert.Equal(0, sampleRate);
        Assert.Equal(0, channelCount);
    }

    /// <summary>
    ///     Proves that <see cref="UnavailableAudioPlaybackDevice.Instance"/> reports itself as
    ///     unavailable.
    /// </summary>
    [Fact]
    public void UnavailableAudioPlaybackDevice_IsAvailable_Read_ReturnsFalse()
    {
        // Arrange & Act: read the availability flag from the shared instance
        var isAvailable = UnavailableAudioPlaybackDevice.Instance.IsAvailable;

        // Assert: the device honestly reports itself as unavailable
        Assert.False(isAvailable);
    }

    /// <summary>
    ///     Proves that calling <see cref="IAudioPlaybackDevice.Start"/> on the unavailable
    ///     playback device throws <see cref="AudioDeviceUnavailableException"/>.
    /// </summary>
    [Fact]
    public void UnavailableAudioPlaybackDevice_Start_Always_ThrowsAudioDeviceUnavailableException()
    {
        // Arrange: the shared unavailable playback device
        var device = UnavailableAudioPlaybackDevice.Instance;

        // Act & Assert: starting an unavailable device throws the documented exception
        Assert.Throws<AudioDeviceUnavailableException>(device.Start);
    }

    /// <summary>
    ///     Proves that calling <see cref="IAudioPlaybackDevice.Stop"/> on the unavailable
    ///     playback device throws <see cref="AudioDeviceUnavailableException"/>.
    /// </summary>
    [Fact]
    public void UnavailableAudioPlaybackDevice_Stop_Always_ThrowsAudioDeviceUnavailableException()
    {
        // Arrange: the shared unavailable playback device
        var device = UnavailableAudioPlaybackDevice.Instance;

        // Act & Assert: stopping an unavailable device throws the documented exception
        Assert.Throws<AudioDeviceUnavailableException>(device.Stop);
    }

    /// <summary>
    ///     Proves that calling <see cref="IAudioPlaybackDevice.Write"/> on the unavailable
    ///     playback device throws <see cref="AudioDeviceUnavailableException"/>.
    /// </summary>
    [Fact]
    public void UnavailableAudioPlaybackDevice_Write_Always_ThrowsAudioDeviceUnavailableException()
    {
        // Arrange: the shared unavailable playback device and a sample buffer
        var device = UnavailableAudioPlaybackDevice.Instance;
        float[] samples = [0.0f, 0.5f];

        // Act & Assert: writing to an unavailable device throws the documented exception
        Assert.Throws<AudioDeviceUnavailableException>(() => device.Write(samples));
    }

    /// <summary>
    ///     Proves that the unavailable playback device reports a zero playback format rather than
    ///     a plausible-looking default, keeping the fallback honest for consumers that must
    ///     resample or upmix.
    /// </summary>
    [Fact]
    public void UnavailableAudioPlaybackDevice_PlaybackFormat_Read_ReturnsZeroRateAndChannelCount()
    {
        // Arrange: the shared unavailable playback device
        var device = UnavailableAudioPlaybackDevice.Instance;

        // Act: read the reported playback format
        var sampleRate = device.SampleRate;
        var channelCount = device.ChannelCount;

        // Assert: both are zero, matching the false availability flag
        Assert.Equal(0, sampleRate);
        Assert.Equal(0, channelCount);
    }

    /// <summary>
    ///     Proves that the unavailable playback device reports zero pending samples, since it
    ///     never accepts any via <see cref="IAudioPlaybackDevice.Write"/>, matching its false
    ///     availability flag.
    /// </summary>
    [Fact]
    public void UnavailableAudioPlaybackDevice_PendingSampleCount_Read_ReturnsZero()
    {
        // Arrange: the shared unavailable playback device
        var device = UnavailableAudioPlaybackDevice.Instance;

        // Act: read the reported pending sample count
        var pendingSampleCount = device.PendingSampleCount;

        // Assert: zero, matching the false availability flag
        Assert.Equal(0, pendingSampleCount);
    }

    /// <summary>
    ///     Proves that <see cref="UnavailableAudioCaptureDeviceProbe.Instance"/> always
    ///     enumerates zero devices rather than throwing.
    /// </summary>
    [Fact]
    public void UnavailableAudioCaptureDeviceProbe_Enumerate_Always_ReturnsEmptyList()
    {
        // Arrange & Act: enumerate using the shared unavailable capture probe
        var devices = UnavailableAudioCaptureDeviceProbe.Instance.Enumerate();

        // Assert: the enumeration is empty, not null, and does not throw
        Assert.Empty(devices);
    }

    /// <summary>
    ///     Proves that <see cref="UnavailableAudioPlaybackDeviceProbe.Instance"/> always
    ///     enumerates zero devices rather than throwing.
    /// </summary>
    [Fact]
    public void UnavailableAudioPlaybackDeviceProbe_Enumerate_Always_ReturnsEmptyList()
    {
        // Arrange & Act: enumerate using the shared unavailable playback probe
        var devices = UnavailableAudioPlaybackDeviceProbe.Instance.Enumerate();

        // Assert: the enumeration is empty, not null, and does not throw
        Assert.Empty(devices);
    }

    /// <summary>
    ///     Proves that <see cref="AudioDeviceUnavailableException"/> exposes the message supplied
    ///     to its single-argument constructor, confirming standard exception conformance.
    /// </summary>
    [Fact]
    public void AudioDeviceUnavailableException_Constructor_WithMessage_ExposesMessage()
    {
        // Arrange: a specific message
        const string message = "No capture device is available.";

        // Act: construct the exception with the message
        var exception = new AudioDeviceUnavailableException(message);

        // Assert: the message is exposed unchanged
        Assert.Equal(message, exception.Message);
    }

    /// <summary>
    ///     Proves that <see cref="AudioDeviceUnavailableException"/> exposes both the message and
    ///     inner exception supplied to its two-argument constructor.
    /// </summary>
    [Fact]
    public void AudioDeviceUnavailableException_Constructor_WithInnerException_ExposesBoth()
    {
        // Arrange: a message and an inner exception
        const string message = "Device initialization failed.";
        var inner = new InvalidOperationException("native backend faulted");

        // Act: construct the exception with both
        var exception = new AudioDeviceUnavailableException(message, inner);

        // Assert: both the message and inner exception are exposed unchanged
        Assert.Equal(message, exception.Message);
        Assert.Same(inner, exception.InnerException);
    }

    /// <summary>
    ///     Proves that the default (parameterless) constructor of
    ///     <see cref="AudioDeviceUnavailableException"/> produces a non-empty default message.
    /// </summary>
    [Fact]
    public void AudioDeviceUnavailableException_Constructor_Default_HasNonEmptyMessage()
    {
        // Act: construct with no arguments
        var exception = new AudioDeviceUnavailableException();

        // Assert: a default, non-empty message is provided
        Assert.False(string.IsNullOrEmpty(exception.Message));
    }
}
