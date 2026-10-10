using DemaConsulting.Speech.AudioSubsystem;
using DemaConsulting.Speech.Diagnostics;
using DemaConsulting.Speech.ModelManagementSubsystem;
using DemaConsulting.Speech.RecognitionSubsystem;
using DemaConsulting.Speech.Tests.ModelManagementSubsystem.Fakes;
using DemaConsulting.Speech.Tests.RecognitionSubsystem.Fakes;
using NSubstitute;
using NSubstitute.Core;

namespace DemaConsulting.Speech.Tests.RecognitionSubsystem;

/// <summary>
///     Unit tests for <see cref="RecognitionSession"/>, exercising the full capture →
///     resample → backend → buffered-result pipeline through a substitute capture device and a
///     fake backend, with no microphone and no native sherpa-onnx runtime.
/// </summary>
/// <remarks>
///     Every test constructs its own session directly (bypassing <see cref="SpeechRecognizerEngine"/>,
///     whose lease behavior is covered by <c>SpeechRecognizerEngineTests</c>) with a no-op
///     <c>releaseLease</c> callback, since this session's own lifecycle is what is under test.
/// </remarks>
public class RecognitionSessionTests
{
    /// <summary>
    ///     Proves that starting a freshly created session subscribes to and starts the capture
    ///     device and transitions it to <see cref="RecognitionSessionState.Running"/>.
    /// </summary>
    [Fact]
    public async Task RecognitionSession_StartAsync_FromCreated_TransitionsToRunning()
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
    ///     Proves that <see cref="RecognitionSession.StartAsync"/> returns control to
    ///     its caller well before a slow, synchronous <see cref="IAudioCaptureDevice.Start"/>
    ///     call returns - closing the review finding that the native device-start call used to
    ///     run inline on the calling thread, blocking it (for example a UI thread) for the whole
    ///     capture window.
    /// </summary>
    [Fact]
    public async Task RecognitionSession_StartAsync_SlowDeviceStart_DoesNotBlockCaller()
    {
        // Arrange: a device whose Start() blocks until this test explicitly releases it
        using var startEntered = new ManualResetEventSlim(false);
        using var startRelease = new ManualResetEventSlim(false);
        var device = CreateCaptureDevice();
        device.When(d => d.Start()).Do(_ =>
        {
            startEntered.Set();
            startRelease.Wait(TestContext.Current.CancellationToken);
        });
        await using var session = CreateSession(new FakeRecognitionEngine(), device);

        // Act: call StartAsync and prove it returns a task - without blocking this thread - well
        // before the device's own blocking Start() call has returned
        var startTask = session.StartAsync(TestContext.Current.CancellationToken);
        var enteredInTime = startEntered.Wait(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        // Assert: the device call was genuinely entered, but StartAsync has not yet completed and
        // this thread was never blocked waiting for it - the session is still Starting, not yet
        // Running
        Assert.True(enteredInTime, "The capture device's Start() was never entered.");
        Assert.False(startTask.IsCompleted);
        Assert.Equal(RecognitionSessionState.Starting, session.State);

        // Act: release the blocked device call and let the start genuinely finish
        startRelease.Set();
        await startTask.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        // Assert: the session has now converged to Running, and the device was started exactly
        // once
        Assert.Equal(RecognitionSessionState.Running, session.State);
        device.Received(1).Start();
    }

    /// <summary>
    ///     Proves that starting a session that has already reached
    ///     <see cref="RecognitionSessionState.Stopped"/> throws <see cref="InvalidOperationException"/>
    ///     rather than permitting a restart (Decision #1): a session is single-use.
    /// </summary>
    [Fact]
    public async Task RecognitionSession_StartAsync_FromStopped_ThrowsInvalidOperationException()
    {
        // Arrange: a session that was started and stopped once already
        await using var session = CreateSession(new FakeRecognitionEngine(), CreateCaptureDevice());
        await session.StartAsync(TestContext.Current.CancellationToken);
        await session.StopAsync(TestContext.Current.CancellationToken);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => session.StartAsync(TestContext.Current.CancellationToken));
    }

    /// <summary>
    ///     Proves that two concurrent <see cref="RecognitionSession.StopAsync"/> calls
    ///     both complete once the session has converged on <see cref="RecognitionSessionState.Stopped"/>,
    ///     rather than one of them hanging or throwing.
    /// </summary>
    [Fact]
    public async Task RecognitionSession_StopAsync_CalledConcurrentlyTwice_BothCompleteOnceStopped()
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
    ///     Proves that a finite WAV source applies backpressure instead of dropping the oldest
    ///     blocks when recognition falls behind, so every sample of the file reaches the backend.
    /// </summary>
    [Fact]
    public async Task RecognitionSession_WavFileSource_SlowBackend_DeliversEverySample()
    {
        // Arrange: a 16 kHz mono file of 200 blocks (well over the 64-block pending capacity)
        // and a backend held blocked until the whole file has been offered
        const int blockCount = 200;
        const int totalSamples = blockCount * WavFileAudioCaptureDevice.DefaultFrameSampleCount;
        var path = Path.Join(Path.GetTempPath(), $"{Guid.NewGuid():N}.wav");
        try
        {
            await WriteWavFileAsync(path, totalSamples);

            using var release = new ManualResetEvent(false);
            var engine = new FakeRecognitionEngine(acceptSamplesBlock: release);
            var device = new WavFileAudioCaptureDevice(path);
            await using var session = CreateSession(engine, device);

            // Act
            var start = session.StartAsync(TestContext.Current.CancellationToken);
            await Task.Delay(300, TestContext.Current.CancellationToken);
            release.Set();
            await start.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
            await session.StopAsync(TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(totalSamples, engine.AcceptedSamples.Count);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    ///     Proves that stopping a finite WAV source waits for a slow backend to finish draining
    ///     (beyond the abandon timeout) and still delivers its flushed final result.
    /// </summary>
    [Fact]
    public async Task RecognitionSession_WavFileSource_StopAsync_SlowDrain_DeliversFlushedResult()
    {
        // Arrange: a short file whose queued blocks the backend decodes slower than the abandon
        // timeout, with a flushed final result at the end
        var path = Path.Join(Path.GetTempPath(), $"{Guid.NewGuid():N}.wav");
        try
        {
            await WriteWavFileAsync(path, 10 * WavFileAudioCaptureDevice.DefaultFrameSampleCount);

            using var release = new ManualResetEvent(false);
            var engine = new FakeRecognitionEngine(
                scriptedFlushResult: new SpeechRecognitionResult("tail", IsFinal: true),
                acceptSamplesBlock: release);
            await using var session = CreateSession(engine, new WavFileAudioCaptureDevice(path));
            await session.StartAsync(TestContext.Current.CancellationToken);

            // Act
            var stop = session.StopAsync(TestContext.Current.CancellationToken);
            await Task.Delay(DedicatedWorker.DefaultAbandonTimeout + TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);
            release.Set();
            await stop.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);

            // Assert
            var results = await CollectAsync(session.GetResultsAsync(TestContext.Current.CancellationToken));
            Assert.Equal("tail", Assert.Single(results).Result.Text);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    ///     Proves that a backend fault while a long WAV file is still being delivered does not
    ///     leave the file source blocked on backpressure, so starting the session completes.
    /// </summary>
    [Fact]
    public async Task RecognitionSession_WavFileSource_BackendFaults_StartAsyncStillCompletes()
    {
        // Arrange: a file far larger than the pending capacity and a backend that faults on its first block
        var path = Path.Join(Path.GetTempPath(), $"{Guid.NewGuid():N}.wav");
        try
        {
            await WriteWavFileAsync(path, 200 * WavFileAudioCaptureDevice.DefaultFrameSampleCount);
            var engine = new FakeRecognitionEngine(acceptSamplesException: new InvalidOperationException("boom"));
            await using var session = CreateSession(engine, new WavFileAudioCaptureDevice(path));

            // Act
            await session.StartAsync(TestContext.Current.CancellationToken)
                .WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(RecognitionSessionState.Faulted, session.State);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>Writes a 16 kHz mono 16-bit PCM WAV file of constant low-level samples.</summary>
    private static async Task WriteWavFileAsync(string path, int totalSamples)
    {
        await using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream);
        var dataBytes = totalSamples * 2;
        writer.Write("RIFF"u8.ToArray());
        writer.Write(36 + dataBytes);
        writer.Write("WAVE"u8.ToArray());
        writer.Write("fmt "u8.ToArray());
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)1);
        writer.Write(16000);
        writer.Write(32000);
        writer.Write((short)2);
        writer.Write((short)16);
        writer.Write("data"u8.ToArray());
        writer.Write(dataBytes);
        for (var i = 0; i < totalSamples; i++)
        {
            writer.Write((short)1000);
        }
    }

    /// <summary>
    ///     Proves that stopping flushes any trailing audio the backend had accepted but not yet
    ///     decoded, so the flushed final result is enumerable via
    ///     <see cref="RecognitionSession.GetResultsAsync"/> once the stop completes.
    /// </summary>
    [Fact]
    public async Task RecognitionSession_StopAsync_FlushesTrailingResultsBeforeCompleting()
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
    ///     Proves that <see cref="RecognitionSession.GetResultsAsync"/> is single-consumer:
    ///     a second concurrent enumeration attempt, made while one enumeration is still active,
    ///     throws <see cref="InvalidOperationException"/> immediately.
    /// </summary>
    [Fact]
    public async Task RecognitionSession_GetResultsAsync_CalledConcurrently_ThrowsInvalidOperationException()
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
    ///     Proves that cancelling the token passed to <see cref="RecognitionSession.GetResultsAsync"/>
    ///     ends only that enumeration - the session itself keeps running and is not stopped.
    /// </summary>
    [Fact]
    public async Task RecognitionSession_GetResultsAsync_CancelledToken_EndsEnumerationWithoutStoppingSession()
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
    ///     Proves that a slow consumer of <see cref="RecognitionSession.GetResultsAsync"/>
    ///     never sees every provisional result produced while it was not reading: later
    ///     provisional results coalesce into the latest one (Decision #5).
    /// </summary>
    [Fact]
    public async Task RecognitionSession_GetResultsAsync_SlowConsumer_CoalescesProvisionalResults()
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
    ///     Proves that a slow consumer of <see cref="RecognitionSession.GetResultsAsync"/>
    ///     never loses a final result while the buffered backlog remains under the byte cap
    ///     (Decision #5): every final result is still delivered, in order.
    /// </summary>
    [Fact]
    public async Task RecognitionSession_GetResultsAsync_SlowConsumer_NeverDropsFinalResultsUnderByteCap()
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
    ///     enumerating <see cref="RecognitionSession.GetResultsAsync"/> throws
    ///     <see cref="RecognitionSessionFaultedException"/> from the enumerator.
    /// </summary>
    [Fact]
    public async Task RecognitionSession_GetResultsAsync_SessionFaulted_ThrowsRecognitionSessionFaultedException()
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
    ///     hanging <see cref="RecognitionSession.StopAsync"/> forever, and that the
    ///     abandonment is reported through diagnostics (Decision #4).
    /// </summary>
    [Fact]
    public async Task RecognitionSession_NativeCallExceedsAbandonTimeout_TaskCompletesAndDiagnosticsReportsWarning()
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
    ///     Proves that cancelling <see cref="RecognitionSession.StartAsync"/>'s own
    ///     <see cref="CancellationToken"/> while <see cref="IAudioCaptureDevice.Start"/> is still
    ///     blocking is actually honored - closing the review finding that the device-start worker
    ///     call used <see cref="CancellationToken.None"/>, so a caller's cancellation request was
    ///     ignored indefinitely rather than bounded by the abandon-timeout policy every other
    ///     dedicated-worker call in this session already uses.
    /// </summary>
    [Fact]
    public async Task RecognitionSession_StartAsync_CancelledWhileDeviceStarting_AbandonsWithinTimeout()
    {
        // Arrange: a device whose Start() blocks forever (standing in for a stuck native call,
        // consistent with IAudioCaptureDevice.Start() having no cancellation token of its own),
        // and a worker with a near-zero abandon timeout so the test stays fast
        using var startEntered = new ManualResetEventSlim(false);
        using var neverReturns = new ManualResetEventSlim(false);
        var device = CreateCaptureDevice();
        device.When(d => d.Start()).Do(_ =>
        {
            startEntered.Set();
            neverReturns.Wait();
        });
        var diagnostics = Substitute.For<ISpeechDiagnostics>();
        var worker = new DedicatedWorker(
            abandonTimeout: TimeSpan.FromMilliseconds(1),
            diagnostics: diagnostics,
            diagnosticsCategory: "RecognitionSubsystem");
        await using var session = CreateSession(new FakeRecognitionEngine(), device, worker: worker, diagnostics: diagnostics);

        using var startCts = new CancellationTokenSource();
        var startTask = session.StartAsync(startCts.Token);
        var enteredInTime = startEntered.Wait(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.True(enteredInTime, "The capture device's Start() was never entered.");

        // Act: cancel the caller's own token while Start() is still blocked; the call must
        // complete within a bounded time rather than waiting forever for a device call that never
        // honors cancellation
        await startCts.CancelAsync();

        // Assert: the abandoned start surfaces as a failure to start, not a silent hang, and the
        // abandonment was reported
        await Assert.ThrowsAsync<SpeechRecognizerUnavailableException>(
            () => startTask.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
        Assert.Equal(RecognitionSessionState.Faulted, session.State);
        diagnostics.Received().Report(
            SpeechDiagnosticLevel.Warning,
            "RecognitionSubsystem",
            Arg.Is<string>(message => message.Contains("abandon", StringComparison.OrdinalIgnoreCase)));

        // Cleanup: release the abandoned background thread so it can exit
        neverReturns.Set();
    }

    /// <summary>
    ///     Proves that a capture device going unavailable mid-session transitions the session to
    ///     <see cref="RecognitionSessionState.Faulted"/>.
    /// </summary>
    [Fact]
    public async Task RecognitionSession_DeviceLostMidSession_TransitionsToFaulted()
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
    ///     Proves that <see cref="RecognitionSession.StateChanged"/> raises every state
    ///     transition, in order, across a full Start/Stop lifecycle.
    /// </summary>
    [Fact]
    public async Task RecognitionSession_StateChanged_EmitsEveryTransitionInOrder()
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
    ///     Proves, across many iterations, that the Stopping transition is always raised before
    ///     the Stopped transition it logically precedes (findings 32/33): with every dependency
    ///     trivially fast to complete (as here), the teardown that produces the Stopped transition
    ///     can genuinely run to completion synchronously the instant it is started, so only
    ///     starting it strictly after Stopping has already been raised - never racing that
    ///     ordering on whether the underlying tasks happen to complete synchronously - keeps every
    ///     iteration below in order.
    /// </summary>
    [Fact]
    public async Task RecognitionSession_StateChanged_StoppingAlwaysPrecedesStoppedAcrossManyIterations()
    {
        for (var iteration = 0; iteration < 200; iteration++)
        {
            // Arrange
            await using var session = CreateSession(new FakeRecognitionEngine(), CreateCaptureDevice());
            var transitions = new List<RecognitionSessionState>();
            session.StateChanged += (_, args) => transitions.Add(args.Current);

            // Act
            await session.StartAsync(TestContext.Current.CancellationToken);
            await session.StopAsync(TestContext.Current.CancellationToken);

            // Assert: Stopping must appear, and strictly before Stopped
            var stoppingIndex = transitions.IndexOf(RecognitionSessionState.Stopping);
            var stoppedIndex = transitions.IndexOf(RecognitionSessionState.Stopped);
            Assert.True(stoppingIndex >= 0, $"Iteration {iteration}: Stopping transition was never raised.");
            Assert.True(stoppedIndex >= 0, $"Iteration {iteration}: Stopped transition was never raised.");
            Assert.True(
                stoppingIndex < stoppedIndex,
                $"Iteration {iteration}: Stopped (index {stoppedIndex}) was raised before or alongside Stopping (index {stoppingIndex}): [{string.Join(", ", transitions)}]");
        }
    }

    /// <summary>
    ///     Proves that a captured frame still flows through downmixing into the backend - the
    ///     same pipeline <c>SherpaOnnxSpeechRecognizer</c> used before the Engine/Session split.
    ///     Uses a device sample rate equal to the model's so the resampling step is an identity
    ///     pass-through, keeping the downmix arithmetic exactly predictable.
    /// </summary>
    [Fact]
    public async Task RecognitionSession_FrameCaptured_StereoAtModelRate_FeedsDownmixedMonoToBackend()
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
    ///     set to <see langword="true"/>, before it is buffered for <see cref="RecognitionSession.GetResultsAsync"/>.
    /// </summary>
    [Fact]
    public async Task RecognitionSession_FrameCaptured_FinalResult_AppliesModelNormalizeTextWithIsFinalTrue()
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
    ///     reset guarantee documented on <see cref="RecognitionSession.StopAsync"/>.
    /// </summary>
    [Fact]
    public async Task RecognitionSession_StopAsync_BackendResetFails_CompletesAndReportsFault()
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
    ///     Proves that a concurrent <see cref="RecognitionSession.StopAsync"/> cannot
    ///     observe a <see cref="RecognitionSession.StartAsync"/> call's intermediate
    ///     <see cref="RecognitionSessionState.Starting"/> state, converge the session to
    ///     <see cref="RecognitionSessionState.Stopped"/>, and return while the device is still
    ///     being started - closing the review finding that this race could leave capture running
    ///     against a session the caller believes is stopped.
    /// </summary>
    [Fact]
    public async Task RecognitionSession_StopAsync_ConcurrentWithStartAsync_DeviceEndsGenuinelyStopped()
    {
        // Arrange: a device whose Start() blocks until this test explicitly releases it,
        // standing in for a slow synchronous device start that a concurrent StopAsync could
        // otherwise race past.
        using var startEntered = new ManualResetEventSlim(false);
        using var startRelease = new ManualResetEventSlim(false);
        var device = CreateCaptureDevice();
        device.When(d => d.Start()).Do(_ =>
        {
            startEntered.Set();
            startRelease.Wait(TestContext.Current.CancellationToken);
        });
        var engine = new FakeRecognitionEngine();
        await using var session = CreateSession(engine, device);

        // Act: begin starting, wait until Start() has genuinely been entered, then begin
        // stopping concurrently before Start() returns. Both calls are dispatched through
        // Task.Run: StartAsync's body (and therefore the lock it holds) runs synchronously on
        // its own thread, and StopAsync - a synchronous method that itself blocks acquiring the
        // same lock before it can even return a Task - must run on a thread other than this
        // test's own, or this test's own thread would deadlock waiting on the very lock whose
        // release depends on this test later calling startRelease.Set().
        var startTask = Task.Run(() => session.StartAsync(TestContext.Current.CancellationToken), TestContext.Current.CancellationToken);
        startEntered.Wait(TestContext.Current.CancellationToken);
        var stopTask = Task.Run(
            async () => await session.StopAsync(TestContext.Current.CancellationToken),
            TestContext.Current.CancellationToken);

        // Assert: StopAsync cannot race ahead of the still-in-flight StartAsync - both calls
        // serialize on the same lock, so StopAsync has not completed while Start() is blocked
        await Task.Delay(TimeSpan.FromMilliseconds(50), TestContext.Current.CancellationToken);
        Assert.False(stopTask.IsCompleted);

        // Act: let the device start complete
        startRelease.Set();
        await startTask;
        await stopTask.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        // Assert: the session converged to genuinely Stopped, and the device was genuinely
        // started then genuinely stopped exactly once each - never left running
        Assert.Equal(RecognitionSessionState.Stopped, session.State);
        device.Received(1).Start();
        device.Received(1).Stop();
    }

    /// <summary>
    ///     Proves that the pump is draining the pending-frame channel before a synchronous-replay
    ///     capture device (for example, a file-backed device) emits its blocks from within
    ///     <c>Start()</c>, so a file longer than the bounded channel's capacity does not silently
    ///     drop its earliest blocks before anything exists to read them.
    /// </summary>
    [Fact]
    public async Task RecognitionSession_StartAsync_DeviceEmitsManyBlocksSynchronouslyFromStart_NoneAreDropped()
    {
        // Arrange: a device whose Start() synchronously raises far more FrameCaptured blocks than
        // the pump's bounded channel can hold at once, exactly like a file-backed device replaying
        // an entire file before Start() returns. Each block is only raised once the previous one
        // has genuinely been accepted by the backend - deterministically proving the pump is
        // already draining the channel concurrently with Start(), rather than racing an
        // unsynchronized flood against however fast the pump thread happens to be scheduled.
        const int blockCount = 200;
        var device = CreateCaptureDevice();
        IAudioCaptureDevice? capturedDevice = null;
        var engine = new FakeRecognitionEngine();
        device.When(d => d.Start()).Do(_ =>
        {
            for (var i = 0; i < blockCount; i++)
            {
                RaiseFrameCaptured(capturedDevice!, [0.1f]);

                var expected = i + 1;
                Assert.True(
                    SpinWait.SpinUntil(() => engine.AcceptSamplesCallCount >= expected, TimeSpan.FromSeconds(5)),
                    $"The pump never accepted block {expected} of {blockCount}; it was not draining the channel concurrently with Start().");
            }
        });
        capturedDevice = device;
        await using var session = CreateSession(engine, device);

        // Act: start (synchronously emitting every block before returning, each one drained
        // before the next is raised), then stop to converge the pump
        await session.StartAsync(TestContext.Current.CancellationToken);
        await session.StopAsync(TestContext.Current.CancellationToken).WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        // Assert: every block emitted from within Start() was still accepted by the backend -
        // none were dropped because the pump was already draining the channel before Start() ran
        Assert.Equal(blockCount, engine.AcceptSamplesCallCount);
    }

    /// <summary>
    ///     Proves that a session faulted mid-stream (for example, by the capture device becoming
    ///     unavailable) still runs the full teardown - resetting the shared backend and stopping
    ///     the device - once disposed, rather than releasing the engine's lease while the capture
    ///     stream and pump could still be active.
    /// </summary>
    [Fact]
    public async Task RecognitionSession_DeviceLostMidSession_DisposeAsyncStillTearsDownBackendAndDevice()
    {
        // Arrange: a running session whose device later reports itself unavailable
        var device = CreateCaptureDevice();
        var engine = new FakeRecognitionEngine();
        var releaseCount = 0;
        await using var session = CreateSession(engine, device, releaseLease: () => releaseCount++);
        await session.StartAsync(TestContext.Current.CancellationToken);

        // Act: fault the session, then dispose it
        device.IsAvailable.Returns(false);
        RaiseFrameCaptured(device, [0.1f]);
        Assert.Equal(RecognitionSessionState.Faulted, session.State);
        await session.DisposeAsync();

        // Assert: teardown genuinely ran - the backend was reset and the device stopped - and
        // the fault was preserved through to Disposed, with the lease released exactly once
        Assert.Equal(RecognitionSessionState.Disposed, session.State);
        Assert.Equal(1, engine.ResetCallCount);
        device.Received(1).Stop();
        Assert.Equal(1, releaseCount);
    }

    /// <summary>
    ///     Proves that <see cref="RecognitionSession.StopAsync"/>'s
    ///     <see cref="CancellationToken"/> parameter is genuinely observed (finding 19): a caller
    ///     who cancels it stops waiting for that call's own completion promptly, without being
    ///     stuck behind a slow drain - but the shared teardown itself is never aborted by that
    ///     cancellation, since it is shared with every other concurrent/overlapping caller (and
    ///     <see cref="RecognitionSession.DisposeAsync"/>), all of whom still require the
    ///     drain to genuinely happen.
    /// </summary>
    [Fact(Timeout = 10000)]
    public async Task RecognitionSession_StopAsync_CallerTokenCanceled_ReturnsEarlyWithoutAbortingSharedTeardown()
    {
        // Arrange: a backend whose AcceptSamples blocks until this test releases it
        using var block = new ManualResetEvent(false);
        var engine = new FakeRecognitionEngine(acceptSamplesBlock: block);
        var device = CreateCaptureDevice();
        await using var session = CreateSession(engine, device);
        await session.StartAsync(TestContext.Current.CancellationToken);
        RaiseFrameCaptured(device, [0.1f]);

        // Wait until the pump has genuinely entered (and is blocked inside) AcceptSamples
        SpinWait.SpinUntil(() => engine.AcceptSamplesCallCount >= 1, TimeSpan.FromSeconds(5));
        Assert.Equal(1, engine.AcceptSamplesCallCount);

        // Act: call StopAsync with a token that is canceled immediately after the call begins
        using var cts = new CancellationTokenSource();
        var stopTask = session.StopAsync(cts.Token);
        await cts.CancelAsync();

        // Assert: this caller's own wait is canceled promptly, well before the still-blocked
        // drain could ever converge on its own
        Exception? stopException = null;
        try
        {
            await stopTask;
        }
        catch (Exception ex)
        {
            stopException = ex;
        }

        Assert.IsType<OperationCanceledException>(stopException, exactMatch: false);

        // Assert: the shared teardown itself was not aborted by that cancellation - a second,
        // uncancelled StopAsync call still observes the same in-flight teardown, which only
        // converges once the backend genuinely unblocks
        var secondStopTask = session.StopAsync(TestContext.Current.CancellationToken);
        await Task.Delay(TimeSpan.FromMilliseconds(100), TestContext.Current.CancellationToken);
        Assert.False(secondStopTask.IsCompleted);

        block.Set();
        await secondStopTask.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.Equal(RecognitionSessionState.Stopped, session.State);
    }

    /// <summary>
    ///     Proves that the engine's exclusivity lease is not released until the dedicated pump
    ///     worker has genuinely exited - not merely been abandoned after its timeout - so a new
    ///     session (or engine disposal) can never touch or dispose the shared backend while an
    ///     abandoned pump thread is still inside a blocking backend call.
    /// </summary>
    [Fact(Timeout = 10000)]
    public async Task RecognitionSession_DisposeAsync_AbandonedPumpWorker_DoesNotReleaseLeaseUntilWorkerExits()
    {
        // Arrange: a backend whose AcceptSamples blocks forever (until this test releases it),
        // and a worker with a near-zero abandon timeout so the test stays fast
        using var neverSignaled = new ManualResetEvent(false);
        var engine = new FakeRecognitionEngine(acceptSamplesBlock: neverSignaled);
        var worker = new DedicatedWorker(
            abandonTimeout: TimeSpan.FromMilliseconds(1),
            diagnostics: NullSpeechDiagnostics.Instance,
            diagnosticsCategory: "RecognitionSubsystem");
        var device = CreateCaptureDevice();
        var releaseCount = 0;
        var session = CreateSession(engine, device, worker: worker, releaseLease: () => releaseCount++);
        await session.StartAsync(TestContext.Current.CancellationToken);
        RaiseFrameCaptured(device, [0.1f]);
        await Task.Delay(TimeSpan.FromMilliseconds(50), TestContext.Current.CancellationToken);

        // Act: stop (completes quickly via the abandon policy) then begin disposing
        await session.StopAsync(TestContext.Current.CancellationToken).WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        var disposeTask = session.DisposeAsync().AsTask();

        // Assert: the lease must not be released while the abandoned pump thread is still stuck
        // inside the backend's blocking call
        await Task.Delay(TimeSpan.FromMilliseconds(100), TestContext.Current.CancellationToken);
        Assert.Equal(0, releaseCount);
        Assert.False(disposeTask.IsCompleted);

        // Act: release the abandoned background thread so it can genuinely exit
        neverSignaled.Set();
        await disposeTask.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        // Assert: only once the worker genuinely exited was the lease released
        Assert.Equal(1, releaseCount);
    }

    /// <summary>
    ///     Proves that two concurrent <see cref="RecognitionSession.DisposeAsync"/>
    ///     calls share the exact same in-flight teardown, rather than the second call returning
    ///     the instant the first merely begins - both complete only once the real teardown (and
    ///     the lease release it gates) is genuinely done.
    /// </summary>
    [Fact]
    public async Task RecognitionSession_DisposeAsync_CalledConcurrentlyTwice_BothCompleteAfterSingleTeardown()
    {
        // Arrange
        var engine = new FakeRecognitionEngine();
        var device = CreateCaptureDevice();
        var releaseCount = 0;
        var session = CreateSession(engine, device, releaseLease: () => releaseCount++);
        await session.StartAsync(TestContext.Current.CancellationToken);

        // Act: dispose concurrently from two callers
        async Task DisposeOnceAsync() => await session.DisposeAsync();
        var first = DisposeOnceAsync();
        var second = DisposeOnceAsync();
        await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        // Assert: both callers converged, the destructive teardown steps ran exactly once, and
        // the lease was released exactly once
        Assert.Equal(RecognitionSessionState.Disposed, session.State);
        Assert.Equal(1, engine.ResetCallCount);
        device.Received(1).Stop();
        Assert.Equal(1, releaseCount);
    }

    /// <summary>
    ///     Proves that a <see cref="RecognitionSession.StateChanged"/> handler calling
    ///     back into this session (for example <see cref="RecognitionSession.StopAsync"/>)
    ///     and then synchronously blocking on the result does not deadlock (finding 21): the event
    ///     must be raised only after <c>_syncRoot</c> has been released.
    /// </summary>
    [Fact(Timeout = 5000)]
    public async Task RecognitionSession_StateChangedHandlerBlocksOnStopAsync_DoesNotDeadlock()
    {
        // Arrange
        var session = CreateSession(new FakeRecognitionEngine(), CreateCaptureDevice());
        var handlerCompleted = false;
        session.StateChanged += (_, args) =>
        {
            if (args.Current == RecognitionSessionState.Running)
            {
                // A host handler that synchronously blocks on the result of calling back into
                // this very session must not deadlock against the state lock.
                session.StopAsync(CancellationToken.None).GetAwaiter().GetResult();
                handlerCompleted = true;
            }
        };

        try
        {
            // Act
            await session.StartAsync(TestContext.Current.CancellationToken);

            // Assert
            Assert.True(handlerCompleted);
            Assert.Equal(RecognitionSessionState.Stopped, session.State);
        }
        finally
        {
            await session.DisposeAsync();
        }
    }

    /// <summary>
    ///     Proves that a backend exception thrown from <see cref="IRecognitionBackend.AcceptSamples"/>
    ///     faults the session and completes <see cref="RecognitionSession.GetResultsAsync"/>
    ///     with <see cref="RecognitionSessionFaultedException"/>, rather than leaving the session
    ///     <see cref="RecognitionSessionState.Running"/> with its result buffer open forever
    ///     (finding 22).
    /// </summary>
    [Fact]
    public async Task RecognitionSession_BackendThrowsFromAcceptSamples_FaultsSessionAndCompletesResultBuffer()
    {
        // Arrange
        var engine = new FakeRecognitionEngine(acceptSamplesException: new InvalidOperationException("Backend failure."));
        var device = CreateCaptureDevice();
        await using var session = CreateSession(engine, device);
        await session.StartAsync(TestContext.Current.CancellationToken);

        // Act: a captured block reaches the pump thread and the backend throws while accepting it
        RaiseFrameCaptured(device, [0.1f]);
        SpinWait.SpinUntil(() => session.State == RecognitionSessionState.Faulted, TimeSpan.FromSeconds(5));

        // Assert: the session faulted, and a consumer of GetResultsAsync is unblocked with the fault
        // rather than hanging forever
        Assert.Equal(RecognitionSessionState.Faulted, session.State);
        await Assert.ThrowsAsync<RecognitionSessionFaultedException>(async () =>
        {
            await foreach (var _ in session.GetResultsAsync(TestContext.Current.CancellationToken))
            {
                // No iterations are expected to survive the fault.
            }
        });
    }

    /// <summary>
    ///     Proves that calling <see cref="RecognitionSession.StopAsync"/> on a session
    ///     that was never started still completes its result buffer, so
    ///     <see cref="RecognitionSession.GetResultsAsync"/> returns instead of hanging
    ///     forever (finding 23).
    /// </summary>
    [Fact]
    public async Task RecognitionSession_StopAsync_BeforeStartAsync_CompletesResultBuffer()
    {
        // Arrange
        await using var session = CreateSession(new FakeRecognitionEngine(), CreateCaptureDevice());

        // Act
        await session.StopAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(RecognitionSessionState.Stopped, session.State);
        using var bounded = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await foreach (var _ in session.GetResultsAsync(bounded.Token))
        {
            // No results are expected: the buffer should complete immediately and empty.
        }
    }

    /// <summary>
    ///     Proves that when the dedicated pump worker is abandoned after its timeout, the shared
    ///     recognition backend is not reset (nor the device stopped) until the pump thread has
    ///     genuinely exited, closing the race in which the raw pump thread could still be inside
    ///     <see cref="IRecognitionBackend.AcceptSamples"/> while teardown concurrently reset the
    ///     same backend (finding 24).
    /// </summary>
    [Fact(Timeout = 10000)]
    public async Task RecognitionSession_AbandonedPumpWorker_DoesNotResetBackendUntilWorkerExits()
    {
        // Arrange: a backend whose AcceptSamples blocks forever, and a worker with a near-zero
        // abandon timeout so the test stays fast
        using var neverSignaled = new ManualResetEvent(false);
        var engine = new FakeRecognitionEngine(acceptSamplesBlock: neverSignaled);
        var worker = new DedicatedWorker(
            abandonTimeout: TimeSpan.FromMilliseconds(1),
            diagnostics: NullSpeechDiagnostics.Instance,
            diagnosticsCategory: "RecognitionSubsystem");
        var device = CreateCaptureDevice();
        await using var session = CreateSession(engine, device, worker: worker);
        await session.StartAsync(TestContext.Current.CancellationToken);
        RaiseFrameCaptured(device, [0.1f]);
        await Task.Delay(TimeSpan.FromMilliseconds(50), TestContext.Current.CancellationToken);

        // Act: stop completes quickly via the abandon policy
        await session.StopAsync(TestContext.Current.CancellationToken).WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        // Assert: the backend must not be reset, nor the device stopped, while the abandoned pump
        // thread may still be inside the blocking backend call
        await Task.Delay(TimeSpan.FromMilliseconds(100), TestContext.Current.CancellationToken);
        Assert.Equal(0, engine.ResetCallCount);
        device.DidNotReceive().Stop();

        // Act: release the abandoned background thread so it can genuinely exit
        neverSignaled.Set();

        // Waits for both the backend reset and the device stop (not merely the first of the two
        // sequential calls ResetBackendAndStopDeviceCore makes) so this assertion cannot flake on
        // the brief window between them.
        SpinWait.SpinUntil(
            () => engine.ResetCallCount >= 1 && device.ReceivedCalls().Any(call => call.GetMethodInfo().Name == nameof(IAudioCaptureDevice.Stop)),
            TimeSpan.FromSeconds(5));

        // Assert: only once the worker genuinely exited was the backend reset and device stopped
        Assert.Equal(1, engine.ResetCallCount);
        device.Received(1).Stop();
    }

    /// <summary>
    ///     Proves that passing an already-canceled token to <see cref="RecognitionSession.StopAsync"/>
    ///     only bounds this caller's own wait - the call throws <see cref="OperationCanceledException"/>
    ///     immediately rather than waiting for teardown - while the shared teardown itself still
    ///     converges for every other observer (findings 19/25).
    /// </summary>
    [Fact]
    public async Task RecognitionSession_StopAsync_PreCanceledToken_ThrowsButTeardownStillConverges()
    {
        // Arrange
        var engine = new FakeRecognitionEngine();
        var device = CreateCaptureDevice();
        await using var session = CreateSession(engine, device);
        await session.StartAsync(TestContext.Current.CancellationToken);

        using var preCanceled = new CancellationTokenSource();
        await preCanceled.CancelAsync();

        // Act & Assert: this caller's own wait is bounded by its already-canceled token
        await Assert.ThrowsAsync<TaskCanceledException>(() => session.StopAsync(preCanceled.Token));

        // Assert: the shared teardown was never aborted by that caller's canceled wait - it still
        // converges for every other observer
        SpinWait.SpinUntil(() => session.State == RecognitionSessionState.Stopped, TimeSpan.FromSeconds(5));
        Assert.Equal(RecognitionSessionState.Stopped, session.State);
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
    ///     Builds a <see cref="RecognitionSession"/> directly over the supplied backend
    ///     and device, bypassing <see cref="SpeechRecognizerEngine"/>'s lease since this
    ///     session's own lifecycle, not the engine's, is under test here.
    /// </summary>
    private static RecognitionSession CreateSession(
        IRecognitionBackend engine,
        IAudioCaptureDevice device,
        IRecognitionModel? model = null,
        ISpeechDiagnostics? diagnostics = null,
        DedicatedWorker? worker = null,
        Action? releaseLease = null) =>
        new(
            engine,
            device,
            targetSampleRate: 16000,
            model ?? new FakeRecognitionModel(),
            releaseLease: releaseLease ?? (static () => { }),
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
