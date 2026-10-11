using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace DemaConsulting.Speech.Onnx.ChatterboxTurbo.SynthesisSubsystem;

/// <summary>
///     ONNX Runtime implementation of <see cref="IChatterboxTurboNetworks"/> over the four Chatterbox Turbo
///     graphs (speech encoder, token embedding, language model, conditional decoder), on the CPU.
/// </summary>
/// <remarks>
///     <b>Dynamic details are read from the graphs, not hard-coded</b>: the key/value cache element type
///     (float16 for the half-precision language model, float32 otherwise), the language model's input element
///     type, the number of layers (count of <c>past_key_values.*.key</c> inputs) and the head geometry all come
///     from session metadata, so the fp16 and fp32 graph variants share one implementation. The speech
///     encoder's outputs are bound by name (<c>audio_features</c>, <c>audio_tokens</c>,
///     <c>speaker_embeddings</c>, <c>speaker_features</c>), falling back to the documented positional order only
///     when a graph exposes exactly four unnamed-compatible outputs.
///     <para>
///     <b>Threads</b>: every session uses the configured intra-op thread count, one inter-op thread and
///     sequential execution; intra-op threads beyond about six do not help this workload. <b>Ownership</b>:
///     all four sessions are disposed with this object, and are disposed immediately if construction fails
///     part-way. Not thread-safe; the engine serializes calls.
///     </para>
/// </remarks>
internal sealed class OnnxChatterboxTurboNetworks : IChatterboxTurboNetworks
{
    /// <summary>The conditioning output names, in the reference positional order.</summary>
    private static readonly string[] EncoderOutputNames =
        ["audio_features", "audio_tokens", "speaker_embeddings", "speaker_features"];

    /// <summary>The default key/value head count when the graph leaves it dynamic.</summary>
    private const int DefaultKvHeads = 16;

    /// <summary>The default per-head width when the graph leaves it dynamic.</summary>
    private const int DefaultHeadDimension = 64;

    /// <summary>Prefix of the cache input names.</summary>
    private const string PastPrefix = "past_key_values.";

    /// <summary>Prefix of the cache output names.</summary>
    private const string PresentPrefix = "present.";

    /// <summary>The speech encoder session.</summary>
    private readonly InferenceSession _encoder;

    /// <summary>The token embedding session.</summary>
    private readonly InferenceSession _embedder;

    /// <summary>The language model session.</summary>
    private readonly InferenceSession _languageModel;

    /// <summary>The conditional decoder session.</summary>
    private readonly InferenceSession _decoder;

    /// <summary>Encoder output names resolved against the graph.</summary>
    private readonly string[] _encoderOutputs;

    /// <summary>Cache input names in graph order.</summary>
    private readonly string[] _pastNames;


    /// <summary>All language-model output names: logits first, then the present tensors.</summary>
    private readonly string[] _languageModelOutputs;

    /// <summary>The cache element type.</summary>
    private readonly TensorElementType _kvType;

    /// <summary>The language model's embedding input element type.</summary>
    private readonly TensorElementType _embeddingInputType;

    /// <summary>The key/value head count.</summary>
    private readonly int _kvHeads;

    /// <summary>The per-head width.</summary>
    private readonly int _headDimension;

    /// <summary>Shared run options.</summary>
    private readonly RunOptions _runOptions = new();

    /// <summary>
    ///     Initializes a new instance of the <see cref="OnnxChatterboxTurboNetworks"/> class, creating all four sessions.
    /// </summary>
    /// <param name="speechEncoderPath">The speech encoder graph path.</param>
    /// <param name="embedTokensPath">The token embedding graph path.</param>
    /// <param name="languageModelPath">The language model graph path.</param>
    /// <param name="conditionalDecoderPath">The conditional decoder graph path.</param>
    /// <param name="intraOpThreads">The intra-op thread count; must be positive.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="intraOpThreads"/> is not positive.</exception>
    /// <exception cref="OnnxRuntimeException">Thrown when a graph cannot be loaded; sessions created so far are disposed.</exception>
    /// <exception cref="InvalidOperationException">Thrown when a graph does not have the expected inputs/outputs.</exception>
    public OnnxChatterboxTurboNetworks(
        string speechEncoderPath,
        string embedTokensPath,
        string languageModelPath,
        string conditionalDecoderPath,
        int intraOpThreads)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(intraOpThreads, 0);
        IntraOpThreads = intraOpThreads;

        // Create sessions one at a time, disposing everything created so far if any step fails
        var created = new List<InferenceSession>();
        try
        {
            _encoder = Create(speechEncoderPath, intraOpThreads, created);
            _embedder = Create(embedTokensPath, intraOpThreads, created);
            _languageModel = Create(languageModelPath, intraOpThreads, created);
            _decoder = Create(conditionalDecoderPath, intraOpThreads, created);

            _encoderOutputs = ResolveEncoderOutputs(_encoder);

            // Derive the cache layout from the language model's own metadata
            _pastNames = [.. _languageModel.InputMetadata.Keys.Where(n => n.StartsWith(PastPrefix, StringComparison.Ordinal))];
            if (_pastNames.Length == 0)
            {
                throw new InvalidOperationException("The language model graph has no past_key_values inputs.");
            }

            _languageModelOutputs = [_languageModel.OutputMetadata.Keys.First(), .. _pastNames.Select(n => PresentPrefix + n[PastPrefix.Length..])];
            var first = _languageModel.InputMetadata[_pastNames[0]];
            _kvType = first.ElementDataType;
            _kvHeads = first.Dimensions.Length > 1 && first.Dimensions[1] > 0 ? first.Dimensions[1] : DefaultKvHeads;
            _headDimension = first.Dimensions.Length > 3 && first.Dimensions[3] > 0 ? first.Dimensions[3] : DefaultHeadDimension;
            _embeddingInputType = _languageModel.InputMetadata["inputs_embeds"].ElementDataType;
        }
        catch
        {
            foreach (var session in created)
            {
                session.Dispose();
            }

            _runOptions.Dispose();
            throw;
        }
    }

    /// <summary>
    ///     Test-only hook invoked with each session as it is created, so tests can verify it is disposed on failure.
    /// </summary>
    internal static Action<InferenceSession>? OnSessionCreated { get; set; }

    /// <summary>Gets the intra-op thread count applied to every session.</summary>
    internal int IntraOpThreads { get; }

    /// <summary>Gets the number of transformer layers, derived from the cache inputs.</summary>
    internal int LayerCount => _pastNames.Length / 2;

    /// <summary>Gets the cache element type read from the language model graph.</summary>
    internal TensorElementType KeyValueElementType => _kvType;

    /// <inheritdoc/>
    public ChatterboxTurboConditioning Encode(float[] referenceAudio)
    {
        ArgumentNullException.ThrowIfNull(referenceAudio);

        using var input = OrtValue.CreateTensorValueFromMemory(referenceAudio, [1, referenceAudio.Length]);
        using var outputs = _encoder.Run(_runOptions, ["audio_values"], [input], _encoderOutputs);

        var embeddingShape = outputs[0].GetTensorTypeAndShape().Shape;
        var featureShape = outputs[3].GetTensorTypeAndShape().Shape;
        return new ChatterboxTurboConditioning(
            outputs[0].GetTensorDataAsSpan<float>().ToArray(),
            (int)embeddingShape[1],
            outputs[1].GetTensorDataAsSpan<long>().ToArray(),
            outputs[2].GetTensorDataAsSpan<float>().ToArray(),
            outputs[3].GetTensorDataAsSpan<float>().ToArray(),
            (int)featureShape[1]);
    }

    /// <inheritdoc/>
    public float[] Embed(long[] tokenIds)
    {
        ArgumentNullException.ThrowIfNull(tokenIds);

        using var input = OrtValue.CreateTensorValueFromMemory(tokenIds, [1, tokenIds.Length]);
        using var outputs = _embedder.Run(
            _runOptions,
            [_embedder.InputMetadata.Keys.First()],
            [input],
            [_embedder.OutputMetadata.Keys.First()]);
        return outputs[0].GetTensorDataAsSpan<float>().ToArray();
    }

    /// <inheritdoc/>
    public IChatterboxTurboLanguageModelState BeginGeneration() => new LanguageModelState(this);

    /// <inheritdoc/>
    public float[] Decode(long[] speechTokens, ChatterboxTurboConditioning conditioning)
    {
        ArgumentNullException.ThrowIfNull(speechTokens);
        ArgumentNullException.ThrowIfNull(conditioning);

        var featureWidth = conditioning.SpeakerFeatures.Length / Math.Max(1, conditioning.SpeakerFeatureFrames);
        using var tokens = OrtValue.CreateTensorValueFromMemory(speechTokens, [1, speechTokens.Length]);
        using var embeddings = OrtValue.CreateTensorValueFromMemory(
            conditioning.SpeakerEmbeddings,
            [1, conditioning.SpeakerEmbeddings.Length]);
        using var features = OrtValue.CreateTensorValueFromMemory(
            conditioning.SpeakerFeatures,
            [1, conditioning.SpeakerFeatureFrames, featureWidth]);
        using var outputs = _decoder.Run(
            _runOptions,
            ["speech_tokens", "speaker_embeddings", "speaker_features"],
            [tokens, embeddings, features],
            [_decoder.OutputMetadata.Keys.First()]);
        return outputs[0].GetTensorDataAsSpan<float>().ToArray();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _encoder.Dispose();
        _embedder.Dispose();
        _languageModel.Dispose();
        _decoder.Dispose();
        _runOptions.Dispose();
    }

    /// <summary>Creates one CPU session and registers it for failure cleanup.</summary>
    private static InferenceSession Create(string path, int intraOpThreads, List<InferenceSession> created)
    {
        using var options = new SessionOptions
        {
            IntraOpNumThreads = intraOpThreads,
            InterOpNumThreads = 1,
            ExecutionMode = ExecutionMode.ORT_SEQUENTIAL,
        };
        var session = new InferenceSession(path, options);
        created.Add(session);
        OnSessionCreated?.Invoke(session);
        return session;
    }

    /// <summary>Binds the encoder outputs by name, or by the reference positional order for exactly four outputs.</summary>
    private static string[] ResolveEncoderOutputs(InferenceSession encoder)
    {
        if (EncoderOutputNames.All(encoder.OutputMetadata.ContainsKey))
        {
            return EncoderOutputNames;
        }

        if (encoder.OutputMetadata.Count == EncoderOutputNames.Length)
        {
            return [.. encoder.OutputMetadata.Keys];
        }

        throw new InvalidOperationException(
            "The speech encoder graph must expose audio_features, audio_tokens, speaker_embeddings and speaker_features outputs.");
    }

    /// <summary>One language-model generation: the cache outputs of the previous step become the next step's inputs.</summary>
    private sealed class LanguageModelState : IChatterboxTurboLanguageModelState
    {
        /// <summary>The owning networks.</summary>
        private readonly OnnxChatterboxTurboNetworks _owner;

        /// <summary>The previous step's outputs (logits then present tensors); null before the first step.</summary>
        private IDisposableReadOnlyCollection<OrtValue>? _previous;

        /// <summary>Initializes a new instance of the <see cref="LanguageModelState"/> class.</summary>
        public LanguageModelState(OnnxChatterboxTurboNetworks owner)
        {
            _owner = owner;
        }

        /// <inheritdoc/>
        public float[] Step(float[] inputEmbeddings, int sequenceLength, int attentionLength, int firstPosition)
        {
            ArgumentNullException.ThrowIfNull(inputEmbeddings);

            var owner = _owner;
            var names = new List<string> { "inputs_embeds", "attention_mask", "position_ids" };
            var values = new List<OrtValue>();
            var temporaries = new List<OrtValue>();
            try
            {
                // Embeddings are float32 for the shipped graphs; convert when a graph declares float16
                temporaries.Add(owner._embeddingInputType == TensorElementType.Float16
                    ? OrtValue.CreateTensorValueFromMemory(
                        inputEmbeddings.Select(v => (Half)v).ToArray(),
                        [1, sequenceLength, OnnxChatterboxTurboSynthesisEngine.EmbeddingSize])
                    : OrtValue.CreateTensorValueFromMemory(
                        inputEmbeddings,
                        [1, sequenceLength, OnnxChatterboxTurboSynthesisEngine.EmbeddingSize]));
                var mask = Enumerable.Repeat(1L, attentionLength).ToArray();
                var positions = Enumerable.Range(firstPosition, sequenceLength).Select(p => (long)p).ToArray();
                temporaries.Add(OrtValue.CreateTensorValueFromMemory(mask, [1, attentionLength]));
                temporaries.Add(OrtValue.CreateTensorValueFromMemory(positions, [1, sequenceLength]));
                values.AddRange(temporaries);

                // Cache inputs: zero-length tensors on the first step, the previous presents afterwards
                for (var i = 0; i < owner._pastNames.Length; i++)
                {
                    names.Add(owner._pastNames[i]);
                    if (_previous is null)
                    {
                        var empty = OrtValue.CreateAllocatedTensorValue(
                            OrtAllocator.DefaultInstance,
                            owner._kvType,
                            [1, owner._kvHeads, 0, owner._headDimension]);
                        temporaries.Add(empty);
                        values.Add(empty);
                    }
                    else
                    {
                        values.Add(_previous[i + 1]);
                    }
                }

                var outputs = owner._languageModel.Run(owner._runOptions, names, values, owner._languageModelOutputs);

                // The new presents replace the old ones only after the run that consumed them completed
                _previous?.Dispose();
                _previous = outputs;
                return ReadLastLogits(outputs[0]);
            }
            finally
            {
                foreach (var temporary in temporaries)
                {
                    temporary.Dispose();
                }
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            _previous?.Dispose();
            _previous = null;
        }

        /// <summary>Copies the logits of the final position out of a [1, positions, vocabulary] tensor.</summary>
        private static float[] ReadLastLogits(OrtValue logits)
        {
            var info = logits.GetTensorTypeAndShape();
            var positions = (int)info.Shape[1];
            var vocabulary = (int)info.Shape[2];
            var offset = (positions - 1) * vocabulary;
            if (info.ElementDataType == TensorElementType.Float16)
            {
                return logits.GetTensorDataAsSpan<Half>().Slice(offset, vocabulary).ToArray().Select(h => (float)h).ToArray();
            }

            return logits.GetTensorDataAsSpan<float>().Slice(offset, vocabulary).ToArray();
        }
    }
}
