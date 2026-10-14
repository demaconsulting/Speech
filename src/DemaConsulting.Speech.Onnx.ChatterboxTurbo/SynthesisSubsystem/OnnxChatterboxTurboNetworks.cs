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
///     when a graph exposes exactly four outputs under different names.
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

    /// <summary>The run options shared by all four sessions; created after argument validation.</summary>
    private readonly RunOptions _runOptions;

    /// <summary>True once <see cref="Dispose"/> has run.</summary>
    private bool _disposed;

    /// <summary>
    ///     Initializes a new instance of the <see cref="OnnxChatterboxTurboNetworks"/> class, creating all four sessions.
    /// </summary>
    /// <param name="speechEncoderPath">The speech encoder graph path.</param>
    /// <param name="embedTokensPath">The token embedding graph path.</param>
    /// <param name="languageModelPath">The language model graph path.</param>
    /// <param name="conditionalDecoderPath">The conditional decoder graph path.</param>
    /// <param name="intraOpThreads">The intra-op thread count; must be positive.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="intraOpThreads"/> is not positive.</exception>
    /// <exception cref="FileNotFoundException">Thrown when a graph file does not exist; sessions created so far are disposed.</exception>
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
        _runOptions = new RunOptions();

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

            ValidateLanguageModelLayout(_languageModel, _pastNames);

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
        ObjectDisposedException.ThrowIf(_disposed, this);

        using var input = OrtValue.CreateTensorValueFromMemory(referenceAudio, [1, referenceAudio.Length]);
        using var outputs = _encoder.Run(_runOptions, ["audio_values"], [input], _encoderOutputs);

        var embeddingShape = outputs[0].GetTensorTypeAndShape().Shape;
        var featureShape = outputs[3].GetTensorTypeAndShape().Shape;
        return new ChatterboxTurboConditioning(
            ReadFloats(outputs[0]),
            (int)embeddingShape[1],
            outputs[1].GetTensorDataAsSpan<long>().ToArray(),
            ReadFloats(outputs[2]),
            ReadFloats(outputs[3]),
            (int)featureShape[1]);
    }

    /// <inheritdoc/>
    public float[] Embed(long[] tokenIds)
    {
        ArgumentNullException.ThrowIfNull(tokenIds);
        ObjectDisposedException.ThrowIf(_disposed, this);

        // No tokens means no embedding rows: answer directly instead of running the graph on an empty tensor
        if (tokenIds.Length == 0)
        {
            return [];
        }

        using var input = OrtValue.CreateTensorValueFromMemory(tokenIds, [1, tokenIds.Length]);
        using var outputs = _embedder.Run(
            _runOptions,
            [_embedder.InputMetadata.Keys.First()],
            [input],
            [_embedder.OutputMetadata.Keys.First()]);
        return ReadFloats(outputs[0]);
    }

    /// <summary>
    ///     Copies a float32 or float16 tensor into a float array. The shipped auxiliary graphs (every variant) emit
    ///     float32, but reading by the declared element type keeps a half-precision output from throwing.
    /// </summary>
    /// <param name="value">The output tensor.</param>
    /// <returns>The tensor data as floats.</returns>
    internal static float[] ReadFloats(OrtValue value)
    {
        if (value.GetTensorTypeAndShape().ElementDataType == TensorElementType.Float16)
        {
            // ORT exposes float16 tensors as its own Float16 struct (System.Half is rejected)
            return HalfToFloats(value.GetTensorDataAsSpan<Float16>());
        }

        return value.GetTensorDataAsSpan<float>().ToArray();
    }

    /// <summary>Converts a span of ORT float16 values to floats.</summary>
    private static float[] HalfToFloats(ReadOnlySpan<Float16> values)
    {
        var result = new float[values.Length];
        for (var i = 0; i < values.Length; i++)
        {
            result[i] = (float)BitConverter.UInt16BitsToHalf(values[i].value);
        }

        return result;
    }

    /// <summary>Creates a float16 tensor from float values (ORT rejects <see cref="Half"/> arrays).</summary>
    /// <param name="values">The values to store, converted to half precision.</param>
    /// <param name="shape">The tensor shape; its element count must equal the number of values.</param>
    /// <returns>A new tensor owned by the caller.</returns>
    internal static OrtValue CreateFloat16Tensor(float[] values, long[] shape)
    {
        var tensor = OrtValue.CreateAllocatedTensorValue(OrtAllocator.DefaultInstance, TensorElementType.Float16, shape);
        var bytes = tensor.GetTensorMutableRawData();
        for (var i = 0; i < values.Length; i++)
        {
            BitConverter.TryWriteBytes(bytes.Slice(i * 2, 2), (Half)values[i]);
        }

        return tensor;
    }

    /// <inheritdoc/>
    public IChatterboxTurboLanguageModelState BeginGeneration()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return new LanguageModelState(this);
    }

    /// <inheritdoc/>
    public float[] Decode(long[] speechTokens, ChatterboxTurboConditioning conditioning)
    {
        ArgumentNullException.ThrowIfNull(speechTokens);
        ArgumentNullException.ThrowIfNull(conditioning);
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (speechTokens.Length == 0)
        {
            throw new ArgumentException("At least one speech token is required.", nameof(speechTokens));
        }

        var featureWidth = conditioning.SpeakerFeatures.Length / Math.Max(1, conditioning.SpeakerFeatureFrames);
        using var tokens = OrtValue.CreateTensorValueFromMemory(speechTokens, [1, speechTokens.Length]);

        // ONNX Runtime needs writable backing arrays, so the read-only conditioning is copied for this call
        var speakerEmbeddings = conditioning.SpeakerEmbeddings.ToArray();
        var speakerFeatures = conditioning.SpeakerFeatures.ToArray();
        using var embeddings = OrtValue.CreateTensorValueFromMemory(
            speakerEmbeddings,
            [1, speakerEmbeddings.Length]);
        using var features = OrtValue.CreateTensorValueFromMemory(
            speakerFeatures,
            [1, conditioning.SpeakerFeatureFrames, featureWidth]);
        using var outputs = _decoder.Run(
            _runOptions,
            ["speech_tokens", "speaker_embeddings", "speaker_features"],
            [tokens, embeddings, features],
            [_decoder.OutputMetadata.Keys.First()]);
        return ReadFloats(outputs[0]);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        DisposeAll(_encoder, _embedder, _languageModel, _decoder, _runOptions);
    }

    /// <summary>
    ///     Disposes every item in order even when an earlier dispose throws, then rethrows the failure (the only
    ///     one as is, several as an <see cref="AggregateException"/>), so one failing dispose never leaks the rest.
    ///     Internal so tests can prove the guarantee with disposables that throw.
    /// </summary>
    /// <param name="items">The items to dispose, in order.</param>
    internal static void DisposeAll(params IDisposable[] items)
    {
        List<Exception>? failures = null;
        foreach (var item in items)
        {
            try
            {
                item.Dispose();
            }
            catch (Exception ex)
            {
                (failures ??= []).Add(ex);
            }
        }

        if (failures is { Count: 1 })
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failures[0]).Throw();
        }

        if (failures is { Count: > 1 })
        {
            throw new AggregateException(failures);
        }
    }

    /// <summary>Creates one CPU session and registers it for failure cleanup.</summary>
    private static InferenceSession Create(string path, int intraOpThreads, List<InferenceSession> created)
    {
        // A missing graph is a file problem, not an ONNX Runtime one
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"The Chatterbox Turbo graph '{path}' does not exist.", path);
        }

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

    /// <summary>
    ///     Verifies the language model exposes the inputs and outputs the generation loop relies on: the embedding
    ///     input, a key and a value input per layer, a present output per cache input, and a logits output.
    /// </summary>
    private static void ValidateLanguageModelLayout(InferenceSession languageModel, string[] pastNames)
    {
        if (!languageModel.InputMetadata.ContainsKey("inputs_embeds"))
        {
            throw new InvalidOperationException("The language model graph has no inputs_embeds input.");
        }

        // Every cache input needs its key/value partner and a matching present output
        var inputs = new HashSet<string>(pastNames, StringComparer.Ordinal);
        foreach (var name in pastNames)
        {
            string? partner = null;
            if (name.EndsWith(".key", StringComparison.Ordinal))
            {
                partner = name[..^".key".Length] + ".value";
            }
            else if (name.EndsWith(".value", StringComparison.Ordinal))
            {
                partner = name[..^".value".Length] + ".key";
            }

            if (partner == null || !inputs.Contains(partner))
            {
                throw new InvalidOperationException(
                    $"The language model graph input {name} is not part of a complete key/value pair.");
            }

            var present = PresentPrefix + name[PastPrefix.Length..];
            if (!languageModel.OutputMetadata.ContainsKey(present))
            {
                throw new InvalidOperationException($"The language model graph has no {present} output.");
            }
        }

        if (languageModel.OutputMetadata.Count == 0)
        {
            throw new InvalidOperationException("The language model graph has no logits output.");
        }
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

        /// <summary>True once <see cref="Dispose"/> has run on this state.</summary>
        private bool _disposed;

        /// <summary>Initializes a new instance of the <see cref="LanguageModelState"/> class.</summary>
        public LanguageModelState(OnnxChatterboxTurboNetworks owner)
        {
            _owner = owner;
        }

        /// <inheritdoc/>
        /// <exception cref="ObjectDisposedException">Thrown when this state or the owning networks were disposed.</exception>
        public float[] Step(float[] inputEmbeddings, int sequenceLength, int attentionLength, int firstPosition)
        {
            ArgumentNullException.ThrowIfNull(inputEmbeddings);
            ObjectDisposedException.ThrowIf(_disposed || _owner._disposed, this);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sequenceLength);
            ArgumentOutOfRangeException.ThrowIfNegative(firstPosition);
            if (inputEmbeddings.Length != (long)sequenceLength * OnnxChatterboxTurboSynthesisEngine.EmbeddingSize)
            {
                throw new ArgumentException(
                    $"The embeddings must contain sequenceLength * {OnnxChatterboxTurboSynthesisEngine.EmbeddingSize} values.",
                    nameof(inputEmbeddings));
            }

            if (attentionLength < sequenceLength)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(attentionLength),
                    attentionLength,
                    "The attention length must be at least the sequence length.");
            }

            var owner = _owner;
            var names = new List<string> { "inputs_embeds", "attention_mask", "position_ids" };
            var values = new List<OrtValue>();
            var temporaries = new List<OrtValue>();
            try
            {
                // Embeddings are float32 for the shipped graphs; convert when a graph declares float16
                temporaries.Add(owner._embeddingInputType == TensorElementType.Float16
                    ? CreateFloat16Tensor(
                        inputEmbeddings,
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
            _disposed = true;
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
                return HalfToFloats(logits.GetTensorDataAsSpan<Float16>().Slice(offset, vocabulary));
            }

            return logits.GetTensorDataAsSpan<float>().Slice(offset, vocabulary).ToArray();
        }
    }
}
