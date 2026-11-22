using System.Collections.Concurrent;
using DemaConsulting.Speech.Onnx.Kokoro.ModelManagementSubsystem;
using DemaConsulting.Speech.Onnx.Kokoro.SynthesisSubsystem;
using DemaConsulting.Speech.Onnx.Kokoro.Tests.ModelManagementSubsystem;
using DemaConsulting.Speech.SynthesisSubsystem;
using Microsoft.ML.OnnxRuntime;
using ProductionKokoroModel = DemaConsulting.Speech.Onnx.Kokoro.ModelManagementSubsystem.OnnxKokoroEnglishSynthesisModel;

namespace DemaConsulting.Speech.Onnx.Kokoro.Tests.SynthesisSubsystem;

/// <summary>
///     Behavior tests for <see cref="OnnxKokoroSynthesisEngine"/>: constructor argument validation,
///     the zero-token short-circuit, the disposed-engine guard, style-vector selection, the annotated
///     path and the split of text that exceeds the model's token limit, and
///     <see cref="OnnxKokoroSynthesisEngine.RunProbeInference"/>'s successful-probe path - all against a
///     tiny, hand-built ONNX test fixture (<c>TestData/fake-kokoro-model.onnx</c>) whose single node
///     forwards its <c>style</c> input straight through as the output, never the real ~88 MiB
///     production model.
/// </summary>
public sealed class OnnxKokoroSynthesisEngineTests
{
    private static readonly string FixtureModelPath = Path.Combine(AppContext.BaseDirectory, "TestData", "fake-kokoro-model.onnx");

    /// <summary>
    ///     Proves that the constructor throws <see cref="ArgumentNullException"/> for each
    ///     individually-null required parameter.
    /// </summary>
    [Fact]
    public void Constructor_NullSession_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new OnnxKokoroSynthesisEngine(
            null!,
            NewPipeline(),
            SingleVoiceStyles()));
    }

    /// <summary>
    ///     Proves that the constructor throws <see cref="ArgumentNullException"/> for a null
    ///     phoneme pipeline.
    /// </summary>
    [Fact]
    public void Constructor_NullPipeline_ThrowsArgumentNullException()
    {
        // Arrange
        using var session = new InferenceSession(FixtureModelPath);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new OnnxKokoroSynthesisEngine(
            session,
            null!,
            SingleVoiceStyles()));
    }

    /// <summary>
    ///     Proves that the constructor throws <see cref="ArgumentNullException"/> for a null
    ///     voice-styles dictionary.
    /// </summary>
    [Fact]
    public void Constructor_NullVoiceStyles_ThrowsArgumentNullException()
    {
        // Arrange
        using var session = new InferenceSession(FixtureModelPath);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new OnnxKokoroSynthesisEngine(
            session,
            NewPipeline(),
            null!));
    }

    /// <summary>
    ///     Proves that the constructor throws <see cref="ArgumentException"/> for an empty
    ///     voice-styles dictionary, since <see cref="OnnxKokoroSynthesisEngine"/> cannot select a
    ///     fallback voice with no voices supplied at all.
    /// </summary>
    [Fact]
    public void Constructor_EmptyVoiceStyles_ThrowsArgumentException()
    {
        // Arrange
        using var session = new InferenceSession(FixtureModelPath);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => new OnnxKokoroSynthesisEngine(
            session,
            NewPipeline(),
            new Dictionary<int, float[]>()));
    }

    /// <summary>
    ///     Proves that <see cref="ISynthesisBackend.SampleRate"/> is Kokoro's own fixed 24000 Hz
    ///     output rate.
    /// </summary>
    [Fact]
    public void SampleRate_IsAlways24000()
    {
        // Arrange
        using var session = new InferenceSession(FixtureModelPath);
        ISynthesisBackend engine = new OnnxKokoroSynthesisEngine(session, NewPipeline(), SingleVoiceStyles());

        // Act & Assert
        Assert.Equal(24000, engine.SampleRate);
    }

    /// <summary>
    ///     Proves that <see cref="OnnxKokoroSynthesisEngine.Generate"/> short-circuits to an empty
    ///     <see cref="BackendAudio"/>, never running the ONNX graph at all, when the text has nothing
    ///     to say (empty, or only a character the voice has no sound for).
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("\U0001F600")]
    public void SynthesisSubsystem_TextWithoutSounds_ReturnsEmptyAudio(string text)
    {
        // Arrange
        using var session = new InferenceSession(FixtureModelPath);
        using ISynthesisBackend engine = new OnnxKokoroSynthesisEngine(session, NewPipeline(), SingleVoiceStyles());

        // Act
        var audio = engine.Generate(text, 1.0f, speakerId: 0);

        // Assert
        Assert.Empty(audio.Samples);
        Assert.Equal(24000, audio.SampleRate);
    }

    /// <summary>
    ///     Proves that a word absent from the dictionary is still spoken (never silently dropped).
    /// </summary>
    [Fact]
    public void SynthesisSubsystem_UnknownWord_IsStillSpoken()
    {
        // Arrange
        using var session = new InferenceSession(FixtureModelPath);
        using ISynthesisBackend engine = new OnnxKokoroSynthesisEngine(session, NewPipeline(), SingleVoiceStyles());

        // Act
        var audio = engine.Generate("notarealenglishwordxyz", 1.0f, speakerId: 0);

        // Assert
        Assert.NotEmpty(audio.Samples);
    }

    /// <summary>
    ///     Proves that <see cref="OnnxKokoroSynthesisEngine.Generate"/> throws
    ///     <see cref="ArgumentNullException"/> for a null text argument, before attempting any
    ///     phonemization.
    /// </summary>
    [Fact]
    public void Generate_NullText_ThrowsArgumentNullException()
    {
        // Arrange
        using var session = new InferenceSession(FixtureModelPath);
        using ISynthesisBackend engine = new OnnxKokoroSynthesisEngine(session, NewPipeline(), SingleVoiceStyles());

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => engine.Generate(null!, 1.0f, speakerId: 0));
    }

    /// <summary>
    ///     Proves that <see cref="OnnxKokoroSynthesisEngine.Generate"/> throws
    ///     <see cref="ObjectDisposedException"/> once this engine has been disposed, rather than
    ///     running against a disposed native session.
    /// </summary>
    [Fact]
    public void Generate_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        var session = new InferenceSession(FixtureModelPath);
        ISynthesisBackend engine = new OnnxKokoroSynthesisEngine(session, NewPipeline(), SingleVoiceStyles());
        engine.Dispose();

        // Act & Assert
        Assert.Throws<ObjectDisposedException>(() => engine.Generate("cat", 1.0f, speakerId: 0));
    }

    /// <summary>
    ///     Proves that disposing this engine twice is a harmless no-op, matching
    ///     <see cref="IDisposable"/>'s standard idempotency contract.
    /// </summary>
    [Fact]
    public void Dispose_CalledTwice_DoesNotThrow()
    {
        // Arrange
        var session = new InferenceSession(FixtureModelPath);
        ISynthesisBackend engine = new OnnxKokoroSynthesisEngine(session, NewPipeline(), SingleVoiceStyles());

        // Act
        engine.Dispose();
        var exception = Record.Exception(engine.Dispose);

        // Assert
        Assert.Null(exception);
    }

    /// <summary>
    ///     Proves that <see cref="OnnxKokoroSynthesisEngine.RunProbeInference"/> runs its
    ///     representative inference successfully, without throwing, against a session that can
    ///     actually run the graph - the "candidate accepted" path
    ///     <see cref="Onnx.OnnxRuntimeSubsystem.OnnxExecutionProviderSelector.Create"/> relies on.
    /// </summary>
    [Fact]
    public void RunProbeInference_RunnableSession_DoesNotThrow()
    {
        // Arrange
        using var session = new InferenceSession(FixtureModelPath);

        // Act
        var exception = Record.Exception(() => OnnxKokoroSynthesisEngine.RunProbeInference(session));

        // Assert
        Assert.Null(exception);
    }

    /// <summary>
    ///     Proves that <see cref="OnnxKokoroSynthesisEngine.Generate"/> selects the style-vector row
    ///     indexed by the utterance's phoneme-token count. The fixture model forwards the style
    ///     input as its output, and each synthetic row is filled with its own row index.
    /// </summary>
    [Fact]
    public void Generate_MultiRowVoice_SelectsRowByTokenCount()
    {
        // Arrange
        var pipeline = NewPipeline();
        var tokenCount = Assert.Single(pipeline.ToTokenIds("cat", null)).Count;
        using var session = new InferenceSession(FixtureModelPath);
        using ISynthesisBackend engine = new OnnxKokoroSynthesisEngine(
            session,
            pipeline,
            new Dictionary<int, float[]> { [0] = BuildRowIndexedVoice(rowCount: tokenCount + 3) });

        // Act
        var audio = engine.Generate("cat", 1.0f, speakerId: 0);

        // Assert
        Assert.All(audio.Samples, sample => Assert.Equal((float)tokenCount, sample));
    }

    /// <summary>
    ///     Proves that <see cref="OnnxKokoroSynthesisEngine.Generate"/> clamps to the voice's last
    ///     style-vector row when the utterance is longer than the voice's row count.
    /// </summary>
    [Fact]
    public void Generate_ShortVoice_ClampsToLastRow()
    {
        // Arrange
        var pipeline = NewPipeline();
        var tokenCount = Assert.Single(pipeline.ToTokenIds("cat", null)).Count;
        using var session = new InferenceSession(FixtureModelPath);
        using ISynthesisBackend engine = new OnnxKokoroSynthesisEngine(
            session,
            pipeline,
            new Dictionary<int, float[]> { [0] = BuildRowIndexedVoice(rowCount: tokenCount) });

        // Act
        var audio = engine.Generate("cat", 1.0f, speakerId: 0);

        // Assert
        Assert.All(audio.Samples, sample => Assert.Equal((float)(tokenCount - 1), sample));
    }

    /// <summary>
    ///     Proves that <see cref="OnnxKokoroSynthesisEngine.Generate"/> falls back to the first
    ///     available voice, rather than throwing, for an unrecognized speaker id.
    /// </summary>
    [Fact]
    public void Generate_UnknownSpeakerId_FallsBackToFirstVoice()
    {
        // Arrange
        using var session = new InferenceSession(FixtureModelPath);
        using ISynthesisBackend engine = new OnnxKokoroSynthesisEngine(
            session,
            NewPipeline(),
            new Dictionary<int, float[]> { [3] = Enumerable.Repeat(7.0f, 256).ToArray() });

        // Act
        var audio = engine.Generate("cat", 1.0f, speakerId: 99);

        // Assert
        Assert.NotEmpty(audio.Samples);
        Assert.All(audio.Samples, sample => Assert.Equal(7.0f, sample));
    }

    /// <summary>
    ///     Proves that the annotated path speaks the text with the annotations: a replacement annotation changes
    ///     what is phonemized, so the style row chosen by the token count differs from the plain call.
    /// </summary>
    [Fact]
    public void SynthesisSubsystem_AnnotatedReplacement_IsSpokenAsReplaced()
    {
        // Arrange
        using var session = new InferenceSession(FixtureModelPath);
        using var annotatedEngine = new OnnxKokoroSynthesisEngine(
            session,
            NewPipeline(),
            new Dictionary<int, float[]> { [0] = BuildRowIndexedVoice(rowCount: 400) });
        var annotations = new[] { new SegmentAnnotation(0, 4, "en.norm", "twenty twenty-six") };

        // Act
        var plain = annotatedEngine.Generate("2026", 1.0f, speakerId: 0);
        var annotated = annotatedEngine.GenerateAnnotated("2026", annotations, 1.0f, speakerId: 0);

        // Assert: the replacement is much longer than four digits spoken one by one
        Assert.NotEmpty(plain.Samples);
        Assert.NotEmpty(annotated.Samples);
        Assert.True(annotated.Samples[0] > plain.Samples[0], "The annotated text should phonemize to more tokens.");
    }

    /// <summary>
    ///     Proves that the annotated path speaks the reading the model's text analysis chose for a heteronym:
    ///     the adjective <c>separate</c> has fewer sounds than the verb reading the plain call uses, so the
    ///     style row chosen by the token count differs.
    /// </summary>
    [Fact]
    public void SynthesisSubsystem_AnnotatedHeteronym_IsSpokenWithTheChosenReading()
    {
        // Arrange
        const string text = "A separate room.";
        var analysis = new ProductionKokoroModel().TextAnalyzer!.Analyze(text, CancellationToken.None);
        using var session = new InferenceSession(FixtureModelPath);
        using var engine = new OnnxKokoroSynthesisEngine(
            session,
            NewPipeline(),
            new Dictionary<int, float[]> { [0] = BuildRowIndexedVoice(rowCount: 400) });

        // Act
        var plain = engine.Generate(text, 1.0f, speakerId: 0);
        var annotated = engine.GenerateAnnotated(text, analysis.Annotations, 1.0f, speakerId: 0);

        // Assert
        Assert.True(annotated.Samples[0] < plain.Samples[0], "The adjective reading has fewer sounds than the verb reading.");
    }

    /// <summary>
    ///     Proves that a segment that phonemizes to more tokens than the model accepts is spoken in parts rather
    ///     than truncated: every part is one run of the graph (the fixture returns one style row per run).
    /// </summary>
    [Fact]
    public void SynthesisSubsystem_TextBeyondTokenLimit_IsSpokenInSeveralParts()
    {
        // Arrange
        using var session = new InferenceSession(FixtureModelPath);
        using ISynthesisBackend engine = new OnnxKokoroSynthesisEngine(session, NewPipeline(), SingleVoiceStyles());
        var text = string.Join(' ', Enumerable.Repeat("The cat sat on the mat, and the dog barked.", 40));

        // Act
        var audio = engine.Generate(text, 1.0f, speakerId: 0);

        // Assert
        Assert.Equal(0, audio.Samples.Length % 256);
        Assert.True(audio.Samples.Length / 256 >= 3, "About 1,000 tokens need at least three runs of the graph.");
    }

    /// <summary>
    ///     Proves that disposing the engine releases its ONNX Runtime session, so the session can
    ///     no longer be used.
    /// </summary>
    [Fact]
    public void Dispose_ReleasesSession()
    {
        // Arrange
        var session = new InferenceSession(FixtureModelPath);
        ISynthesisBackend engine = new OnnxKokoroSynthesisEngine(session, NewPipeline(), SingleVoiceStyles());

        // Act
        engine.Dispose();

        // Assert
        Assert.ThrowsAny<Exception>(() => OnnxKokoroSynthesisEngine.RunProbeInference(session));
    }

    /// <summary>
    ///     Proves a disposal that starts while a synthesis is running waits for it: the running synthesis still returns
    ///     its audio, a synthesis started after the disposal began is refused, and the session is released only after the
    ///     running synthesis has finished, exactly once even though two callers disposed concurrently.
    /// </summary>
    [Fact]
    public async Task Dispose_WhileSynthesisRuns_WaitsForTheRunAndReleasesTheSessionOnce()
    {
        // Arrange: a synthesis held inside the engine, so the overlap with the disposals is deterministic
        var session = new DisposalTrackingInferenceSession(FixtureModelPath);
        var engine = new OnnxKokoroSynthesisEngine(session, NewPipeline(), SingleVoiceStyles());
        var gate = new BlockingAnnotationList();
        var running = Task.Run(() => engine.GenerateAnnotated("cat", gate, 1.0f, speakerId: 0), TestContext.Current.CancellationToken);
        Assert.True(gate.Entered.WaitOne(TimeSpan.FromSeconds(60)));

        // Act: two callers dispose while the synthesis is still running
        var firstDispose = Task.Run(engine.Dispose, TestContext.Current.CancellationToken);
        var secondDispose = Task.Run(engine.Dispose, TestContext.Current.CancellationToken);
        while (Record.Exception(() => engine.Generate("cat", 1.0f, speakerId: 0)) is not ObjectDisposedException)
        {
            Thread.Yield();
        }

        var releasedWhileRunning = session.DisposeCount;
        gate.Release();
        var audio = await running.WaitAsync(TimeSpan.FromSeconds(60), TestContext.Current.CancellationToken);
        await Task.WhenAll(new List<Task> { firstDispose, secondDispose }).WaitAsync(TimeSpan.FromSeconds(60), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(0, releasedWhileRunning);
        Assert.NotEmpty(audio.Samples);
        Assert.Equal(1, session.DisposeCount);
    }

    /// <summary>Proves concurrent disposals release the session exactly once and none of them throws.</summary>
    [Fact]
    public void Dispose_ConcurrentCallers_ReleaseTheSessionOnce()
    {
        // Arrange
        var session = new DisposalTrackingInferenceSession(FixtureModelPath);
        var engine = new OnnxKokoroSynthesisEngine(session, NewPipeline(), SingleVoiceStyles());

        // Act
        var exceptions = new ConcurrentBag<Exception>();
        Parallel.For(0, 16, _ =>
        {
            var exception = Record.Exception(engine.Dispose);
            if (exception is not null)
            {
                exceptions.Add(exception);
            }
        });

        // Assert
        Assert.Empty(exceptions);
        Assert.Equal(1, session.DisposeCount);
    }

    /// <summary>
    ///     Proves synthesis racing a disposal either completes with audio or is refused with
    ///     <see cref="ObjectDisposedException"/>, never anything else, and the session is released exactly once.
    /// </summary>
    [Fact]
    public async Task Generate_ConcurrentWithDispose_CompletesOrThrowsObjectDisposed()
    {
        // Arrange
        var session = new DisposalTrackingInferenceSession(FixtureModelPath);
        var engine = new OnnxKokoroSynthesisEngine(session, NewPipeline(), SingleVoiceStyles());
        using var start = new ManualResetEventSlim(false);
        var outcomes = new ConcurrentBag<object>();
        var workers = Enumerable.Range(0, 8).Select(_ => Task.Run(() =>
        {
            start.Wait();
            for (var i = 0; i < 25; i++)
            {
                try
                {
                    outcomes.Add(engine.Generate("cat", 1.0f, speakerId: 0).Samples.Length);
                }
                catch (Exception exception)
                {
                    outcomes.Add(exception);
                }
            }
        }, TestContext.Current.CancellationToken)).ToList();
        var disposer = Task.Run(() =>
        {
            start.Wait();
            engine.Dispose();
        }, TestContext.Current.CancellationToken);

        // Act
        start.Set();
        await Task.WhenAll(new List<Task>(workers) { disposer }).WaitAsync(TimeSpan.FromSeconds(120), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(200, outcomes.Count);
        Assert.All(outcomes, outcome =>
        {
            if (outcome is int length)
            {
                Assert.True(length > 0);
            }
            else
            {
                Assert.IsType<ObjectDisposedException>(outcome);
            }
        });
        Assert.Equal(1, session.DisposeCount);
    }

    /// <summary>Builds a voice of <paramref name="rowCount"/> 256-float rows, each filled with its own row index.</summary>
    private static float[] BuildRowIndexedVoice(int rowCount)
    {
        var voice = new float[rowCount * 256];
        for (var row = 0; row < rowCount; row++)
        {
            Array.Fill(voice, (float)row, row * 256, 256);
        }

        return voice;
    }

    /// <summary>Builds a single-voice style dictionary with one all-zero 256-float row.</summary>
    private static Dictionary<int, float[]> SingleVoiceStyles() => new() { [0] = new float[256] };

    /// <summary>Builds the shipped phoneme pipeline.</summary>
    private static KokoroPhonemePipeline NewPipeline() => new(new KokoroPhonemeVocabulary());

    /// <summary>
    ///     Resolves the real installed model directory using the same default store root
    ///     <see cref="ProductionKokoroModel"/>'s own download
    ///     descriptor installs to, without depending on <c>SpeechModelStore</c>'s non-test-only
    ///     construction path.
    /// </summary>
    private static string InstalledModelDirectory => Path.Join(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DemaConsulting.Speech",
        "Models",
        ProductionKokoroModel.ModelId,
        "current");

    /// <summary>
    ///     Whether the real, ~88 MiB production Kokoro model - all 30 vendor files of its bundle,
    ///     not merely a non-empty directory, since a partial/interrupted real install must still
    ///     skip rather than fail - is installed in this environment.
    /// </summary>
    private static bool IsRealModelInstalled => Directory.Exists(InstalledModelDirectory) &&
        ProductionKokoroModel.Bundles.SelectMany(bundle => bundle.Contents.Files).All(
            file => File.Exists(Path.Join(InstalledModelDirectory, file.RelativeInstallPath)));

    /// <summary>
    ///     Proves, end-to-end, that the real production Kokoro model (when installed in this
    ///     environment - see sibling test class
    ///     <c>DemaConsulting.Speech.Onnx.Kokoro.Tests.ModelManagementSubsystem.OnnxKokoroEnglishSynthesisModelTests</c>
    ///     for the equivalent always-run synthetic-fixture test) synthesizes non-empty, real
    ///     24000 Hz audio for known English text through
    ///     <see cref="ProductionKokoroModel.CreateBackend"/> and
    ///     <see cref="ISynthesisBackend.Generate"/> together, and that the two readings of a
    ///     heteronym sound different. Skipped (not failed) when the
    ///     real model is not installed, matching this repository's established Sherpa
    ///     real-model-skip convention.
    /// </summary>
    [Fact]
    public void Generate_RealInstalledModel_ProducesNonEmptyAudio()
    {
        // Arrange
        if (!IsRealModelInstalled)
        {
            Assert.Skip("The real production Kokoro ONNX model is not installed in this environment.");
        }

        var model = new ProductionKokoroModel();
        using var backend = model.CreateBackend(InstalledModelDirectory);

        // Act
        var audio = backend.Generate("The quick brown fox jumps over the lazy dog.", 1.0f, speakerId: 0);

        // Assert
        Assert.NotEmpty(audio.Samples);
        Assert.Equal(24000, audio.SampleRate);
        Assert.Contains(audio.Samples, sample => Math.Abs(sample) > 0.01f);
    }
}
