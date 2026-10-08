using DemaConsulting.Speech.ModelManagementSubsystem;
using DemaConsulting.Speech.Sherpa.ModelManagementSubsystem;
using DemaConsulting.Speech.SynthesisSubsystem;

namespace DemaConsulting.Speech.Sherpa.Tests.SynthesisSubsystem;

/// <summary>
///     Unit tests for <see cref="SherpaOnnxSynthesisEngine"/>, the real <see cref="ISynthesisBackend"/>
///     implementation wrapping sherpa-onnx's native offline text-to-speech synthesizer.
/// </summary>
/// <remarks>
///     <see cref="SherpaOnnxSynthesisEngine"/> wraps a native, un-fakeable sherpa-onnx
///     <c>OfflineTts</c> instance, so - per this project's testing standards for
///     native-runtime-dependent code, and mirroring <c>SherpaOnnxRecognitionEngineTests</c> -
///     these tests load the real, already installed VITS/Piper synthesis model and drive the
///     engine with real native inference rather than mocking the native surface. Tests are
///     skipped, not failed, when the model is not installed in this environment (for example a
///     fresh CI checkout that has not downloaded any model), since downloading a ~78 MiB
///     production model is outside the scope of a unit test run.
/// </remarks>
public sealed class SherpaOnnxSynthesisEngineTests
{
    /// <summary>The empirically proven output sample rate this model's loaded native engine reports.</summary>
    private const int ExpectedSampleRate = 22050;

    /// <summary>
    ///     Resolves the real installed model directory for the VITS/Piper model using the same
    ///     default store root <see cref="SpeechModelStore"/> uses, without depending on that
    ///     class's non-test-only construction path.
    /// </summary>
    private static string InstalledModelDirectory => Path.Join(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DemaConsulting.Speech",
        "Models",
        SherpaOnnxVitsLibriTtsEnglishSynthesisModel.ModelId,
        "current");

    /// <summary>Whether the real VITS/Piper model is installed in this environment.</summary>
    private static bool IsModelInstalled => Directory.Exists(InstalledModelDirectory) &&
        Directory.EnumerateFileSystemEntries(InstalledModelDirectory).Any();

    /// <summary>
    ///     Builds a real <see cref="SherpaOnnxSynthesisEngine"/> over the installed VITS/Piper
    ///     model, skipping the calling test if the model is not installed in this environment.
    /// </summary>
    private static SherpaOnnxSynthesisEngine CreateEngine()
    {
        if (!IsModelInstalled)
        {
            Assert.Skip("The real VITS/Piper synthesis model is not installed in this environment.");
        }

        var config = SherpaOnnxVitsLibriTtsEnglishSynthesisModel.BuildEngineConfig(InstalledModelDirectory);
        return new SherpaOnnxSynthesisEngine(config);
    }

    /// <summary>
    ///     Proves that constructing the engine from the installed model's configuration reports
    ///     the model's declared, empirically proven output sample rate.
    /// </summary>
    [Fact]
    public void SherpaOnnxSynthesisEngine_Construct_ReportsModelSampleRate()
    {
        // Arrange & Act
        using var engine = CreateEngine();

        // Assert
        Assert.Equal(ExpectedSampleRate, engine.SampleRate);
    }

    /// <summary>
    ///     Proves that synthesizing non-empty text produces non-empty, real audio at the engine's
    ///     declared sample rate.
    /// </summary>
    [Fact]
    public void SherpaOnnxSynthesisEngine_Generate_NonEmptyText_ReturnsNonEmptyAudio()
    {
        // Arrange
        using var engine = CreateEngine();

        // Act
        var audio = engine.Generate("This is a test of speech synthesis.", 1.0f, 0);

        // Assert
        Assert.NotEmpty(audio.Samples);
        Assert.Equal(engine.SampleRate, audio.SampleRate);
    }

    /// <summary>
    ///     Proves that a null text argument throws <see cref="ArgumentNullException"/> rather
    ///     than reaching the native synthesizer with an unusable value.
    /// </summary>
    [Fact]
    public void SherpaOnnxSynthesisEngine_Generate_NullText_ThrowsArgumentNullException()
    {
        // Arrange
        using var engine = CreateEngine();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => engine.Generate(null!, 1.0f, 0));
    }

    /// <summary>
    ///     Proves that calling <see cref="SherpaOnnxSynthesisEngine.Generate"/> after disposal
    ///     throws <see cref="ObjectDisposedException"/> rather than using a released native
    ///     engine.
    /// </summary>
    [Fact]
    public void SherpaOnnxSynthesisEngine_Generate_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        var engine = CreateEngine();
        engine.Dispose();

        // Act & Assert
        Assert.Throws<ObjectDisposedException>(() => engine.Generate("Hello.", 1.0f, 0));
    }

    /// <summary>
    ///     Proves that calling <see cref="SherpaOnnxSynthesisEngine.Dispose"/> more than once is
    ///     a safe no-op, never throwing on the second call.
    /// </summary>
    [Fact]
    public void SherpaOnnxSynthesisEngine_Dispose_CalledTwice_IsIdempotent()
    {
        // Arrange
        var engine = CreateEngine();

        // Act
        engine.Dispose();
        var exception = Record.Exception(engine.Dispose);

        // Assert
        Assert.Null(exception);
    }
}
