using System.Collections.Concurrent;
using DemaConsulting.Speech.Onnx.Piper.ModelManagementSubsystem;
using DemaConsulting.Speech.Onnx.Piper.SynthesisSubsystem;
using DemaConsulting.Speech.SynthesisSubsystem;
using Microsoft.ML.OnnxRuntime;

namespace DemaConsulting.Speech.Onnx.Piper.Tests.SynthesisSubsystem;

/// <summary>
///     Behavior tests for <see cref="OnnxPiperSynthesisEngine"/>. Most run against a tiny stand-in graph with the Piper
///     input contract whose output echoes what the engine fed it (<c>[ids, noise scale, length scale, noise width,
///     speaker, N]</c>), so the framing, the scales, the speed and the speaker are observed exactly; one test runs the
///     real ~78 MB voice and is skipped when the voice is not installed on the machine.
/// </summary>
public sealed class OnnxPiperSynthesisEngineTests : IDisposable
{
    private readonly string _root = Path.Join(Path.GetTempPath(), "DemaConsulting.Speech.Onnx.Piper.Tests", Guid.NewGuid().ToString("N"));
    private readonly string _echoModelPath;
    private readonly PiperVoiceConfig _config = PiperVoiceConfig.Parse(PiperTestFixtures.TrimmedConfigJson);

    /// <summary>Writes the stand-in graph to a scratch directory that no test shares.</summary>
    public OnnxPiperSynthesisEngineTests()
    {
        Directory.CreateDirectory(_root);
        _echoModelPath = Path.Combine(_root, "echo.onnx");
        File.WriteAllBytes(_echoModelPath, PiperTestFixtures.BuildEchoModel());
    }

    /// <summary>Deletes the scratch directory.</summary>
    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // Best-effort cleanup only.
        }
    }

    /// <summary>Proves null constructor arguments are rejected.</summary>
    [Fact]
    public void OnnxPiperSynthesisEngine_Constructor_NullArgument_Throws()
    {
        // Arrange
        using var session = new InferenceSession(_echoModelPath);
        var pipeline = new PiperPhonemePipeline(_config);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new OnnxPiperSynthesisEngine(null!, _config, pipeline));
        Assert.Throws<ArgumentNullException>(() => new OnnxPiperSynthesisEngine(session, null!, pipeline));
        Assert.Throws<ArgumentNullException>(() => new OnnxPiperSynthesisEngine(session, _config, null!));
    }

    /// <summary>
    ///     Proves the model is fed Piper's framing, the voice's own noise settings and the speaker: for a sentence of the
    ///     renderer's golden corpus the input ids are the begin id and the pad id, each pinned id followed by the pad id, and the end id.
    /// </summary>
    [Fact]
    public void OnnxPiperSynthesisEngine_Generate_Sentence_FeedsFramedIdsVoiceScalesAndSpeaker()
    {
        // Arrange
        var row = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "TestData", "piper-ipa-golden.tsv"))[1].Split('\t');
        var pinnedIds = row[3].Split(' ').Select(long.Parse).ToList();
        List<long> expectedIds = [1, 0];
        foreach (var id in pinnedIds)
        {
            expectedIds.Add(id);
            expectedIds.Add(0);
        }

        expectedIds.Add(2);
        using var engine = NewEngine();

        // Act
        var analysis = new OnnxPiperLibriTtsEnSynthesisModel().TextAnalyzer!.Analyze(row[1], CancellationToken.None);
        var echo = Decode(engine.GenerateAnnotated(row[1], analysis.Annotations, 1.0f, speakerId: 0));

        // Assert
        Assert.Equal(22050, engine.SampleRate);
        Assert.Equal(expectedIds, echo.Ids);
        Assert.Equal(expectedIds.Count, echo.Length);
        Assert.Equal([0.333f, 1.0f, 0.333f], echo.Scales);
        Assert.Equal(0, echo.Speaker);
    }

    /// <summary>Proves speed divides the voice's length scale (faster speech is a smaller length scale) and that a speed that is not a positive number is treated as normal speed.</summary>
    [Theory]
    [InlineData(2.0f, 0.5f)]
    [InlineData(0.5f, 2.0f)]
    [InlineData(1.0f, 1.0f)]
    [InlineData(0.0f, 1.0f)]
    [InlineData(-1.0f, 1.0f)]
    [InlineData(float.NaN, 1.0f)]
    [InlineData(float.PositiveInfinity, 1.0f)]
    public void OnnxPiperSynthesisEngine_Generate_Speed_ScalesTheLengthScale(float speed, float expectedLengthScale)
    {
        // Arrange
        using var engine = NewEngine();

        // Act
        var echo = Decode(engine.Generate("Hello there.", speed, speakerId: 0));

        // Assert
        Assert.Equal(expectedLengthScale, echo.Scales[1]);
        Assert.Equal(0.333f, echo.Scales[0]);
        Assert.Equal(0.333f, echo.Scales[2]);
    }

    /// <summary>Proves the speaker reaches the model and that an id outside the voice's speakers is brought into range.</summary>
    [Theory]
    [InlineData(0, 0)]
    [InlineData(5, 5)]
    [InlineData(903, 903)]
    [InlineData(5000, 903)]
    [InlineData(-3, 0)]
    public void OnnxPiperSynthesisEngine_Generate_Speaker_ReachesTheModelWithinRange(int speakerId, long expected)
    {
        // Arrange
        using var engine = NewEngine();

        // Act
        var echo = Decode(engine.Generate("Hello there.", 1.0f, speakerId));

        // Assert
        Assert.Equal(expected, echo.Speaker);
    }

    /// <summary>Proves text with nothing to say returns empty audio at the voice's rate without running the graph, and null text is rejected.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\U0001F600")]
    public void OnnxPiperSynthesisEngine_Generate_TextWithNothingToSay_ReturnsEmptyAudio(string text)
    {
        // Arrange
        using var engine = NewEngine();

        // Act
        var audio = engine.Generate(text, 1.0f, 0);

        // Assert
        Assert.Empty(audio.Samples);
        Assert.Equal(22050, audio.SampleRate);
        Assert.Throws<ArgumentNullException>(() => engine.Generate(null!, 1.0f, 0));
        Assert.Throws<ArgumentNullException>(() => engine.GenerateAnnotated(null!, [], 1.0f, 0));
        Assert.Throws<ArgumentNullException>(() => engine.GenerateAnnotated("x", null!, 1.0f, 0));
    }

    /// <summary>Proves the annotations of the text analysis reach the phonemes: a heteronym sentence run with its annotations feeds different ids than the same text run without.</summary>
    [Fact]
    public void OnnxPiperSynthesisEngine_GenerateAnnotated_HeteronymSentence_FeedsTheChosenReadings()
    {
        // Arrange
        const string sentence = "They plan to record the song, and then everyone will play the record.";
        var analysis = new OnnxPiperLibriTtsEnSynthesisModel().TextAnalyzer!.Analyze(sentence, CancellationToken.None);
        using var engine = NewEngine();

        // Act
        var plain = Decode(engine.Generate(sentence, 1.0f, 0));
        var annotated = Decode(engine.GenerateAnnotated(sentence, analysis.Annotations, 1.0f, 0));

        // Assert
        Assert.NotEqual(plain.Ids, annotated.Ids);
    }

    /// <summary>Proves a segment too long for one utterance is spoken in several runs of the graph and the audio is joined.</summary>
    [Fact]
    public void OnnxPiperSynthesisEngine_Generate_VeryLongText_IsSpokenInPartsAndJoined()
    {
        // Arrange
        var pipeline = new PiperPhonemePipeline(_config);
        var text = string.Join(' ', Enumerable.Repeat("The cat sat on the mat, and the dog barked.", 40));
        var expectedLength = pipeline.ToUtterances(text, null).Sum(utterance => utterance.Ids.Count + 5);
        using var engine = NewEngine();

        // Act
        var audio = engine.Generate(text, 1.0f, 0);

        // Assert
        Assert.True(pipeline.ToUtterances(text, null).Count >= 3);
        Assert.Equal(expectedLength, audio.Samples.Length);
    }

    /// <summary>Proves a symbol that the voice's table lacks is counted and listed rather than dropped silently, while the rest of the text is still spoken.</summary>
    [Fact]
    public void OnnxPiperSynthesisEngine_Generate_SymbolOutsideTheTable_IsCountedAndReported()
    {
        // Arrange
        var document = System.Text.Json.Nodes.JsonNode.Parse(PiperTestFixtures.TrimmedConfigJson)!;
        document["phoneme_id_map"]!.AsObject().Remove("ɑ");
        var config = PiperVoiceConfig.Parse(document.ToJsonString());
        using var session = new InferenceSession(_echoModelPath);
        using var engine = new OnnxPiperSynthesisEngine(session, config, new PiperPhonemePipeline(config));

        // Act
        var clean = engine.Generate("Hello there.", 1.0f, 0);
        var countBefore = engine.UnknownSymbolCount;
        var audio = engine.Generate("hot", 1.0f, 0);

        // Assert
        Assert.NotEmpty(clean.Samples);
        Assert.Equal(0, countBefore);
        Assert.NotEmpty(audio.Samples);
        Assert.Equal(1, engine.UnknownSymbolCount);
        Assert.Equal(["ɑ"], engine.UnknownSymbols);
    }

    /// <summary>Proves the provider probe runs the graph with a sentence-sized input and succeeds on a runnable session.</summary>
    [Fact]
    public void OnnxPiperSynthesisEngine_RunProbeInference_RunnableSession_DoesNotThrow()
    {
        // Arrange
        using var session = new InferenceSession(_echoModelPath);
        var pipeline = new PiperPhonemePipeline(_config);

        // Act
        var probeException = Record.Exception(() => OnnxPiperSynthesisEngine.RunProbeInference(session, _config, pipeline));

        // Assert
        Assert.Null(probeException);
    }

    /// <summary>Proves disposing the engine releases the session, is idempotent, and ends use.</summary>
    [Fact]
    public void OnnxPiperSynthesisEngine_Dispose_Engine_ReleasesTheSessionOnceAndEndsUse()
    {
        // Arrange
        var session = new DisposalTrackingInferenceSession(_echoModelPath);
        var engine = new OnnxPiperSynthesisEngine(session, _config, new PiperPhonemePipeline(_config));

        // Act
        engine.Dispose();
        var secondDispose = Record.Exception(engine.Dispose);

        // Assert
        Assert.Null(secondDispose);
        Assert.True(session.WasDisposed);
        Assert.Equal(1, session.DisposeCount);
        Assert.Throws<ObjectDisposedException>(() => engine.Generate("Hello.", 1.0f, 0));
    }

    /// <summary>
    ///     Proves a disposal that starts while a synthesis is running waits for it: the running synthesis still returns
    ///     its audio, a synthesis started after the disposal began is refused, and the session is released only after the
    ///     running synthesis has finished, exactly once even though two callers disposed concurrently.
    /// </summary>
    [Fact]
    public async Task OnnxPiperSynthesisEngine_Dispose_WhileSynthesisRuns_WaitsForTheRunAndReleasesTheSessionOnce()
    {
        // Arrange: a synthesis held inside the engine, so the overlap with the disposals is deterministic
        var session = new DisposalTrackingInferenceSession(_echoModelPath);
        var engine = new OnnxPiperSynthesisEngine(session, _config, new PiperPhonemePipeline(_config));
        var gate = new BlockingAnnotationList();
        var running = Task.Run(() => engine.GenerateAnnotated("Hello there.", gate, 1.0f, 0), TestContext.Current.CancellationToken);
        Assert.True(gate.Entered.WaitOne(TimeSpan.FromSeconds(60)));

        // Act: two callers dispose while the synthesis is still running
        var firstDispose = Task.Run(engine.Dispose, TestContext.Current.CancellationToken);
        var secondDispose = Task.Run(engine.Dispose, TestContext.Current.CancellationToken);
        while (Record.Exception(() => engine.Generate("Hello.", 1.0f, 0)) is not ObjectDisposedException)
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
    public void OnnxPiperSynthesisEngine_Dispose_ConcurrentCallers_ReleaseTheSessionOnce()
    {
        // Arrange
        var session = new DisposalTrackingInferenceSession(_echoModelPath);
        var engine = new OnnxPiperSynthesisEngine(session, _config, new PiperPhonemePipeline(_config));

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
    public async Task OnnxPiperSynthesisEngine_Generate_ConcurrentWithDispose_CompletesOrThrowsObjectDisposed()
    {
        // Arrange
        var session = new DisposalTrackingInferenceSession(_echoModelPath);
        var engine = new OnnxPiperSynthesisEngine(session, _config, new PiperPhonemePipeline(_config));
        using var start = new ManualResetEventSlim(false);
        var outcomes = new ConcurrentBag<object>();
        var workers = Enumerable.Range(0, 8).Select(_ => Task.Run(() =>
        {
            start.Wait();
            for (var i = 0; i < 25; i++)
            {
                try
                {
                    outcomes.Add(engine.Generate("Hello there.", 1.0f, 0).Samples.Length);
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

    /// <summary>
    ///     Proves, against the real installed voice (skipped when it is not installed), that the engine produces audible
    ///     audio at the voice's 22,050 Hz for plain and annotated text and for another speaker, that the phonemizer's
    ///     output never needs a symbol the voice lacks, and that a faster speed is shorter.
    /// </summary>
    [Fact]
    public void OnnxPiperSynthesisEngine_Generate_RealInstalledVoice_ProducesAudibleAudio()
    {
        // Arrange
        var directory = InstalledVoiceDirectory;
        if (!IsRealVoiceInstalled)
        {
            Assert.Skip("The real Piper LibriTTS-R voice is not installed in this environment.");
        }

        var model = new OnnxPiperLibriTtsEnSynthesisModel();
        using var backend = model.CreateBackend(directory);
        var engine = Assert.IsType<OnnxPiperSynthesisEngine>(backend);
        var sentence = "They plan to record the song, and then everyone will play the record.";
        var analysis = model.TextAnalyzer!.Analyze(sentence, CancellationToken.None);

        // Act
        var plain = engine.Generate(sentence, 1.0f, 0);
        var annotated = engine.GenerateAnnotated(sentence, analysis.Annotations, 1.0f, 0);
        var fast = engine.Generate(sentence, 2.0f, 0);
        var otherSpeaker = engine.Generate(sentence, 1.0f, 17);

        // Assert
        Assert.Equal(22050, plain.SampleRate);
        Assert.All(new[] { plain, annotated, fast, otherSpeaker }, audio =>
        {
            Assert.InRange(audio.Samples.Length, 22050, 22050 * 12);
            Assert.True(audio.Samples.Max(Math.Abs) > 0.05f, "The audio must not be silent.");
        });
        Assert.True(fast.Samples.Length < plain.Samples.Length * 0.85, "Double speed must be clearly shorter.");
        Assert.Equal(0, engine.UnknownSymbolCount);
    }

    /// <summary>Gets the directory the real voice is installed to by the model store.</summary>
    private static string InstalledVoiceDirectory => Path.Join(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DemaConsulting.Speech",
        "Models",
        OnnxPiperLibriTtsEnSynthesisModel.ModelId,
        "current");

    /// <summary>Gets a value indicating whether both files of the real voice are installed.</summary>
    private static bool IsRealVoiceInstalled =>
        File.Exists(Path.Join(InstalledVoiceDirectory, "en_US-libritts_r-medium.onnx")) &&
        File.Exists(Path.Join(InstalledVoiceDirectory, "en_US-libritts_r-medium.onnx.json"));

    /// <summary>Builds an engine over the stand-in graph with the trimmed real configuration.</summary>
    private OnnxPiperSynthesisEngine NewEngine() =>
        new(new InferenceSession(_echoModelPath), _config, new PiperPhonemePipeline(_config));

    /// <summary>Decodes the echo graph's output: <c>[ids (N), noise scale, length scale, noise width, speaker, N]</c>.</summary>
    private static (long[] Ids, float[] Scales, long Speaker, long Length) Decode(BackendAudio audio) =>
        PiperTestFixtures.DecodeEcho(audio);
}
