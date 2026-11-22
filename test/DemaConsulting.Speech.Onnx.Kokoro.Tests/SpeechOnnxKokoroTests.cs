using DemaConsulting.Speech.ModelManagementSubsystem;
using DemaConsulting.Speech.Onnx.Kokoro.ModelManagementSubsystem;
using DemaConsulting.Speech.SynthesisSubsystem;

namespace DemaConsulting.Speech.Onnx.Kokoro.Tests;

/// <summary>
///     System tests for the Kokoro package, through its public API only: the catalog registration
///     (<see cref="SpeechModelCatalogKokoroExtensions.AddKokoroModels"/>), the synthesis model loaded
///     into a speech synthesizer engine, and the speech the engine produces for text.
/// </summary>
/// <remarks>
///     The ONNX graph is the tiny hand-built fixture <c>TestData/fake-kokoro-model.onnx</c>, which returns
///     the style row it was given as its audio, and the installed voices are synthetic: every row of voice
///     <c>v</c> holds <c>v * 1000 + row</c>. Each run of the graph therefore returns 256 samples that all
///     equal the voice's marker plus the number of phoneme tokens of that run (the row index is the token
///     count), which makes the voice that spoke and how much was spoken directly observable. The phonemes
///     themselves are the real ones - real text analysis, real phonemizer, real renderer and vocabulary.
/// </remarks>
public sealed class SpeechOnnxKokoroTests : IClassFixture<SpeechOnnxKokoroTests.InstalledFakeModel>
{
    /// <summary>The length of the audio one run of the fixture graph returns (one style row).</summary>
    private const int SamplesPerRun = 256;

    /// <summary>The installed fixture model shared by the tests of this class.</summary>
    private readonly InstalledFakeModel _installed;

    /// <summary>Initializes a new instance of the <see cref="SpeechOnnxKokoroTests"/> class.</summary>
    /// <param name="installed">The installed fixture model.</param>
    public SpeechOnnxKokoroTests(InstalledFakeModel installed) => _installed = installed;

    /// <summary>
    ///     Proves that registering the Kokoro models gives the catalog (the same one, so registrations chain)
    ///     the English synthesis model, with a
    ///     voice choice of all declared voices and a default, so a host can offer the voices to its users.
    /// </summary>
    [Fact]
    public void SpeechOnnxKokoro_AddKokoroModels_OffersTheEnglishSynthesisModelWithItsVoices()
    {
        // Arrange
        using var catalog = new SpeechModelCatalog(new SpeechModelStoreOptions { RootPathOverride = _installed.Root });

        // Act
        var result = catalog.AddKokoroModels();
        var descriptors = result.Enumerate();

        // Assert
        Assert.Same(catalog, result);
        var descriptor = Assert.Single(descriptors);
        Assert.Equal(SpeechModelRole.Synthesis, descriptor.Role);
        var model = Assert.IsAssignableFrom<ISynthesisModel>(descriptor.Model);
        var voices = Assert.IsType<ChoiceParameter>(Assert.Single(model.Parameters));
        Assert.Equal(29, voices.Options.Count);
        Assert.Contains(voices.Default, voices.Options.Select(option => option.Value));
    }

    /// <summary>
    ///     Proves that the voice the caller selects is the voice that speaks, that the default voice speaks
    ///     when none is selected.
    /// </summary>
    [Fact]
    public async Task SpeechOnnxKokoro_Synthesize_SelectedVoice_SpeaksWithItsOwnStyle()
    {
        // Arrange
        var defaultVoice = _installed.VoiceIndex("af_heart");
        var selectedVoice = _installed.VoiceIndex("bm_george");

        // Act
        var byDefault = await SpeakAsync("The cat sat on the mat.", null);
        var selected = await SpeakAsync("The cat sat on the mat.", "bm_george");

        // Assert
        Assert.All(byDefault, run => Assert.Equal(defaultVoice, run.Voice));
        Assert.All(selected, run => Assert.Equal(selectedVoice, run.Voice));
        Assert.Equal(byDefault.Select(run => run.Tokens), selected.Select(run => run.Tokens));
    }

    /// <summary>
    ///     Proves that the engine speaks the reading the text analysis chose for a heteronym: the adjective
    ///     <c>separate</c> has fewer sounds than the verb the word is read as by default, so the sentence is
    ///     shorter than the same text spoken as written with every word in its default reading.
    /// </summary>
    [Fact]
    public async Task SpeechOnnxKokoro_Synthesize_HeteronymSentence_SpeaksTheChosenReadings()
    {
        // Arrange
        const string sentence = "A separate room.";
        using var backend = new OnnxKokoroEnglishSynthesisModel().CreateBackend(_installed.ModelDirectory);

        // Act
        var chosenReadings = await SpeakAsync(sentence, null);
        var defaultReadings = backend.Generate(sentence, 1.0f, speakerId: 0);

        // Assert
        var chosenTokens = Assert.Single(chosenReadings).Tokens;
        Assert.True(chosenTokens < defaultReadings.Samples[0], "The adjective reading has fewer sounds than the verb reading.");
    }

    /// <summary>
    ///     Proves that numbers and symbols are spoken as words, not as the characters they are written with:
    ///     an amount of money is spoken as its words, which is longer than the characters read as written.
    /// </summary>
    [Fact]
    public async Task SpeechOnnxKokoro_Synthesize_NumbersAndSymbols_AreSpokenAsWords()
    {
        // Arrange
        using var backend = new OnnxKokoroEnglishSynthesisModel().CreateBackend(_installed.ModelDirectory);

        // Act
        var spokenAsWords = Assert.Single(await SpeakAsync("It costs $5.", null)).Tokens;
        var asWritten = backend.Generate("It costs $5.", 1.0f, speakerId: 0).Samples[0];

        // Assert
        Assert.True(spokenAsWords > asWritten, "Five dollars spoken as words is longer than the symbol and the digit read as written.");
    }

    /// <summary>
    ///     Proves that a word absent from the dictionary is still spoken, with sounds worked out from its
    ///     spelling, rather than silently left out.
    /// </summary>
    [Fact]
    public async Task SpeechOnnxKokoro_Synthesize_WordAbsentFromDictionary_IsStillSpoken()
    {
        // Act
        var withoutWord = Assert.Single(await SpeakAsync("Hello there.", null)).Tokens;
        var withWord = Assert.Single(await SpeakAsync("Hello there Kathmandu.", null)).Tokens;

        // Assert
        Assert.True(withWord >= withoutWord + 6, "The name must add the sounds of its own.");
    }

    /// <summary>
    ///     Proves that a segment with more sounds than the model accepts in one run is spoken in parts and
    ///     nothing is cut off: the parts together carry at least as many sounds as the same text spoken in
    ///     small pieces would have.
    /// </summary>
    [Fact]
    public async Task SpeechOnnxKokoro_Synthesize_LongSegment_IsSpokenInPartsWithoutLoss()
    {
        // Arrange
        const string piece = "The cat sat on the mat and the dog barked at the moon";
        var pieceTokens = Assert.Single(await SpeakAsync(piece, null)).Tokens;
        var text = string.Join(' ', Enumerable.Repeat(piece, 40));

        // Act
        var runs = await SpeakAsync(text, null);

        // Assert
        Assert.True(runs.Count >= 3, "About 1,500 sounds need at least three runs of the graph.");
        Assert.All(runs, run => Assert.InRange(run.Tokens, 1, KokoroPhonemeVocabulary.MaxPhonemeTokens));
        Assert.True(runs.Sum(run => run.Tokens) >= 40 * pieceTokens - runs.Count, "No word may be cut off.");
    }

    /// <summary>
    ///     Loads the fixture model into a synthesizer engine, speaks <paramref name="text"/> and returns every run of
    ///     the graph, in order, with the voice and the number of sounds it carried.
    /// </summary>
    /// <param name="text">The text to speak.</param>
    /// <param name="voice">The selected voice, or <see langword="null"/> for the default voice.</param>
    /// <returns>One entry per run of the graph.</returns>
    private async Task<IReadOnlyList<Run>> SpeakAsync(string text, string? voice)
    {
        var parameterValues = voice is null
            ? null
            : new Dictionary<string, object> { [OnnxKokoroEnglishSynthesisModel.VoiceParameterId] = voice };
        await using var engine = await SpeechSynthesizerFactory.LoadAsync(
            new OnnxKokoroEnglishSynthesisModel(),
            _installed.ModelDirectory,
            parameterValues: parameterValues);
        Assert.True(engine.IsAvailable);

        var runs = new List<Run>();
        foreach (var speech in await engine.SynthesizeAsync(text))
        {
            Assert.Equal(0, speech.Samples.Count % SamplesPerRun);
            for (var offset = 0; offset < speech.Samples.Count; offset += SamplesPerRun)
            {
                var marker = (int)speech.Samples[offset];
                runs.Add(new Run(marker / 1000, marker % 1000));
            }
        }

        return runs;
    }

    /// <summary>One run of the fixture graph: the voice that spoke and the number of phoneme tokens it carried.</summary>
    /// <param name="Voice">The index of the voice, in declaration order.</param>
    /// <param name="Tokens">The number of phoneme tokens of the run.</param>
    private sealed record Run(int Voice, int Tokens);

    /// <summary>
    ///     A fixture model installed once into a scratch directory: the fixture graph and one synthetic voice
    ///     file for every voice the model declares.
    /// </summary>
    public sealed class InstalledFakeModel : IDisposable
    {
        /// <summary>One more row than the largest token count of a run, so every count has a row of its own.</summary>
        private const int RowsPerVoice = KokoroPhonemeVocabulary.MaxPhonemeTokens + 1;

        private readonly List<string> _voices = [];

        /// <summary>Initializes a new instance of the <see cref="InstalledFakeModel"/> class and installs the files.</summary>
        public InstalledFakeModel()
        {
            Root = Path.Join(Path.GetTempPath(), "DemaConsulting.Speech.Onnx.Kokoro.Tests", Guid.NewGuid().ToString("N"));
            ModelDirectory = Path.Join(Root, "model");
            var onnxDirectory = Path.Join(ModelDirectory, "onnx");
            var voicesDirectory = Path.Join(ModelDirectory, "voices");
            Directory.CreateDirectory(onnxDirectory);
            Directory.CreateDirectory(voicesDirectory);
            File.Copy(
                Path.Combine(AppContext.BaseDirectory, "TestData", "fake-kokoro-model.onnx"),
                Path.Join(onnxDirectory, "model_quantized.onnx"));

            // The declared voice files, in declaration (speaker id) order
            foreach (var file in OnnxKokoroEnglishSynthesisModel.Bundles.SelectMany(bundle => bundle.Contents.Files))
            {
                if (!file.RelativeInstallPath.StartsWith("voices/", StringComparison.Ordinal))
                {
                    continue;
                }

                var voiceIndex = _voices.Count;
                _voices.Add(Path.GetFileNameWithoutExtension(file.RelativeInstallPath));
                var style = new float[RowsPerVoice * SamplesPerRun];
                for (var row = 0; row < RowsPerVoice; row++)
                {
                    Array.Fill(style, (float)((voiceIndex * 1000) + row), row * SamplesPerRun, SamplesPerRun);
                }

                var bytes = new byte[style.Length * sizeof(float)];
                Buffer.BlockCopy(style, 0, bytes, 0, bytes.Length);
                File.WriteAllBytes(Path.Join(ModelDirectory, file.RelativeInstallPath), bytes);
            }
        }

        /// <summary>Gets the scratch root directory.</summary>
        public string Root { get; }

        /// <summary>Gets the directory the fixture model is installed to.</summary>
        public string ModelDirectory { get; }

        /// <summary>Returns the declaration-order index of a voice.</summary>
        /// <param name="voice">The voice value, for example <c>af_heart</c>.</param>
        /// <returns>The index of the voice.</returns>
        public int VoiceIndex(string voice) => _voices.IndexOf(voice);

        /// <inheritdoc/>
        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Root))
                {
                    Directory.Delete(Root, recursive: true);
                }
            }
            catch (IOException)
            {
                // Best-effort cleanup only; a leftover temp directory does not fail the test.
            }
        }
    }
}
