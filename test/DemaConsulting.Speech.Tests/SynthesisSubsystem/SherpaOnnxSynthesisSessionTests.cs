using DemaConsulting.Speech.AudioSubsystem;
using DemaConsulting.Speech.Diagnostics;
using DemaConsulting.Speech.ModelManagementSubsystem;
using DemaConsulting.Speech.SynthesisSubsystem;
using DemaConsulting.Speech.Tests.ModelManagementSubsystem.Fakes;
using DemaConsulting.Speech.Tests.SynthesisSubsystem.Fakes;
using NSubstitute;

namespace DemaConsulting.Speech.Tests.SynthesisSubsystem;

/// <summary>
///     Unit tests for <see cref="SherpaOnnxSynthesisSession"/>, exercising the full chunk →
///     synthesize → resample → play pipeline through a fake backend and a substitute playback
///     device, with no speakers and no native sherpa-onnx runtime, plus the state machine and
///     overlap-rule behavior unique to the session abstraction.
/// </summary>
/// <remarks>
///     Every test is deterministic without timing assumptions: segments are synthesized and
///     played strictly in order (one at a time), so every assertion runs only once the awaited
///     call has genuinely completed.
/// </remarks>
public class SherpaOnnxSynthesisSessionTests
{
    /// <summary>
    ///     Proves that plain text with no tags synthesizes to at least one segment carrying real
    ///     audio at the backend's declared sample rate.
    /// </summary>
    [Fact]
    public async Task SherpaOnnxSynthesisSession_SynthesizeAsync_PlainText_YieldsAudioSegment()
    {
        // Arrange
        var backend = new FakeSynthesisEngine(sampleRate: 22050);
        var device = CreateAvailablePlaybackDevice();
        await using var session = CreateSession(backend, device);

        // Act
        var segments = await session.SynthesizeAsync("Hello world.", TestContext.Current.CancellationToken);

        // Assert: one segment of real synthesized audio at the backend's rate
        var segment = Assert.Single(segments);
        Assert.NotEmpty(segment.Samples);
        Assert.Equal(22050, segment.SampleRate);
        Assert.Single(backend.GenerateCalls);
    }

    /// <summary>
    ///     Proves that <see cref="ISynthesisSession.SynthesizeAsync"/> returns the full-fidelity
    ///     ordered segment list, including a pure-silence segment for a pause tag, with no
    ///     backend call for the silent segment.
    /// </summary>
    [Fact]
    public async Task SherpaOnnxSynthesisSession_SynthesizeAsync_ReturnsFullFidelitySegmentListIncludingSilence()
    {
        // Arrange
        var backend = new FakeSynthesisEngine();
        var device = CreateAvailablePlaybackDevice();
        await using var session = CreateSession(backend, device);

        // Act
        var segments = await session.SynthesizeAsync("Hello [short pause] world.", TestContext.Current.CancellationToken);

        // Assert: a pure-silence segment appears, and the backend was never asked to synthesize it
        var silenceSegment = Assert.Single(segments, segment => segment.Samples.Count == 0);
        Assert.True(silenceSegment.PostSilence > TimeSpan.Zero);
        Assert.DoesNotContain(backend.GenerateCalls, call => call.Text.Length == 0);
    }

    /// <summary>
    ///     Proves that a fault while synthesizing a segment is reported through diagnostics,
    ///     propagates to the caller, and transitions the session to <see cref="SynthesisSessionState.Faulted"/>.
    /// </summary>
    [Fact]
    public async Task SherpaOnnxSynthesisSession_SynthesizeAsync_BackendThrows_ReportsFaultAndTransitionsToFaulted()
    {
        // Arrange
        var backend = new FakeSynthesisEngine(generateException: new InvalidOperationException("backend failed"));
        var device = CreateAvailablePlaybackDevice();
        var diagnostics = Substitute.For<ISpeechDiagnostics>();
        await using var session = CreateSession(backend, device, diagnostics: diagnostics);

        // Act & Assert: the call surfaces the backend's fault
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => session.SynthesizeAsync("Hello world.", TestContext.Current.CancellationToken));

        diagnostics.Received().Report(
            SpeechDiagnosticLevel.Error,
            "SynthesisSubsystem",
            Arg.Is<string>(message => message.Contains("faulted", StringComparison.Ordinal)));
        Assert.Equal(SynthesisSessionState.Faulted, session.State);
    }

    /// <summary>
    ///     Proves that a faulted session rejects any further operation with
    ///     <see cref="SynthesisSessionFaultedException"/>, rather than attempting to synthesize
    ///     again.
    /// </summary>
    [Fact]
    public async Task SherpaOnnxSynthesisSession_SynthesizeAsync_AfterFault_ThrowsSynthesisSessionFaultedException()
    {
        // Arrange: fault the session with one failing call
        var backend = new FakeSynthesisEngine(generateException: new InvalidOperationException("backend failed"));
        var device = CreateAvailablePlaybackDevice();
        await using var session = CreateSession(backend, device);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => session.SynthesizeAsync("Hello world.", TestContext.Current.CancellationToken));

        // Act & Assert: a subsequent call fails fast with the faulted-session exception
        await Assert.ThrowsAsync<SynthesisSessionFaultedException>(
            () => session.SynthesizeAsync("Hello again.", TestContext.Current.CancellationToken));
    }

    /// <summary>
    ///     Proves that <see cref="ISynthesisSession.SpeakAsync"/> starts the playback device,
    ///     writes resampled audio, and stops the device once synthesis and playback have
    ///     completed.
    /// </summary>
    [Fact]
    public async Task SherpaOnnxSynthesisSession_SpeakAsync_PlainText_StartsWritesAndStopsDevice()
    {
        // Arrange: a mono 16 kHz playback device
        var backend = new FakeSynthesisEngine();
        var device = CreateAvailablePlaybackDevice(sampleRate: 16000, channelCount: 1);
        await using var session = CreateSession(backend, device);

        // Act
        await session.SpeakAsync("Hello world.", TestContext.Current.CancellationToken);

        // Assert: the device lifecycle and write order are exactly as expected
        Received.InOrder(() =>
        {
            device.Start();
            device.Write(Arg.Any<IReadOnlyList<float>>());
            device.Stop();
        });
    }

    /// <summary>
    ///     Proves that a session supports multiple independent <see cref="ISynthesisSession.SpeakAsync"/>
    ///     calls on the same instance without reconstruction, so a host may construct one session
    ///     per model/device combination and reuse it across many conversation turns for
    ///     low-latency, repeated synthesis.
    /// </summary>
    [Fact]
    public async Task SherpaOnnxSynthesisSession_SpeakAsync_CalledTwiceOnSameInstance_ReusesSameInstanceWithoutReconstruction()
    {
        // Arrange: a single session instance over a mono 16 kHz playback device
        var backend = new FakeSynthesisEngine();
        var device = CreateAvailablePlaybackDevice(sampleRate: 16000, channelCount: 1);
        await using var session = CreateSession(backend, device);

        // Act: run two independent speak calls on the same instance
        await session.SpeakAsync("Hello world.", TestContext.Current.CancellationToken);
        await session.SpeakAsync("Hello again.", TestContext.Current.CancellationToken);

        // Assert: both calls genuinely started and stopped the playback device, proving the
        // session remains usable across repeated calls without being disposed and recreated
        device.Received(2).Start();
        device.Received(2).Stop();
        Assert.Equal(SynthesisSessionState.Stopped, session.State);
    }

    /// <summary>
    ///     Proves that a playback device fault while writing propagates to the caller, and that
    ///     the device is still stopped in the guaranteeing <c>finally</c> block.
    /// </summary>
    [Fact]
    public async Task SherpaOnnxSynthesisSession_SpeakAsync_PlaybackDeviceWriteThrows_PropagatesAndStillStopsDevice()
    {
        // Arrange
        var backend = new FakeSynthesisEngine();
        var device = CreateAvailablePlaybackDevice();
        device
            .When(d => d.Write(Arg.Any<IReadOnlyList<float>>()))
            .Do(_ => throw new AudioDeviceUnavailableException("speakers disconnected"));
        await using var session = CreateSession(backend, device);

        // Act & Assert: the fault propagates, but the device is still stopped
        await Assert.ThrowsAsync<AudioDeviceUnavailableException>(
            () => session.SpeakAsync("Hello world.", TestContext.Current.CancellationToken));
        device.Received(1).Stop();
    }

    /// <summary>
    ///     Proves that a playback device that stops working (reported unavailable) surfaces
    ///     honestly rather than hanging or crashing.
    /// </summary>
    [Fact]
    public async Task SherpaOnnxSynthesisSession_SpeakAsync_PlaybackDeviceUnavailable_ThrowsRatherThanHanging()
    {
        // Arrange: an unavailable playback device, whose Start() throws per its contract
        var backend = new FakeSynthesisEngine();
        await using var session = CreateSession(backend, UnavailableAudioPlaybackDevice.Instance);

        // Act & Assert
        await Assert.ThrowsAsync<AudioDeviceUnavailableException>(
            () => session.SpeakAsync("Hello world.", TestContext.Current.CancellationToken));
    }

    /// <summary>
    ///     Proves that a playback device whose <c>Start()</c> itself throws still reaches the
    ///     <c>finally</c> block's <c>Stop()</c> teardown, rather than leaking whatever partial
    ///     resource the device acquired before faulting.
    /// </summary>
    [Fact]
    public async Task SherpaOnnxSynthesisSession_SpeakAsync_PlaybackDeviceStartThrows_StillCallsStop()
    {
        // Arrange: a substitute device whose Start() throws, matching an unavailable-device fault
        var backend = new FakeSynthesisEngine();
        var device = CreateAvailablePlaybackDevice();
        device
            .When(d => d.Start())
            .Do(_ => throw new AudioDeviceUnavailableException("speakers disconnected before start"));
        await using var session = CreateSession(backend, device);

        // Act & Assert: the Start() fault propagates, but the finally block still calls Stop()
        await Assert.ThrowsAsync<AudioDeviceUnavailableException>(
            () => session.SpeakAsync("Hello world.", TestContext.Current.CancellationToken));
        device.Received(1).Stop();
    }

    /// <summary>
    ///     Proves that <see cref="ISynthesisSession.StopAsync"/> cancels an in-flight
    ///     <see cref="ISynthesisSession.SpeakAsync"/> operation deterministically, ending its task
    ///     without hanging - and, critically, only after the producer's in-flight native-style
    ///     <c>Generate</c> call has genuinely returned, never orphaning it.
    /// </summary>
    /// <remarks>
    ///     A <c>Timeout</c> is set as a safety net: if cancellation ever regressed to orphaning
    ///     the worker (or to hanging indefinitely), this test would fail fast with a timeout
    ///     rather than hanging the whole test run forever.
    /// </remarks>
    [Fact(Timeout = 5000)]
    public async Task SherpaOnnxSynthesisSession_StopAsync_WhileSpeaking_CancelsInFlightOperationOnlyAfterInFlightGenerateReturns()
    {
        // Arrange: a backend that signals it has started, then blocks until the test explicitly
        // releases it - simulating a native call that keeps running for a little while after
        // cancellation is requested, exactly like the real sherpa-onnx binding, which has no
        // in-flight cancellation primitive of its own.
        using var generateStarted = new SemaphoreSlim(0, 1);
        using var generateRelease = new SemaphoreSlim(0, 1);
        var backend = new BlockingSynthesisEngine(generateStarted, generateRelease, TestContext.Current.CancellationToken);
        var device = CreateAvailablePlaybackDevice();
        await using var session = CreateSession(backend, device);

        // Act: start speaking, wait until synthesis has begun, then stop
        var speakTask = session.SpeakAsync("Hello world.", TestContext.Current.CancellationToken);
        await generateStarted.WaitAsync(TestContext.Current.CancellationToken);
        await session.StopAsync(TestContext.Current.CancellationToken);

        // Assert: the operation must not be reported complete while the backend's Generate call
        // is still in flight - this is exactly the window in which a caller previously could
        // (and, per the bug report this design supersedes, did) dispose the engine out from
        // under it.
        Assert.False(speakTask.IsCompleted);

        // Act: only now let the in-flight native-style call finish, as the real backend
        // eventually would on its own
        generateRelease.Release();

        // Assert: the operation ends via cancellation rather than hanging or faulting some other way
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => speakTask);
        Assert.True(backend.GenerateReturned);
        Assert.Equal(SynthesisSessionState.Stopped, session.State);
    }

    /// <summary>
    ///     Proves that calling <see cref="ISynthesisSession.StopAsync"/> with no operation in
    ///     flight is a safe no-op.
    /// </summary>
    [Fact]
    public async Task SherpaOnnxSynthesisSession_StopAsync_NoOperationInFlight_IsNoOp()
    {
        // Arrange
        var backend = new FakeSynthesisEngine();
        var device = CreateAvailablePlaybackDevice();
        await using var session = CreateSession(backend, device);

        // Act
        var exception = await Record.ExceptionAsync(() => session.StopAsync(TestContext.Current.CancellationToken));

        // Assert
        Assert.Null(exception);
    }

    /// <summary>
    ///     Proves that a <see cref="ISynthesisSession.StopAsync"/> call racing against the
    ///     in-flight operation's own completion never surfaces an unhandled
    ///     <see cref="ObjectDisposedException"/> - closing the check-then-act race window in which
    ///     <c>StopAsync</c>/<c>DisposeAsync</c> read the operation's cancellation source outside
    ///     the lock and call <c>CancelAsync</c> on it just as the operation's own <c>finally</c>
    ///     block disposes that same source.
    /// </summary>
    /// <remarks>
    ///     Repeated many times with a backend that completes near-instantly to maximize the
    ///     chance of landing inside the narrow race window if the fix ever regressed.
    /// </remarks>
    [Fact]
    public async Task SherpaOnnxSynthesisSession_StopAsync_RacingOperationCompletion_NeverThrowsObjectDisposedException()
    {
        for (var i = 0; i < 200; i++)
        {
            // Arrange
            var backend = new FakeSynthesisEngine();
            var device = CreateAvailablePlaybackDevice();
            await using var session = CreateSession(backend, device);

            // Act: fire the operation and race a Stop against its completion with no synchronization
            var speakTask = session.SpeakAsync("Hi.", TestContext.Current.CancellationToken);
            var stopTask = session.StopAsync(TestContext.Current.CancellationToken);

            var speakException = await Record.ExceptionAsync(() => speakTask);
            var stopException = await Record.ExceptionAsync(() => stopTask);

            // Assert: neither task ever surfaces an ObjectDisposedException from the race
            Assert.IsNotType<ObjectDisposedException>(speakException);
            Assert.IsNotType<ObjectDisposedException>(stopException);
        }
    }

    /// <summary>
    ///     Proves that calling <see cref="ISynthesisSession.SpeakAsync"/> while another
    ///     <see cref="ISynthesisSession.SpeakAsync"/> call is already in flight on the same
    ///     session throws <see cref="InvalidOperationException"/> rather than producing undefined
    ///     interleaving.
    /// </summary>
    [Fact]
    public async Task SherpaOnnxSynthesisSession_SpeakAsync_CalledWhileAlreadySpeaking_ThrowsInvalidOperationException()
    {
        // Arrange: a backend that blocks the first call in flight
        using var generateStarted = new SemaphoreSlim(0, 1);
        using var generateRelease = new SemaphoreSlim(0, 1);
        var backend = new BlockingSynthesisEngine(generateStarted, generateRelease, TestContext.Current.CancellationToken);
        var device = CreateAvailablePlaybackDevice();
        await using var session = CreateSession(backend, device);
        var firstCall = session.SpeakAsync("Hello world.", TestContext.Current.CancellationToken);
        await generateStarted.WaitAsync(TestContext.Current.CancellationToken);

        // Act & Assert: a second, overlapping call is rejected
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => session.SpeakAsync("Hello again.", TestContext.Current.CancellationToken));

        // Cleanup: release the first call so it completes and the session can be disposed cleanly
        generateRelease.Release();
        await firstCall;
    }

    /// <summary>
    ///     Proves that calling <see cref="ISynthesisSession.SynthesizeAsync"/> while a
    ///     <see cref="ISynthesisSession.SpeakAsync"/> call is already in flight on the same
    ///     session is also rejected, proving the overlap rule applies across both operations, not
    ///     just between two calls of the same kind.
    /// </summary>
    [Fact]
    public async Task SherpaOnnxSynthesisSession_SynthesizeAsync_CalledWhileAlreadySpeaking_ThrowsInvalidOperationException()
    {
        // Arrange
        using var generateStarted = new SemaphoreSlim(0, 1);
        using var generateRelease = new SemaphoreSlim(0, 1);
        var backend = new BlockingSynthesisEngine(generateStarted, generateRelease, TestContext.Current.CancellationToken);
        var device = CreateAvailablePlaybackDevice();
        await using var session = CreateSession(backend, device);
        var firstCall = session.SpeakAsync("Hello world.", TestContext.Current.CancellationToken);
        await generateStarted.WaitAsync(TestContext.Current.CancellationToken);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => session.SynthesizeAsync("Hello again.", TestContext.Current.CancellationToken));

        // Cleanup
        generateRelease.Release();
        await firstCall;
    }

    /// <summary>
    ///     Proves that <see cref="ISynthesisSession.StateChanged"/> raises every expected
    ///     transition, in order, for one successful <see cref="ISynthesisSession.SynthesizeAsync"/>
    ///     call: <c>Created → Starting → Running → Stopping → Stopped</c>.
    /// </summary>
    [Fact]
    public async Task SherpaOnnxSynthesisSession_StateChanged_OneSuccessfulOperation_RaisesExpectedTransitionsInOrder()
    {
        // Arrange
        var backend = new FakeSynthesisEngine();
        var device = CreateAvailablePlaybackDevice();
        await using var session = CreateSession(backend, device);
        List<SynthesisSessionState> observedStates = [];
        session.StateChanged += (_, args) => observedStates.Add(args.Current);

        // Act
        await session.SynthesizeAsync("Hello world.", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            [
                SynthesisSessionState.Starting,
                SynthesisSessionState.Running,
                SynthesisSessionState.Stopping,
                SynthesisSessionState.Stopped
            ],
            observedStates);
    }

    /// <summary>
    ///     Proves that a <see cref="ISynthesisSession.StateChanged"/> handler that throws does not
    ///     propagate out of the session and does not destabilize its own lifecycle, mirroring this
    ///     library's event-exception-isolation convention.
    /// </summary>
    [Fact]
    public async Task SherpaOnnxSynthesisSession_StateChanged_HandlerThrows_IsIsolatedAndDoesNotPropagate()
    {
        // Arrange
        var backend = new FakeSynthesisEngine();
        var device = CreateAvailablePlaybackDevice();
        var diagnostics = Substitute.For<ISpeechDiagnostics>();
        await using var session = CreateSession(backend, device, diagnostics: diagnostics);
        session.StateChanged += (_, _) => throw new InvalidOperationException("handler faulted");

        // Act
        var exception = await Record.ExceptionAsync(
            () => session.SynthesizeAsync("Hello world.", TestContext.Current.CancellationToken));

        // Assert: the operation itself still completed successfully
        Assert.Null(exception);
        diagnostics.Received().Report(
            SpeechDiagnosticLevel.Warning,
            "SynthesisSubsystem",
            Arg.Is<string>(message => message.Contains("StateChanged", StringComparison.Ordinal)));
    }

    /// <summary>
    ///     Proves that disposal is idempotent and releases the engine's exclusivity lease exactly
    ///     once, even when called more than once.
    /// </summary>
    [Fact]
    public async Task SherpaOnnxSynthesisSession_DisposeAsync_CalledTwice_ReleasesLeaseOnce()
    {
        // Arrange
        var backend = new FakeSynthesisEngine();
        var device = CreateAvailablePlaybackDevice();
        var releaseCount = 0;
        var session = new SherpaOnnxSynthesisSession(
            backend, device, new FakeSynthesisModel(), null, NullSpeechDiagnostics.Instance, () => releaseCount++);

        // Act
        await session.DisposeAsync();
        await session.DisposeAsync();

        // Assert
        Assert.Equal(1, releaseCount);
        Assert.Equal(SynthesisSessionState.Disposed, session.State);
    }

    /// <summary>
    ///     Proves that <c>DisposeAsync</c> does not release the engine's exclusivity lease while
    ///     an in-flight operation's backend call is still demonstrably running - closing the race
    ///     in which a prior implementation released the lease the instant cancellation was
    ///     requested, before the operation had genuinely stopped.
    /// </summary>
    /// <remarks>
    ///     A <c>Timeout</c> is set as a safety net: if disposal ever regressed to not awaiting the
    ///     in-flight operation at all, this test would still pass trivially, but if it regressed to
    ///     awaiting indefinitely (ignoring the abandon policy), this would fail fast instead of
    ///     hanging the whole test run forever.
    /// </remarks>
    [Fact(Timeout = 5000)]
    public async Task SherpaOnnxSynthesisSession_DisposeAsync_WhileSpeaking_DoesNotReleaseLeaseBeforeOperationSettles()
    {
        // Arrange: a backend that blocks inside Generate until explicitly released, simulating a
        // native call still genuinely in flight when disposal is requested.
        using var generateStarted = new SemaphoreSlim(0, 1);
        using var generateRelease = new SemaphoreSlim(0, 1);
        var backend = new BlockingSynthesisEngine(generateStarted, generateRelease, TestContext.Current.CancellationToken);
        var device = CreateAvailablePlaybackDevice();
        var releaseCount = 0;
        var session = new SherpaOnnxSynthesisSession(
            backend, device, new FakeSynthesisModel(), null, NullSpeechDiagnostics.Instance, () => releaseCount++);

        // Act: start speaking, wait until the backend call has begun, then start disposing
        var speakTask = session.SpeakAsync("Hello world.", TestContext.Current.CancellationToken);
        await generateStarted.WaitAsync(TestContext.Current.CancellationToken);
        var disposeTask = session.DisposeAsync().AsTask();

        // Let the in-flight backend call finish so neither task hangs for the rest of the test
        generateRelease.Release();
        await disposeTask;

        // Assert: by the time DisposeAsync returned, the backend call had genuinely finished and
        // the operation had settled (whichever way it settled), so the lease was never released
        // while the operation was still running
        Assert.True(backend.GenerateReturned);
        Assert.True(speakTask.IsCompleted);
        Assert.Equal(1, releaseCount);

        // Observe the task's outcome so it is never reported as an unobserved exception
        await Record.ExceptionAsync(() => speakTask);
    }

    /// <summary>
    ///     Proves that operating on a disposed session throws <see cref="ObjectDisposedException"/>
    ///     rather than silently doing nothing.
    /// </summary>
    [Fact]
    public async Task SherpaOnnxSynthesisSession_SynthesizeAsync_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        var backend = new FakeSynthesisEngine();
        var device = CreateAvailablePlaybackDevice();
        var session = CreateSession(backend, device);
        await session.DisposeAsync();

        // Act & Assert
        await Assert.ThrowsAsync<ObjectDisposedException>(
            () => session.SynthesizeAsync("hello", TestContext.Current.CancellationToken));
    }

    /// <summary>
    ///     Proves that <see cref="ISynthesisSession.IsAvailable"/> reports <see langword="true"/>
    ///     for a freshly created session, but <see langword="false"/> once disposed.
    /// </summary>
    [Fact]
    public async Task SherpaOnnxSynthesisSession_IsAvailable_BeforeAndAfterDispose_ReflectsLifecycle()
    {
        // Arrange
        var backend = new FakeSynthesisEngine();
        var device = CreateAvailablePlaybackDevice();
        var session = CreateSession(backend, device);

        // Act & Assert: available before disposal
        Assert.True(session.IsAvailable);

        // Act
        await session.DisposeAsync();

        // Assert: unavailable after disposal
        Assert.False(session.IsAvailable);
    }

    /// <summary>
    ///     Proves that <see cref="ISynthesisSession.SpeakAsync"/> genuinely waits for the playback
    ///     device to report a drained queue before stopping it, rather than treating "every
    ///     segment enqueued" as "finished playing". Uses a controllable fake (an NSubstitute stub
    ///     whose <c>PendingSampleCount</c> getter signals a semaphore on every read) so the
    ///     assertion that the task has not yet completed is driven by an observed poll, not by an
    ///     arbitrary sleep racing the implementation.
    /// </summary>
    [Fact]
    public async Task SherpaOnnxSynthesisSession_SpeakAsync_PlaybackDeviceReportsPendingSamples_WaitsForDrainBeforeStopping()
    {
        // Arrange: a playback device that reports samples still pending until the test releases it
        var backend = new FakeSynthesisEngine();
        var device = CreateAvailablePlaybackDevice();
        var pendingSampleCount = 1;
        using var polled = new SemaphoreSlim(0);
        device.PendingSampleCount.Returns(_ =>
        {
            var value = Volatile.Read(ref pendingSampleCount);
            polled.Release();
            return value;
        });
        await using var session = CreateSession(backend, device);

        // Act: start speaking; every segment is enqueued almost immediately, but the fake device
        // keeps reporting pending samples until the test signals otherwise
        var speakTask = session.SpeakAsync("Hi.", TestContext.Current.CancellationToken);

        // Wait until the drain wait has genuinely begun polling the device at least once
        await polled.WaitAsync(TestContext.Current.CancellationToken);

        // Assert: playback has not been treated as finished while samples are still reported pending
        Assert.False(speakTask.IsCompleted);
        device.DidNotReceive().Stop();

        // Act: signal the fake device has now genuinely drained
        Volatile.Write(ref pendingSampleCount, 0);
        await speakTask;

        // Assert: only once the device reports a drained queue does playback stop
        device.Received(1).Stop();
    }

    /// <summary>
    ///     Proves that the speaker id resolved from <see cref="ISynthesisModel.ResolveSpeakerId"/>
    ///     (rather than a hard-coded <c>0</c>) reaches the backend, for both a default (no
    ///     parameter bag supplied) case and a non-default (bag supplied) case.
    /// </summary>
    [Fact]
    public async Task SherpaOnnxSynthesisSession_SynthesizeAsync_NoParameterValues_ResolvesDefaultSpeakerIdFromModel()
    {
        // Arrange: a model whose ResolveSpeakerId hook always returns a distinctive non-zero id
        var backend = new FakeSynthesisEngine();
        var device = CreateAvailablePlaybackDevice();
        var model = new FakeSynthesisModel(resolveSpeakerId: _ => 7);
        await using var session = CreateSession(backend, device, model);

        // Act
        await session.SynthesizeAsync("Hello world.", TestContext.Current.CancellationToken);

        // Assert
        var call = Assert.Single(backend.GenerateCalls);
        Assert.Equal(7, call.SpeakerId);
    }

    /// <summary>
    ///     Proves that a supplied parameter value bag reaches
    ///     <see cref="ISynthesisModel.ResolveSpeakerId"/> and, through it, the speaker id passed
    ///     to <see cref="ISynthesisBackend.Generate"/>.
    /// </summary>
    [Fact]
    public async Task SherpaOnnxSynthesisSession_SynthesizeAsync_ParameterValuesSupplied_ResolvesSpeakerIdFromBag()
    {
        // Arrange: a model whose ResolveSpeakerId hook echoes back a bag value
        var backend = new FakeSynthesisEngine();
        var device = CreateAvailablePlaybackDevice();
        var model = new FakeSynthesisModel(
            resolveSpeakerId: values => values is not null && values.TryGetValue("voice", out var value) && value is int id ? id : 0);
        IReadOnlyDictionary<string, object> parameterValues = new Dictionary<string, object> { ["voice"] = 3 };
        await using var session = CreateSession(backend, device, model, parameterValues);

        // Act
        await session.SynthesizeAsync("Hello world.", TestContext.Current.CancellationToken);

        // Assert
        var call = Assert.Single(backend.GenerateCalls);
        Assert.Equal(3, call.SpeakerId);
    }

    /// <summary>
    ///     Proves that a segment's own Natural Language Audio Tag speed/volume overrides are
    ///     unaffected by, and coexist correctly with, a supplied parameter value bag driving
    ///     speaker-id resolution - the two mechanisms remain independent.
    /// </summary>
    [Fact]
    public async Task SherpaOnnxSynthesisSession_SynthesizeAsync_ParameterValuesSuppliedAlongsideSpeedTag_BothMechanismsApplyIndependently()
    {
        // Arrange
        var backend = new FakeSynthesisEngine();
        var device = CreateAvailablePlaybackDevice();
        var model = new FakeSynthesisModel(resolveSpeakerId: _ => 5);
        IReadOnlyDictionary<string, object> parameterValues = new Dictionary<string, object> { ["voice"] = 5 };
        await using var session = CreateSession(backend, device, model, parameterValues);

        // Act: "fast" is a natural language audio tag mapped to the model's own "tempo" parameter
        await session.SynthesizeAsync("[fast] Hello world.", TestContext.Current.CancellationToken);

        // Assert: the speaker id still resolves from the bag, and the segment's speed differs from 1.0
        var call = Assert.Single(backend.GenerateCalls);
        Assert.Equal(5, call.SpeakerId);
        Assert.NotEqual(1.0f, call.Speed);
    }

    /// <summary>
    ///     Proves that a long, multi-sentence input still produces every segment, in the exact
    ///     order the sentences appear in the source text, and that
    ///     <see cref="ISynthesisBackend.Generate"/> is never called concurrently with itself.
    /// </summary>
    [Fact]
    public async Task SherpaOnnxSynthesisSession_SynthesizeAsync_LongMultiSentenceInput_ProducesOrderedSegmentsSequentially()
    {
        // Arrange: 12 short, uniquely numbered sentences, so ordering across many chunks can be
        // cross-checked against the source text.
        const int sentenceCount = 12;
        var sentences = Enumerable.Range(1, sentenceCount).Select(i => $"Sentence number {i}.");
        var text = string.Join(" ", sentences);
        var backend = new FakeSynthesisEngine(simulatedGenerateDelay: TimeSpan.FromMilliseconds(5));
        var device = CreateAvailablePlaybackDevice();
        await using var session = CreateSession(backend, device);

        // Act
        var segments = await session.SynthesizeAsync(text, TestContext.Current.CancellationToken);

        // Assert: every sentence was synthesized exactly once, in source order
        Assert.Equal(sentenceCount, backend.GenerateCalls.Count);
        for (var i = 0; i < sentenceCount; i++)
        {
            Assert.Contains($"number {i + 1}", backend.GenerateCalls[i].Text, StringComparison.Ordinal);
        }

        Assert.Equal(sentenceCount, segments.Count);
        for (var i = 0; i < sentenceCount; i++)
        {
            Assert.Equal(backend.GenerateCalls[i].Text.Length * 4, segments[i].Samples.Count);
        }

        // Assert: Generate was never entered concurrently with itself
        Assert.Equal(1, backend.MaxConcurrentGenerateCalls);
    }

    /// <summary>
    ///     Builds a substitute playback device reporting itself available with the given format.
    /// </summary>
    private static IAudioPlaybackDevice CreateAvailablePlaybackDevice(int sampleRate = 16000, int channelCount = 1)
    {
        var device = Substitute.For<IAudioPlaybackDevice>();
        device.IsAvailable.Returns(true);
        device.SampleRate.Returns(sampleRate);
        device.ChannelCount.Returns(channelCount);
        return device;
    }

    /// <summary>
    ///     Builds a <see cref="SherpaOnnxSynthesisSession"/> directly against a backend and
    ///     device, with a no-op lease-release callback, for tests that exercise the session in
    ///     isolation from <see cref="SherpaOnnxSpeechSynthesizerEngine"/>.
    /// </summary>
    private static SherpaOnnxSynthesisSession CreateSession(
        ISynthesisBackend backend,
        IAudioPlaybackDevice device,
        ISynthesisModel? model = null,
        IReadOnlyDictionary<string, object>? parameterValues = null,
        ISpeechDiagnostics? diagnostics = null) =>
        new(backend, device, model ?? new FakeSynthesisModel(), parameterValues, diagnostics ?? NullSpeechDiagnostics.Instance, () => { });

    /// <summary>
    ///     Test-only <see cref="ISynthesisBackend"/> whose <see cref="Generate"/> signals a
    ///     semaphore once called, then blocks until the test explicitly releases a second
    ///     semaphore - simulating a native call that keeps running for a while after the
    ///     session's cancellation token is cancelled, exactly like the real sherpa-onnx binding,
    ///     which has no in-flight cancellation primitive of its own and so cannot be interrupted
    ///     by <see cref="ISynthesisSession.StopAsync"/> or an externally cancelled token.
    /// </summary>
    /// <remarks>
    ///     Deliberately does <em>not</em> unblock <see cref="Generate"/> from <see cref="Dispose"/>:
    ///     the whole point of the regression this fake supports is that
    ///     <see cref="SherpaOnnxSynthesisSession"/> must never call <see cref="Dispose"/> (or let
    ///     a caller do so) while this call is still in flight, so a test relying on
    ///     <see cref="Dispose"/> to unblock it would either mask that exact bug or deadlock
    ///     against the fix. Callers must release <paramref name="generateRelease"/> explicitly to
    ///     let the in-flight call complete. However, <see cref="Generate"/> still waits against
    ///     <paramref name="testCancellationToken"/>, so a failed or timed-out test unblocks the
    ///     background producer thread instead of leaving it blocked for the rest of the test run.
    /// </remarks>
    /// <param name="generateStarted">Released once <see cref="Generate"/> is called.</param>
    /// <param name="generateRelease">Awaited by <see cref="Generate"/> before it returns.</param>
    /// <param name="testCancellationToken">
    ///     The owning test's own cancellation/timeout token (for example,
    ///     <see cref="TestContext.CancellationToken"/>), observed only as a failure-mode safety
    ///     net so the wait never outlives the test, not as part of the behavior under test.
    /// </param>
    private sealed class BlockingSynthesisEngine(
        SemaphoreSlim generateStarted,
        SemaphoreSlim generateRelease,
        CancellationToken testCancellationToken) : ISynthesisBackend
    {
        public int SampleRate => 16000;

        /// <summary>Gets a value indicating whether the in-flight <see cref="Generate"/> call has genuinely returned.</summary>
        public bool GenerateReturned { get; private set; }

        public EngineAudio Generate(string text, float speed, int speakerId)
        {
            generateStarted.Release();

            // Blocks until the test explicitly allows this in-flight call to complete, rather
            // than observing any cancellation token that the session under test might use -
            // matching the real native call this fake stands in for. The test's own
            // cancellation/timeout token is still observed here purely as a safety net so a
            // failed or timed-out test cannot leave this background thread blocked forever.
            generateRelease.Wait(testCancellationToken);

            GenerateReturned = true;
            return new EngineAudio([], SampleRate);
        }

        public void Dispose()
        {
            // Intentionally does not unblock Generate(): see remarks above.
        }
    }
}
