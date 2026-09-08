using DemaConsulting.Speech.AudioSubsystem;
using DemaConsulting.Speech.Diagnostics;
using DemaConsulting.Speech.ModelManagementSubsystem;
using DemaConsulting.Speech.SynthesisSubsystem;
using DemaConsulting.Speech.Tests.ModelManagementSubsystem.Fakes;
using DemaConsulting.Speech.Tests.SynthesisSubsystem.Fakes;
using NSubstitute;

namespace DemaConsulting.Speech.Tests.SynthesisSubsystem;

/// <summary>
///     Unit tests for <see cref="SherpaOnnxSpeechSynthesizer"/>, exercising the full chunk →
///     synthesize → resample → play pipeline through a fake engine and a substitute playback
///     device, with no speakers and no native sherpa-onnx runtime.
/// </summary>
/// <remarks>
///     Every test is deterministic without timing assumptions: the pipeline's bounded channel and
///     awaited tasks mean every produced segment is either fully consumed or the operation has
///     genuinely completed by the time an assertion runs.
/// </remarks>
public class SherpaOnnxSpeechSynthesizerTests
{
    /// <summary>
    ///     Proves that plain text with no tags synthesizes to at least one segment carrying real
    ///     audio at the engine's declared sample rate.
    /// </summary>
    [Fact]
    public async Task SynthesizeStreamAsync_PlainText_YieldsAudioSegment()
    {
        // Arrange
        var engine = new FakeSynthesisEngine(sampleRate: 22050);
        var playbackDevice = CreateAvailablePlaybackDevice();
        using var synthesizer = new SherpaOnnxSpeechSynthesizer(engine, playbackDevice, new FakeSynthesisModel());

        // Act
        var segments = await CollectAsync(
            synthesizer.SynthesizeStreamAsync("Hello world.", TestContext.Current.CancellationToken));

        // Assert: one segment of real synthesized audio at the engine's rate
        var segment = Assert.Single(segments);
        Assert.NotEmpty(segment.Samples);
        Assert.Equal(22050, segment.SampleRate);
        Assert.Single(engine.GenerateCalls);
    }

    /// <summary>
    ///     Proves that a pause tag produces a pure-silence segment with no engine call, ordered
    ///     between the flushed text segments surrounding it.
    /// </summary>
    [Fact]
    public async Task SynthesizeStreamAsync_TextWithPauseTag_YieldsSilenceSegmentWithNoEngineCall()
    {
        // Arrange
        var engine = new FakeSynthesisEngine();
        var playbackDevice = CreateAvailablePlaybackDevice();
        using var synthesizer = new SherpaOnnxSpeechSynthesizer(engine, playbackDevice, new FakeSynthesisModel());

        // Act
        var segments = await CollectAsync(
            synthesizer.SynthesizeStreamAsync("Hello [short pause] world.", TestContext.Current.CancellationToken));

        // Assert: a pure-silence segment appears, and the engine was never asked to synthesize it
        var silenceSegment = Assert.Single(segments, segment => segment.Samples.Count == 0);
        Assert.True(silenceSegment.PostSilence > TimeSpan.Zero);
        Assert.DoesNotContain(engine.GenerateCalls, call => call.Text.Length == 0);
    }

    /// <summary>
    ///     Proves that a fault while synthesizing a segment is reported through diagnostics and
    ///     surfaces to the caller enumerating the stream, rather than hanging.
    /// </summary>
    [Fact]
    public async Task SynthesizeStreamAsync_EngineThrows_ReportsFaultAndPropagatesToCaller()
    {
        // Arrange
        var engine = new FakeSynthesisEngine(generateException: new InvalidOperationException("engine failed"));
        var playbackDevice = CreateAvailablePlaybackDevice();
        var diagnostics = Substitute.For<ISpeechDiagnostics>();
        using var synthesizer = new SherpaOnnxSpeechSynthesizer(
            engine, playbackDevice, new FakeSynthesisModel(), diagnostics: diagnostics);

        // Act & Assert: enumerating the stream surfaces the engine's fault
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await foreach (var _ in synthesizer.SynthesizeStreamAsync("Hello world.", TestContext.Current.CancellationToken))
            {
                // Draining is enough to trigger the fault.
            }
        });

        diagnostics.Received().Report(
            SpeechDiagnosticLevel.Error,
            "SynthesisSubsystem",
            Arg.Is<string>(message => message.Contains("could not be synthesized", StringComparison.Ordinal)));
    }

    /// <summary>
    ///     Proves that playing a stream starts the playback device, writes silence and resampled
    ///     audio in order, and stops the device once every segment has played.
    /// </summary>
    [Fact]
    public async Task PlayStreamAsync_OrderedSegments_StartsWritesInOrderAndStops()
    {
        // Arrange: a mono 16 kHz playback device and two pre-built segments (silence, then audio)
        var engine = new FakeSynthesisEngine();
        var playbackDevice = CreateAvailablePlaybackDevice(sampleRate: 16000, channelCount: 1);
        using var synthesizer = new SherpaOnnxSpeechSynthesizer(engine, playbackDevice, new FakeSynthesisModel());
        SynthesizedSpeech[] segments =
        [
            new SynthesizedSpeech([], engine.SampleRate, TimeSpan.Zero, TimeSpan.FromMilliseconds(100)),
            new SynthesizedSpeech([0.1f, 0.2f], engine.SampleRate, TimeSpan.Zero, TimeSpan.Zero)
        ];

        // Act
        await synthesizer.PlayStreamAsync(ToAsyncEnumerable(segments), TestContext.Current.CancellationToken);

        // Assert: the device lifecycle and write order are exactly as expected
        Received.InOrder(() =>
        {
            playbackDevice.Start();
            playbackDevice.Write(Arg.Any<IReadOnlyList<float>>());
            playbackDevice.Write(Arg.Any<IReadOnlyList<float>>());
            playbackDevice.Stop();
        });
    }

    /// <summary>
    ///     Proves that a playback device fault while writing propagates to the caller, and that
    ///     the device is still stopped in the guaranteeing <c>finally</c> block.
    /// </summary>
    [Fact]
    public async Task PlayStreamAsync_PlaybackDeviceWriteThrows_PropagatesAndStillStopsDevice()
    {
        // Arrange
        var engine = new FakeSynthesisEngine();
        var playbackDevice = CreateAvailablePlaybackDevice();
        playbackDevice
            .When(device => device.Write(Arg.Any<IReadOnlyList<float>>()))
            .Do(_ => throw new AudioDeviceUnavailableException("speakers disconnected"));
        using var synthesizer = new SherpaOnnxSpeechSynthesizer(engine, playbackDevice, new FakeSynthesisModel());
        SynthesizedSpeech[] segments = [new SynthesizedSpeech([0.1f], engine.SampleRate, TimeSpan.Zero, TimeSpan.Zero)];

        // Act & Assert: the fault propagates, but the device is still stopped
        await Assert.ThrowsAsync<AudioDeviceUnavailableException>(
            () => synthesizer.PlayStreamAsync(ToAsyncEnumerable(segments), TestContext.Current.CancellationToken));
        playbackDevice.Received(1).Stop();
    }

    /// <summary>
    ///     Proves that a playback device that stops working (reported unavailable) surfaces
    ///     honestly rather than hanging or crashing.
    /// </summary>
    [Fact]
    public async Task PlayStreamAsync_PlaybackDeviceUnavailable_ThrowsRatherThanHanging()
    {
        // Arrange: an unavailable playback device, whose Start() throws per its contract
        var engine = new FakeSynthesisEngine();
        using var synthesizer = new SherpaOnnxSpeechSynthesizer(
            engine, UnavailableAudioPlaybackDevice.Instance, new FakeSynthesisModel());
        SynthesizedSpeech[] segments = [new SynthesizedSpeech([0.1f], engine.SampleRate, TimeSpan.Zero, TimeSpan.Zero)];

        // Act & Assert
        await Assert.ThrowsAsync<AudioDeviceUnavailableException>(
            () => synthesizer.PlayStreamAsync(ToAsyncEnumerable(segments), TestContext.Current.CancellationToken));
    }

    /// <summary>
    ///     Proves that a playback device whose <c>Start()</c> itself throws still reaches the
    ///     <c>finally</c> block's <c>Stop()</c> teardown, rather than leaking whatever partial
    ///     resource the device acquired before faulting.
    /// </summary>
    [Fact]
    public async Task PlayStreamAsync_PlaybackDeviceStartThrows_StillCallsStop()
    {
        // Arrange: a substitute device whose Start() throws, matching an unavailable-device fault
        var engine = new FakeSynthesisEngine();
        var playbackDevice = CreateAvailablePlaybackDevice();
        playbackDevice
            .When(device => device.Start())
            .Do(_ => throw new AudioDeviceUnavailableException("speakers disconnected before start"));
        using var synthesizer = new SherpaOnnxSpeechSynthesizer(engine, playbackDevice, new FakeSynthesisModel());
        SynthesizedSpeech[] segments = [new SynthesizedSpeech([0.1f], engine.SampleRate, TimeSpan.Zero, TimeSpan.Zero)];

        // Act & Assert: the Start() fault propagates, but the finally block still calls Stop()
        await Assert.ThrowsAsync<AudioDeviceUnavailableException>(
            () => synthesizer.PlayStreamAsync(ToAsyncEnumerable(segments), TestContext.Current.CancellationToken));
        playbackDevice.Received(1).Stop();
    }

    /// <summary>
    ///     Proves that <see cref="ISpeechSynthesizer.Stop"/> cancels an in-flight
    ///     <see cref="ISpeechSynthesizer.SpeakAsync"/> session deterministically, ending its task
    ///     without hanging - and, critically, only after the producer's in-flight native-style
    ///     <c>Generate</c> call has genuinely returned, never orphaning it. This is the regression
    ///     coverage for the <c>AccessViolationException</c> crash: before the fix,
    ///     <c>SynthesizeStreamCore</c> could return control to its caller (and the caller could
    ///     then dispose the engine) while the producer task was still mid-way through a native
    ///     call on a background thread.
    /// </summary>
    /// <remarks>
    ///     A <c>Timeout</c> is set as a safety net: if the fix ever regressed to orphaning the
    ///     producer task (or to hanging indefinitely), this test would fail fast with a timeout
    ///     rather than hanging the whole test run forever.
    /// </remarks>
    [Fact(Timeout = 5000)]
    public async Task Stop_WhileSpeaking_CancelsInFlightSessionOnlyAfterInFlightGenerateReturns()
    {
        // Arrange: an engine that signals it has started, then blocks until the test explicitly
        // releases it - simulating a native call that keeps running for a little while after
        // cancellation is requested, exactly like the real sherpa-onnx binding, which has no
        // in-flight cancellation primitive of its own.
        using var generateStarted = new SemaphoreSlim(0, 1);
        using var generateRelease = new SemaphoreSlim(0, 1);
        var engine = new BlockingSynthesisEngine(generateStarted, generateRelease);
        var playbackDevice = CreateAvailablePlaybackDevice();
        using var synthesizer = new SherpaOnnxSpeechSynthesizer(engine, playbackDevice, new FakeSynthesisModel());

        // Act: start speaking, wait until synthesis has begun, then stop
        var speakTask = synthesizer.SpeakAsync("Hello world.", TestContext.Current.CancellationToken);
        await generateStarted.WaitAsync(TestContext.Current.CancellationToken);
        synthesizer.Stop();

        // Assert: the session must not be reported complete while the producer's Generate call
        // is still in flight - this is exactly the window in which the caller previously could
        // (and, per the bug report, did) dispose the engine out from under it.
        Assert.False(speakTask.IsCompleted);

        // Act: only now let the in-flight native-style call finish, as the real engine eventually
        // would on its own
        generateRelease.Release();

        // Assert: the session ends via cancellation rather than hanging or faulting some other way
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => speakTask);
        Assert.True(engine.GenerateReturned);
    }

    /// <summary>
    ///     Proves that <see cref="SherpaOnnxSpeechSynthesizer.SynthesizeStreamAsync"/> never
    ///     returns control to its caller while the producer's <c>Generate</c> call is still in
    ///     flight, even when cancellation - rather than <see cref="ISpeechSynthesizer.Stop"/> -
    ///     is what ends the enumeration, and that disposing the engine only once the enumeration
    ///     has genuinely finished is safe (never touches a still-executing call).
    /// </summary>
    [Fact(Timeout = 5000)]
    public async Task SynthesizeStreamAsync_CancelledMidGenerate_AwaitsProducerBeforeEnumerationCompletesAndDisposalIsSafe()
    {
        // Arrange
        using var generateStarted = new SemaphoreSlim(0, 1);
        using var generateRelease = new SemaphoreSlim(0, 1);
        var engine = new BlockingSynthesisEngine(generateStarted, generateRelease);
        var playbackDevice = CreateAvailablePlaybackDevice();
        using var synthesizer = new SherpaOnnxSpeechSynthesizer(engine, playbackDevice, new FakeSynthesisModel());
        using var cts = new CancellationTokenSource();

        // Act: begin enumerating on a background task, cancel once Generate has started but
        // before it returns
        var enumerationTask = Task.Run(async () =>
        {
            await foreach (var _ in synthesizer.SynthesizeStreamAsync("Hello world.", cts.Token))
            {
                // Draining is enough; no segment is expected to arrive before cancellation.
            }
        }, TestContext.Current.CancellationToken);
        await generateStarted.WaitAsync(TestContext.Current.CancellationToken);
        await cts.CancelAsync();

        // Assert: the enumeration must not complete while Generate is still in flight - the exact
        // window in which the previously-orphaned producer task could race a caller's disposal
        Assert.False(enumerationTask.IsCompleted);

        // Act: let the in-flight native-style call finish
        generateRelease.Release();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => enumerationTask);

        // Assert: Generate genuinely returned before the enumeration completed, and only one
        // Generate call was ever made - disposing now must be safe, proving the engine was never
        // touched again (and, in the real engine, never freed) while still executing
        Assert.True(engine.GenerateReturned);
        Assert.Equal(1, engine.GenerateCallCount);
        var disposeException = Record.Exception(synthesizer.Dispose);
        Assert.Null(disposeException);
    }

    /// <summary>
    ///     Proves that calling <see cref="ISpeechSynthesizer.Stop"/> with no session in flight is
    ///     a safe no-op.
    /// </summary>
    [Fact]
    public void Stop_NoSessionInFlight_IsNoOp()
    {
        // Arrange
        var engine = new FakeSynthesisEngine();
        var playbackDevice = CreateAvailablePlaybackDevice();
        using var synthesizer = new SherpaOnnxSpeechSynthesizer(engine, playbackDevice, new FakeSynthesisModel());

        // Act
        var exception = Record.Exception(synthesizer.Stop);

        // Assert
        Assert.Null(exception);
    }

    /// <summary>
    ///     Proves that disposal is idempotent and disposes the owned engine exactly once, even
    ///     when called more than once.
    /// </summary>
    [Fact]
    public void Dispose_CalledTwice_DisposesEngineOnce()
    {
        // Arrange
        var engine = new FakeSynthesisEngine();
        var playbackDevice = CreateAvailablePlaybackDevice();
        var synthesizer = new SherpaOnnxSpeechSynthesizer(engine, playbackDevice, new FakeSynthesisModel());

        // Act
        synthesizer.Dispose();
        synthesizer.Dispose();

        // Assert
        Assert.Equal(1, engine.DisposeCallCount);
    }

    /// <summary>
    ///     Proves that operating on a disposed synthesizer throws <see cref="ObjectDisposedException"/>
    ///     rather than silently doing nothing.
    /// </summary>
    [Fact]
    public void SynthesizeStreamAsync_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        var engine = new FakeSynthesisEngine();
        var playbackDevice = CreateAvailablePlaybackDevice();
        var synthesizer = new SherpaOnnxSpeechSynthesizer(engine, playbackDevice, new FakeSynthesisModel());
        synthesizer.Dispose();

        // Act & Assert
        Assert.Throws<ObjectDisposedException>(
            () => synthesizer.SynthesizeStreamAsync("hello", TestContext.Current.CancellationToken));
    }

    /// <summary>
    ///     Proves that <see cref="SherpaOnnxSpeechSynthesizer.IsAvailable"/> always reports
    ///     <see langword="true"/>, since this type is only ever constructed after successful
    ///     composition.
    /// </summary>
    [Fact]
    public void IsAvailable_Always_ReturnsTrue()
    {
        // Arrange
        var engine = new FakeSynthesisEngine();
        var playbackDevice = CreateAvailablePlaybackDevice();
        using var synthesizer = new SherpaOnnxSpeechSynthesizer(engine, playbackDevice, new FakeSynthesisModel());

        // Act & Assert
        Assert.True(synthesizer.IsAvailable);
    }

    /// <summary>
    ///     Proves that <see cref="ISpeechSynthesizer.PlayStreamAsync"/> genuinely waits for the
    ///     playback device to report a drained queue before stopping it, rather than treating
    ///     "every segment enqueued" as "finished playing" - the root cause of the TTS panel
    ///     cutting audio off almost instantly. Uses a controllable fake (an NSubstitute stub whose
    ///     <c>PendingSampleCount</c> getter signals a semaphore on every read) so the assertion
    ///     that the task has not yet completed is driven by an observed poll, not by an arbitrary
    ///     sleep racing the implementation.
    /// </summary>
    [Fact]
    public async Task PlayStreamAsync_PlaybackDeviceReportsPendingSamples_WaitsForDrainBeforeStopping()
    {
        // Arrange: a playback device that reports samples still pending until the test releases it
        var engine = new FakeSynthesisEngine();
        var playbackDevice = CreateAvailablePlaybackDevice();
        var pendingSampleCount = 1;
        using var polled = new SemaphoreSlim(0);
        playbackDevice.PendingSampleCount.Returns(_ =>
        {
            var value = Volatile.Read(ref pendingSampleCount);
            polled.Release();
            return value;
        });
        using var synthesizer = new SherpaOnnxSpeechSynthesizer(engine, playbackDevice, new FakeSynthesisModel());
        SynthesizedSpeech[] segments = [new SynthesizedSpeech([0.1f], engine.SampleRate, TimeSpan.Zero, TimeSpan.Zero)];

        // Act: start playback; every segment is enqueued almost immediately, but the fake device
        // keeps reporting pending samples until the test signals otherwise
        var playTask = synthesizer.PlayStreamAsync(ToAsyncEnumerable(segments), TestContext.Current.CancellationToken);

        // Wait until the drain wait has genuinely begun polling the device at least once
        await polled.WaitAsync(TestContext.Current.CancellationToken);

        // Assert: playback has not been treated as finished while samples are still reported pending
        Assert.False(playTask.IsCompleted);
        playbackDevice.DidNotReceive().Stop();

        // Act: signal the fake device has now genuinely drained
        Volatile.Write(ref pendingSampleCount, 0);
        await playTask;

        // Assert: only once the device reports a drained queue does playback stop
        playbackDevice.Received(1).Stop();
    }

    /// <summary>
    ///     Proves that <c>SherpaOnnxSpeechSynthesizer.GenerateSegment</c> (exercised
    ///     through <see cref="ISpeechSynthesizer.SynthesizeStreamAsync"/>) resolves the speaker id
    ///     passed to <c>ISynthesisEngine.Generate</c> from
    ///     <see cref="ISynthesisModel.ResolveSpeakerId"/> rather than a hard-coded <c>0</c>, for
    ///     both a default (no parameter bag supplied) case and a non-default (bag supplied) case.
    /// </summary>
    [Fact]
    public async Task SynthesizeStreamAsync_NoParameterValues_ResolvesDefaultSpeakerIdFromModel()
    {
        // Arrange: a model whose ResolveSpeakerId hook always returns a distinctive non-zero id
        var engine = new FakeSynthesisEngine();
        var playbackDevice = CreateAvailablePlaybackDevice();
        var model = new FakeSynthesisModel(resolveSpeakerId: _ => 7);
        using var synthesizer = new SherpaOnnxSpeechSynthesizer(engine, playbackDevice, model);

        // Act
        await CollectAsync(synthesizer.SynthesizeStreamAsync("Hello world.", TestContext.Current.CancellationToken));

        // Assert
        var call = Assert.Single(engine.GenerateCalls);
        Assert.Equal(7, call.SpeakerId);
    }

    /// <summary>
    ///     Proves that a supplied parameter value bag reaches
    ///     <see cref="ISynthesisModel.ResolveSpeakerId"/> and, through it, the speaker id passed
    ///     to <see cref="ISynthesisEngine.Generate"/>.
    /// </summary>
    [Fact]
    public async Task SynthesizeStreamAsync_ParameterValuesSupplied_ResolvesSpeakerIdFromBag()
    {
        // Arrange: a model whose ResolveSpeakerId hook echoes back a bag value
        var engine = new FakeSynthesisEngine();
        var playbackDevice = CreateAvailablePlaybackDevice();
        var model = new FakeSynthesisModel(
            resolveSpeakerId: values => values is not null && values.TryGetValue("voice", out var value) && value is int id ? id : 0);
        IReadOnlyDictionary<string, object> parameterValues = new Dictionary<string, object> { ["voice"] = 3 };
        using var synthesizer = new SherpaOnnxSpeechSynthesizer(engine, playbackDevice, model, parameterValues);

        // Act
        await CollectAsync(synthesizer.SynthesizeStreamAsync("Hello world.", TestContext.Current.CancellationToken));

        // Assert
        var call = Assert.Single(engine.GenerateCalls);
        Assert.Equal(3, call.SpeakerId);
    }

    /// <summary>
    ///     Proves that a segment's own Natural Language Audio Tag speed/volume overrides are
    ///     unaffected by, and coexist correctly with, a supplied parameter value bag driving
    ///     speaker-id resolution - the two mechanisms remain independent.
    /// </summary>
    [Fact]
    public async Task SynthesizeStreamAsync_ParameterValuesSuppliedAlongsideSpeedTag_BothMechanismsApplyIndependently()
    {
        // Arrange
        var engine = new FakeSynthesisEngine();
        var playbackDevice = CreateAvailablePlaybackDevice();
        var model = new FakeSynthesisModel(resolveSpeakerId: _ => 5);
        IReadOnlyDictionary<string, object> parameterValues = new Dictionary<string, object> { ["voice"] = 5 };
        using var synthesizer = new SherpaOnnxSpeechSynthesizer(engine, playbackDevice, model, parameterValues);

        // Act: "fast" is a natural language audio tag mapped to the model's own "tempo" parameter
        await CollectAsync(synthesizer.SynthesizeStreamAsync("[fast] Hello world.", TestContext.Current.CancellationToken));

        // Assert: the speaker id still resolves from the bag, and the segment's speed differs from 1.0
        var call = Assert.Single(engine.GenerateCalls);
        Assert.Equal(5, call.SpeakerId);
        Assert.NotEqual(1.0f, call.Speed);
    }

    /// <summary>
    ///     Proves that a long, multi-sentence input (well beyond the pending-segment channel's
    ///     capacity of 5) still produces every segment, in the exact order the sentences appear in
    ///     the source text, and that <see cref="ISynthesisEngine.Generate"/> is never called
    ///     concurrently with itself - the pipeline must remain strictly sequential even when the
    ///     look-ahead buffer lets synthesis run several chunks ahead of playback.
    /// </summary>
    [Fact]
    public async Task SynthesizeStreamAsync_LongMultiSentenceInput_ProducesOrderedSegmentsSequentially()
    {
        // Arrange: 12 short, uniquely numbered sentences - more than the 5-segment look-ahead
        // buffer, so the producer must genuinely block/refill rather than buffering everything.
        const int sentenceCount = 12;
        var sentences = Enumerable.Range(1, sentenceCount).Select(i => $"Sentence number {i}.");
        var text = string.Join(" ", sentences);
        var engine = new FakeSynthesisEngine(simulatedGenerateDelay: TimeSpan.FromMilliseconds(5));
        var playbackDevice = CreateAvailablePlaybackDevice();
        using var synthesizer = new SherpaOnnxSpeechSynthesizer(engine, playbackDevice, new FakeSynthesisModel());

        // Act
        var segments = await CollectAsync(
            synthesizer.SynthesizeStreamAsync(text, TestContext.Current.CancellationToken));

        // Assert: every sentence was synthesized exactly once, in source order, and every
        // resulting segment reflects that same order (audio length is proportional to text
        // length in the fake, so segment order can be cross-checked against call order)
        Assert.Equal(sentenceCount, engine.GenerateCalls.Count);
        for (var i = 0; i < sentenceCount; i++)
        {
            Assert.Contains($"number {i + 1}", engine.GenerateCalls[i].Text, StringComparison.Ordinal);
        }

        Assert.Equal(sentenceCount, segments.Count);
        for (var i = 0; i < sentenceCount; i++)
        {
            Assert.Equal(engine.GenerateCalls[i].Text.Length * 4, segments[i].Samples.Count);
        }

        // Assert: Generate was never entered concurrently with itself
        Assert.Equal(1, engine.MaxConcurrentGenerateCalls);
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

    /// <summary>Materializes an asynchronous sequence into a list.</summary>
    private static async Task<List<SynthesizedSpeech>> CollectAsync(IAsyncEnumerable<SynthesizedSpeech> stream)
    {
        var results = new List<SynthesizedSpeech>();
        await foreach (var item in stream)
        {
            results.Add(item);
        }

        return results;
    }

    /// <summary>Wraps an array of pre-built segments as an asynchronous sequence.</summary>
    private static async IAsyncEnumerable<SynthesizedSpeech> ToAsyncEnumerable(IEnumerable<SynthesizedSpeech> segments)
    {
        foreach (var segment in segments)
        {
            yield return segment;
        }

        await Task.CompletedTask;
    }

    /// <summary>
    ///     Test-only <see cref="ISynthesisEngine"/> whose <see cref="Generate"/> signals a
    ///     semaphore once called, then blocks until the test explicitly releases a second
    ///     semaphore - simulating a native call that keeps running for a while after the
    ///     session's cancellation token is cancelled, exactly like the real sherpa-onnx binding,
    ///     which has no in-flight cancellation primitive of its own and so cannot be interrupted
    ///     by <see cref="ISpeechSynthesizer.Stop"/> or an externally cancelled token.
    /// </summary>
    /// <remarks>
    ///     Deliberately does <em>not</em> unblock <see cref="Generate"/> from <see cref="Dispose"/>:
    ///     the whole point of the regression this fake supports is that
    ///     <see cref="SherpaOnnxSpeechSynthesizer"/> must never call <see cref="Dispose"/> (or let
    ///     a caller do so) while this call is still in flight, so a test relying on
    ///     <see cref="Dispose"/> to unblock it would either mask that exact bug or deadlock
    ///     against the fix. Callers must release <paramref name="generateRelease"/> explicitly to
    ///     let the in-flight call complete.
    /// </remarks>
    /// <param name="generateStarted">Released once <see cref="Generate"/> is called.</param>
    /// <param name="generateRelease">Awaited by <see cref="Generate"/> before it returns.</param>
    private sealed class BlockingSynthesisEngine(SemaphoreSlim generateStarted, SemaphoreSlim generateRelease) : ISynthesisEngine
    {
        public int SampleRate => 16000;

        /// <summary>Gets the number of <see cref="Generate"/> calls this engine has received.</summary>
        public int GenerateCallCount { get; private set; }

        /// <summary>Gets a value indicating whether the in-flight <see cref="Generate"/> call has genuinely returned.</summary>
        public bool GenerateReturned { get; private set; }

        public EngineAudio Generate(string text, float speed, int speakerId)
        {
            GenerateCallCount++;
            generateStarted.Release();

            // Blocks until the test explicitly allows this in-flight call to complete, rather
            // than observing any cancellation token - matching the real native call this fake
            // stands in for.
            generateRelease.Wait();

            GenerateReturned = true;
            return new EngineAudio([], SampleRate);
        }

        public void Dispose()
        {
            // Intentionally does not unblock Generate(): see remarks above.
        }
    }
}
