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

        // Assert: every field of the record carries the encoder output unchanged
        Assert.Equal(2, conditioning.ConditioningLength);
        Assert.Equal(2 * ChatterboxTurboGraphFixture.EmbeddingSize, conditioning.ConditioningEmbeddings.Length);
        Assert.All(conditioning.ConditioningEmbeddings.ToArray(), value => Assert.Equal(0.5f, value));
        Assert.Equal(ChatterboxTurboGraphFixture.PromptTokens, conditioning.PromptTokens.ToArray());
        Assert.Equal([1f, 2f, 3f, 4f], conditioning.SpeakerEmbeddings.ToArray());
        Assert.Equal(3, conditioning.SpeakerFeatureFrames);
        Assert.Equal([1f, 2f, 3f, 4f, 5f, 6f], conditioning.SpeakerFeatures.ToArray());
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

        // Assert: positional binding puts each of the four outputs in its own field
        Assert.Equal(2, conditioning.ConditioningLength);
        Assert.All(conditioning.ConditioningEmbeddings.ToArray(), value => Assert.Equal(0.5f, value));
        Assert.Equal(ChatterboxTurboGraphFixture.PromptTokens, conditioning.PromptTokens.ToArray());
        Assert.Equal([1f, 2f, 3f, 4f], conditioning.SpeakerEmbeddings.ToArray());
        Assert.Equal(3, conditioning.SpeakerFeatureFrames);
        Assert.Equal([1f, 2f, 3f, 4f, 5f, 6f], conditioning.SpeakerFeatures.ToArray());
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

    /// <summary>Proves an empty token array embeds to an empty result without running the graph.</summary>
    [Fact]
    public void OnnxChatterboxTurboNetworks_Embed_EmptyTokenIds_ReturnsEmpty()
    {
        // Arrange
        var paths = ChatterboxTurboGraphFixture.Write(_root);
        using var networks = Create(paths);

        // Act
        var embeddings = networks.Embed([]);

        // Assert
        Assert.Empty(embeddings);
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

    /// <summary>Proves the decoder receives the speech tokens and the conditioning's speaker inputs and returns the waveform.</summary>
    [Fact]
    public void OnnxChatterboxTurboNetworks_Decode_SpeechTokens_ReturnsWaveform()
    {
        // Arrange: the waveform is each token plus the sum of the speaker embeddings (10) and of the features (21)
        var paths = ChatterboxTurboGraphFixture.Write(_root, speakerSensitiveDecoder: true);
        using var networks = Create(paths);
        var conditioning = networks.Encode(new float[24000]);

        // Act
        var waveform = networks.Decode([5, 6, 7], conditioning);

        // Assert
        Assert.Equal([36f, 37f, 38f], waveform);
    }

    /// <summary>Proves the conditioning's speaker embeddings and features, not constants, reach the decoder.</summary>
    [Fact]
    public void OnnxChatterboxTurboNetworks_Decode_ChangedConditioning_ChangesWaveform()
    {
        // Arrange: embeddings now sum to 100 and the three frames of features to 6
        var paths = ChatterboxTurboGraphFixture.Write(_root, speakerSensitiveDecoder: true);
        using var networks = Create(paths);
        var encoded = networks.Encode(new float[24000]);
        var conditioning = new ChatterboxTurboConditioning(
            encoded.ConditioningEmbeddings.ToArray(),
            encoded.ConditioningLength,
            encoded.PromptTokens.ToArray(),
            [100f, 0f, 0f, 0f],
            [1f, 1f, 1f, 1f, 1f, 1f],
            encoded.SpeakerFeatureFrames);

        // Act
        var waveform = networks.Decode([5, 6], conditioning);

        // Assert
        Assert.Equal([111f, 112f], waveform);
    }

    /// <summary>Proves an empty speech-token array is rejected before any native call.</summary>
    [Fact]
    public void OnnxChatterboxTurboNetworks_Decode_EmptyTokens_ThrowsArgumentException()
    {
        // Arrange
        var paths = ChatterboxTurboGraphFixture.Write(_root);
        using var networks = Create(paths);
        var conditioning = networks.Encode(new float[24000]);

        // Act
        var exception = Record.Exception(() => networks.Decode([], conditioning));

        // Assert
        Assert.IsType<ArgumentException>(exception);
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

        // Act
        var exception = Record.Exception(() => state.Step(new float[ChatterboxTurboGraphFixture.EmbeddingSize - 1], 1, 1, 0));

        // Assert
        Assert.IsType<ArgumentException>(exception);
    }

    /// <summary>Proves an attention length shorter than the sequence length is rejected.</summary>
    [Fact]
    public void OnnxChatterboxTurboNetworks_Step_AttentionShorterThanSequence_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var paths = ChatterboxTurboGraphFixture.Write(_root);
        using var networks = Create(paths);
        using var state = networks.BeginGeneration();

        // Act
        var exception = Record.Exception(() => state.Step(new float[2 * ChatterboxTurboGraphFixture.EmbeddingSize], 2, 1, 0));

        // Assert
        Assert.IsType<ArgumentOutOfRangeException>(exception);
    }

    /// <summary>Proves a zero or negative sequence length is rejected before any native call.</summary>
    /// <param name="sequenceLength">The invalid sequence length.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void OnnxChatterboxTurboNetworks_Step_NonPositiveSequenceLength_ThrowsArgumentOutOfRangeException(int sequenceLength)
    {
        // Arrange
        var paths = ChatterboxTurboGraphFixture.Write(_root);
        using var networks = Create(paths);
        using var state = networks.BeginGeneration();

        // Act
        var exception = Record.Exception(() => state.Step([], sequenceLength, 1, 0));

        // Assert
        Assert.IsType<ArgumentOutOfRangeException>(exception);
    }

    /// <summary>Proves a negative first position is rejected before any native call.</summary>
    [Fact]
    public void OnnxChatterboxTurboNetworks_Step_NegativeFirstPosition_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var paths = ChatterboxTurboGraphFixture.Write(_root);
        using var networks = Create(paths);
        using var state = networks.BeginGeneration();

        // Act
        var exception = Record.Exception(() => state.Step(new float[ChatterboxTurboGraphFixture.EmbeddingSize], 1, 1, -1));

        // Assert
        Assert.IsType<ArgumentOutOfRangeException>(exception);
    }

    /// <summary>Proves a null embeddings array is rejected by <c>Step</c>.</summary>
    [Fact]
    public void OnnxChatterboxTurboNetworks_Step_NullEmbeddings_ThrowsArgumentNullException()
    {
        // Arrange
        var paths = ChatterboxTurboGraphFixture.Write(_root);
        using var networks = Create(paths);
        using var state = networks.BeginGeneration();

        // Act
        var exception = Record.Exception(() => state.Step(null!, 1, 1, 0));

        // Assert
        Assert.IsType<ArgumentNullException>(exception);
    }

    /// <summary>Proves a disposed generation state rejects <c>Step</c> instead of restarting with an empty cache.</summary>
    [Fact]
    public void OnnxChatterboxTurboNetworks_Step_AfterStateDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        var paths = ChatterboxTurboGraphFixture.Write(_root);
        using var networks = Create(paths);
        var state = networks.BeginGeneration();
        state.Dispose();

        // Act
        var exception = Record.Exception(() => state.Step(new float[ChatterboxTurboGraphFixture.EmbeddingSize], 1, 1, 0));

        // Assert
        Assert.IsType<ObjectDisposedException>(exception);
    }

    /// <summary>Proves a generation state rejects <c>Step</c> once the owning networks were disposed.</summary>
    [Fact]
    public void OnnxChatterboxTurboNetworks_Step_AfterNetworksDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        var paths = ChatterboxTurboGraphFixture.Write(_root);
        var networks = Create(paths);
        using var state = networks.BeginGeneration();
        networks.Dispose();

        // Act
        var exception = Record.Exception(() => state.Step(new float[ChatterboxTurboGraphFixture.EmbeddingSize], 1, 1, 0));

        // Assert
        Assert.IsType<ObjectDisposedException>(exception);
    }

    /// <summary>Proves a state that already ran a step can be disposed after its networks, and twice, without throwing.</summary>
    [Fact]
    public void OnnxChatterboxTurboNetworks_StateDispose_AfterNetworksDispose_IsSafe()
    {
        // Arrange: the state holds the cache of one completed step when the networks go away first
        var paths = ChatterboxTurboGraphFixture.Write(_root);
        var networks = Create(paths);
        var state = networks.BeginGeneration();
        state.Step(new float[ChatterboxTurboGraphFixture.EmbeddingSize], 1, 1, 0);
        networks.Dispose();

        // Act
        var exception = Record.Exception(() =>
        {
            state.Dispose();
            state.Dispose();
        });

        // Assert
        Assert.Null(exception);
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

        // Act
        var exception = Record.Exception(() => Create(paths));

        // Assert
        Assert.IsType<InvalidOperationException>(exception);
    }

    /// <summary>Proves the intra-op thread count is applied.</summary>
    [Fact]
    public void OnnxChatterboxTurboNetworks_Constructor_ThreadCount_IsApplied()
    {
        // Arrange
        var paths = ChatterboxTurboGraphFixture.Write(_root);

        // Act
        using var networks = Create(paths, threads: 2);

        // Assert
        Assert.Equal(2, networks.IntraOpThreads);
    }

    /// <summary>Proves a non-positive intra-op thread count is rejected.</summary>
    /// <param name="threads">The invalid thread count.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void OnnxChatterboxTurboNetworks_Constructor_NonPositiveThreadCount_ThrowsArgumentOutOfRangeException(int threads)
    {
        // Arrange
        var paths = ChatterboxTurboGraphFixture.Write(_root);

        // Act
        var exception = Record.Exception(() => Create(paths, threads: threads));

        // Assert
        Assert.IsType<ArgumentOutOfRangeException>(exception);
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
            // Act
            var exception = Record.Exception(() => Create(paths));

            // Assert
            Assert.IsType<InvalidOperationException>(exception);
            Assert.Equal(4, created.Count);
            Assert.All(created, session => Assert.True(IsDisposed(session)));
        }
        finally
        {
            OnnxChatterboxTurboNetworks.OnSessionCreated = null;
        }
    }

    /// <summary>Proves an incomplete cache layout or missing embedding input is rejected and sessions are disposed.</summary>
    /// <param name="omitValueInput">True to omit the layer-0 value cache input.</param>
    /// <param name="omitValuePresentOutput">True to omit the layer-0 value present output.</param>
    /// <param name="embeddingsInputName">The embedding input name.</param>
    [Theory]
    [InlineData(true, false, "inputs_embeds")]
    [InlineData(false, true, "inputs_embeds")]
    [InlineData(false, false, "other_embeds")]
    public void OnnxChatterboxTurboNetworks_Constructor_IncompleteLanguageModelLayout_DisposesSessionsAndThrows(
        bool omitValueInput,
        bool omitValuePresentOutput,
        string embeddingsInputName)
    {
        // Arrange: a language model missing a value input, a present output or the embedding input
        var paths = ChatterboxTurboGraphFixture.Write(_root);
        File.WriteAllBytes(
            paths.LanguageModel,
            ChatterboxTurboGraphFixture.LanguageModel(
                halfCache: true,
                withPastInputs: true,
                omitValueInput: omitValueInput,
                omitValuePresentOutput: omitValuePresentOutput,
                embeddingsInputName: embeddingsInputName));
        var created = new List<InferenceSession>();
        OnnxChatterboxTurboNetworks.OnSessionCreated = created.Add;
        try
        {
            // Act
            var exception = Record.Exception(() => Create(paths));

            // Assert: construction fails with the documented exception and every session is disposed
            Assert.IsType<InvalidOperationException>(exception);
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
            // Act
            var exception = Record.Exception(() => new OnnxChatterboxTurboNetworks(
                paths.Encoder,
                paths.Embedder,
                Path.Combine(_root, "missing.onnx"),
                paths.Decoder,
                1));

            // Assert
            Assert.IsType<FileNotFoundException>(exception);
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

    /// <summary>Proves a throwing dispose does not stop the later items from being disposed, and the failure surfaces.</summary>
    [Fact]
    public void OnnxChatterboxTurboNetworks_DisposeAll_FirstDisposeThrows_StillDisposesLaterItems()
    {
        // Arrange
        var first = new RecordingDisposable(new InvalidOperationException("first"));
        var second = new RecordingDisposable();
        var third = new RecordingDisposable();

        // Act
        var exception = Record.Exception(() => OnnxChatterboxTurboNetworks.DisposeAll(first, second, third));

        // Assert
        Assert.Equal("first", Assert.IsType<InvalidOperationException>(exception).Message);
        Assert.Equal(1, first.DisposeCount);
        Assert.Equal(1, second.DisposeCount);
        Assert.Equal(1, third.DisposeCount);
    }

    /// <summary>Proves several dispose failures are all reported, and every item is still disposed.</summary>
    [Fact]
    public void OnnxChatterboxTurboNetworks_DisposeAll_SeveralThrow_ReportsAllFailures()
    {
        // Arrange
        var first = new RecordingDisposable(new InvalidOperationException("first"));
        var second = new RecordingDisposable(new InvalidCastException("second"));
        var third = new RecordingDisposable();

        // Act
        var exception = Record.Exception(() => OnnxChatterboxTurboNetworks.DisposeAll(first, second, third));

        // Assert
        var aggregate = Assert.IsType<AggregateException>(exception);
        Assert.Equal(2, aggregate.InnerExceptions.Count);
        Assert.Equal(1, third.DisposeCount);
    }

    /// <summary>Proves disposing items that do not throw completes silently.</summary>
    [Fact]
    public void OnnxChatterboxTurboNetworks_DisposeAll_NoFailures_DisposesEveryItem()
    {
        // Arrange
        var first = new RecordingDisposable();
        var second = new RecordingDisposable();

        // Act
        var exception = Record.Exception(() => OnnxChatterboxTurboNetworks.DisposeAll(first, second));

        // Assert
        Assert.Null(exception);
        Assert.Equal(1, first.DisposeCount);
        Assert.Equal(1, second.DisposeCount);
    }

    /// <summary>Proves float16 and float32 output tensors are both read back as floats.</summary>
    [Fact]
    public void OnnxChatterboxTurboNetworks_ReadFloats_HandlesFloat32AndFloat16()
    {
        // Arrange
        float[] single = [1.5f, -2f, 0.25f];
        using var halfValue = OnnxChatterboxTurboNetworks.CreateFloat16Tensor(single, [1, 3]);
        using var singleValue = OrtValue.CreateTensorValueFromMemory(single, [1, 3]);

        // Act
        var fromHalf = OnnxChatterboxTurboNetworks.ReadFloats(halfValue);
        var fromSingle = OnnxChatterboxTurboNetworks.ReadFloats(singleValue);

        // Assert
        Assert.Equal(single, fromHalf);
        Assert.Equal(single, fromSingle);
    }

    /// <summary>Proves a disposed networks object rejects each entry point with <see cref="ObjectDisposedException"/>.</summary>
    [Fact]
    public void OnnxChatterboxTurboNetworks_AfterDispose_RejectsUse()
    {
        // Arrange
        var paths = ChatterboxTurboGraphFixture.Write(_root);
        var networks = Create(paths);
        var conditioning = networks.Encode(new float[24000]);
        networks.Dispose();

        // Act
        var exceptions = new[]
        {
            Record.Exception(() => networks.Encode(new float[24000])),
            Record.Exception(() => networks.Embed([1])),
            Record.Exception(() => networks.Decode([1], conditioning)),
            Record.Exception(() => networks.BeginGeneration()),
        };

        // Assert
        Assert.All(exceptions, exception => Assert.IsType<ObjectDisposedException>(exception));
    }

    /// <summary>Proves a second dispose of the networks is a no-op.</summary>
    [Fact]
    public void OnnxChatterboxTurboNetworks_Dispose_Twice_IsNoOp()
    {
        // Arrange
        var paths = ChatterboxTurboGraphFixture.Write(_root);
        var networks = Create(paths);
        networks.Dispose();

        // Act
        var exception = Record.Exception(networks.Dispose);

        // Assert
        Assert.Null(exception);
    }

    /// <summary>Proves null arguments are rejected by the encoder, embedder and decoder entry points.</summary>
    [Fact]
    public void OnnxChatterboxTurboNetworks_NullArguments_Throw()
    {
        // Arrange
        var paths = ChatterboxTurboGraphFixture.Write(_root);
        using var networks = Create(paths);
        var conditioning = networks.Encode(new float[24000]);

        // Act
        var exceptions = new[]
        {
            Record.Exception(() => networks.Encode(null!)),
            Record.Exception(() => networks.Embed(null!)),
            Record.Exception(() => networks.Decode(null!, conditioning)),
            Record.Exception(() => networks.Decode([1], null!)),
        };

        // Assert
        Assert.All(exceptions, exception => Assert.IsType<ArgumentNullException>(exception));
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
        Assert.Equal([1f, 2f, 3f, 4f], conditioning.SpeakerEmbeddings.ToArray());
        Assert.Equal([1f, 2f, 3f, 4f, 5f, 6f], conditioning.SpeakerFeatures.ToArray());
        Assert.Equal(0.5f, conditioning.ConditioningEmbeddings.Span[0]);
        Assert.Equal(0.004f, embeddings[0], 3);
        Assert.Equal([5f, 6f], waveform);
    }

    /// <summary>A disposable that counts disposals and optionally throws.</summary>
    private sealed class RecordingDisposable(Exception? failure = null) : IDisposable
    {
        /// <summary>Gets the number of times <see cref="Dispose"/> ran.</summary>
        public int DisposeCount { get; private set; }

        /// <inheritdoc />
        public void Dispose()
        {
            DisposeCount++;
            if (failure != null)
            {
#pragma warning disable S3877 // The throwing Dispose is the behavior under test
                throw failure;
#pragma warning restore S3877
            }
        }
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
