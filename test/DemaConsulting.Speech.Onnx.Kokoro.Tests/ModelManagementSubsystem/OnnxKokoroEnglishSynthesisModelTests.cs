using System.Reflection;
using DemaConsulting.Speech.AudioSubsystem;
using DemaConsulting.Speech.ModelManagementSubsystem;
using DemaConsulting.Speech.Onnx.Kokoro.ModelManagementSubsystem;
using DemaConsulting.Speech.Onnx.Kokoro.SynthesisSubsystem;
using DemaConsulting.Speech.SynthesisSubsystem;
using Microsoft.ML.OnnxRuntime;

namespace DemaConsulting.Speech.Onnx.Kokoro.Tests.ModelManagementSubsystem;

/// <summary>
///     Unit tests for <see cref="OnnxKokoroEnglishSynthesisModel"/>, proving its declared catalog
///     metadata, 30-file download descriptor shape, speaker-id resolution, and
///     <see cref="OnnxKokoroEnglishSynthesisModel.CreateBackend"/> wiring - the latter against a
///     tiny, hand-built ONNX test fixture and synthetic voice style-vector files, never the real
///     ~163 MiB production model/voices download.
/// </summary>
public sealed class OnnxKokoroEnglishSynthesisModelTests : IDisposable
{
    /// <summary>
    ///     This package's own declared voice subset, in <see cref="OnnxKokoroEnglishSynthesisModel.DownloadDescriptor"/>
    ///     /speaker-id order, reproduced here (not reflected out of the production class) so a
    ///     test failure clearly shows which declared order assumption broke.
    /// </summary>
    private static readonly string[] ExpectedVoiceOrder =
    [
        "af_heart", "af_alloy", "af_aoede", "af_bella", "af_jessica", "af_kore", "af_nicole",
        "af_nova", "af_river", "af_sarah", "af_sky", "af",
        "am_adam", "am_echo", "am_eric", "am_fenrir", "am_liam", "am_michael", "am_onyx",
        "am_puck", "am_santa",
        "bf_alice", "bf_emma", "bf_isabella", "bf_lily",
        "bm_daniel", "bm_fable", "bm_george", "bm_lewis",
    ];

    /// <summary>A scratch root directory unique to this test instance, cleaned up on dispose.</summary>
    private readonly string _testRoot;

    /// <summary>Initializes a fresh, unique scratch directory for each test.</summary>
    public OnnxKokoroEnglishSynthesisModelTests()
    {
        _testRoot = Path.Join(Path.GetTempPath(), "DemaConsulting.Speech.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testRoot);
    }

    /// <summary>Deletes the scratch directory tree created for this test instance.</summary>
    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testRoot))
            {
                Directory.Delete(_testRoot, recursive: true);
            }
        }
        catch (IOException)
        {
            // Best-effort cleanup only; a leftover temp directory does not fail the test.
        }
    }

    /// <summary>
    ///     Proves this model declares its expected, stable catalog identity, and its one
    ///     voice-selection <see cref="ChoiceParameter"/> with all 29 declared English voices.
    /// </summary>
    [Fact]
    public void OnnxKokoroEnglishSynthesisModel_Identity_DeclaresExpectedValues()
    {
        // Arrange
        var model = new OnnxKokoroEnglishSynthesisModel();

        // Act & Assert
        Assert.Equal("kokoro-onnx-v1_0-en", model.Id);
        Assert.Equal(OnnxKokoroEnglishSynthesisModel.ModelId, model.Id);
        Assert.Equal("Kokoro ONNX v1.0 English (fp16, 29 voices)", model.DisplayName);
        Assert.Equal(SpeechModelRole.Synthesis, model.Role);
        Assert.Equal(SpeechModelAudioTagSupport.None, model.AudioTagSupport);
        Assert.Equal("Apache-2.0", model.LicenseName);
        Assert.Equal(new Uri("https://www.apache.org/licenses/LICENSE-2.0"), model.LicenseUrl);

        var parameter = Assert.Single(model.Parameters);
        var choice = Assert.IsType<ChoiceParameter>(parameter);
        Assert.Equal(OnnxKokoroEnglishSynthesisModel.VoiceParameterId, choice.Id);
        Assert.Equal(29, choice.Options.Count);
        Assert.Equal("af_heart", choice.Default);
        Assert.Equal(ExpectedVoiceOrder, choice.Options.Select(option => option.Value));
    }

    /// <summary>
    ///     Proves that this model declares exactly 30 HTTPS download files (one ONNX model graph
    ///     plus 29 voice style-vector files), each with a well-formed 64-character hexadecimal
    ///     SHA-256 checksum and a unique relative install path, and that the model file and every
    ///     voice file land at the paths <see cref="OnnxKokoroEnglishSynthesisModel.CreateBackend"/>
    ///     resolves.
    /// </summary>
    [Fact]
    public void OnnxKokoroEnglishSynthesisModel_DownloadDescriptor_Declares30UniqueValidatedFiles()
    {
        // Arrange
        var model = new OnnxKokoroEnglishSynthesisModel();

        // Act
        var files = model.DownloadDescriptor.Files;

        // Assert
        Assert.Equal(30, files.Count);
        foreach (var file in files)
        {
            Assert.Equal("https", file.Uri.Scheme);
            Assert.Equal(64, file.Sha256Checksum.Length);
            Assert.True(file.Sha256Checksum.All(char.IsAsciiHexDigit));
        }

        Assert.Equal(files.Count, files.Select(file => file.RelativeInstallPath).Distinct(StringComparer.Ordinal).Count());
        Assert.Contains(files, file => file.RelativeInstallPath == "onnx/model_fp16.onnx");
        foreach (var voice in ExpectedVoiceOrder)
        {
            Assert.Contains(files, file => file.RelativeInstallPath == $"voices/{voice}.bin");
        }
    }

    /// <summary>
    ///     Proves that this model exposes its best-effort preferred mono playback format, matching
    ///     <see cref="OnnxKokoroSynthesisEngine.SampleRate"/>.
    /// </summary>
    [Fact]
    public void OnnxKokoroEnglishSynthesisModel_PreferredAudioFormat_IsMono24000()
    {
        // Arrange
        ISynthesisModel model = new OnnxKokoroEnglishSynthesisModel();

        // Act
        var preferredFormat = model.PreferredAudioFormat;

        // Assert
        Assert.Equal(new AudioFormat(24000, 1), preferredFormat);
    }

    /// <summary>
    ///     Proves that <see cref="ISynthesisModel.ResolveSpeakerId"/> resolves every one of the 29
    ///     declared voice values to its own <see cref="OnnxKokoroEnglishSynthesisModel"/>
    ///     declaration-order index.
    /// </summary>
    [Theory]
    [MemberData(nameof(VoiceIdTestCases))]
    public void OnnxKokoroEnglishSynthesisModel_ResolveSpeakerId_KnownVoice_ReturnsDeclarationOrderIndex(string voice, int expectedId)
    {
        // Arrange
        ISynthesisModel model = new OnnxKokoroEnglishSynthesisModel();
        IReadOnlyDictionary<string, object> parameterValues = new Dictionary<string, object>
        {
            [OnnxKokoroEnglishSynthesisModel.VoiceParameterId] = voice,
        };

        // Act
        var speakerId = model.ResolveSpeakerId(parameterValues);

        // Assert
        Assert.Equal(expectedId, speakerId);
    }

    /// <summary>Supplies every declared voice/expected-speaker-id pair for the theory above.</summary>
    public static TheoryData<string, int> VoiceIdTestCases()
    {
        TheoryData<string, int> testCases = [];
        for (var i = 0; i < ExpectedVoiceOrder.Length; i++)
        {
            testCases.Add(ExpectedVoiceOrder[i], i);
        }

        return testCases;
    }

    /// <summary>
    ///     Proves that an unrecognized voice value falls back to the default voice's speaker id
    ///     rather than throwing.
    /// </summary>
    [Fact]
    public void OnnxKokoroEnglishSynthesisModel_ResolveSpeakerId_UnknownVoice_ReturnsDefaultId()
    {
        // Arrange
        ISynthesisModel model = new OnnxKokoroEnglishSynthesisModel();
        IReadOnlyDictionary<string, object> parameterValues = new Dictionary<string, object>
        {
            [OnnxKokoroEnglishSynthesisModel.VoiceParameterId] = "not-a-real-voice",
        };

        // Act
        var speakerId = model.ResolveSpeakerId(parameterValues);

        // Assert: "af_heart" (id 0) is the declared default.
        Assert.Equal(0, speakerId);
    }

    /// <summary>
    ///     Proves that a <see langword="null"/> parameter value bag falls back to the default
    ///     voice's speaker id rather than throwing.
    /// </summary>
    [Fact]
    public void OnnxKokoroEnglishSynthesisModel_ResolveSpeakerId_NullBag_ReturnsDefaultId()
    {
        // Arrange
        ISynthesisModel model = new OnnxKokoroEnglishSynthesisModel();

        // Act
        var speakerId = model.ResolveSpeakerId(null);

        // Assert
        Assert.Equal(0, speakerId);
    }

    /// <summary>
    ///     Proves that a bag missing the declared voice key falls back to the default voice's
    ///     speaker id rather than throwing.
    /// </summary>
    [Fact]
    public void OnnxKokoroEnglishSynthesisModel_ResolveSpeakerId_MissingKey_ReturnsDefaultId()
    {
        // Arrange
        ISynthesisModel model = new OnnxKokoroEnglishSynthesisModel();
        IReadOnlyDictionary<string, object> parameterValues = new Dictionary<string, object>
        {
            ["some-other-parameter"] = "value",
        };

        // Act
        var speakerId = model.ResolveSpeakerId(parameterValues);

        // Assert
        Assert.Equal(0, speakerId);
    }

    /// <summary>
    ///     Proves that <see cref="OnnxKokoroEnglishSynthesisModel.CreateBackend"/> throws
    ///     <see cref="ArgumentException"/> for a null or empty installed directory, before
    ///     attempting to load any model or voice file.
    /// </summary>
    [Fact]
    public void OnnxKokoroEnglishSynthesisModel_CreateBackend_EmptyDirectory_ThrowsArgumentException()
    {
        // Arrange
        var model = new OnnxKokoroEnglishSynthesisModel();

        // Act & Assert
        Assert.Throws<ArgumentException>(() => model.CreateBackend(string.Empty));
    }

    /// <summary>
    ///     Proves that <see cref="OnnxKokoroEnglishSynthesisModel.CreateBackend"/> loads the ONNX
    ///     model graph (via <see cref="Onnx.OnnxRuntimeSubsystem.OnnxExecutionProviderSelector"/>,
    ///     probed with <see cref="OnnxKokoroSynthesisEngine.RunProbeInference"/>) and every
    ///     declared voice's style-vector file, and that the constructed engine's
    ///     <see cref="ISynthesisBackend.Generate"/> selects and forwards the requested voice's own
    ///     style vector - against a tiny, hand-built ONNX test fixture
    ///     (<c>TestData/fake-kokoro-model.onnx</c>) whose single node forwards its <c>style</c>
    ///     input straight through as the output, and synthetic per-voice <c>.bin</c> files filled
    ///     with that voice's own declaration-order index, never the real production model/voices.
    /// </summary>
    [Fact]
    public void OnnxKokoroEnglishSynthesisModel_CreateBackend_ValidModelAndVoices_GeneratesUsingSelectedVoiceStyle()
    {
        // Arrange
        var onnxDirectory = Path.Join(_testRoot, "onnx");
        Directory.CreateDirectory(onnxDirectory);
        var fixtureModelPath = Path.Combine(AppContext.BaseDirectory, "TestData", "fake-kokoro-model.onnx");
        File.Copy(fixtureModelPath, Path.Join(onnxDirectory, "model_fp16.onnx"));

        var voicesDirectory = Path.Join(_testRoot, "voices");
        Directory.CreateDirectory(voicesDirectory);
        for (var speakerId = 0; speakerId < ExpectedVoiceOrder.Length; speakerId++)
        {
            var styleRow = Enumerable.Repeat((float)speakerId, 256).ToArray();
            var bytes = new byte[styleRow.Length * sizeof(float)];
            Buffer.BlockCopy(styleRow, 0, bytes, 0, bytes.Length);
            File.WriteAllBytes(Path.Join(voicesDirectory, $"{ExpectedVoiceOrder[speakerId]}.bin"), bytes);
        }

        var model = new OnnxKokoroEnglishSynthesisModel();

        // Act
        using var backend = model.CreateBackend(_testRoot);
        var audio = backend.Generate("cat", 1.0f, speakerId: 5);

        // Assert: the fake model's single Identity(style) node forwards the requested voice's
        // (af_kore, declaration-order index 5) own style vector straight through as output.
        Assert.NotEmpty(audio.Samples);
        Assert.All(audio.Samples, sample => Assert.Equal(5.0f, sample));
        Assert.Equal(OnnxKokoroSynthesisEngine.SampleRate, audio.SampleRate);
    }

    /// <summary>
    ///     Proves that when a declared voice's style-vector file is missing - a failure that
    ///     occurs only after <see cref="OnnxKokoroEnglishSynthesisModel.CreateBackend"/> has
    ///     already created its ONNX <see cref="InferenceSession"/> - the method disposes that
    ///     session itself before rethrowing, rather than leaking the native session handle,
    ///     since no <see cref="OnnxKokoroSynthesisEngine"/> is ever constructed to take ownership
    ///     of it on this path.
    /// </summary>
    [Fact]
    public void OnnxKokoroEnglishSynthesisModel_CreateBackend_MissingVoiceFile_DisposesSessionAndThrows()
    {
        // Arrange: a valid ONNX model, but no voices directory at all, so reading the first
        // declared voice's style file fails.
        var onnxDirectory = Path.Join(_testRoot, "onnx");
        Directory.CreateDirectory(onnxDirectory);
        var fixtureModelPath = Path.Combine(AppContext.BaseDirectory, "TestData", "fake-kokoro-model.onnx");
        File.Copy(fixtureModelPath, Path.Join(onnxDirectory, "model_fp16.onnx"));

        var model = new OnnxKokoroEnglishSynthesisModel();

        InferenceSession? capturedSession = null;
        OnnxKokoroEnglishSynthesisModel.OnSessionCreated = session => capturedSession = session;
        try
        {
            // Act & Assert
            Assert.Throws<DirectoryNotFoundException>(() => model.CreateBackend(_testRoot));

            Assert.NotNull(capturedSession);
            Assert.True(IsDisposed(capturedSession));
        }
        finally
        {
            OnnxKokoroEnglishSynthesisModel.OnSessionCreated = null;
        }
    }

    /// <summary>
    ///     Reads <see cref="InferenceSession"/>'s own private <c>_disposed</c> field via
    ///     reflection, since the type exposes no public equivalent of <see cref="SessionOptions"/>'s
    ///     inherited <c>IsClosed</c> (it derives directly from <see cref="object"/>, not
    ///     <see cref="System.Runtime.InteropServices.SafeHandle"/>) - this is the only way to
    ///     observe disposal from outside the class under test.
    /// </summary>
    private static bool IsDisposed(InferenceSession session)
    {
        var field = typeof(InferenceSession).GetField("_disposed", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(field);
        return (bool)field.GetValue(session)!;
    }
}
