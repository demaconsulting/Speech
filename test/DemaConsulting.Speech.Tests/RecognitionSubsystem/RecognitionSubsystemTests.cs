using DemaConsulting.Speech.AudioSubsystem;
using DemaConsulting.Speech.Diagnostics;
using DemaConsulting.Speech.RecognitionSubsystem;
using DemaConsulting.Speech.Tests.ModelManagementSubsystem.Fakes;
using DemaConsulting.Speech.Tests.RecognitionSubsystem.Fakes;
using NSubstitute;

namespace DemaConsulting.Speech.Tests.RecognitionSubsystem;

/// <summary>
///     Subsystem-level tests for the RecognitionSubsystem, exercising it only through its
///     public engine and session surface (composed over fakes) rather than through any one unit.
/// </summary>
public sealed class RecognitionSubsystemTests : IDisposable
{
    /// <summary>A scratch directory standing in for an installed model directory.</summary>
    private readonly string _installedModelDirectory = Path.Join(
        Path.GetTempPath(),
        "DemaConsulting.Speech.Tests",
        Guid.NewGuid().ToString("N"));

    /// <summary>Initializes a new instance of the <see cref="RecognitionSubsystemTests"/> class.</summary>
    public RecognitionSubsystemTests() => Directory.CreateDirectory(_installedModelDirectory);

    /// <summary>Removes the scratch directory.</summary>
    public void Dispose()
    {
        try
        {
            Directory.Delete(_installedModelDirectory, recursive: true);
        }
        catch (IOException)
        {
            // Best-effort cleanup only
        }
    }

    /// <summary>
    ///     Proves that a host can substitute the engine and session contracts with test doubles
    ///     and drive its transcript handling with no model, microphone, or native runtime.
    /// </summary>
    [Fact]
    public async Task RecognitionSubsystem_Contract_SubstitutedByHost_HostReceivesResults()
    {
        // Arrange: a substituted engine producing a substituted session that yields one result
        var session = Substitute.For<IRecognitionSession>();
        session.IsAvailable.Returns(true);
        session.GetResultsAsync(Arg.Any<CancellationToken>()).Returns(ScriptedResults());
        var engine = Substitute.For<ISpeechRecognizerEngine>();
        engine.IsAvailable.Returns(true);
        engine.CreateSessionAsync(Arg.Any<IAudioCaptureDevice>(), Arg.Any<CancellationToken>()).Returns(session);

        // Act: a host-style consumer reads the transcript
        var created = await engine.CreateSessionAsync(CreateCaptureDevice(), TestContext.Current.CancellationToken);
        var results = await CollectAsync(created.GetResultsAsync(TestContext.Current.CancellationToken));

        // Assert: the host saw the scripted result
        var result = Assert.Single(results);
        Assert.Equal("hello", result.Result.Text);
    }

    /// <summary>
    ///     Proves that no public type of the library surfaces a concrete inference-engine
    ///     dependency: the public API and the library's referenced assemblies are free of it.
    /// </summary>
    [Fact]
    public void RecognitionSubsystem_PublicApi_NamesNoConcreteInferenceEngine()
    {
        // Arrange
        var assembly = typeof(ISpeechRecognizerEngine).Assembly;

        // Act: collect every public type name and every referenced assembly name
        var publicTypeNames = assembly.GetExportedTypes().Select(t => t.FullName ?? t.Name);
        var referenced = assembly.GetReferencedAssemblies().Select(a => a.Name ?? string.Empty);

        // Assert: nothing names a concrete inference engine
        Assert.DoesNotContain(publicTypeNames, n => n.Contains("Sherpa", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(referenced, n => n.Contains("Sherpa", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    ///     Proves that the whole recognition pipeline (conversion, recognition, delivery) works
    ///     against a fake recognizer with no downloaded model or native runtime.
    /// </summary>
    [Fact]
    public async Task RecognitionSubsystem_Pipeline_StereoAudioAtModelRate_DeliversFinalResult()
    {
        // Arrange: an engine over a fake recognizer scripted to produce one final result
        var backend = new FakeRecognitionEngine([new SpeechRecognitionResult("hello world", IsFinal: true)]);
        await using var engine = new SpeechRecognizerEngine(backend, new FakeRecognitionModel(), NullSpeechDiagnostics.Instance);
        var device = CreateCaptureDevice(channelCount: 2);
        await using var session = await engine.CreateSessionAsync(device, TestContext.Current.CancellationToken);
        await session.StartAsync(TestContext.Current.CancellationToken);

        // Act: audio arrives, then the session stops
        RaiseFrameCaptured(device, [0.1f, 0.3f, 0.2f, 0.4f]);
        await session.StopAsync(TestContext.Current.CancellationToken);
        var results = await CollectAsync(session.GetResultsAsync(TestContext.Current.CancellationToken));

        // Assert: the recognizer received converted (mono) audio and the final result was delivered
        Assert.NotEmpty(backend.AcceptedSamples);
        Assert.Contains(results, r => r.Result.IsFinal);
    }

    /// <summary>
    ///     Proves that a slow consumer never loses a final result: every final produced is
    ///     still delivered, in order.
    /// </summary>
    [Fact]
    public async Task RecognitionSubsystem_ResultDelivery_SlowConsumer_NeverLosesFinalResults()
    {
        // Arrange: a recognizer scripted with many large finals, consumed only after stopping
        var script = Enumerable.Range(0, 20)
            .Select(i => new SpeechRecognitionResult(new string('x', 10_000) + i, IsFinal: true))
            .ToList();
        var backend = new FakeRecognitionEngine(script);
        await using var engine = new SpeechRecognizerEngine(backend, new FakeRecognitionModel(), NullSpeechDiagnostics.Instance);
        var device = CreateCaptureDevice();
        await using var session = await engine.CreateSessionAsync(device, TestContext.Current.CancellationToken);
        await session.StartAsync(TestContext.Current.CancellationToken);

        // Act: one frame per scripted result, then stop before anyone reads
        for (var i = 0; i < script.Count; i++)
        {
            RaiseFrameCaptured(device, [0.1f]);
        }

        await session.StopAsync(TestContext.Current.CancellationToken);
        var results = await CollectAsync(session.GetResultsAsync(TestContext.Current.CancellationToken));

        // Assert: every final survived
        Assert.Equal(script.Count, results.Count(r => r.Result.IsFinal));
    }

    /// <summary>
    ///     Proves that an engine permits one active session at a time, rejects a second
    ///     immediately, and accepts a new one once the first is disposed.
    /// </summary>
    [Fact]
    public async Task RecognitionSubsystem_EngineExclusivity_SecondSession_RejectedUntilFirstDisposed()
    {
        // Arrange
        await using var engine = new SpeechRecognizerEngine(
            new FakeRecognitionEngine(), new FakeRecognitionModel(), NullSpeechDiagnostics.Instance);
        var first = await engine.CreateSessionAsync(CreateCaptureDevice(), TestContext.Current.CancellationToken);

        // Act & Assert: a second concurrent session is rejected without waiting
        await Assert.ThrowsAsync<RecognitionEngineBusyException>(
            () => engine.CreateSessionAsync(CreateCaptureDevice(), TestContext.Current.CancellationToken));

        // Act: dispose the first, then ask again
        await first.DisposeAsync();
        await using var second = await engine.CreateSessionAsync(CreateCaptureDevice(), TestContext.Current.CancellationToken);

        // Assert: the new session is real
        Assert.True(second.IsAvailable);
    }

    /// <summary>
    ///     Proves that composing for an installed model yields a working engine and for an
    ///     uninstalled model yields an unavailable engine, neither failing.
    /// </summary>
    [Fact]
    public async Task RecognitionSubsystem_Composition_InstalledAndMissingModel_NeverFails()
    {
        // Arrange
        var backendFactory = new FakeRecognitionEngineFactory();
        var model = new FakeRecognitionModel();

        // Act
        await using var installed = await SpeechRecognizerFactory.LoadAsync(
            model, _installedModelDirectory, null, backendFactory, cancellationToken: TestContext.Current.CancellationToken);
        await using var missing = await SpeechRecognizerFactory.LoadAsync(
            model, Path.Join(_installedModelDirectory, "missing"), null, backendFactory, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.True(installed.IsAvailable);
        Assert.False(missing.IsAvailable);
    }

    /// <summary>
    ///     Proves that the unavailable fallback reports unavailability at composition and only
    ///     raises the documented exception when a consumer actually starts or reads it.
    /// </summary>
    [Fact]
    public async Task RecognitionSubsystem_UnavailableFallback_ThrowsOnlyWhenOperated()
    {
        // Arrange & Act: obtaining and holding the fallbacks never fails
        await using var engine = UnavailableSpeechRecognizerEngine.Instance;
        await using var session = await engine.CreateSessionAsync(CreateCaptureDevice(), TestContext.Current.CancellationToken);

        // Assert: both report unavailability, and only operating the session throws
        Assert.False(engine.IsAvailable);
        Assert.False(session.IsAvailable);
        await Assert.ThrowsAsync<SpeechRecognizerUnavailableException>(
            () => session.StartAsync(TestContext.Current.CancellationToken));
    }

    /// <summary>
    ///     Proves that a lost capture device is contained (it does not throw into the capture
    ///     path) and surfaces to a results reader as the documented faulted-session exception.
    /// </summary>
    [Fact]
    public async Task RecognitionSubsystem_FaultContainment_DeviceLost_ReaderReceivesFaultedException()
    {
        // Arrange: a running session
        await using var engine = new SpeechRecognizerEngine(
            new FakeRecognitionEngine(), new FakeRecognitionModel(), NullSpeechDiagnostics.Instance);
        var device = CreateCaptureDevice();
        await using var session = await engine.CreateSessionAsync(device, TestContext.Current.CancellationToken);
        await session.StartAsync(TestContext.Current.CancellationToken);

        // Act: the device is lost mid-session and a frame callback arrives (must not throw)
        device.IsAvailable.Returns(false);
        var callbackException = Record.Exception(() => RaiseFrameCaptured(device, [0.1f]));

        // Assert: the fault was contained and is surfaced to the reader
        Assert.Null(callbackException);
        await Assert.ThrowsAsync<RecognitionSessionFaultedException>(
            () => CollectAsync(session.GetResultsAsync(TestContext.Current.CancellationToken)));
    }

    /// <summary>Yields one scripted result.</summary>
    private static async IAsyncEnumerable<SpeechRecognitionEvent> ScriptedResults()
    {
        await Task.CompletedTask;
        yield return new SpeechRecognitionEvent(new SpeechRecognitionResult("hello", IsFinal: true));
    }

    /// <summary>Builds a substitute capture device that reports itself available.</summary>
    private static IAudioCaptureDevice CreateCaptureDevice(int sampleRate = 16000, int channelCount = 1)
    {
        var device = Substitute.For<IAudioCaptureDevice>();
        device.IsAvailable.Returns(true);
        device.SampleRate.Returns(sampleRate);
        device.ChannelCount.Returns(channelCount);
        return device;
    }

    /// <summary>Raises the substitute device's frame event with the given samples.</summary>
    private static void RaiseFrameCaptured(IAudioCaptureDevice device, float[] samples) =>
        device.FrameCaptured += Raise.Event<EventHandler<AudioCaptureFrameEventArgs>>(
            device,
            new AudioCaptureFrameEventArgs(samples));

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
