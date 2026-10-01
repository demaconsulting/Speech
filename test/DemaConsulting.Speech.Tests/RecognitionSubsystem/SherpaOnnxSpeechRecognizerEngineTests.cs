using DemaConsulting.Speech.AudioSubsystem;
using DemaConsulting.Speech.Diagnostics;
using DemaConsulting.Speech.RecognitionSubsystem;
using DemaConsulting.Speech.Tests.ModelManagementSubsystem.Fakes;
using DemaConsulting.Speech.Tests.RecognitionSubsystem.Fakes;
using NSubstitute;

namespace DemaConsulting.Speech.Tests.RecognitionSubsystem;

/// <summary>
///     Unit tests for <see cref="SherpaOnnxSpeechRecognizerEngine"/>, proving the single-session
///     exclusivity lease (Decision #2) over a fake backend and a substitute capture device, with
///     no native sherpa-onnx runtime.
/// </summary>
public class SherpaOnnxSpeechRecognizerEngineTests
{
    /// <summary>
    ///     Proves that creating a session with no prior session active succeeds and returns a
    ///     real, available session bound to the supplied device.
    /// </summary>
    [Fact]
    public async Task SherpaOnnxSpeechRecognizerEngine_CreateSessionAsync_NoActiveSession_ReturnsSession()
    {
        // Arrange: an engine over a fake backend and an available capture device
        var engine = new SherpaOnnxSpeechRecognizerEngine(
            new FakeRecognitionEngine(), new FakeRecognitionModel(), NullSpeechDiagnostics.Instance);
        var device = CreateCaptureDevice();

        // Act
        var session = await engine.CreateSessionAsync(device, TestContext.Current.CancellationToken);

        // Assert: a real, available session was returned
        Assert.True(session.IsAvailable);
        Assert.IsType<SherpaOnnxRecognitionSession>(session);

        // Cleanup
        await session.DisposeAsync();
        await engine.DisposeAsync();
    }

    /// <summary>
    ///     Proves that a concurrent <see cref="SherpaOnnxSpeechRecognizerEngine.CreateSessionAsync"/>
    ///     call, made while a previously created session still holds the engine's exclusivity
    ///     lease, fails fast with <see cref="RecognitionEngineBusyException"/> rather than queuing.
    /// </summary>
    [Fact]
    public async Task SherpaOnnxSpeechRecognizerEngine_CreateSessionAsync_SessionAlreadyLeased_ThrowsRecognitionEngineBusyException()
    {
        // Arrange: an engine with one already-created session
        var engine = new SherpaOnnxSpeechRecognizerEngine(
            new FakeRecognitionEngine(), new FakeRecognitionModel(), NullSpeechDiagnostics.Instance);
        var firstSession = await engine.CreateSessionAsync(CreateCaptureDevice(), TestContext.Current.CancellationToken);

        // Act & Assert: a second concurrent session request fails fast
        await Assert.ThrowsAsync<RecognitionEngineBusyException>(() => engine.CreateSessionAsync(CreateCaptureDevice(), TestContext.Current.CancellationToken));

        // Cleanup
        await firstSession.DisposeAsync();
        await engine.DisposeAsync();
    }

    /// <summary>
    ///     Proves that the exclusivity lease is still held - and a concurrent
    ///     <see cref="SherpaOnnxSpeechRecognizerEngine.CreateSessionAsync"/> still fails fast -
    ///     while a prior session's own <see cref="IAsyncDisposable.DisposeAsync"/> is still in
    ///     flight, not yet complete (Decision #2).
    /// </summary>
    [Fact]
    public async Task SherpaOnnxSpeechRecognizerEngine_CreateSessionAsync_PriorSessionDisposing_ThrowsRecognitionEngineBusyException()
    {
        // Arrange: a running session whose capture device blocks when stopped, standing in for
        // teardown work that has not yet completed. The block is lifted only by this test's own
        // explicit stopGate.Set() (in the finally below), never by a timeout - a bounded wait here
        // would race against this test's own continuation under heavy parallel test-run CPU
        // contention: if that continuation were delayed past the bound, Stop() would return (and
        // the lease would be released) before the Act below ever ran, intermittently passing for
        // the wrong reason. The capture device's own TestContext cancellation token is observed
        // purely as a safety net so a failed/aborted test run cannot leave this thread blocked
        // forever, not as part of the behavior under test.
        using var stopGate = new ManualResetEventSlim(false);
        var device = CreateCaptureDevice();
        device.When(d => d.Stop()).Do(_ => stopGate.Wait(TestContext.Current.CancellationToken));
        var engine = new SherpaOnnxSpeechRecognizerEngine(
            new FakeRecognitionEngine(), new FakeRecognitionModel(), NullSpeechDiagnostics.Instance);
        var session = await engine.CreateSessionAsync(device, TestContext.Current.CancellationToken);
        await session.StartAsync(TestContext.Current.CancellationToken);

        // Act: begin disposing the session but do not await completion yet
        var disposeTask = session.DisposeAsync().AsTask();
        try
        {
            // Assert: a concurrent create fails fast while the lease is still held
            await Assert.ThrowsAsync<RecognitionEngineBusyException>(() => engine.CreateSessionAsync(CreateCaptureDevice(), TestContext.Current.CancellationToken));
        }
        finally
        {
            // Cleanup: release the blocked teardown and let disposal complete
            stopGate.Set();
            await disposeTask;
        }

        await engine.DisposeAsync();
    }

    /// <summary>
    ///     Proves that once a prior session has fully disposed - releasing the exclusivity lease
    ///     - a fresh <see cref="SherpaOnnxSpeechRecognizerEngine.CreateSessionAsync"/> call
    ///     succeeds and returns a new session over the same backend.
    /// </summary>
    [Fact]
    public async Task SherpaOnnxSpeechRecognizerEngine_CreateSessionAsync_AfterPriorSessionFullyDisposed_ReturnsNewSession()
    {
        // Arrange: an engine whose first session has already fully disposed
        var engine = new SherpaOnnxSpeechRecognizerEngine(
            new FakeRecognitionEngine(), new FakeRecognitionModel(), NullSpeechDiagnostics.Instance);
        var firstSession = await engine.CreateSessionAsync(CreateCaptureDevice(), TestContext.Current.CancellationToken);
        await firstSession.StartAsync(TestContext.Current.CancellationToken);
        await firstSession.DisposeAsync();

        // Act: create a new session now that the lease has been released
        var secondSession = await engine.CreateSessionAsync(CreateCaptureDevice(), TestContext.Current.CancellationToken);

        // Assert: a new, independent, available session was returned
        Assert.True(secondSession.IsAvailable);
        Assert.NotSame(firstSession, secondSession);

        // Cleanup
        await secondSession.DisposeAsync();
        await engine.DisposeAsync();
    }

    /// <summary>
    ///     Proves that <see cref="SherpaOnnxSpeechRecognizerEngine.CreateSessionAsync"/> rejects
    ///     a null device, since this is a genuine caller error rather than an ordinary
    ///     unavailable state.
    /// </summary>
    [Fact]
    public async Task SherpaOnnxSpeechRecognizerEngine_CreateSessionAsync_NullDevice_ThrowsArgumentNullException()
    {
        // Arrange
        var engine = new SherpaOnnxSpeechRecognizerEngine(
            new FakeRecognitionEngine(), new FakeRecognitionModel(), NullSpeechDiagnostics.Instance);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => engine.CreateSessionAsync(null!, TestContext.Current.CancellationToken));

        // Cleanup
        await engine.DisposeAsync();
    }

    /// <summary>
    ///     Proves that disposing an engine with an active session disposes that session first -
    ///     so its own teardown (and the lease release it performs) completes cleanly - before the
    ///     shared backend itself is disposed.
    /// </summary>
    [Fact]
    public async Task SherpaOnnxSpeechRecognizerEngine_DisposeAsync_WithActiveSession_DisposesSessionFirst()
    {
        // Arrange: an engine with one active, started session, and a backend that records
        // whether the session had already reached Disposed by the time the backend itself was
        // disposed
        IRecognitionSession? session = null;
        var sessionDisposedBeforeBackend = false;
        var backend = new FakeRecognitionEngine(onDispose: () =>
            sessionDisposedBeforeBackend = session?.State == RecognitionSessionState.Disposed);
        var engine = new SherpaOnnxSpeechRecognizerEngine(backend, new FakeRecognitionModel(), NullSpeechDiagnostics.Instance);
        session = await engine.CreateSessionAsync(CreateCaptureDevice(), TestContext.Current.CancellationToken);
        await session.StartAsync(TestContext.Current.CancellationToken);

        // Act: dispose the engine directly, without disposing the session first
        await engine.DisposeAsync();

        // Assert: the session was already Disposed by the time the backend was disposed, and the
        // backend was disposed exactly once
        Assert.True(sessionDisposedBeforeBackend);
        Assert.Equal(1, backend.DisposeCallCount);
    }

    /// <summary>
    ///     Builds a substitute capture device reporting itself available at a plain mono 16 kHz
    ///     format.
    /// </summary>
    private static IAudioCaptureDevice CreateCaptureDevice()
    {
        var device = Substitute.For<IAudioCaptureDevice>();
        device.IsAvailable.Returns(true);
        device.SampleRate.Returns(16000);
        device.ChannelCount.Returns(1);
        return device;
    }
}
