using DemaConsulting.Speech.AudioSubsystem;
using NSubstitute;

namespace DemaConsulting.Speech.Tests.AudioSubsystem;

/// <summary>
///     Unit tests for <see cref="PreRollAudioCaptureDevice"/>, driving it through a substitute
///     underlying capture device so captured blocks can be raised deterministically, with no
///     audio hardware.
/// </summary>
public class PreRollAudioCaptureDeviceTests
{
    /// <summary>
    ///     Proves that audio captured while stopped is delivered as the first block of the next
    ///     run, before any live audio, so speech starting as push-to-talk is pressed is kept.
    /// </summary>
    [Fact]
    public void PreRollAudioCaptureDevice_Start_AfterStop_DeliversAudioCapturedWhileStoppedFirst()
    {
        // Arrange: a 1 kHz mono device (300 ms pre-roll = 300 samples) that ran once already
        var inner = CreateInnerDevice(sampleRate: 1000, channelCount: 1);
        using var device = new PreRollAudioCaptureDevice(inner);
        var received = Record(device);
        device.Start();
        device.Stop();

        // Act: audio arrives while stopped, then the next run starts and live audio follows
        RaiseFrame(inner, [1f, 2f, 3f]);
        device.Start();
        RaiseFrame(inner, [4f, 5f]);

        // Assert: the pre-roll block came first, followed by the live block
        Assert.Equal(2, received.Count);
        Assert.Equal([1f, 2f, 3f], received[0]);
        Assert.Equal([4f, 5f], received[1]);
    }

    /// <summary>
    ///     Proves that a long pause delivers only the most recent pre-roll duration of audio,
    ///     not everything captured while stopped.
    /// </summary>
    [Fact]
    public void PreRollAudioCaptureDevice_Start_AfterLongPause_DeliversOnlyMostRecentPreRoll()
    {
        // Arrange: a 1 kHz mono device whose 300 ms pre-roll holds 300 samples
        var inner = CreateInnerDevice(sampleRate: 1000, channelCount: 1);
        using var device = new PreRollAudioCaptureDevice(inner);
        var received = Record(device);
        device.Start();
        device.Stop();

        // Act: 500 samples arrive while stopped, in two blocks, then the next run starts
        RaiseFrame(inner, [.. Enumerable.Range(0, 250).Select(i => (float)i)]);
        RaiseFrame(inner, [.. Enumerable.Range(250, 250).Select(i => (float)i)]);
        device.Start();

        // Assert: only the newest 300 samples (200..499) were delivered, oldest first
        var preRoll = Assert.Single(received);
        Assert.Equal(Enumerable.Range(200, 300).Select(i => (float)i), preRoll);
    }

    /// <summary>
    ///     Proves that the pre-roll keeps whole interleaved frames for a multi-channel device, so
    ///     the delivered block stays channel-aligned.
    /// </summary>
    [Fact]
    public void PreRollAudioCaptureDevice_Start_StereoDevice_PreRollKeepsWholeFrames()
    {
        // Arrange: a 10 Hz stereo device, so a 300 ms pre-roll holds 3 frames (6 samples)
        var inner = CreateInnerDevice(sampleRate: 10, channelCount: 2);
        using var device = new PreRollAudioCaptureDevice(inner);
        var received = Record(device);
        device.Start();
        device.Stop();

        // Act: 5 stereo frames arrive while stopped
        RaiseFrame(inner, [1f, -1f, 2f, -2f, 3f, -3f, 4f, -4f, 5f, -5f]);
        device.Start();

        // Assert: the newest 3 whole frames were delivered, left/right pairs intact
        Assert.Equal([3f, -3f, 4f, -4f, 5f, -5f], Assert.Single(received));
    }

    /// <summary>
    ///     Proves that audio already delivered during a run is never replayed as pre-roll for the
    ///     next run, so a repeated press cannot duplicate words.
    /// </summary>
    [Fact]
    public void PreRollAudioCaptureDevice_Start_AudioDeliveredInPreviousRun_IsNotReplayed()
    {
        // Arrange
        var inner = CreateInnerDevice(sampleRate: 1000, channelCount: 1);
        using var device = new PreRollAudioCaptureDevice(inner);
        var received = Record(device);

        // Act: deliver a live block, stop, and start again with nothing captured in between
        device.Start();
        RaiseFrame(inner, [7f, 8f]);
        device.Stop();
        device.Start();

        // Assert: the live block was delivered exactly once and no pre-roll block followed
        Assert.Equal([7f, 8f], Assert.Single(received));
    }

    /// <summary>
    ///     Proves that a non-zero pre-roll keeps the underlying device capturing across a stop,
    ///     starting it only once for many runs.
    /// </summary>
    [Fact]
    public void PreRollAudioCaptureDevice_Stop_WithPreRoll_KeepsUnderlyingDeviceRunning()
    {
        // Arrange
        var inner = CreateInnerDevice(sampleRate: 1000, channelCount: 1);
        using var device = new PreRollAudioCaptureDevice(inner);

        // Act: several push-to-talk runs
        device.Start();
        device.Stop();
        device.Start();
        device.Stop();

        // Assert: the underlying device was started once and never stopped
        inner.Received(1).Start();
        inner.DidNotReceive().Stop();
    }

    /// <summary>
    ///     Proves that a zero pre-roll disables the feature: each stop stops the underlying device
    ///     and nothing captured while stopped is delivered.
    /// </summary>
    [Fact]
    public void PreRollAudioCaptureDevice_Stop_ZeroPreRoll_StopsUnderlyingDeviceAndDeliversNoPreRoll()
    {
        // Arrange
        var inner = CreateInnerDevice(sampleRate: 1000, channelCount: 1);
        using var device = new PreRollAudioCaptureDevice(inner, TimeSpan.Zero);
        var received = Record(device);

        // Act: run, stop, see a stray block while stopped, run again
        device.Start();
        device.Stop();
        RaiseFrame(inner, [1f, 2f]);
        device.Start();

        // Assert: the device behaved as a plain pass-through
        inner.Received(1).Stop();
        inner.Received(2).Start();
        Assert.Empty(received);
    }

    /// <summary>
    ///     Proves that disposal stops the underlying device and makes this device report itself
    ///     unavailable, so a host can release the open microphone.
    /// </summary>
    [Fact]
    public void PreRollAudioCaptureDevice_Dispose_StopsUnderlyingDeviceAndReportsUnavailable()
    {
        // Arrange: a device kept running between runs
        var inner = CreateInnerDevice(sampleRate: 1000, channelCount: 1);
        var device = new PreRollAudioCaptureDevice(inner);
        device.Start();
        device.Stop();

        // Act
        device.Dispose();
        device.Dispose();

        // Assert: the underlying device was stopped exactly once and the device is now unusable
        inner.Received(1).Stop();
        Assert.False(device.IsAvailable);
        Assert.Throws<ObjectDisposedException>(device.Start);
    }

    /// <summary>
    ///     Proves that the default pre-roll is 300 ms and that the format reported is the
    ///     underlying device's.
    /// </summary>
    [Fact]
    public void PreRollAudioCaptureDevice_Constructor_Defaults_ReportsDefaultPreRollAndUnderlyingFormat()
    {
        // Arrange
        var inner = CreateInnerDevice(sampleRate: 48000, channelCount: 2);

        // Act
        using var device = new PreRollAudioCaptureDevice(inner);

        // Assert
        Assert.Equal(TimeSpan.FromMilliseconds(300), device.PreRoll);
        Assert.Equal(TimeSpan.FromMilliseconds(300), PreRollAudioCaptureDevice.DefaultPreRoll);
        Assert.True(device.IsAvailable);
        Assert.Equal(48000, device.SampleRate);
        Assert.Equal(2, device.ChannelCount);
    }

    /// <summary>
    ///     Proves that invalid constructor arguments are rejected immediately.
    /// </summary>
    [Fact]
    public void PreRollAudioCaptureDevice_Constructor_InvalidArguments_Throw()
    {
        // Arrange
        var inner = CreateInnerDevice(sampleRate: 1000, channelCount: 1);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new PreRollAudioCaptureDevice(null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PreRollAudioCaptureDevice(inner, TimeSpan.FromMilliseconds(-1)));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new PreRollAudioCaptureDevice(inner, PreRollAudioCaptureDevice.MaxPreRoll + TimeSpan.FromMilliseconds(1)));
    }

    /// <summary>
    ///     Proves that starting over an unavailable underlying device throws the library's
    ///     documented unavailable-device exception rather than silently capturing nothing.
    /// </summary>
    [Fact]
    public void PreRollAudioCaptureDevice_Start_UnavailableUnderlyingDevice_ThrowsAudioDeviceUnavailableException()
    {
        // Arrange
        using var device = new PreRollAudioCaptureDevice(UnavailableAudioCaptureDevice.Instance);

        // Act & Assert
        Assert.False(device.IsAvailable);
        Assert.Throws<AudioDeviceUnavailableException>(device.Start);
    }

    /// <summary>
    ///     Proves that blocks an underlying device raises from inside its own first Start - before
    ///     it has even reported its format - are delivered rather than lost.
    /// </summary>
    [Fact]
    public void PreRollAudioCaptureDevice_Start_DeviceRaisesFramesDuringItsOwnStart_DeliversThem()
    {
        // Arrange: a device reporting no format until started, which raises a block synchronously
        // from inside Start, like a synchronous-replay source
        var inner = CreateInnerDevice(sampleRate: 0, channelCount: 0);
        inner.When(d => d.Start()).Do(_ => RaiseFrame(inner, [1f, 2f]));
        using var device = new PreRollAudioCaptureDevice(inner);
        var received = Record(device);

        // Act
        device.Start();

        // Assert: the block raised during the underlying start reached the subscriber
        Assert.Equal([1f, 2f], Assert.Single(received));
    }

    /// <summary>
    ///     Proves that a finite file source is rejected: it has no audio "before the press", and
    ///     must reach a recognition session unwrapped so the whole file is recognized.
    /// </summary>
    [Fact]
    public void PreRollAudioCaptureDevice_Constructor_FiniteFileSource_ThrowsArgumentException()
    {
        // Arrange
        var fileSource = new WavFileAudioCaptureDevice(Path.Join(Path.GetTempPath(), "unused.wav"));

        // Act & Assert
        Assert.Throws<ArgumentException>(() => new PreRollAudioCaptureDevice(fileSource));
    }

    /// <summary>
    ///     Proves the handoff between a stopping subscriber and the pre-roll buffer loses nothing:
    ///     unsubscribing while a block is being raised to that subscriber waits until it has been
    ///     delivered, and a block captured after the unsubscribe becomes pre-roll instead.
    /// </summary>
    [Fact(Timeout = 60000)]
    public async Task PreRollAudioCaptureDevice_Unsubscribe_WhileBlockBeingRaised_DeliversItThenBuffersLaterAudio()
    {
        // Arrange: a running device whose only subscriber blocks inside its first block
        var inner = CreateInnerDevice(sampleRate: 1000, channelCount: 1);
        using var device = new PreRollAudioCaptureDevice(inner);
        using var handlerEntered = new ManualResetEventSlim(false);
        using var releaseHandler = new ManualResetEventSlim(false);
        var stoppingSubscriberReceived = new List<float[]>();
        void StoppingSubscriber(object? sender, AudioCaptureFrameEventArgs e)
        {
            stoppingSubscriberReceived.Add([.. e.Samples]);
            handlerEntered.Set();
            releaseHandler.Wait(TestContext.Current.CancellationToken);
        }

        device.FrameCaptured += StoppingSubscriber;
        device.Start();
        var raise = Task.Run(() => RaiseFrame(inner, [1f]), TestContext.Current.CancellationToken);
        Assert.True(handlerEntered.Wait(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken));

        // Act: the subscriber unsubscribes while its block is still being raised
        var unsubscribe = Task.Run(() => { device.FrameCaptured -= StoppingSubscriber; }, TestContext.Current.CancellationToken);
        var unsubscribedWhileRaising = await Task.WhenAny(unsubscribe, Task.Delay(100, TestContext.Current.CancellationToken)) == unsubscribe;
        releaseHandler.Set();
        await raise;
        await unsubscribe;

        // Act: audio arrives with nobody subscribed, then the next run starts
        RaiseFrame(inner, [2f]);
        var nextRun = Record(device);
        device.Stop();
        device.Start();

        // Assert: the unsubscribe waited for the in-flight block, which was delivered once; the
        // later block became the next run's pre-roll
        Assert.False(unsubscribedWhileRaising);
        Assert.Equal([1f], Assert.Single(stoppingSubscriberReceived));
        Assert.Equal([2f], Assert.Single(nextRun));
    }

    /// <summary>
    ///     Proves that stopping (and disposing) never waits for a subscriber that is stuck handling
    ///     a block, so a recognition session's teardown cannot hang on the device's Stop.
    /// </summary>
    [Fact(Timeout = 60000)]
    public async Task PreRollAudioCaptureDevice_StopAndDispose_WhileHandlerStuck_ReturnWithoutWaiting()
    {
        // Arrange: a running device whose subscriber is stuck inside a block
        var inner = CreateInnerDevice(sampleRate: 1000, channelCount: 1);
        var device = new PreRollAudioCaptureDevice(inner);
        using var handlerEntered = new ManualResetEventSlim(false);
        using var releaseHandler = new ManualResetEventSlim(false);
        device.FrameCaptured += (_, _) =>
        {
            handlerEntered.Set();
            releaseHandler.Wait(TestContext.Current.CancellationToken);
        };
        device.Start();
        var raise = Task.Run(() => RaiseFrame(inner, [1f]), TestContext.Current.CancellationToken);
        Assert.True(handlerEntered.Wait(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken));

        try
        {
            // Act: stop and dispose while the handler is still stuck
            await Task.Run(device.Stop, TestContext.Current.CancellationToken)
                .WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
            await Task.Run(device.Dispose, TestContext.Current.CancellationToken)
                .WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);

            // Assert: both returned, and the underlying device was released
            inner.Received(1).Stop();
            Assert.False(device.IsAvailable);
        }
        finally
        {
            // Cleanup: release the stuck handler
            releaseHandler.Set();
            await raise;
        }
    }

    /// <summary>Builds an available substitute capture device with the given format.</summary>
    private static IAudioCaptureDevice CreateInnerDevice(int sampleRate, int channelCount)
    {
        var device = Substitute.For<IAudioCaptureDevice>();
        device.IsAvailable.Returns(true);
        device.SampleRate.Returns(sampleRate);
        device.ChannelCount.Returns(channelCount);
        return device;
    }

    /// <summary>Raises one captured block on the substitute underlying device.</summary>
    private static void RaiseFrame(IAudioCaptureDevice inner, float[] samples) =>
        inner.FrameCaptured += Raise.Event<EventHandler<AudioCaptureFrameEventArgs>>(
            inner,
            new AudioCaptureFrameEventArgs(samples));

    /// <summary>Subscribes to <paramref name="device"/> and records every delivered block.</summary>
    private static List<float[]> Record(PreRollAudioCaptureDevice device)
    {
        var received = new List<float[]>();
        device.FrameCaptured += (_, e) => received.Add([.. e.Samples]);
        return received;
    }
}
