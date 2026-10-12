using System.Reflection;
using DemaConsulting.Speech.Onnx.ChatterboxTurbo.SynthesisSubsystem;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace DemaConsulting.Speech.Onnx.ChatterboxTurbo.Tests.SynthesisSubsystem;

/// <summary>
///     Serializes the test classes that install the static session-created hook, so one test's hook never
///     observes sessions created by another test running in parallel.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public static class SessionHookCollection
{
    /// <summary>The collection name.</summary>
    public const string Name = "ChatterboxTurboSessionHook";
}

/// <summary>
///     Unit tests for <see cref="OnnxChatterboxTurboNetworks"/>, run against tiny synthetic ONNX graphs whose names,
///     types and ranks match the real Chatterbox Turbo contracts.
/// </summary>
[Collection(SessionHookCollection.Name)]
public sealed class OnnxChatterboxTurboNetworksTests : IDisposable
{
    /// <summary>A scratch directory unique to this test instance.</summary>
    private readonly string _root;

    /// <summary>Initializes a fresh scratch directory.</summary>
    public OnnxChatterboxTurboNetworksTests()
    {
        _root = Path.Join(Path.GetTempPath(), "DemaConsulting.Speech.Onnx.ChatterboxTurbo.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
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
        catch (UnauthorizedAccessException)
        {
            // Best-effort cleanup only.
        }
    }

    /// <summary>Proves the encoder outputs are bound by name and reshaped into the conditioning record.</summary>
    [Fact]
    public void OnnxChatterboxTurboNetworks_Encode_NamedOutputs_ReturnsConditioning()
    {
        // Arrange
        var paths = ChatterboxTurboGraphFixture.Write(_root);
        using var networks = Create(paths);

        // Act
        var conditioning = networks.Encode(new float[24000]);

        // Assert
        Assert.Equal(2, conditioning.ConditioningLength);
        Assert.Equal(2 * ChatterboxTurboGraphFixture.EmbeddingSize, conditioning.ConditioningEmbeddings.Length);
        Assert.Equal(ChatterboxTurboGraphFixture.PromptTokens, conditioning.PromptTokens);
        Assert.Equal([1f, 2f, 3f, 4f], conditioning.SpeakerEmbeddings);
        Assert.Equal(3, conditioning.SpeakerFeatureFrames);
        Assert.Equal(6, conditioning.SpeakerFeatures.Length);
    }

    /// <summary>Proves an encoder whose four outputs are not named as expected is bound positionally.</summary>
    [Fact]
    public void OnnxChatterboxTurboNetworks_Encode_UnnamedFourOutputs_BindsPositionally()
    {
        // Arrange
        var paths = ChatterboxTurboGraphFixture.Write(_root, namedEncoderOutputs: false);
        using var networks = Create(paths);

        // Act
        var conditioning = networks.Encode(new float[24000]);

        // Assert
        Assert.Equal(ChatterboxTurboGraphFixture.PromptTokens, conditioning.PromptTokens);
    }

    /// <summary>Proves the embedding graph is run for the supplied token ids.</summary>
    [Fact]
    public void OnnxChatterboxTurboNetworks_Embed_TokenIds_ReturnsTableRows()
    {
        // Arrange
        var paths = ChatterboxTurboGraphFixture.Write(_root);
        using var networks = Create(paths);

        // Act
        var embeddings = networks.Embed([3, 7]);

        // Assert
        Assert.Equal(2 * ChatterboxTurboGraphFixture.EmbeddingSize, embeddings.Length);
        Assert.Equal(0.003f, embeddings[0], 6);
        Assert.Equal(0.007f, embeddings[ChatterboxTurboGraphFixture.EmbeddingSize], 6);
    }

    /// <summary>
    ///     Proves the language model runs with a zero-length cache on the first step and feeds the returned
    ///     cache back on the second, for both float16 and float32 caches.
    /// </summary>
    /// <param name="halfCache">True for a float16 cache.</param>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void OnnxChatterboxTurboNetworks_Step_TwoSteps_ReusesCacheAndReturnsLastLogits(bool halfCache)
    {
        // Arrange
        var paths = ChatterboxTurboGraphFixture.Write(_root, halfCache: halfCache);
        using var networks = Create(paths);
        using var state = networks.BeginGeneration();
        var embeddings = Enumerable.Repeat(1f, 2 * ChatterboxTurboGraphFixture.EmbeddingSize).ToArray();

        // Act
        var first = state.Step(embeddings, 2, 2, 0);
        var second = state.Step(embeddings[..ChatterboxTurboGraphFixture.EmbeddingSize], 1, 3, 2);

        // Assert
        Assert.Equal(halfCache ? TensorElementType.Float16 : TensorElementType.Float, networks.KeyValueElementType);
        Assert.Equal(1, networks.LayerCount);
        Assert.Equal(ChatterboxTurboGraphFixture.Vocabulary, first.Length);
        Assert.Equal(1.024f * 3, first[3], 3);
        Assert.Equal(1.024f * 5, second[5], 3);
    }

    /// <summary>Proves the decoder receives the speech tokens and returns the waveform.</summary>
    [Fact]
    public void OnnxChatterboxTurboNetworks_Decode_SpeechTokens_ReturnsWaveform()
    {
        // Arrange
        var paths = ChatterboxTurboGraphFixture.Write(_root);
        using var networks = Create(paths);
        var conditioning = networks.Encode(new float[24000]);

        // Act
        var waveform = networks.Decode([5, 6, 7], conditioning);

        // Assert
        Assert.Equal([5f, 6f, 7f], waveform);
    }

    /// <summary>Proves a half-precision <c>inputs_embeds</c> input and half-precision logits are converted both ways.</summary>
    [Fact]
    public void OnnxChatterboxTurboNetworks_Step_HalfEmbeddingsAndLogits_AreConverted()
    {
        // Arrange
        var paths = ChatterboxTurboGraphFixture.Write(_root, halfEmbeddings: true, halfLogits: true);
        using var networks = Create(paths);
        using var state = networks.BeginGeneration();
        var embeddings = Enumerable.Repeat(1f, ChatterboxTurboGraphFixture.EmbeddingSize).ToArray();

        // Act
        var logits = state.Step(embeddings, 1, 1, 0);

        // Assert: 1024 * (3 / 1000) = 3.072, within half precision
        Assert.Equal(ChatterboxTurboGraphFixture.Vocabulary, logits.Length);
        Assert.Equal(3.072f, logits[3], 2);
    }

    /// <summary>Proves an embeddings array of the wrong length is rejected.</summary>
    [Fact]
    public void OnnxChatterboxTurboNetworks_Step_WrongEmbeddingLength_ThrowsArgumentException()
    {
        // Arrange
        var paths = ChatterboxTurboGraphFixture.Write(_root);
        using var networks = Create(paths);
        using var state = networks.BeginGeneration();

        // Act and Assert
        Assert.Throws<ArgumentException>(() => state.Step(new float[ChatterboxTurboGraphFixture.EmbeddingSize - 1], 1, 1, 0));
    }

    /// <summary>Proves an attention length shorter than the sequence length is rejected.</summary>
    [Fact]
    public void OnnxChatterboxTurboNetworks_Step_AttentionShorterThanSequence_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var paths = ChatterboxTurboGraphFixture.Write(_root);
        using var networks = Create(paths);
        using var state = networks.BeginGeneration();

        // Act and Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => state.Step(new float[2 * ChatterboxTurboGraphFixture.EmbeddingSize], 2, 1, 0));
    }

    /// <summary>Proves an encoder graph without the expected outputs is rejected.</summary>
    [Fact]
    public void OnnxChatterboxTurboNetworks_Constructor_UnrecognizedEncoderOutputs_ThrowsInvalidOperationException()
    {
        // Arrange: a single-output encoder matches neither the named nor the positional contract
        var paths = ChatterboxTurboGraphFixture.Write(_root);
        File.WriteAllBytes(
            paths.Encoder,
            new OnnxModelWriter("encoder")
                .Input("audio_values", OnnxModelWriter.Float, "1", "n")
                .FloatInitializer("c_only", [1, 1], [1f])
                .Node("Identity", ["c_only"], ["only"])
                .Output("only", OnnxModelWriter.Float, "1", "1")
                .Build());

        // Act and Assert
        Assert.Throws<InvalidOperationException>(() => Create(paths));
    }

    /// <summary>Proves the intra-op thread count is applied and a non-positive count is rejected.</summary>
    [Fact]
    public void OnnxChatterboxTurboNetworks_Constructor_ThreadCount_IsAppliedAndValidated()
    {
        // Arrange
        var paths = ChatterboxTurboGraphFixture.Write(_root);

        // Act
        using var networks = Create(paths, threads: 2);

        // Assert
        Assert.Equal(2, networks.IntraOpThreads);
        Assert.Throws<ArgumentOutOfRangeException>(() => Create(paths, threads: 0));
    }

    /// <summary>Proves sessions created before a failing graph are disposed.</summary>
    [Fact]
    public void OnnxChatterboxTurboNetworks_Constructor_InvalidLanguageModel_DisposesSessionsAndThrows()
    {
        // Arrange: a language model without cache inputs is invalid
        var paths = ChatterboxTurboGraphFixture.Write(_root);
        File.WriteAllBytes(paths.LanguageModel, ChatterboxTurboGraphFixture.LanguageModel(halfCache: true, withPastInputs: false));
        var created = new List<InferenceSession>();
        OnnxChatterboxTurboNetworks.OnSessionCreated = created.Add;
        try
        {
            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => Create(paths));
            Assert.Equal(4, created.Count);
            Assert.All(created, session => Assert.True(IsDisposed(session)));
        }
        finally
        {
            OnnxChatterboxTurboNetworks.OnSessionCreated = null;
        }
    }

    /// <summary>Proves a missing graph file fails creation and disposes the sessions created so far.</summary>
    [Fact]
    public void OnnxChatterboxTurboNetworks_Constructor_MissingGraph_DisposesEarlierSessionsAndThrows()
    {
        // Arrange
        var paths = ChatterboxTurboGraphFixture.Write(_root);
        var created = new List<InferenceSession>();
        OnnxChatterboxTurboNetworks.OnSessionCreated = created.Add;
        try
        {
            // Act & Assert
            Assert.Throws<FileNotFoundException>(() => new OnnxChatterboxTurboNetworks(
                paths.Encoder,
                paths.Embedder,
                Path.Combine(_root, "missing.onnx"),
                paths.Decoder,
                1));
            Assert.Equal(2, created.Count);
            Assert.All(created, session => Assert.True(IsDisposed(session)));
        }
        finally
        {
            OnnxChatterboxTurboNetworks.OnSessionCreated = null;
        }
    }

    /// <summary>Proves disposing the networks disposes every session.</summary>
    [Fact]
    public void OnnxChatterboxTurboNetworks_Dispose_DisposesAllSessions()
    {
        // Arrange
        var paths = ChatterboxTurboGraphFixture.Write(_root);
        var created = new List<InferenceSession>();
        OnnxChatterboxTurboNetworks.OnSessionCreated = created.Add;
        try
        {
            var networks = Create(paths);

            // Act
            networks.Dispose();

            // Assert
            Assert.Equal(4, created.Count);
            Assert.All(created, session => Assert.True(IsDisposed(session)));
        }
        finally
        {
            OnnxChatterboxTurboNetworks.OnSessionCreated = null;
        }
    }

    /// <summary>Proves float16 and float32 output tensors are both read back as floats.</summary>
    [Fact]
    public void OnnxChatterboxTurboNetworks_ReadFloats_HandlesFloat32AndFloat16()
    {
        // Arrange
        float[] single = [1.5f, -2f, 0.25f];
        using var halfValue = OnnxChatterboxTurboNetworks.CreateFloat16Tensor(single, [1, 3]);

        using var singleValue = OrtValue.CreateTensorValueFromMemory(single, [1, 3]);

        // Act / Assert
        Assert.Equal(single, OnnxChatterboxTurboNetworks.ReadFloats(halfValue));
        Assert.Equal(single, OnnxChatterboxTurboNetworks.ReadFloats(singleValue));
    }

    /// <summary>Proves a disposed networks object rejects further use with <see cref="ObjectDisposedException"/>.</summary>
    [Fact]
    public void OnnxChatterboxTurboNetworks_AfterDispose_RejectsUse()
    {
        // Arrange
        var paths = ChatterboxTurboGraphFixture.Write(_root);
        var networks = Create(paths);
        var conditioning = networks.Encode(new float[24000]);
        networks.Dispose();

        // Act and Assert
        Assert.Throws<ObjectDisposedException>(() => networks.Encode(new float[24000]));
        Assert.Throws<ObjectDisposedException>(() => networks.Embed([1]));
        Assert.Throws<ObjectDisposedException>(() => networks.Decode([1], conditioning));
        Assert.Throws<ObjectDisposedException>(() => networks.BeginGeneration());
        networks.Dispose();
    }

    /// <summary>Proves null arguments are rejected by the encoder, embedder and decoder entry points.</summary>
    [Fact]
    public void OnnxChatterboxTurboNetworks_NullArguments_Throw()
    {
        // Arrange
        var paths = ChatterboxTurboGraphFixture.Write(_root);
        using var networks = Create(paths);
        var conditioning = networks.Encode(new float[24000]);

        // Act and Assert
        Assert.Throws<ArgumentNullException>(() => networks.Encode(null!));
        Assert.Throws<ArgumentNullException>(() => networks.Embed(null!));
        Assert.Throws<ArgumentNullException>(() => networks.Decode(null!, conditioning));
        Assert.Throws<ArgumentNullException>(() => networks.Decode([1], null!));
    }

    /// <summary>Proves float16 encoder, embedder and decoder outputs are read back as floats.</summary>
    [Fact]
    public void OnnxChatterboxTurboNetworks_HalfAuxiliaryOutputs_AreConvertedToFloats()
    {
        // Arrange
        var paths = ChatterboxTurboGraphFixture.Write(_root, halfAuxiliary: true);
        using var networks = Create(paths);

        // Act
        var conditioning = networks.Encode(new float[24000]);
        var embeddings = networks.Embed([4]);
        var waveform = networks.Decode([5, 6], conditioning);

        // Assert
        Assert.Equal([1f, 2f, 3f, 4f], conditioning.SpeakerEmbeddings);
        Assert.Equal([1f, 2f, 3f, 4f, 5f, 6f], conditioning.SpeakerFeatures);
        Assert.Equal(0.5f, conditioning.ConditioningEmbeddings[0]);
        Assert.Equal(0.004f, embeddings[0], 3);
        Assert.Equal([5f, 6f], waveform);
    }

    /// <summary>Creates networks over the fixture paths.</summary>
    private static OnnxChatterboxTurboNetworks Create(
        (string Encoder, string Embedder, string LanguageModel, string Decoder) paths,
        int threads = 1) =>
        new(paths.Encoder, paths.Embedder, paths.LanguageModel, paths.Decoder, threads);

    /// <summary>Reads the session's private disposed flag, since ONNX Runtime exposes no public equivalent and a call on a disposed session is unsafe.</summary>
    private static bool IsDisposed(InferenceSession session)
    {
        var field = typeof(InferenceSession).GetField("_disposed", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(field);
        return (bool)field.GetValue(session)!;
    }
}
