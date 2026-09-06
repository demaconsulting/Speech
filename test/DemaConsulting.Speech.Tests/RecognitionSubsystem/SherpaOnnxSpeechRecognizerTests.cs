using DemaConsulting.Speech.AudioSubsystem;
using DemaConsulting.Speech.Diagnostics;
using DemaConsulting.Speech.ModelManagementSubsystem;
using DemaConsulting.Speech.RecognitionSubsystem;
using DemaConsulting.Speech.Tests.ModelManagementSubsystem.Fakes;
using DemaConsulting.Speech.Tests.RecognitionSubsystem.Fakes;
using NSubstitute;

namespace DemaConsulting.Speech.Tests.RecognitionSubsystem;

/// <summary>
///     Unit tests for <see cref="SherpaOnnxSpeechRecognizer"/>, exercising the full capture →
///     resample → engine → event pipeline through a substitute capture device and a fake engine,
///     with no microphone and no native sherpa-onnx runtime.
/// </summary>
/// <remarks>
///     Every test is deterministic without timing assumptions: <c>Stop()</c> completes the
///     recognizer's internal queue and joins its consumer task, so all results derived from
///     frames raised before the call have been delivered by the time it returns.
/// </remarks>
public class SherpaOnnxSpeechRecognizerTests
{
    /// <summary>
    ///     Proves that starting subscribes to the capture device and starts it, so audio begins
    ///     flowing only once the recognizer is ready to receive it.
    /// </summary>
    [Fact]
    public void SherpaOnnxSpeechRecognizer_Start_Always_SubscribesAndStartsCaptureDevice()
    {
        // Arrange: a substitute mono 16 kHz capture device and a fake engine
        var captureDevice = CreateCaptureDevice(sampleRate: 16000, channelCount: 1);
        using var recognizer = new SherpaOnnxSpeechRecognizer(
            new FakeRecognitionEngine(), captureDevice, 16000, new FakeRecognitionModel());

        // Act: start recognition
        recognizer.Start();

        // Assert: the device was started and the recognizer reports itself available
        captureDevice.Received(1).Start();
        Assert.True(recognizer.IsAvailable);

        // Cleanup: stop so the consumer task is joined before the test ends
        recognizer.Stop();
    }

    /// <summary>
    ///     Proves that starting an already-running recognizer is a safe no-op rather than opening
    ///     a second capture session.
    /// </summary>
    [Fact]
    public void SherpaOnnxSpeechRecognizer_Start_AlreadyRunning_IsNoOp()
    {
        // Arrange: a running recognizer over a substitute device
        var captureDevice = CreateCaptureDevice(sampleRate: 16000, channelCount: 1);
        using var recognizer = new SherpaOnnxSpeechRecognizer(
            new FakeRecognitionEngine(), captureDevice, 16000, new FakeRecognitionModel());
        recognizer.Start();

        // Act: start again
        recognizer.Start();

        // Assert: the device was started exactly once
        captureDevice.Received(1).Start();

        // Cleanup
        recognizer.Stop();
    }

    /// <summary>
    ///     Proves that a captured frame flows through downmixing and resampling into the engine,
    ///     converted to the mono rate the model declared.
    /// </summary>
    [Fact]
    public void SherpaOnnxSpeechRecognizer_FrameCaptured_StereoAtHigherRate_FeedsResampledMonoToEngine()
    {
        // Arrange: a stereo 32 kHz device feeding a 16 kHz model
        var captureDevice = CreateCaptureDevice(sampleRate: 32000, channelCount: 2);
        var engine = new FakeRecognitionEngine();
        using var recognizer = new SherpaOnnxSpeechRecognizer(engine, captureDevice, 16000, new FakeRecognitionModel());
        recognizer.Start();

        // Act: raise one stereo block of four frames, then stop to drain the pipeline
        RaiseFrameCaptured(captureDevice, [0.0f, 0.0f, 1.0f, 1.0f, 2.0f, 2.0f, 3.0f, 3.0f]);
        recognizer.Stop();

        // Assert: four stereo frames became four mono samples, then anti-aliased and halved by
        // the resampler
        Assert.Equal(1, engine.AcceptSamplesCallCount);
        Assert.Collection(
            engine.AcceptedSamples,
            sample => Assert.InRange(sample, 0.11705f, 0.11706f),
            sample => Assert.InRange(sample, 2.06629f, 2.06630f));
    }

    /// <summary>
    ///     Proves that each result the engine decodes is raised through
    ///     <see cref="ISpeechRecognizer.ResultReceived"/>, preserving both provisional and final
    ///     results in order.
    /// </summary>
    [Fact]
    public void SherpaOnnxSpeechRecognizer_FrameCaptured_EngineDecodesResults_RaisesResultReceivedInOrder()
    {
        // Arrange: an engine scripted to yield one partial then one final result
        var captureDevice = CreateCaptureDevice(sampleRate: 16000, channelCount: 1);
        var engine = new FakeRecognitionEngine(
        [
            new SpeechRecognitionResult("hello", IsFinal: false),
            new SpeechRecognitionResult("hello world", IsFinal: true)
        ]);
        using var recognizer = new SherpaOnnxSpeechRecognizer(engine, captureDevice, 16000, new FakeRecognitionModel());
        var received = new List<SpeechRecognitionResult>();
        recognizer.ResultReceived += (_, args) => received.Add(args.Result);
        recognizer.Start();

        // Act: raise one frame, then stop to drain the pipeline
        RaiseFrameCaptured(captureDevice, [0.1f, 0.2f, 0.3f]);
        recognizer.Stop();

        // Assert: both results arrived, in order, with their finality preserved
        Assert.Equal(2, received.Count);
        Assert.Equal(new SpeechRecognitionResult("hello", IsFinal: false), received[0]);
        Assert.Equal(new SpeechRecognitionResult("hello world", IsFinal: true), received[1]);
    }

    /// <summary>
    ///     Proves that stopping unsubscribes from the capture device and stops it, so no further
    ///     audio is consumed after the call returns.
    /// </summary>
    [Fact]
    public void SherpaOnnxSpeechRecognizer_Stop_WhileRunning_UnsubscribesAndStopsCaptureDevice()
    {
        // Arrange: a running recognizer with a scripted engine
        var captureDevice = CreateCaptureDevice(sampleRate: 16000, channelCount: 1);
        var engine = new FakeRecognitionEngine([new SpeechRecognitionResult("ignored", IsFinal: true)]);
        using var recognizer = new SherpaOnnxSpeechRecognizer(engine, captureDevice, 16000, new FakeRecognitionModel());
        var received = new List<SpeechRecognitionResult>();
        recognizer.ResultReceived += (_, args) => received.Add(args.Result);
        recognizer.Start();

        // Act: stop, then raise a frame that must no longer be consumed
        recognizer.Stop();
        RaiseFrameCaptured(captureDevice, [0.5f, 0.5f]);

        // Assert: the device was stopped and the post-stop frame never reached the engine
        captureDevice.Received(1).Stop();
        Assert.Equal(0, engine.AcceptSamplesCallCount);
        Assert.Empty(received);
    }

    /// <summary>
    ///     Proves that stopping a recognizer that was never started is a safe no-op.
    /// </summary>
    [Fact]
    public void SherpaOnnxSpeechRecognizer_Stop_NotRunning_IsNoOp()
    {
        // Arrange: a recognizer that has never been started
        var captureDevice = CreateCaptureDevice(sampleRate: 16000, channelCount: 1);
        using var recognizer = new SherpaOnnxSpeechRecognizer(
            new FakeRecognitionEngine(), captureDevice, 16000, new FakeRecognitionModel());

        // Act: stop without ever starting
        var exception = Record.Exception(recognizer.Stop);

        // Assert: nothing threw and the device was never touched
        Assert.Null(exception);
        captureDevice.DidNotReceive().Stop();
    }

    /// <summary>
    ///     Proves that disposal stops the pipeline, disposes the owned engine, and is idempotent.
    /// </summary>
    [Fact]
    public void SherpaOnnxSpeechRecognizer_Dispose_CalledTwice_StopsAndDisposesEngineOnce()
    {
        // Arrange: a running recognizer with a fake engine
        var captureDevice = CreateCaptureDevice(sampleRate: 16000, channelCount: 1);
        var engine = new FakeRecognitionEngine();
        var recognizer = new SherpaOnnxSpeechRecognizer(engine, captureDevice, 16000, new FakeRecognitionModel());
        recognizer.Start();

        // Act: dispose twice
        recognizer.Dispose();
        recognizer.Dispose();

        // Assert: the device was stopped once and the engine disposed exactly once
        captureDevice.Received(1).Stop();
        Assert.Equal(1, engine.DisposeCallCount);
    }

    /// <summary>
    ///     Proves that starting a disposed recognizer throws
    ///     <see cref="ObjectDisposedException"/> rather than silently doing nothing.
    /// </summary>
    [Fact]
    public void SherpaOnnxSpeechRecognizer_Start_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange: a disposed recognizer
        var captureDevice = CreateCaptureDevice(sampleRate: 16000, channelCount: 1);
        var recognizer = new SherpaOnnxSpeechRecognizer(
            new FakeRecognitionEngine(), captureDevice, 16000, new FakeRecognitionModel());
        recognizer.Dispose();

        // Act & Assert: starting after disposal is rejected
        Assert.Throws<ObjectDisposedException>(recognizer.Start);
    }

    /// <summary>
    ///     Proves that a capture device which claimed to be available but fails on start surfaces
    ///     as <see cref="SpeechRecognizerUnavailableException"/> and leaves the recognizer stopped.
    /// </summary>
    [Fact]
    public void SherpaOnnxSpeechRecognizer_Start_CaptureDeviceFails_ThrowsSpeechRecognizerUnavailableException()
    {
        // Arrange: a device whose Start throws
        var captureDevice = CreateCaptureDevice(sampleRate: 16000, channelCount: 1);
        captureDevice
            .When(device => device.Start())
            .Do(_ => throw new AudioDeviceUnavailableException("native stream open failed"));
        var diagnostics = Substitute.For<ISpeechDiagnostics>();
        using var recognizer = new SherpaOnnxSpeechRecognizer(
            new FakeRecognitionEngine(), captureDevice, 16000, new FakeRecognitionModel(), diagnostics);

        // Act & Assert: the first-use failure is surfaced as the documented exception
        var exception = Assert.Throws<SpeechRecognizerUnavailableException>(recognizer.Start);
        Assert.IsType<AudioDeviceUnavailableException>(exception.InnerException);
        diagnostics.Received().Report(
            SpeechDiagnosticLevel.Error,
            "RecognitionSubsystem",
            Arg.Is<string>(message => message.Contains("capture device could not start", StringComparison.Ordinal)));
    }

    /// <summary>
    ///     Proves that a host result handler which throws is contained and reported through the
    ///     diagnostics sink, never rethrown into the capture callback and never stopping the
    ///     recognizer.
    /// </summary>
    [Fact]
    public void SherpaOnnxSpeechRecognizer_ResultReceived_HandlerThrows_ReportsFaultAndDoesNotRethrow()
    {
        // Arrange: a running recognizer whose result handler always throws
        var captureDevice = CreateCaptureDevice(sampleRate: 16000, channelCount: 1);
        var diagnostics = Substitute.For<ISpeechDiagnostics>();
        var engine = new FakeRecognitionEngine([new SpeechRecognitionResult("boom", IsFinal: true)]);
        using var recognizer = new SherpaOnnxSpeechRecognizer(engine, captureDevice, 16000, new FakeRecognitionModel(), diagnostics);
        recognizer.ResultReceived += (_, _) => throw new InvalidOperationException("handler failed");
        recognizer.Start();

        // Act: raise a frame that produces a result, then drain
        var exception = Record.Exception(() =>
        {
            RaiseFrameCaptured(captureDevice, [0.1f, 0.2f]);
            recognizer.Stop();
        });

        // Assert: nothing escaped, and the fault was reported as a structural fact
        Assert.Null(exception);
        diagnostics.Received().Report(
            SpeechDiagnosticLevel.Error,
            "RecognitionSubsystem",
            Arg.Is<string>(message => message.Contains("could not be recognized", StringComparison.Ordinal)));
    }

    /// <summary>
    ///     Proves that an engine fault while consuming a frame is contained and reported, so one
    ///     bad block never ends the session.
    /// </summary>
    [Fact]
    public void SherpaOnnxSpeechRecognizer_FrameCaptured_EngineThrows_ReportsFaultAndKeepsRunning()
    {
        // Arrange: a running recognizer over an engine that always faults on accept
        var captureDevice = CreateCaptureDevice(sampleRate: 16000, channelCount: 1);
        var diagnostics = Substitute.For<ISpeechDiagnostics>();
        var engine = new FakeRecognitionEngine(
            acceptSamplesException: new InvalidOperationException("engine failed"));
        using var recognizer = new SherpaOnnxSpeechRecognizer(engine, captureDevice, 16000, new FakeRecognitionModel(), diagnostics);
        recognizer.Start();

        // Act: raise two frames, then drain
        RaiseFrameCaptured(captureDevice, [0.1f]);
        RaiseFrameCaptured(captureDevice, [0.2f]);
        recognizer.Stop();

        // Assert: both frames were still delivered to the engine and both faults were reported
        Assert.Equal(2, engine.AcceptSamplesCallCount);
        diagnostics.Received(2).Report(
            SpeechDiagnosticLevel.Error,
            "RecognitionSubsystem",
            Arg.Is<string>(message => message.Contains("could not be recognized", StringComparison.Ordinal)));
    }

    /// <summary>
    ///     Proves that a capture device reporting an unusable audio format degrades to a
    ///     pass-through conversion and reports the fact, rather than throwing at composition.
    /// </summary>
    [Fact]
    public void SherpaOnnxSpeechRecognizer_Constructor_DeviceReportsUnusableFormat_FallsBackToPassThrough()
    {
        // Arrange: a device reporting a zero sample rate and zero channels
        var captureDevice = CreateCaptureDevice(sampleRate: 0, channelCount: 0);
        var diagnostics = Substitute.For<ISpeechDiagnostics>();
        var engine = new FakeRecognitionEngine();

        // Act: compose, then push one frame through the pipeline
        using var recognizer = new SherpaOnnxSpeechRecognizer(engine, captureDevice, 16000, new FakeRecognitionModel(), diagnostics);
        recognizer.Start();
        RaiseFrameCaptured(captureDevice, [0.1f, 0.2f]);
        recognizer.Stop();

        // Assert: the format warning was reported and the samples passed through untouched
        diagnostics.Received().Report(
            SpeechDiagnosticLevel.Warning,
            "RecognitionSubsystem",
            Arg.Is<string>(message => message.Contains("unusable audio format", StringComparison.Ordinal)));
        Assert.Equal([0.1f, 0.2f], engine.AcceptedSamples);
    }

    /// <summary>
    ///     Proves that an empty capture block is ignored rather than being pushed into the engine.
    /// </summary>
    [Fact]
    public void SherpaOnnxSpeechRecognizer_FrameCaptured_EmptyBlock_IsIgnored()
    {
        // Arrange: a running recognizer over a fake engine
        var captureDevice = CreateCaptureDevice(sampleRate: 16000, channelCount: 1);
        var engine = new FakeRecognitionEngine();
        using var recognizer = new SherpaOnnxSpeechRecognizer(engine, captureDevice, 16000, new FakeRecognitionModel());
        recognizer.Start();

        // Act: raise an empty block, then drain
        RaiseFrameCaptured(captureDevice, []);
        recognizer.Stop();

        // Assert: the engine was never asked to accept anything
        Assert.Equal(0, engine.AcceptSamplesCallCount);
    }

    /// <summary>
    ///     Proves that the constructor rejects a null model, since every recognizer must have an
    ///     owning model to apply its <see cref="IRecognitionModel.NormalizeText(string,bool)"/>
    ///     hook to results.
    /// </summary>
    [Fact]
    public void SherpaOnnxSpeechRecognizer_Constructor_NullModel_ThrowsArgumentNullException()
    {
        // Arrange
        var captureDevice = CreateCaptureDevice(sampleRate: 16000, channelCount: 1);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new SherpaOnnxSpeechRecognizer(new FakeRecognitionEngine(), captureDevice, 16000, null!));
    }

    /// <summary>
    ///     Proves that a final result's text is passed through the owning model's
    ///     <see cref="IRecognitionModel.NormalizeText(string,bool)"/> hook, with <c>isFinal</c>
    ///     set to <see langword="true"/>, before <see cref="ISpeechRecognizer.ResultReceived"/>
    ///     raises it - proving the wiring itself, not just the hook's own logic.
    /// </summary>
    [Fact]
    public void SherpaOnnxSpeechRecognizer_FrameCaptured_FinalResult_AppliesModelNormalizeTextWithIsFinalTrue()
    {
        // Arrange: a model whose NormalizeText is a distinguishable, recorded transform
        var captureDevice = CreateCaptureDevice(sampleRate: 16000, channelCount: 1);
        var engine = new FakeRecognitionEngine([new SpeechRecognitionResult("HELLO WORLD", IsFinal: true)]);
        var calls = new List<(string Text, bool IsFinal)>();
        var model = new FakeRecognitionModel(normalizeText: (text, isFinal) =>
        {
            calls.Add((text, isFinal));
            return $"normalized:{text}";
        });
        using var recognizer = new SherpaOnnxSpeechRecognizer(engine, captureDevice, 16000, model);
        var received = new List<SpeechRecognitionResult>();
        recognizer.ResultReceived += (_, args) => received.Add(args.Result);
        recognizer.Start();

        // Act: raise one frame that produces the scripted final result, then drain
        RaiseFrameCaptured(captureDevice, [0.1f, 0.2f]);
        recognizer.Stop();

        // Assert: the model saw the raw text with isFinal true, and the raised result carries
        // the model's normalized text while preserving IsFinal
        var result = Assert.Single(received);
        Assert.Equal("normalized:HELLO WORLD", result.Text);
        Assert.True(result.IsFinal);
        var call = Assert.Single(calls);
        Assert.Equal("HELLO WORLD", call.Text);
        Assert.True(call.IsFinal);
    }

    /// <summary>
    ///     Proves that a provisional result's text is passed through the owning model's
    ///     <see cref="IRecognitionModel.NormalizeText(string,bool)"/> hook, with <c>isFinal</c>
    ///     set to <see langword="false"/>, before <see cref="ISpeechRecognizer.ResultReceived"/>
    ///     raises it.
    /// </summary>
    [Fact]
    public void SherpaOnnxSpeechRecognizer_FrameCaptured_ProvisionalResult_AppliesModelNormalizeTextWithIsFinalFalse()
    {
        // Arrange: a model whose NormalizeText is a distinguishable, recorded transform
        var captureDevice = CreateCaptureDevice(sampleRate: 16000, channelCount: 1);
        var engine = new FakeRecognitionEngine([new SpeechRecognitionResult("HELLO", IsFinal: false)]);
        var calls = new List<(string Text, bool IsFinal)>();
        var model = new FakeRecognitionModel(normalizeText: (text, isFinal) =>
        {
            calls.Add((text, isFinal));
            return $"normalized:{text}";
        });
        using var recognizer = new SherpaOnnxSpeechRecognizer(engine, captureDevice, 16000, model);
        var received = new List<SpeechRecognitionResult>();
        recognizer.ResultReceived += (_, args) => received.Add(args.Result);
        recognizer.Start();

        // Act: raise one frame that produces the scripted provisional result, then drain
        RaiseFrameCaptured(captureDevice, [0.1f, 0.2f]);
        recognizer.Stop();

        // Assert: the model saw the raw text with isFinal false, and the raised result carries
        // the model's normalized text while preserving IsFinal
        var result = Assert.Single(received);
        Assert.Equal("normalized:HELLO", result.Text);
        Assert.False(result.IsFinal);
        var call = Assert.Single(calls);
        Assert.Equal("HELLO", call.Text);
        Assert.False(call.IsFinal);
    }

    /// <summary>
    ///     Builds a substitute capture device reporting itself available with the given capture
    ///     format.
    /// </summary>
    /// <param name="sampleRate">The rate the device reports.</param>
    /// <param name="channelCount">The channel count the device reports.</param>
    /// <returns>The configured substitute capture device.</returns>
    private static IAudioCaptureDevice CreateCaptureDevice(int sampleRate, int channelCount)
    {
        var captureDevice = Substitute.For<IAudioCaptureDevice>();
        captureDevice.IsAvailable.Returns(true);
        captureDevice.SampleRate.Returns(sampleRate);
        captureDevice.ChannelCount.Returns(channelCount);
        return captureDevice;
    }

    /// <summary>
    ///     Raises the substitute capture device's <see cref="IAudioCaptureDevice.FrameCaptured"/>
    ///     event with one block of interleaved samples, standing in for a real audio callback.
    /// </summary>
    /// <param name="captureDevice">The substitute capture device to raise the event on.</param>
    /// <param name="samples">The interleaved samples the block carries.</param>
    private static void RaiseFrameCaptured(IAudioCaptureDevice captureDevice, float[] samples)
    {
        captureDevice.FrameCaptured += Raise.Event<EventHandler<AudioCaptureFrameEventArgs>>(
            captureDevice,
            new AudioCaptureFrameEventArgs(samples));
    }
}
