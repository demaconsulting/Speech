using DemaConsulting.Speech.AudioSubsystem;
using DemaConsulting.Speech.Diagnostics;
using DemaConsulting.Speech.ModelManagementSubsystem;
using DemaConsulting.Speech.RecognitionSubsystem;
using DemaConsulting.Speech.Tests.ModelManagementSubsystem.Fakes;
using DemaConsulting.Speech.Tests.RecognitionSubsystem.Fakes;
using NSubstitute;

namespace DemaConsulting.Speech.Tests.RecognitionSubsystem;

/// <summary>
///     Unit tests for <see cref="SherpaOnnxRecognitionSession"/>, exercising the full capture →
///     resample → backend → buffered-result pipeline through a substitute capture device and a
///     fake backend, with no microphone and no native sherpa-onnx runtime.
/// </summary>
/// <remarks>
///     Every test constructs its own session directly (bypassing <see cref="SherpaOnnxSpeechRecognizerEngine"/>,
///     whose lease behavior is covered by <c>SherpaOnnxSpeechRecognizerEngineTests</c>) with a no-op
///     <c>releaseLease</c> callback, since this session's own lifecycle is what is under test.
/// </remarks>
public class SherpaOnnxRecognitionSessionTests
{
    /// <summary>
    ///     Proves that starting a freshly created session subscribes to and starts the capture
    ///     device and transitions it to <see cref="RecognitionSessionState.Running"/>.
    /// </summary>
    [Fact]
    public async Task SherpaOnnxRecognitionSession_StartAsync_FromCreated_TransitionsToRunning()
    {
        // Arrange
        var device = CreateCaptureDevice();
        await using var session = CreateSession(new FakeRecognitionEngine(), device);

        // Act
        await session.StartAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(RecognitionSessionState.Running, session.State);
        device.Received(1).Start();
    }

    /// <summary>
    ///     Proves that starting a session that has already reached
    ///     <see cref="RecognitionSessionState.Stopped"/> throws <see cref="InvalidOperationException"/>
    ///     rather than permitting a restart (Decision #1): a session is single-use.
    /// </summary>
    [Fact]
    public async Task SherpaOnnxRecognitionSession_StartAsync_FromStopped_ThrowsInvalidOperationException()
    {
        // Arrange: a session that was started and stopped once already
        await using var session = CreateSession(new FakeRecognitionEngine(), CreateCaptureDevice());
        await session.StartAsync(TestContext.Current.CancellationToken);
        await session.StopAsync(TestContext.Current.CancellationToken);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => session.StartAsync(TestContext.Current.CancellationToken));
    }

    /// <summary>
    ///     Proves that two concurrent <see cref="SherpaOnnxRecognitionSession.StopAsync"/> calls
    ///     both complete once the session has converged on <see cref="RecognitionSessionState.Stopped"/>,
    ///     rather than one of them hanging or throwing.
    /// </summary>
    [Fact]
    public async Task SherpaOnnxRecognitionSession_StopAsync_CalledConcurrentlyTwice_BothCompleteOnceStopped()
    {
        // Arrange
        var engine = new FakeRecognitionEngine();
        var device = CreateCaptureDevice();
        await using var session = CreateSession(engine, device);
        await session.StartAsync(TestContext.Current.CancellationToken);

        // Act: stop concurrently from two callers
        var first = session.StopAsync(TestContext.Current.CancellationToken);
        var second = session.StopAsync(TestContext.Current.CancellationToken);
        await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        // Assert: both callers converged, and the non-idempotent teardown steps ran exactly once -
        // the second caller must not have repeated them after observing the Stopping state
        Assert.Equal(RecognitionSessionState.Stopped, session.State);
        Assert.Equal(1, engine.ResetCallCount);
        device.Received(1).Stop();
    }

    /// <summary>
    ///     Proves that stopping flushes any trailing audio the backend had accepted but not yet
    ///     decoded, so the flushed final result is enumerable via
    ///     <see cref="SherpaOnnxRecognitionSession.GetResultsAsync"/> once the stop completes.
    /// </summary>
    [Fact]
    public async Task SherpaOnnxRecognitionSession_StopAsync_FlushesTrailingResultsBeforeCompleting()
    {
        // Arrange: a backend that yields one flushed final result when the stream ends
        var engine = new FakeRecognitionEngine(scriptedFlushResult: new SpeechRecognitionResult("cut off", IsFinal: true));
        await using var session = CreateSession(engine, CreateCaptureDevice());
        await session.StartAsync(TestContext.Current.CancellationToken);

        // Act
        await session.StopAsync(TestContext.Current.CancellationToken);

        // Assert: the flushed final result is enumerable, and the enumeration ends cleanly since
        // the buffer was completed by the same stop
        var results = await CollectAsync(session.GetResultsAsync(TestContext.Current.CancellationToken));
        var result = Assert.Single(results);
        Assert.Equal("cut off", result.Result.Text);
        Assert.True(result.Result.IsFinal);
    }

    /// <summary>
    ///     Proves that <see cref="SherpaOnnxRecognitionSession.GetResultsAsync"/> is single-consumer:
    ///     a second concurrent enumeration attempt, made while one enumeration is still active,
    ///     throws <see cref="InvalidOperationException"/> immediately.
    /// </summary>
    [Fact]
    public async Task SherpaOnnxRecognitionSession_GetResultsAsync_CalledConcurrently_ThrowsInvalidOperationException()
    {
        // Arrange: a session with no data and no completion yet, so the first enumeration blocks
        // waiting for more
        await using var session = CreateSession(new FakeRecognitionEngine(), CreateCaptureDevice());
        using var firstEnumerationCts = new CancellationTokenSource();
        var firstEnumerator = session.GetResultsAsync(firstEnumerationCts.Token).GetAsyncEnumerator(TestContext.Current.CancellationToken);
        var firstMoveNext = firstEnumerator.MoveNextAsync();

        // Act & Assert: a second, concurrent enumeration attempt fails fast
        var secondEnumerator = session.GetResultsAsync(TestContext.Current.CancellationToken).GetAsyncEnumerator(TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await secondEnumerator.MoveNextAsync());

        // Cleanup: release the first, still-pending enumeration
        await firstEnumerationCts.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await firstMoveNext);
    }

    /// <summary>
    ///     Proves that cancelling the token passed to <see cref="SherpaOnnxRecognitionSession.GetResultsAsync"/>
    ///     ends only that enumeration - the session itself keeps running and is not stopped.
    /// </summary>
    [Fact]
    public async Task SherpaOnnxRecognitionSession_GetResultsAsync_CancelledToken_EndsEnumerationWithoutStoppingSession()
    {
        // Arrange: a running session with an active enumeration
        await using var session = CreateSession(new FakeRecognitionEngine(), CreateCaptureDevice());
        await session.StartAsync(TestContext.Current.CancellationToken);
        using var cts = new CancellationTokenSource();
        var enumerationTask = CollectAsync(session.GetResultsAsync(cts.Token));

        // Act: cancel only the enumeration's token
        await cts.CancelAsync();

        // Assert: the enumeration ends via cancellation, but the session is still running
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => enumerationTask);
        Assert.Equal(RecognitionSessionState.Running, session.State);

        // Cleanup
        await session.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    ///     Proves that a slow consumer of <see cref="SherpaOnnxRecognitionSession.GetResultsAsync"/>
    ///     never sees every provisional result produced while it was not reading: later
    ///     provisional results coalesce into the latest one (Decision #5).
    /// </summary>
    [Fact]
    public async Task SherpaOnnxRecognitionSession_GetResultsAsync_SlowConsumer_CoalescesProvisionalResults()
    {
        // Arrange: a backend that yields three successive provisional results before anything
        // ever reads them
        var engine = new FakeRecognitionEngine(
        [
            new SpeechRecognitionResult("a", IsFinal: false),
            new SpeechRecognitionResult("ab", IsFinal: false),
            new SpeechRecognitionResult("abc", IsFinal: false)
        ]);
        var device = CreateCaptureDevice();
        await using var session = CreateSession(engine, device);
        await session.StartAsync(TestContext.Current.CancellationToken);

        // Act: feed one block (decoded eagerly into all three scripted provisional results) with
        // no consumer reading yet, then stop and read back what survived
        RaiseFrameCaptured(device, [0.1f]);
        await session.StopAsync(TestContext.Current.CancellationToken);
        var results = await CollectAsync(session.GetResultsAsync(TestContext.Current.CancellationToken));

        // Assert: only the latest provisional result survived the overwrite
        var result = Assert.Single(results);
        Assert.Equal("abc", result.Result.Text);
        Assert.False(result.Result.IsFinal);
    }

    /// <summary>
    ///     Proves that a slow consumer of <see cref="SherpaOnnxRecognitionSession.GetResultsAsync"/>
    ///     never loses a final result while the buffered backlog remains under the byte cap
    ///     (Decision #5): every final result is still delivered, in order.
    /// </summary>
    [Fact]
    public async Task SherpaOnnxRecognitionSession_GetResultsAsync_SlowConsumer_NeverDropsFinalResultsUnderByteCap()
    {
        // Arrange: a backend that yields several short final results before anything reads them
        var engine = new FakeRecognitionEngine(
        [
            new SpeechRecognitionResult("one", IsFinal: true),
            new SpeechRecognitionResult("two", IsFinal: true),
            new SpeechRecognitionResult("three", IsFinal: true)
        ]);
        var device = CreateCaptureDevice();
        await using var session = CreateSession(engine, device);
        await session.StartAsync(TestContext.Current.CancellationToken);

        // Act
        RaiseFrameCaptured(device, [0.1f]);
        await session.StopAsync(TestContext.Current.CancellationToken);
        var results = await CollectAsync(session.GetResultsAsync(TestContext.Current.CancellationToken));

        // Assert: every final result survived, in order
        Assert.Collection(
            results,
            r => Assert.Equal("one", r.Result.Text),
            r => Assert.Equal("two", r.Result.Text),
            r => Assert.Equal("three", r.Result.Text));
    }

    /// <summary>
    ///     Proves that once a session has transitioned to <see cref="RecognitionSessionState.Faulted"/>,
    ///     enumerating <see cref="SherpaOnnxRecognitionSession.GetResultsAsync"/> throws
    ///     <see cref="RecognitionSessionFaultedException"/> from the enumerator.
    /// </summary>
    [Fact]
    public async Task SherpaOnnxRecognitionSession_GetResultsAsync_SessionFaulted_ThrowsRecognitionSessionFaultedException()
    {
        // Arrange: a running session whose device goes unavailable mid-session
        var device = CreateCaptureDevice();
        await using var session = CreateSession(new FakeRecognitionEngine(), device);
        await session.StartAsync(TestContext.Current.CancellationToken);
        device.IsAvailable.Returns(false);
        RaiseFrameCaptured(device, [0.1f]);

        // Act & Assert
        await Assert.ThrowsAsync<RecognitionSessionFaultedException>(async () =>
        {
            await foreach (var _ in session.GetResultsAsync(TestContext.Current.CancellationToken))
            {
                // No iterations are expected to survive: the fault is observed before/at this point.
            }
        });
    }

    /// <summary>
    ///     Proves that a dedicated worker's delegate which never observes cancellation (standing
    ///     in for a stuck native call) is bounded by the abandon-timeout policy rather than
    ///     hanging <see cref="SherpaOnnxRecognitionSession.StopAsync"/> forever, and that the
    ///     abandonment is reported through diagnostics (Decision #4).
    /// </summary>
    [Fact]
    public async Task SherpaOnnxRecognitionSession_NativeCallExceedsAbandonTimeout_TaskCompletesAndDiagnosticsReportsWarning()
    {
        // Arrange: a backend whose AcceptSamples blocks forever, and a worker with a near-zero
        // abandon timeout so the test stays fast
        using var neverSignaled = new ManualResetEvent(false);
        var engine = new FakeRecognitionEngine(acceptSamplesBlock: neverSignaled);
        var diagnostics = Substitute.For<ISpeechDiagnostics>();
        var worker = new DedicatedWorker(
            abandonTimeout: TimeSpan.FromMilliseconds(1),
            diagnostics: diagnostics,
            diagnosticsCategory: "RecognitionSubsystem");
        var device = CreateCaptureDevice();
        await using var session = CreateSession(engine, device, worker: worker);
        await session.StartAsync(TestContext.Current.CancellationToken);
        RaiseFrameCaptured(device, [0.1f]);

        // Give the pump thread a moment to actually enter the blocking call before stopping
        await Task.Delay(TimeSpan.FromMilliseconds(50), TestContext.Current.CancellationToken);

        // Act: stop must complete within a bounded time, not hang on the stuck backend call
        await session.StopAsync(TestContext.Current.CancellationToken).WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        // Assert: teardown still converged, and the abandonment was reported
        Assert.Equal(RecognitionSessionState.Stopped, session.State);
        diagnostics.Received().Report(
            SpeechDiagnosticLevel.Warning,
            "RecognitionSubsystem",
            Arg.Is<string>(message => message.Contains("abandon", StringComparison.OrdinalIgnoreCase)));

        // Cleanup: release the abandoned background thread so it can exit
        neverSignaled.Set();
    }

    /// <summary>
    ///     Proves that a capture device going unavailable mid-session transitions the session to
    ///     <see cref="RecognitionSessionState.Faulted"/>.
    /// </summary>
    [Fact]
    public async Task SherpaOnnxRecognitionSession_DeviceLostMidSession_TransitionsToFaulted()
    {
        // Arrange: a running session whose device later reports itself unavailable
        var device = CreateCaptureDevice();
        await using var session = CreateSession(new FakeRecognitionEngine(), device);
        await session.StartAsync(TestContext.Current.CancellationToken);

        // Act
        device.IsAvailable.Returns(false);
        RaiseFrameCaptured(device, [0.1f]);

        // Assert
        Assert.Equal(RecognitionSessionState.Faulted, session.State);
    }

    /// <summary>
    ///     Proves that <see cref="SherpaOnnxRecognitionSession.StateChanged"/> raises every state
    ///     transition, in order, across a full Start/Stop lifecycle.
    /// </summary>
    [Fact]
    public async Task SherpaOnnxRecognitionSession_StateChanged_EmitsEveryTransitionInOrder()
    {
        // Arrange
        await using var session = CreateSession(new FakeRecognitionEngine(), CreateCaptureDevice());
        var transitions = new List<(RecognitionSessionState Previous, RecognitionSessionState Current)>();
        session.StateChanged += (_, args) => transitions.Add((args.Previous, args.Current));

        // Act
        await session.StartAsync(TestContext.Current.CancellationToken);
        await session.StopAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
        [
            (RecognitionSessionState.Created, RecognitionSessionState.Starting),
            (RecognitionSessionState.Starting, RecognitionSessionState.Running),
            (RecognitionSessionState.Running, RecognitionSessionState.Stopping),
            (RecognitionSessionState.Stopping, RecognitionSessionState.Stopped)
        ], transitions);
    }

    /// <summary>
    ///     Proves that a captured frame still flows through downmixing into the backend - the
    ///     same pipeline <c>SherpaOnnxSpeechRecognizer</c> used before the Engine/Session split.
    ///     Uses a device sample rate equal to the model's so the resampling step is an identity
    ///     pass-through, keeping the downmix arithmetic exactly predictable.
    /// </summary>
    [Fact]
    public async Task SherpaOnnxRecognitionSession_FrameCaptured_StereoAtModelRate_FeedsDownmixedMonoToBackend()
    {
        // Arrange: a stereo device already at the model's declared rate
        var device = CreateCaptureDevice(sampleRate: 16000, channelCount: 2);
        var engine = new FakeRecognitionEngine();
        await using var session = CreateSession(engine, device);
        await session.StartAsync(TestContext.Current.CancellationToken);

        // Act: raise one stereo block of four frames, then stop to drain the pipeline
        RaiseFrameCaptured(device, [0.0f, 0.0f, 1.0f, 1.0f, 2.0f, 2.0f, 3.0f, 3.0f]);
        await session.StopAsync(TestContext.Current.CancellationToken);

        // Assert: each stereo frame's two identical channels averaged to that same value
        Assert.Equal(1, engine.AcceptSamplesCallCount);
        Assert.Equal([0.0f, 1.0f, 2.0f, 3.0f], engine.AcceptedSamples);
    }

    /// <summary>
    ///     Proves that a final result's text is passed through the owning model's
    ///     <see cref="IRecognitionModel.NormalizeText(string,bool)"/> hook, with <c>isFinal</c>
    ///     set to <see langword="true"/>, before it is buffered for <see cref="SherpaOnnxRecognitionSession.GetResultsAsync"/>.
    /// </summary>
    [Fact]
    public async Task SherpaOnnxRecognitionSession_FrameCaptured_FinalResult_AppliesModelNormalizeTextWithIsFinalTrue()
    {
        // Arrange: a model whose NormalizeText is a distinguishable, recorded transform
        var device = CreateCaptureDevice();
        var engine = new FakeRecognitionEngine([new SpeechRecognitionResult("HELLO WORLD", IsFinal: true)]);
        var calls = new List<(string Text, bool IsFinal)>();
        var model = new FakeRecognitionModel(normalizeText: (text, isFinal) =>
        {
            calls.Add((text, isFinal));
            return $"normalized:{text}";
        });
        await using var session = CreateSession(engine, device, model);
        await session.StartAsync(TestContext.Current.CancellationToken);

        // Act
        RaiseFrameCaptured(device, [0.1f, 0.2f]);
        await session.StopAsync(TestContext.Current.CancellationToken);
        var results = await CollectAsync(session.GetResultsAsync(TestContext.Current.CancellationToken));

        // Assert: the model saw the raw text with isFinal true, and the buffered result carries
        // the model's normalized text while preserving IsFinal
        var result = Assert.Single(results);
        Assert.Equal("normalized:HELLO WORLD", result.Result.Text);
        Assert.True(result.Result.IsFinal);
        var call = Assert.Single(calls);
        Assert.Equal("HELLO WORLD", call.Text);
        Assert.True(call.IsFinal);
    }

    /// <summary>
    ///     Proves that a fault in the backend's <see cref="IRecognitionBackend.Reset"/> during
    ///     teardown is contained and reported rather than propagated, matching the best-effort
    ///     reset guarantee documented on <see cref="SherpaOnnxRecognitionSession.StopAsync"/>.
    /// </summary>
    [Fact]
    public async Task SherpaOnnxRecognitionSession_StopAsync_BackendResetFails_CompletesAndReportsFault()
    {
        // Arrange: a running session whose backend always faults on Reset()
        var diagnostics = Substitute.For<ISpeechDiagnostics>();
        var engine = new FakeRecognitionEngine(resetException: new InvalidOperationException("reset failed"));
        await using var session = CreateSession(engine, CreateCaptureDevice(), diagnostics: diagnostics);
        await session.StartAsync(TestContext.Current.CancellationToken);

        // Act
        var exception = await Record.ExceptionAsync(() => session.StopAsync(TestContext.Current.CancellationToken));

        // Assert: nothing escaped, the fault was reported, and teardown still converged
        Assert.Null(exception);
        Assert.Equal(RecognitionSessionState.Stopped, session.State);
        diagnostics.Received().Report(
            SpeechDiagnosticLevel.Error,
            "RecognitionSubsystem",
            Arg.Is<string>(message => message.Contains("Failed to reset the recognition backend", StringComparison.Ordinal)));
    }

    /// <summary>
    ///     Builds a substitute capture device reporting itself available with the given capture
    ///     format.
    /// </summary>
    private static IAudioCaptureDevice CreateCaptureDevice(int sampleRate = 16000, int channelCount = 1)
    {
        var device = Substitute.For<IAudioCaptureDevice>();
        device.IsAvailable.Returns(true);
        device.SampleRate.Returns(sampleRate);
        device.ChannelCount.Returns(channelCount);
        return device;
    }

    /// <summary>
    ///     Raises the substitute capture device's <see cref="IAudioCaptureDevice.FrameCaptured"/>
    ///     event with one block of interleaved samples, standing in for a real audio callback.
    /// </summary>
    private static void RaiseFrameCaptured(IAudioCaptureDevice captureDevice, float[] samples)
    {
        captureDevice.FrameCaptured += Raise.Event<EventHandler<AudioCaptureFrameEventArgs>>(
            captureDevice,
            new AudioCaptureFrameEventArgs(samples));
    }

    /// <summary>
    ///     Builds a <see cref="SherpaOnnxRecognitionSession"/> directly over the supplied backend
    ///     and device, bypassing <see cref="SherpaOnnxSpeechRecognizerEngine"/>'s lease since this
    ///     session's own lifecycle, not the engine's, is under test here.
    /// </summary>
    private static SherpaOnnxRecognitionSession CreateSession(
        IRecognitionBackend engine,
        IAudioCaptureDevice device,
        IRecognitionModel? model = null,
        ISpeechDiagnostics? diagnostics = null,
        DedicatedWorker? worker = null) =>
        new(
            engine,
            device,
            targetSampleRate: 16000,
            model ?? new FakeRecognitionModel(),
            releaseLease: static () => { },
            diagnostics ?? NullSpeechDiagnostics.Instance,
            worker ?? new DedicatedWorker(diagnostics: diagnostics ?? NullSpeechDiagnostics.Instance));

    /// <summary>Drains an asynchronous sequence of recognition events into a list.</summary>
    private static async Task<List<SpeechRecognitionEvent>> CollectAsync(IAsyncEnumerable<SpeechRecognitionEvent> source)
    {
        var results = new List<SpeechRecognitionEvent>();
        await foreach (var item in source)
        {
            results.Add(item);
        }

        return results;
    }
}
