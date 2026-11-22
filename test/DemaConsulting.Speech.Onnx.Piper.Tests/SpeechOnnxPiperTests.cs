using System.Text;
using System.Text.Json.Nodes;
using DemaConsulting.Speech.ModelManagementSubsystem;
using DemaConsulting.Speech.Onnx.Piper.ModelManagementSubsystem;
using DemaConsulting.Speech.Onnx.Piper.SynthesisSubsystem;
using DemaConsulting.Speech.SynthesisSubsystem;

namespace DemaConsulting.Speech.Onnx.Piper.Tests;

/// <summary>
///     System-level tests of the whole Piper package as a host uses it: <c>SpeechModelCatalog.AddPiperModels()</c>, the
///     registered model, its <c>models</c>-release download and install, and the backend that <c>CreateBackend</c> returns,
///     over a tiny stand-in graph that echoes what it is fed (<c>[ids, noise scale, length scale, noise width, speaker,
///     N]</c>) and the trimmed copy of the real voice configuration; the real ~78 MB voice is never needed. Only the
///     report of a symbol that the voice's table lacks reads an internal member of the engine.
/// </summary>
public sealed class SpeechOnnxPiperTests : IDisposable
{
    private const string Sentence = "The cat sat on the mat, and the dog barked.";

    private readonly string _root = Path.Join(Path.GetTempPath(), "DemaConsulting.Speech.Onnx.Piper.Tests", Guid.NewGuid().ToString("N"));
    private readonly SpeechModelCatalog _catalog;
    private readonly ISynthesisModel _model;

    /// <summary>Registers the package's model in a catalog over a scratch store that no test shares.</summary>
    public SpeechOnnxPiperTests()
    {
        Directory.CreateDirectory(_root);
        _catalog = new SpeechModelCatalog(new SpeechModelStoreOptions { RootPathOverride = Path.Combine(_root, "store") });
        _model = Assert.IsAssignableFrom<ISynthesisModel>(Assert.Single(_catalog.AddPiperModels().Enumerate()).Model);
    }

    /// <summary>Disposes the catalog and deletes the scratch directory.</summary>
    public void Dispose()
    {
        _catalog.Dispose();
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // Best-effort cleanup only.
        }
    }

    /// <summary>Proves one call registers the one Piper synthesis model with its identity and license, returns the same catalog and rejects a null catalog.</summary>
    [Fact]
    public void SpeechOnnxPiper_AddPiperModels_RegistersTheVoiceAndChains()
    {
        // Arrange
        using var other = new SpeechModelCatalog(new SpeechModelStoreOptions { RootPathOverride = Path.Combine(_root, "other-store") });

        // Act
        var returned = other.AddPiperModels();
        var descriptor = Assert.Single(returned.Enumerate());

        // Assert
        Assert.Same(other, returned);
        Assert.Equal("piper-onnx-libritts-r-en-us-medium", descriptor.Id);
        Assert.Equal(SpeechModelRole.Synthesis, descriptor.Role);
        Assert.Equal("CC BY 4.0", descriptor.LicenseName);
        Assert.Equal("https://creativecommons.org/licenses/by/4.0/", descriptor.LicenseUrl?.ToString());
        Assert.Throws<ArgumentNullException>(() => ((SpeechModelCatalog)null!).AddPiperModels());
    }

    /// <summary>
    ///     Proves the voice is downloaded as one zip from the project's <c>models</c> release, pinned by its SHA-256, and
    ///     never from Hugging Face.
    /// </summary>
    [Fact]
    public void SpeechOnnxPiper_VoiceDownload_IsModelsReleaseBundlePinnedBySha256()
    {
        // Act
        var file = Assert.Single(_model.DownloadDescriptor.Files);

        // Assert
        Assert.Equal(
            "https://github.com/demaconsulting/Speech/releases/download/models/piper-en-us-libritts-r-medium-20261122-fp32-b1.zip",
            file.Uri.AbsoluteUri);
        Assert.Equal(PiperTestFixtures.BundleSha256, file.Sha256Checksum);
    }

    /// <summary>
    ///     Proves a downloaded bundle whose vendor bytes differ from the compiled pins fails the install, and that an
    ///     installed voice (graph and configuration at the installed root) loads and speaks.
    /// </summary>
    [Fact]
    public async Task SpeechOnnxPiper_InstallAndLoad_TamperedBundleIsRejectedAndInstalledVoiceSpeaks()
    {
        // Arrange
        var tampered = Path.Combine(_root, "tampered");
        var installed = Path.Combine(_root, "installed");
        PiperTestFixtures.StageBundleWithWrongFiles(Assert.Single(OnnxPiperLibriTtsEnSynthesisModel.Bundles), tampered);
        PiperTestFixtures.WriteInstalledEchoVoice(installed);

        // Act
        var installFailure = await Record.ExceptionAsync(() => _model.InstallAsync(tampered, TestContext.Current.CancellationToken));
        using var backend = _model.CreateBackend(installed);
        var echo = PiperTestFixtures.DecodeEcho(backend.Generate("Hello there.", 1.0f, 0));

        // Assert
        Assert.IsType<InvalidDataException>(installFailure);
        Assert.Equal(22050, backend.SampleRate);
        Assert.Equal(1, echo.Ids[0]);
        Assert.Equal(2, echo.Ids[^1]);
    }

    /// <summary>
    ///     Proves the speaker is a whole-number parameter from 0 to 903 (default 0) that the voice runs with, that a
    ///     value outside the voice runs speaker 0, that speed divides the voice's length scale, and that the voice has
    ///     no audio tags.
    /// </summary>
    [Fact]
    public void SpeechOnnxPiper_Synthesize_SpeakerAndSpeed_ReachTheVoice()
    {
        // Arrange
        PiperTestFixtures.WriteInstalledEchoVoice(_root);
        using var backend = _model.CreateBackend(_root);
        var parameter = Assert.IsType<NumericParameter>(Assert.Single(_model.Parameters));
        var selected = new Dictionary<string, object> { [parameter.Id] = 7.0 };
        var outside = new Dictionary<string, object> { [parameter.Id] = 904.0 };

        // Act
        var speakerEcho = PiperTestFixtures.DecodeEcho(backend.Generate("Hello there.", 1.0f, _model.ResolveSpeakerId(selected)));
        var outsideEcho = PiperTestFixtures.DecodeEcho(backend.Generate("Hello there.", 1.0f, _model.ResolveSpeakerId(outside)));
        var fastEcho = PiperTestFixtures.DecodeEcho(backend.Generate("Hello there.", 2.0f, 0));
        var slowEcho = PiperTestFixtures.DecodeEcho(backend.Generate("Hello there.", 0.5f, 0));
        var directEcho = PiperTestFixtures.DecodeEcho(backend.Generate("Hello there.", 1.0f, 5000));

        // Assert
        Assert.Equal((0, 903, 0, true), (parameter.Minimum, parameter.Maximum, parameter.Default, parameter.IsInteger));
        Assert.Equal(SpeechModelAudioTagSupport.None, _model.AudioTagSupport);
        Assert.Equal(7, speakerEcho.Speaker);
        Assert.Equal(0, outsideEcho.Speaker);
        Assert.Equal(903, directEcho.Speaker);
        Assert.Equal(0.5f, fastEcho.Scales[1]);
        Assert.Equal(2.0f, slowEcho.Scales[1]);
    }

    /// <summary>Proves the reading the text analysis chooses for a heteronym is the reading spoken: text spoken without annotations says <c>record</c> as the verb, so the verb sentence feeds the same ids with or without the analysis and the noun sentence feeds other ids.</summary>
    [Fact]
    public void SpeechOnnxPiper_Synthesize_HeteronymSentence_SpeaksTheChosenReading()
    {
        // Arrange
        const string verb = "They plan to record the song.";
        const string noun = "Here is the record.";
        PiperTestFixtures.WriteInstalledEchoVoice(_root);
        using var backend = Assert.IsAssignableFrom<IAnnotatedSynthesisBackend>(_model.CreateBackend(_root));

        // Act
        var verbPlain = PiperTestFixtures.DecodeEcho(backend.Generate(verb, 1.0f, 0));
        var verbAnalyzed = PiperTestFixtures.DecodeEcho(backend.GenerateAnnotated(verb, Analyze(verb), 1.0f, 0));
        var nounPlain = PiperTestFixtures.DecodeEcho(backend.Generate(noun, 1.0f, 0));
        var nounAnalyzed = PiperTestFixtures.DecodeEcho(backend.GenerateAnnotated(noun, Analyze(noun), 1.0f, 0));

        // Assert
        Assert.Equal(verbPlain.Ids, verbAnalyzed.Ids);
        Assert.NotEqual(nounPlain.Ids, nounAnalyzed.Ids);
    }

    /// <summary>Proves a word that is in no dictionary is still spoken.</summary>
    [Fact]
    public void SpeechOnnxPiper_Synthesize_UnknownWord_IsStillSpoken()
    {
        // Arrange
        PiperTestFixtures.WriteInstalledEchoVoice(_root);
        using var backend = _model.CreateBackend(_root);

        // Act
        var without = PiperTestFixtures.DecodeEcho(backend.Generate("Welcome", 1.0f, 0));
        var with = PiperTestFixtures.DecodeEcho(backend.Generate("Welcome Zorblax", 1.0f, 0));

        // Assert
        Assert.True(with.Ids.Length >= without.Ids.Length + 10, "The unknown word must add its own sounds.");
    }

    /// <summary>Proves a symbol that the voice's table lacks is counted and reported, while the rest of the text is still spoken.</summary>
    [Fact]
    public void SpeechOnnxPiper_Synthesize_VoiceWithoutASymbol_ReportsTheLossAndStillSpeaks()
    {
        // Arrange
        PiperTestFixtures.WriteInstalledEchoVoice(_root);
        var document = JsonNode.Parse(PiperTestFixtures.TrimmedConfigJson)!;
        document["phoneme_id_map"]!.AsObject().Remove("ɑ");
        File.WriteAllText(Path.Combine(_root, "en_US-libritts_r-medium.onnx.json"), document.ToJsonString(), Encoding.UTF8);
        using var backend = _model.CreateBackend(_root);
        var engine = Assert.IsType<OnnxPiperSynthesisEngine>(backend);

        // Act
        var audio = backend.Generate("hot", 1.0f, 0);

        // Assert
        Assert.NotEmpty(audio.Samples);
        Assert.Equal(1, engine.UnknownSymbolCount);
        Assert.Equal(["ɑ"], engine.UnknownSymbols);
    }

    /// <summary>Proves a long text is spoken whole (every sentence has its sounds in the joined audio) and a text with nothing to say gives empty audio.</summary>
    [Fact]
    public void SpeechOnnxPiper_Synthesize_LongTextAndSoundlessText_AreSpokenWholeOrEmpty()
    {
        // Arrange
        PiperTestFixtures.WriteInstalledEchoVoice(_root);
        using var backend = _model.CreateBackend(_root);
        var text = string.Join(' ', Enumerable.Repeat(Sentence, 40));

        // Act
        var single = PiperTestFixtures.DecodeEcho(backend.Generate(Sentence, 1.0f, 0));
        var whole = backend.Generate(text, 1.0f, 0);
        var silent = backend.Generate("\U0001F600", 1.0f, 0);

        // Assert: the ids of one sentence minus its three framing ids, forty times, with a tenth of slack for the cuts
        Assert.True(whole.Samples.Length >= 0.9 * 40 * (single.Ids.Length - 3), "No sentence may be lost.");
        Assert.Empty(silent.Samples);
        Assert.Equal(22050, silent.SampleRate);
    }

    /// <summary>Proves a voice that is not the 904-speaker, 22,050 Hz voice, or whose configuration is unusable, is rejected with an invalid-data error before any graph is loaded.</summary>
    [Fact]
    public void SpeechOnnxPiper_CreateBackend_VoiceOfAnotherShapeOrBrokenConfiguration_Fails()
    {
        // Arrange
        File.WriteAllBytes(Path.Combine(_root, "en_US-libritts_r-medium.onnx"), "this is not an onnx graph"u8.ToArray());
        var configPath = Path.Combine(_root, "en_US-libritts_r-medium.onnx.json");

        // Act & Assert
        File.WriteAllText(configPath, PiperTestFixtures.TrimmedConfigJson.Replace("\"num_speakers\": 904", "\"num_speakers\": 1", StringComparison.Ordinal));
        Assert.Throws<InvalidDataException>(() => _model.CreateBackend(_root));
        File.WriteAllText(configPath, PiperTestFixtures.TrimmedConfigJson.Replace("\"sample_rate\": 22050", "\"sample_rate\": 16000", StringComparison.Ordinal));
        Assert.Throws<InvalidDataException>(() => _model.CreateBackend(_root));
        File.WriteAllText(configPath, "{}");
        Assert.Throws<InvalidDataException>(() => _model.CreateBackend(_root));
    }

    /// <summary>Proves a preferred execution provider that is not available is skipped: the registered model runs on the CPU provider.</summary>
    [Fact]
    public void SpeechOnnxPiper_CreateBackend_UnavailablePreferredProvider_FallsBackToCpu()
    {
        // Arrange
        PiperTestFixtures.WriteInstalledEchoVoice(_root);
        using var catalog = new SpeechModelCatalog(new SpeechModelStoreOptions { RootPathOverride = Path.Combine(_root, "provider-store") });
        var model = Assert.IsAssignableFrom<ISynthesisModel>(Assert.Single(catalog.AddPiperModels(["NotARealExecutionProvider"]).Enumerate()).Model);

        // Act
        using var backend = model.CreateBackend(_root);
        var echo = PiperTestFixtures.DecodeEcho(backend.Generate("Hello there.", 1.0f, 0));

        // Assert
        Assert.Equal(22050, backend.SampleRate);
        Assert.Equal(1, echo.Ids[0]);
    }

    /// <summary>Proves disposing the backend more than once is harmless and a disposed backend refuses to synthesize.</summary>
    [Fact]
    public void SpeechOnnxPiper_Backend_Dispose_IsIdempotentAndEndsUse()
    {
        // Arrange
        PiperTestFixtures.WriteInstalledEchoVoice(_root);
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
