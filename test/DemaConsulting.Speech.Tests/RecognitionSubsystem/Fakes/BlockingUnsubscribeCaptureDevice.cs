using DemaConsulting.Speech.AudioSubsystem;

namespace DemaConsulting.Speech.Tests.RecognitionSubsystem.Fakes;

/// <summary>
///     Live <see cref="IAudioCaptureDevice"/> test double whose unsubscribe blocks until the test
///     releases it, standing in for a decorating device whose unsubscribe waits for a block it is
///     still dispatching to a stuck handler - so a session's teardown can be shown not to hang on it.
/// </summary>
internal sealed class BlockingUnsubscribeCaptureDevice : IAudioCaptureDevice
{
    /// <summary>The handle every unsubscribe waits on before it removes the handler.</summary>
    private readonly WaitHandle _releaseUnsubscribe;

    /// <summary>The subscribed handlers.</summary>
    private EventHandler<AudioCaptureFrameEventArgs>? _frameCaptured;

    /// <summary>
    ///     Initializes a new instance of the <see cref="BlockingUnsubscribeCaptureDevice"/> class.
    /// </summary>
    /// <param name="releaseUnsubscribe">The handle each unsubscribe waits on before returning.</param>
    public BlockingUnsubscribeCaptureDevice(WaitHandle releaseUnsubscribe) => _releaseUnsubscribe = releaseUnsubscribe;

    /// <summary>Gets the number of times <see cref="Stop"/> was called.</summary>
    public int StopCallCount { get; private set; }

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
        remove
        {
            _releaseUnsubscribe.WaitOne();
            _frameCaptured -= value;
        }
    }

    /// <inheritdoc/>
    public void Start()
    {
        // Nothing to start: blocks are raised by the test through Raise.
    }

    /// <inheritdoc/>
    public void Stop() => StopCallCount++;

    /// <summary>Raises one captured block to the current subscribers.</summary>
    /// <param name="samples">The samples to raise.</param>
    public void Raise(float[] samples) => _frameCaptured?.Invoke(this, new AudioCaptureFrameEventArgs(samples));
}
