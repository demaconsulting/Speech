using DemaConsulting.Speech.AudioSubsystem;
using DemaConsulting.Speech.SynthesisSubsystem;
using DemaConsulting.Speech.Tests.ModelManagementSubsystem.Fakes;
using DemaConsulting.Speech.Tests.SynthesisSubsystem.Fakes;
using NSubstitute;

namespace DemaConsulting.Speech.Tests.SynthesisSubsystem;

/// <summary>
///     Unit tests for <see cref="SherpaOnnxSpeechSynthesizerEngine"/>, exercising session
///     creation, its exclusivity lease, the one-shot <see cref="ISpeechSynthesizerEngine.SpeakAsync"/>/
///     <see cref="ISpeechSynthesizerEngine.SynthesizeAsync"/> convenience methods, and disposal.
/// </summary>
public sealed class SherpaOnnxSpeechSynthesizerEngineTests
{
    /// <summary>
    ///     Proves that a freshly constructed engine always reports itself available, since it is
    ///     only ever constructed after a successful backend load.
    /// </summary>
    [Fact]
    public void SherpaOnnxSpeechSynthesizerEngine_IsAvailable_Always_ReturnsTrue()
    {
        // Arrange
        var engine = new SherpaOnnxSpeechSynthesizerEngine(new FakeSynthesisEngine(), new FakeSynthesisModel());

        // Act & Assert
        Assert.True(engine.IsAvailable);
    }

    /// <summary>
    ///     Proves that <see cref="ISpeechSynthesizerEngine.CreateSessionAsync"/> succeeds when no
    ///     lease is held, returning a real, usable session bound to the supplied device.
    /// </summary>
    [Fact]
    public async Task SherpaOnnxSpeechSynthesizerEngine_CreateSessionAsync_NoLeaseHeld_ReturnsRealSession()
    {
        // Arrange
        var engine = new SherpaOnnxSpeechSynthesizerEngine(new FakeSynthesisEngine(), new FakeSynthesisModel());
        var device = CreateAvailablePlaybackDevice();

        // Act
        await using var session = await engine.CreateSessionAsync(device, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(session.IsAvailable);
        Assert.Equal(SynthesisSessionState.Created, session.State);
    }

    /// <summary>
    ///     Proves that a concurrent <see cref="ISpeechSynthesizerEngine.CreateSessionAsync"/> call
    ///     while a lease is already held fails fast with <see cref="SynthesisEngineBusyException"/>,
    ///     rather than queueing or waiting.
    /// </summary>
    [Fact]
    public async Task SherpaOnnxSpeechSynthesizerEngine_CreateSessionAsync_LeaseAlreadyHeld_ThrowsSynthesisEngineBusyException()
    {
        // Arrange: one session already leased
        var engine = new SherpaOnnxSpeechSynthesizerEngine(new FakeSynthesisEngine(), new FakeSynthesisModel());
        var device = CreateAvailablePlaybackDevice();
        await using var firstSession = await engine.CreateSessionAsync(device, TestContext.Current.CancellationToken);

        // Act & Assert: a second, concurrent lease attempt fails fast
        await Assert.ThrowsAsync<SynthesisEngineBusyException>(
            () => engine.CreateSessionAsync(device, TestContext.Current.CancellationToken));
    }

    /// <summary>
    ///     Proves that once a leased session has been disposed, the lease is released and a new
    ///     session may be created.
    /// </summary>
    [Fact]
    public async Task SherpaOnnxSpeechSynthesizerEngine_CreateSessionAsync_AfterPriorSessionDisposed_SucceedsAgain()
    {
        // Arrange: lease and release a first session
        var engine = new SherpaOnnxSpeechSynthesizerEngine(new FakeSynthesisEngine(), new FakeSynthesisModel());
        var device = CreateAvailablePlaybackDevice();
        var firstSession = await engine.CreateSessionAsync(device, TestContext.Current.CancellationToken);
        await firstSession.DisposeAsync();

        // Act: a second lease attempt, after the first session's disposal, succeeds
        await using var secondSession = await engine.CreateSessionAsync(device, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(secondSession.IsAvailable);
    }

    /// <summary>
    ///     Proves that <see cref="ISpeechSynthesizerEngine.CreateSessionAsync"/> rejects a null
    ///     device.
    /// </summary>
    [Fact]
    public async Task SherpaOnnxSpeechSynthesizerEngine_CreateSessionAsync_NullDevice_ThrowsArgumentNullException()
    {
        // Arrange
        var engine = new SherpaOnnxSpeechSynthesizerEngine(new FakeSynthesisEngine(), new FakeSynthesisModel());

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => engine.CreateSessionAsync(null!, TestContext.Current.CancellationToken));
    }

    /// <summary>
    ///     Proves that the one-shot <see cref="ISpeechSynthesizerEngine.SpeakAsync"/> convenience
    ///     method creates a session, speaks through it, and disposes it, releasing the lease so a
    ///     subsequent call succeeds without the caller managing a session at all.
    /// </summary>
    [Fact]
    public async Task SherpaOnnxSpeechSynthesizerEngine_SpeakAsync_CalledTwice_CreatesAndDisposesASessionEachTime()
    {
        // Arrange
        var engine = new SherpaOnnxSpeechSynthesizerEngine(new FakeSynthesisEngine(), new FakeSynthesisModel());
        var device = CreateAvailablePlaybackDevice();

        // Act: two independent one-shot calls
        await engine.SpeakAsync(device, "Hello world.", TestContext.Current.CancellationToken);
        await engine.SpeakAsync(device, "Hello again.", TestContext.Current.CancellationToken);

        // Assert: both calls genuinely started and stopped the device, proving the lease was
        // released between calls rather than leaking
        device.Received(2).Start();
        device.Received(2).Stop();
    }

    /// <summary>
    ///     Proves that the one-shot <see cref="ISpeechSynthesizerEngine.SynthesizeAsync"/>
    ///     convenience method creates and disposes a session internally, returning the
    ///     full-fidelity synthesized segments with no playback device required.
    /// </summary>
    [Fact]
    public async Task SherpaOnnxSpeechSynthesizerEngine_SynthesizeAsync_NoDeviceSupplied_ReturnsSegments()
    {
        // Arrange
        var fakeBackend = new FakeSynthesisEngine();
        var engine = new SherpaOnnxSpeechSynthesizerEngine(fakeBackend, new FakeSynthesisModel());

        // Act
        var segments = await engine.SynthesizeAsync("Hello world.", TestContext.Current.CancellationToken);

        // Assert
        var segment = Assert.Single(segments);
        Assert.NotEmpty(segment.Samples);
        Assert.Single(fakeBackend.GenerateCalls);
    }

    /// <summary>
    ///     Proves that disposal is idempotent and disposes the owned backend exactly once, even
    ///     when called more than once.
    /// </summary>
    [Fact]
    public async Task SherpaOnnxSpeechSynthesizerEngine_DisposeAsync_CalledTwice_DisposesBackendOnce()
    {
        // Arrange
        var backend = new FakeSynthesisEngine();
        var engine = new SherpaOnnxSpeechSynthesizerEngine(backend, new FakeSynthesisModel());

        // Act
        await engine.DisposeAsync();
        await engine.DisposeAsync();

        // Assert
        Assert.Equal(1, backend.DisposeCallCount);
    }

    /// <summary>
    ///     Proves that disposing the engine while a session is still leased disposes that session
    ///     first (best-effort), releasing its lease, before disposing the owned backend.
    /// </summary>
    [Fact]
    public async Task SherpaOnnxSpeechSynthesizerEngine_DisposeAsync_WithActiveLeasedSession_DisposesSessionFirst()
    {
        // Arrange: lease a session but never dispose it directly
        var backend = new FakeSynthesisEngine();
        var engine = new SherpaOnnxSpeechSynthesizerEngine(backend, new FakeSynthesisModel());
        var device = CreateAvailablePlaybackDevice();
        var session = await engine.CreateSessionAsync(device, TestContext.Current.CancellationToken);

        // Act: dispose the engine directly
        await engine.DisposeAsync();

        // Assert: the leased session was disposed (and so transitioned away from Created) and
        // the backend was disposed too
        Assert.Equal(SynthesisSessionState.Disposed, session.State);
        Assert.Equal(1, backend.DisposeCallCount);
    }

    /// <summary>
    ///     Builds a substitute playback device reporting itself available with a realistic
    ///     format.
    /// </summary>
    private static IAudioPlaybackDevice CreateAvailablePlaybackDevice(int sampleRate = 16000, int channelCount = 1)
    {
        var device = Substitute.For<IAudioPlaybackDevice>();
        device.IsAvailable.Returns(true);
        device.SampleRate.Returns(sampleRate);
        device.ChannelCount.Returns(channelCount);
        return device;
    }
}
