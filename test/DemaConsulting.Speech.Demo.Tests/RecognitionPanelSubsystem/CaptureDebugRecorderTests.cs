using DemaConsulting.Speech.AudioSubsystem;
using DemaConsulting.Speech.Demo.RecognitionPanelSubsystem;
using NSubstitute;

namespace DemaConsulting.Speech.Demo.Tests.RecognitionPanelSubsystem;

/// <summary>
///     Unit tests for <see cref="CaptureDebugRecorder"/>.
/// </summary>
/// <remarks>
///     TEMPORARY diagnostic instrumentation test coverage, matching the scope of
///     <see cref="CaptureDebugRecorder"/> itself: these tests exist to give the capture-debug bug
///     investigation confidence that a normal recording session produces a valid, finalized
///     <c>.wav</c> file, and that the environment-variable opt-in gate behaves as documented -
///     not full production-feature test coverage. The bounded, lock-coordinated force-finalize
///     path <see cref="CaptureDebugRecorder.Dispose"/> falls back to when the writer thread does
///     not drain within its internal timeout is not exercised here, since reliably provoking that
///     specific timing window would require either a multi-second sleep per test run or reducing
///     the class's own (intentionally private, non-configurable) timeout constant.
/// </remarks>
[Collection(ProcessWideStateCollection.Name)]
public class CaptureDebugRecorderTests
{
    /// <summary>
    ///     Clears <see cref="CaptureDebugRecorder.EnvironmentVariableName"/> for the duration of a
    ///     test, restoring its prior value afterward, so tests never depend on (or leak into) the
    ///     ambient environment.
    /// </summary>
    private sealed class ScopedEnvironmentVariable : IDisposable
    {
        private readonly string? _priorValue;

        public ScopedEnvironmentVariable(string? value)
        {
            _priorValue = Environment.GetEnvironmentVariable(CaptureDebugRecorder.EnvironmentVariableName);
            Environment.SetEnvironmentVariable(CaptureDebugRecorder.EnvironmentVariableName, value);
        }

        public void Dispose() =>
            Environment.SetEnvironmentVariable(CaptureDebugRecorder.EnvironmentVariableName, _priorValue);
    }

    /// <summary>
    ///     Builds a fake capture device reporting the given sample rate and channel count, ready
    ///     to have <see cref="IAudioCaptureDevice.FrameCaptured"/> raised on it directly.
    /// </summary>
    private static IAudioCaptureDevice FakeDevice(int sampleRate = 16000, int channelCount = 1)
    {
        var device = Substitute.For<IAudioCaptureDevice>();
        device.SampleRate.Returns(sampleRate);
        device.ChannelCount.Returns(channelCount);
        return device;
    }

    /// <summary>
    ///     Proves that <see cref="CaptureDebugRecorder.TryStart"/> does nothing (and returns
    ///     <see langword="null"/>) when the opt-in environment variable is unset, so recording is
    ///     disabled by default.
    /// </summary>
    [Fact]
    public void CaptureDebugRecorder_TryStart_EnvironmentVariableUnset_ReturnsNull()
    {
        // Arrange
        using var scoped = new ScopedEnvironmentVariable(null);

        // Act
        var recorder = CaptureDebugRecorder.TryStart(FakeDevice());

        // Assert
        Assert.Null(recorder);
    }

    /// <summary>
    ///     Proves that a normal recording session - frames captured, then the recorder disposed -
    ///     produces a fully finalized, valid RIFF/WAVE header describing the actual bytes written,
    ///     exercising the same <see cref="CaptureDebugRecorder.Dispose"/> path used whenever the
    ///     writer thread drains within its timeout (the common case).
    /// </summary>
    [Fact]
    public void CaptureDebugRecorder_TryStart_RecordsFramesThenDisposed_WritesValidFinalizedWavFile()
    {
        // Arrange
        var directory = Path.Join(AppContext.BaseDirectory, "CaptureDebugRecorderTests", Guid.NewGuid().ToString("N"));
        using var scoped = new ScopedEnvironmentVariable(directory);

        var device = FakeDevice(sampleRate: 8000, channelCount: 1);
        var recorder = CaptureDebugRecorder.TryStart(device);
        Assert.NotNull(recorder);

        try
        {
            // Act
            // (raise a couple of frames as the real capture callback would, then stop)
            device.FrameCaptured += Raise.Event<EventHandler<AudioCaptureFrameEventArgs>>(
                device, new AudioCaptureFrameEventArgs([0.1f, -0.2f, 0.3f]));
            device.FrameCaptured += Raise.Event<EventHandler<AudioCaptureFrameEventArgs>>(
                device, new AudioCaptureFrameEventArgs([0.4f]));
        }
        finally
        {
            recorder.Dispose();
        }

        // Assert
        // (exactly one .wav file was written, with a header finalized to the real size)
        var files = Directory.GetFiles(directory, "*.wav");
        var path = Assert.Single(files);
        var bytes = File.ReadAllBytes(path);
        Assert.Equal(44 + 4 * 2, bytes.Length); // 44-byte header + 4 total samples * 16-bit PCM
        Assert.Equal("RIFF", System.Text.Encoding.ASCII.GetString(bytes, 0, 4));
        Assert.Equal(bytes.Length - 8, BitConverter.ToInt32(bytes, 4)); // RIFF chunk size patched
        Assert.Equal("WAVE", System.Text.Encoding.ASCII.GetString(bytes, 8, 4));
        Assert.Equal("data", System.Text.Encoding.ASCII.GetString(bytes, 36, 4));
        Assert.Equal(4 * 2, BitConverter.ToInt32(bytes, 40)); // data chunk size patched
    }

    /// <summary>
    ///     Proves that disposing a recorder that never received any frames still produces a
    ///     valid, empty (header-only) <c>.wav</c> file rather than leaving it never finalized.
    /// </summary>
    [Fact]
    public void CaptureDebugRecorder_Dispose_NoFramesCaptured_StillWritesValidFinalizedWavFile()
    {
        // Arrange
        var directory = Path.Join(AppContext.BaseDirectory, "CaptureDebugRecorderTests", Guid.NewGuid().ToString("N"));
        using var scoped = new ScopedEnvironmentVariable(directory);

        var recorder = CaptureDebugRecorder.TryStart(FakeDevice());
        Assert.NotNull(recorder);

        // Act
        recorder.Dispose();

        // Assert
        var files = Directory.GetFiles(directory, "*.wav");
        var path = Assert.Single(files);
        var bytes = File.ReadAllBytes(path);
        Assert.Equal(44, bytes.Length);
        Assert.Equal(0, BitConverter.ToInt32(bytes, 40)); // empty data chunk
    }

    /// <summary>
    ///     Proves that calling <see cref="CaptureDebugRecorder.Dispose"/> more than once is safe
    ///     and does not throw, matching the idempotent-disposal expectation of <see cref="IDisposable"/>.
    /// </summary>
    [Fact]
    public void CaptureDebugRecorder_Dispose_CalledTwice_DoesNotThrow()
    {
        // Arrange
        var directory = Path.Join(AppContext.BaseDirectory, "CaptureDebugRecorderTests", Guid.NewGuid().ToString("N"));
        using var scoped = new ScopedEnvironmentVariable(directory);

        var recorder = CaptureDebugRecorder.TryStart(FakeDevice());
        Assert.NotNull(recorder);
        recorder.Dispose();

        // Act
        var exception = Record.Exception(recorder.Dispose);

        // Assert
        Assert.Null(exception);
    }

    /// <summary>
    ///     Regression test: a capture callback that was already dispatched when the recorder was disposed
    ///     (so it still holds the handler) must be discarded, never throwing into the audio thread.
    /// </summary>
    [Fact]
    public void CaptureDebugRecorder_FrameCallback_AfterDispose_IsDiscardedWithoutThrowing()
    {
        // Arrange
        EventHandler<AudioCaptureFrameEventArgs>? captured = null;
        var device = FakeDevice();
        device.When(d => d.FrameCaptured += Arg.Any<EventHandler<AudioCaptureFrameEventArgs>>())
            .Do(call => captured = call.Arg<EventHandler<AudioCaptureFrameEventArgs>>());
        var written = new List<float>();
        var recorder = new CaptureDebugRecorder(device, written.AddRange, () => { });
        Assert.NotNull(captured);
        recorder.Dispose();

        // Act
        var exception = Record.Exception(() => captured(device, new AudioCaptureFrameEventArgs([0.5f])));

        // Assert
        Assert.Null(exception);
        Assert.Empty(written);
    }

    /// <summary>
    ///     Regression test: when finalizing the output throws, the failure is contained (not thrown from the
    ///     writer thread or from <see cref="CaptureDebugRecorder.Dispose"/>).
    /// </summary>
    [Fact]
    public void CaptureDebugRecorder_Dispose_WriterFinalizationThrows_IsContained()
    {
        // Arrange
        var device = FakeDevice();
        var closeCalls = 0;
        var recorder = new CaptureDebugRecorder(
            device,
            _ => { },
            () =>
            {
                closeCalls++;
                throw new IOException("finalize failed");
            });

        // Act
        var exception = Record.Exception(recorder.Dispose);

        // Assert
        Assert.Null(exception);
        Assert.Equal(1, closeCalls);
    }

    /// <summary>
    ///     Regression test: a non-IO write failure (such as an access error) stops writing, is contained, and the
    ///     output is still finalized exactly once.
    /// </summary>
    [Fact]
    public void CaptureDebugRecorder_FrameCaptured_WriteThrowsUnauthorized_StopsWritingAndFinalizesOnce()
    {
        // Arrange
        var device = FakeDevice();
        var closeCalls = 0;
        var writes = 0;
        var recorder = new CaptureDebugRecorder(
            device,
            _ =>
            {
                writes++;
                throw new UnauthorizedAccessException("denied");
            },
            () => closeCalls++);

        // Act
        device.FrameCaptured += Raise.Event<EventHandler<AudioCaptureFrameEventArgs>>(
            device, new AudioCaptureFrameEventArgs([0.1f]));
        var exception = Record.Exception(recorder.Dispose);

        // Assert
        Assert.Null(exception);
        Assert.Equal(1, writes);
        Assert.Equal(1, closeCalls);
    }
}