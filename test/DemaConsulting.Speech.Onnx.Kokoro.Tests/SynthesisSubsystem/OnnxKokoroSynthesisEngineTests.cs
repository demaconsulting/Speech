using DemaConsulting.Speech.Onnx.Kokoro.ModelManagementSubsystem;
using DemaConsulting.Speech.Onnx.Kokoro.SynthesisSubsystem;
using DemaConsulting.Speech.SynthesisSubsystem;
using Microsoft.ML.OnnxRuntime;
using ProductionKokoroModel = DemaConsulting.Speech.Onnx.Kokoro.ModelManagementSubsystem.OnnxKokoroEnglishSynthesisModel;

namespace DemaConsulting.Speech.Onnx.Kokoro.Tests.SynthesisSubsystem;

/// <summary>
///     Unit tests for <see cref="OnnxKokoroSynthesisEngine"/>, proving its constructor argument
///     validation, <see cref="ISynthesisBackend.Generate"/>'s zero-token short-circuit and
///     disposed-engine guard, and <see cref="OnnxKokoroSynthesisEngine.RunProbeInference"/>'s
///     successful-probe path - all against a tiny, hand-built ONNX test fixture
///     (<c>TestData/fake-kokoro-model.onnx</c>) whose single node forwards its <c>style</c> input
///     straight through as the output, never the real ~88 MiB production model.
/// </summary>
/// <remarks>
///     <c>OnnxKokoroEnglishSynthesisModelTests</c> already proves
///     <see cref="OnnxKokoroSynthesisEngine.Generate"/>'s voice-style-vector selection and
///     tensor-wiring end-to-end (via <c>CreateBackend</c>), so this class focuses on behavior
///     specific to this engine class itself, reachable only through its own internal constructor.
/// </remarks>
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
            new KokoroPhonemeVocabulary(),
            new KokoroLexiconPhonemizer(),
            SingleVoiceStyles()));
    }

    /// <summary>
    ///     Proves that the constructor throws <see cref="ArgumentNullException"/> for a null
    ///     vocabulary.
    /// </summary>
    [Fact]
    public void Constructor_NullVocabulary_ThrowsArgumentNullException()
    {
        // Arrange
        using var session = new InferenceSession(FixtureModelPath);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new OnnxKokoroSynthesisEngine(
            session,
            null!,
            new KokoroLexiconPhonemizer(),
            SingleVoiceStyles()));
    }

    /// <summary>
    ///     Proves that the constructor throws <see cref="ArgumentNullException"/> for a null
    ///     phonemizer.
    /// </summary>
    [Fact]
    public void Constructor_NullPhonemizer_ThrowsArgumentNullException()
    {
        // Arrange
        using var session = new InferenceSession(FixtureModelPath);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new OnnxKokoroSynthesisEngine(
            session,
            new KokoroPhonemeVocabulary(),
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
            new KokoroPhonemeVocabulary(),
            new KokoroLexiconPhonemizer(),
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
            new KokoroPhonemeVocabulary(),
            new KokoroLexiconPhonemizer(),
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
        ISynthesisBackend engine = new OnnxKokoroSynthesisEngine(
            session,
            new KokoroPhonemeVocabulary(),
            new KokoroLexiconPhonemizer(),
            SingleVoiceStyles());

        // Act & Assert
        Assert.Equal(24000, engine.SampleRate);
    }

    /// <summary>
    ///     Proves that <see cref="OnnxKokoroSynthesisEngine.Generate"/> short-circuits to an empty
    ///     <see cref="EngineAudio"/>, never running the ONNX graph at all, when every word of
    ///     <paramref name="text"/> is absent from the embedded lexicon and so produces zero
    ///     phoneme tokens.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("notarealenglishwordxyz")]
    public void Generate_ZeroTokenText_ReturnsEmptyAudio(string text)
    {
        // Arrange
        using var session = new InferenceSession(FixtureModelPath);
        using ISynthesisBackend engine = new OnnxKokoroSynthesisEngine(
            session,
            new KokoroPhonemeVocabulary(),
            new KokoroLexiconPhonemizer(),
            SingleVoiceStyles());

        // Act
        var audio = engine.Generate(text, 1.0f, speakerId: 0);

        // Assert
        Assert.Empty(audio.Samples);
        Assert.Equal(24000, audio.SampleRate);
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
        using ISynthesisBackend engine = new OnnxKokoroSynthesisEngine(
            session,
            new KokoroPhonemeVocabulary(),
            new KokoroLexiconPhonemizer(),
            SingleVoiceStyles());

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
        ISynthesisBackend engine = new OnnxKokoroSynthesisEngine(
            session,
            new KokoroPhonemeVocabulary(),
            new KokoroLexiconPhonemizer(),
            SingleVoiceStyles());
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
        ISynthesisBackend engine = new OnnxKokoroSynthesisEngine(
            session,
            new KokoroPhonemeVocabulary(),
            new KokoroLexiconPhonemizer(),
            SingleVoiceStyles());

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
    ///     indexed by the utterance's phoneme-token count, and clamps to the last row when the
    ///     utterance is longer than the voice's row count. The fixture model forwards the style
    ///     input as its output, and each synthetic row is filled with its own row index.
    /// </summary>
    [Fact]
    public void Generate_MultiRowVoice_SelectsRowByTokenCountAndClampsToLastRow()
    {
        // Arrange
        var vocabulary = new KokoroPhonemeVocabulary();
        var phonemizer = new KokoroLexiconPhonemizer();
        var tokenCount = vocabulary.ToTokenIds(phonemizer.Phonemize("cat").Phonemes).Count;
        Assert.True(tokenCount > 0);

        // Voice A has enough rows that the token count indexes a middle row; voice B has too few
        // rows, forcing the clamp to its last row.
        var wideVoice = BuildRowIndexedVoice(rowCount: tokenCount + 3);
        var narrowVoice = BuildRowIndexedVoice(rowCount: tokenCount);
        using var session = new InferenceSession(FixtureModelPath);
        using ISynthesisBackend engine = new OnnxKokoroSynthesisEngine(
            session,
            vocabulary,
            phonemizer,
            new Dictionary<int, float[]> { [0] = wideVoice, [1] = narrowVoice });

        // Act
        var wide = engine.Generate("cat", 1.0f, speakerId: 0);
        var narrow = engine.Generate("cat", 1.0f, speakerId: 1);

        // Assert
        Assert.All(wide.Samples, sample => Assert.Equal((float)tokenCount, sample));
        Assert.All(narrow.Samples, sample => Assert.Equal((float)(tokenCount - 1), sample));
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
    ///     Whether the real, ~88 MiB production Kokoro model - all 30 declared download files,
    ///     not merely a non-empty directory, since a partial/interrupted real install must still
    ///     skip rather than fail - is installed in this environment.
    /// </summary>
    private static bool IsRealModelInstalled => Directory.Exists(InstalledModelDirectory) &&
        new ProductionKokoroModel().DownloadDescriptor.Files.All(
            file => File.Exists(Path.Join(InstalledModelDirectory, file.RelativeInstallPath)));

    /// <summary>
    ///     Proves, end-to-end, that the real production Kokoro model (when installed in this
    ///     environment - see sibling test class
    ///     <c>DemaConsulting.Speech.Onnx.Kokoro.Tests.ModelManagementSubsystem.OnnxKokoroEnglishSynthesisModelTests</c>
    ///     for the equivalent always-run synthetic-fixture test) synthesizes non-empty, real
    ///     24000 Hz audio for known English text through
    ///     <see cref="ProductionKokoroModel.CreateBackend"/> and
    ///     <see cref="ISynthesisBackend.Generate"/> together. Skipped (not failed) when the
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
    }
}
