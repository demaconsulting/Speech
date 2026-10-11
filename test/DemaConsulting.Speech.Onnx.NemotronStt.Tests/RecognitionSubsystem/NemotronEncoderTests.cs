using DemaConsulting.Speech.Onnx.NemotronStt.RecognitionSubsystem;
using Microsoft.ML.OnnxRuntime;

namespace DemaConsulting.Speech.Onnx.NemotronStt.Tests.RecognitionSubsystem;

/// <summary>
///     Tests for <see cref="NemotronEncoder"/> against a tiny synthetic ONNX model that honors the
///     real encoder's tensor contract, covering the production input/output wiring and the cache
///     feedback without the real model files.
/// </summary>
public sealed class NemotronEncoderTests
{
    /// <summary>Frames in a real encoder input (pre-encode cache plus chunk).</summary>
    private const int Frames = NemotronEngineOptions.PreEncodeFrames + NemotronEngineOptions.ChunkFrames;

    /// <summary>The language id fed in these tests.</summary>
    private const int Language = 25;

    /// <summary>The expected output value after <paramref name="calls"/> calls.</summary>
    private static float Expected(int calls) => calls + (NemotronEncoderFixture.LanguageWeight * Language);

    /// <summary>Encodes one all-zero chunk.</summary>
    private static float[] Encode(NemotronEncoder encoder, out int outputFrames)
    {
        var span = encoder.Encode(new float[Frames * NemotronFeatureExtractor.MelBands], Frames, out outputFrames);
        return span.ToArray();
    }

    /// <summary>Proves the first call returns seven frames of 1024 values carrying the language id.</summary>
    [Fact]
    public void Encode_FirstChunk_ReturnsFramesWithLanguage()
    {
        using var encoder = new NemotronEncoder(new InferenceSession(NemotronEncoderFixture.Build()), Language);

        var output = Encode(encoder, out var frames);

        Assert.Equal(7, frames);
        Assert.Equal(7 * encoder.HiddenSize, output.Length);
        Assert.All(output, value => Assert.Equal(Expected(1), value));
    }

    /// <summary>Proves the cache outputs are fed back into the next call.</summary>
    [Fact]
    public void Encode_SecondChunk_UsesPreviousCaches()
    {
        using var encoder = new NemotronEncoder(new InferenceSession(NemotronEncoderFixture.Build()), Language);

        Encode(encoder, out _);
        var second = Encode(encoder, out var frames);
        var third = Encode(encoder, out _);

        Assert.Equal(7, frames);
        Assert.All(second, value => Assert.Equal(Expected(2), value));
        Assert.All(third, value => Assert.Equal(Expected(3), value));
    }

    /// <summary>Proves reset restores the zero caches.</summary>
    [Fact]
    public void Reset_AfterEncoding_RestartsCaches()
    {
        using var encoder = new NemotronEncoder(new InferenceSession(NemotronEncoderFixture.Build()), Language);
        Encode(encoder, out _);
        Encode(encoder, out _);

        encoder.Reset();
        var output = Encode(encoder, out _);

        Assert.All(output, value => Assert.Equal(Expected(1), value));
    }

    /// <summary>Proves the probe inference runs without taking ownership of the session.</summary>
    [Fact]
    public void RunProbeInference_ValidSession_LeavesSessionUsable()
    {
        using var session = new InferenceSession(NemotronEncoderFixture.Build());

        NemotronEncoder.RunProbeInference(session);

        using var encoder = new NemotronEncoder(session, Language, ownsSession: false);
        Assert.All(Encode(encoder, out _), value => Assert.Equal(Expected(1), value));
    }

    /// <summary>Proves encoding after disposal is rejected.</summary>
    [Fact]
    public void Encode_AfterDispose_Throws()
    {
        var encoder = new NemotronEncoder(new InferenceSession(NemotronEncoderFixture.Build()), Language);
        encoder.Dispose();

        Assert.Throws<ObjectDisposedException>(() => encoder.Encode(new float[Frames * 128], Frames, out _));
    }

    /// <summary>Proves a null session is rejected.</summary>
    [Fact]
    public void Constructor_NullSession_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new NemotronEncoder(null!, Language));
    }
}
