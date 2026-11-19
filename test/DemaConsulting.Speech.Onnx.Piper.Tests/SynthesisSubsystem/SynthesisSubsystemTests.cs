using System.Text.Json.Nodes;
using DemaConsulting.Speech.Onnx.Piper.ModelManagementSubsystem;
using DemaConsulting.Speech.Onnx.Piper.SynthesisSubsystem;
using DemaConsulting.Speech.SynthesisSubsystem;
using Microsoft.ML.OnnxRuntime;

namespace DemaConsulting.Speech.Onnx.Piper.Tests.SynthesisSubsystem;

/// <summary>
///     Subsystem-level tests of the Piper SynthesisSubsystem: the engine and the phoneme pipeline working together
///     with the real English text analysis of the model, the real phonemizer and renderer, and the real symbol table of
///     the voice (the trimmed copy of its configuration), over a stand-in graph that echoes what it is fed
///     (<c>[ids, noise scale, length scale, noise width, speaker, N]</c>), so what reaches the graph is observed exactly.
/// </summary>
public sealed class SynthesisSubsystemTests : IDisposable
{
    private readonly string _root = Path.Join(Path.GetTempPath(), "DemaConsulting.Speech.Onnx.Piper.Tests", Guid.NewGuid().ToString("N"));
    private readonly OnnxPiperLibriTtsEnSynthesisModel _model = new();

    /// <summary>Writes an installed stand-in voice to a scratch directory that no test shares.</summary>
    public SynthesisSubsystemTests()
    {
        PiperTestFixtures.WriteInstalledEchoVoice(_root);
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

    /// <summary>
    ///     Proves the reading the text analysis chooses for a heteronym is the reading spoken: without annotations the
    ///     engine says <c>record</c> as the verb, so the verb sentence feeds the same ids with or without the analysis,
    ///     and the noun sentence feeds other ids.
    /// </summary>
    [Fact]
    public void SynthesisSubsystem_Synthesize_HeteronymSentence_SpeaksTheChosenReading()
    {
        // Arrange
        const string verb = "They plan to record the song.";
        const string noun = "Here is the record.";
        using var backend = (IAnnotatedSynthesisBackend)_model.CreateBackend(_root);

        // Act
        var verbPlain = PiperTestFixtures.DecodeEcho(backend.Generate(verb, 1.0f, 0));
        var verbAnalyzed = PiperTestFixtures.DecodeEcho(backend.GenerateAnnotated(verb, Analyze(verb), 1.0f, 0));
        var nounPlain = PiperTestFixtures.DecodeEcho(backend.Generate(noun, 1.0f, 0));
        var nounAnalyzed = PiperTestFixtures.DecodeEcho(backend.GenerateAnnotated(noun, Analyze(noun), 1.0f, 0));

        // Assert
        Assert.Equal(verbPlain.Ids, verbAnalyzed.Ids);
        Assert.NotEqual(nounPlain.Ids, nounAnalyzed.Ids);
    }

    /// <summary>Proves a word that is in no dictionary is still spoken: it adds its own sounds to the ids the graph is fed.</summary>
    [Fact]
    public void SynthesisSubsystem_Synthesize_UnknownWord_IsStillSpoken()
    {
        // Arrange
        using var backend = _model.CreateBackend(_root);

        // Act
        var without = PiperTestFixtures.DecodeEcho(backend.Generate("Welcome", 1.0f, 0));
        var with = PiperTestFixtures.DecodeEcho(backend.Generate("Welcome Zorblax", 1.0f, 0));

        // Assert
        Assert.True(with.Ids.Length >= without.Ids.Length + 10, "The unknown word must add its own sounds.");
    }

    /// <summary>
    ///     Proves a symbol that the voice's table lacks is counted and listed by the engine rather than dropped silently,
    ///     while the rest of the text is still spoken.
    /// </summary>
    [Fact]
    public void SynthesisSubsystem_Synthesize_SymbolOutsideTheTable_IsCountedAndReportedWhileTheRestIsSpoken()
    {
        // Arrange
        var document = JsonNode.Parse(PiperTestFixtures.TrimmedConfigJson)!;
        document["phoneme_id_map"]!.AsObject().Remove("ɑ");
        var config = PiperVoiceConfig.Parse(document.ToJsonString());
        using var engine = new OnnxPiperSynthesisEngine(
            new InferenceSession(Path.Combine(_root, "en_US-libritts_r-medium.onnx")),
            config,
            new PiperPhonemePipeline(config));

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

    /// <summary>Proves the speed divides the voice's length scale and the speaker, brought into the voice's range, reach the graph.</summary>
    [Fact]
    public void SynthesisSubsystem_Synthesize_SpeedAndSpeaker_ReachTheGraph()
    {
        // Arrange
        using var backend = _model.CreateBackend(_root);

        // Act
        var normal = PiperTestFixtures.DecodeEcho(backend.Generate("Hello there.", 1.0f, 5));
        var fast = PiperTestFixtures.DecodeEcho(backend.Generate("Hello there.", 2.0f, 903));
        var beyond = PiperTestFixtures.DecodeEcho(backend.Generate("Hello there.", 1.0f, 5000));

        // Assert
        Assert.Equal(1.0f, normal.Scales[1]);
        Assert.Equal(5, normal.Speaker);
        Assert.Equal(0.5f, fast.Scales[1]);
        Assert.Equal(903, fast.Speaker);
        Assert.Equal(903, beyond.Speaker);
    }

    /// <summary>Proves a text too long for one utterance is spoken whole: every part runs the graph and the audio is joined.</summary>
    [Fact]
    public void SynthesisSubsystem_Synthesize_LongText_IsSpokenWholeAndJoined()
    {
        // Arrange
        var pipeline = new PiperPhonemePipeline(PiperVoiceConfig.Parse(PiperTestFixtures.TrimmedConfigJson));
        var text = string.Join(' ', Enumerable.Repeat("The cat sat on the mat, and the dog barked.", 40));
        var parts = pipeline.ToUtterances(text, null);
        using var backend = _model.CreateBackend(_root);

        // Act
        var audio = backend.Generate(text, 1.0f, 0);

        // Assert
        Assert.True(parts.Count >= 3, "The text must need several utterances.");
        Assert.Equal(parts.Sum(part => part.Ids.Count + 5), audio.Samples.Length);
    }

    /// <summary>Proves text with nothing to say returns empty audio at the voice's rate, without a run of the graph.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\U0001F600")]
    public void SynthesisSubsystem_Synthesize_SoundlessText_ReturnsEmptyAudio(string text)
    {
        // Arrange
        using var backend = _model.CreateBackend(_root);

        // Act
        var audio = backend.Generate(text, 1.0f, 0);

        // Assert
        Assert.Empty(audio.Samples);
        Assert.Equal(22050, audio.SampleRate);
    }

    /// <summary>Proves the probe inference that selects an execution provider runs the real pipeline's input through the graph and succeeds on a runnable session.</summary>
    [Fact]
    public void SynthesisSubsystem_Probe_RunnableSession_DoesNotThrow()
    {
        // Arrange
        var config = PiperVoiceConfig.Parse(PiperTestFixtures.TrimmedConfigJson);
        using var session = new InferenceSession(Path.Combine(_root, "en_US-libritts_r-medium.onnx"));

        // Act
        var exception = Record.Exception(() => OnnxPiperSynthesisEngine.RunProbeInference(session, config, new PiperPhonemePipeline(config)));

        // Assert
        Assert.Null(exception);
    }

    /// <summary>Proves disposing the backend more than once is harmless and that a disposed backend refuses to synthesize.</summary>
    [Fact]
    public void SynthesisSubsystem_Backend_Dispose_IsIdempotentAndEndsUse()
    {
        // Arrange
        var backend = _model.CreateBackend(_root);

        // Act
        backend.Dispose();
        var secondDispose = Record.Exception(backend.Dispose);

        // Assert
        Assert.Null(secondDispose);
        Assert.Throws<ObjectDisposedException>(() => backend.Generate("Hello.", 1.0f, 0));
    }

    /// <summary>Runs the model's English text analysis over a text and returns the annotations.</summary>
    private IReadOnlyList<SegmentAnnotation> Analyze(string text) =>
        _model.TextAnalyzer!.Analyze(text, CancellationToken.None).Annotations;
}
