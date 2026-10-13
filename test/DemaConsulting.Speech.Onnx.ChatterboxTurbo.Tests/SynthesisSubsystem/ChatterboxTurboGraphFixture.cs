namespace DemaConsulting.Speech.Onnx.ChatterboxTurbo.Tests.SynthesisSubsystem;

/// <summary>
///     Builds tiny synthetic ONNX graphs whose input and output names, types and ranks match the real
///     Chatterbox Turbo graph contracts, so the ONNX Runtime wiring is tested without the large models.
/// </summary>
internal static class ChatterboxTurboGraphFixture
{
    /// <summary>The embedding width the engine and networks hard-code.</summary>
    public const int EmbeddingSize = 1024;

    /// <summary>The synthetic vocabulary of the language model's logits.</summary>
    public const int Vocabulary = 8;

    /// <summary>Number of rows in the synthetic embedding table.</summary>
    public const int TableRows = 16;

    /// <summary>The constant conditioning prompt tokens emitted by the encoder.</summary>
    public static readonly long[] PromptTokens = [10, 11, 12];

    /// <summary>Writes the four synthetic graphs (and empty weights files) into <paramref name="directory"/>.</summary>
    /// <param name="directory">The directory to fill.</param>
    /// <param name="languageModelName">The language model base name (for example language_model_fp16).</param>
    /// <param name="auxiliarySuffix">The auxiliary suffix (for example _q4).</param>
    /// <param name="halfCache">True for a float16 key/value cache, false for float32.</param>
    /// <param name="namedEncoderOutputs">False to give the encoder differently named (but four) outputs.</param>
    /// <param name="halfEmbeddings">True for a float16 language model <c>inputs_embeds</c> input.</param>
    /// <param name="halfLogits">True for float16 language model logits.</param>
    /// <param name="halfAuxiliary">True for float16 encoder, embedder and decoder outputs.</param>
    /// <param name="speakerSensitiveDecoder">
    ///     True to make the decoder waveform depend on its speaker inputs: each sample is the speech token plus the
    ///     sum of the speaker embeddings plus the sum of the speaker features.
    /// </param>
    /// <returns>The speech encoder, embedder, language model and decoder paths.</returns>
    public static (string Encoder, string Embedder, string LanguageModel, string Decoder) Write(
        string directory,
        string languageModelName = "language_model_fp16",
        string auxiliarySuffix = "_q4",
        bool halfCache = true,
        bool namedEncoderOutputs = true,
        bool halfEmbeddings = false,
        bool halfLogits = false,
        bool halfAuxiliary = false,
        bool speakerSensitiveDecoder = false)
    {
        Directory.CreateDirectory(directory);
        var encoder = Path.Combine(directory, "speech_encoder" + auxiliarySuffix + ".onnx");
        var embedder = Path.Combine(directory, "embed_tokens" + auxiliarySuffix + ".onnx");
        var languageModel = Path.Combine(directory, languageModelName + ".onnx");
        var decoder = Path.Combine(directory, "conditional_decoder" + auxiliarySuffix + ".onnx");

        File.WriteAllBytes(encoder, Encoder(namedEncoderOutputs, halfAuxiliary));
        File.WriteAllBytes(embedder, Embedder(halfAuxiliary));
        File.WriteAllBytes(languageModel, LanguageModel(halfCache, withPastInputs: true, halfEmbeddings, halfLogits));
        File.WriteAllBytes(decoder, Decoder(halfAuxiliary, speakerSensitiveDecoder));
        foreach (var path in new[] { encoder, embedder, languageModel, decoder })
        {
            File.WriteAllBytes(path + "_data", []);
        }

        return (encoder, embedder, languageModel, decoder);
    }

    /// <summary>Builds the speech encoder: constants for the four outputs.</summary>
    /// <param name="namedOutputs">True to name the outputs as the real graph does; false for generic names.</param>
    /// <param name="half">True to emit float16 outputs.</param>
    /// <returns>The serialized graph.</returns>
    public static byte[] Encoder(bool namedOutputs, bool half = false)
    {
        var names = namedOutputs
            ? new[] { "audio_features", "audio_tokens", "speaker_embeddings", "speaker_features" }
            : ["out_a", "out_b", "out_c", "out_d"];
        var floatType = half ? OnnxModelWriter.Float16 : OnnxModelWriter.Float;
        var writer = new OnnxModelWriter("encoder")
            .Input("audio_values", OnnxModelWriter.Float, "1", "n")
            .FloatInitializer("c_features", [1, 2, EmbeddingSize], Enumerable.Repeat(0.5f, 2 * EmbeddingSize).ToArray())
            .Int64Initializer("c_tokens", [1, PromptTokens.Length], PromptTokens)
            .FloatInitializer("c_embeddings", [1, 4], [1f, 2f, 3f, 4f])
            .FloatInitializer("c_speaker", [1, 3, 2], [1f, 2f, 3f, 4f, 5f, 6f])
            .Node("Identity", ["c_tokens"], [names[1]])
            .Output(names[0], floatType, "1", "2", "1024")
            .Output(names[1], OnnxModelWriter.Int64, "1", "3")
            .Output(names[2], floatType, "1", "4")
            .Output(names[3], floatType, "1", "3", "2");
        foreach (var (source, target) in new[] { ("c_features", names[0]), ("c_embeddings", names[2]), ("c_speaker", names[3]) })
        {
            if (half)
            {
                writer.Node("Cast", [source], [target], OnnxModelWriter.IntAttribute("to", OnnxModelWriter.Float16));
            }
            else
            {
                writer.Node("Identity", [source], [target]);
            }
        }

        return writer.Build();
    }

    /// <summary>Builds the embedding graph: ids wrap modulo the table size; row <c>i</c> holds <c>i / 1000</c> in every column.</summary>
    /// <param name="half">True to emit float16 output.</param>
    /// <returns>The serialized graph.</returns>
    public static byte[] Embedder(bool half = false)
    {
        var table = new float[TableRows * EmbeddingSize];
        for (var row = 0; row < TableRows; row++)
        {
            Array.Fill(table, row / 1000f, row * EmbeddingSize, EmbeddingSize);
        }

        var writer = new OnnxModelWriter("embedder")
            .Input("input_ids", OnnxModelWriter.Int64, "1", "s")
            .FloatInitializer("table", [TableRows, EmbeddingSize], table)
            .Int64Initializer("rows", [1], [TableRows])
            .Node("Mod", ["input_ids", "rows"], ["wrapped_ids"]);
        if (half)
        {
            return writer
                .Node("Gather", ["table", "wrapped_ids"], ["gathered"])
                .Node("Cast", ["gathered"], ["inputs_embeds"], OnnxModelWriter.IntAttribute("to", OnnxModelWriter.Float16))
                .Output("inputs_embeds", OnnxModelWriter.Float16, "1", "s", "1024")
                .Build();
        }

        return writer
            .Node("Gather", ["table", "wrapped_ids"], ["inputs_embeds"])
            .Output("inputs_embeds", OnnxModelWriter.Float, "1", "s", "1024")
            .Build();
    }

    /// <summary>
    ///     Builds the language model: logits are a matmul with a table where column <c>j</c> holds <c>j / 1000</c>;
    ///     each present tensor is the past tensor with one constant position appended.
    /// </summary>
    /// <param name="halfCache">True for a float16 cache.</param>
    /// <param name="withPastInputs">False to omit the cache inputs (an invalid graph).</param>
    /// <param name="halfEmbeddings">True to declare a float16 <c>inputs_embeds</c> input (cast to float32 inside the graph).</param>
    /// <param name="halfLogits">True to emit float16 logits (cast from float32 inside the graph).</param>
    /// <returns>The serialized graph.</returns>
    public static byte[] LanguageModel(bool halfCache, bool withPastInputs, bool halfEmbeddings = false, bool halfLogits = false)
    {
        var projection = new float[EmbeddingSize * Vocabulary];
        for (var row = 0; row < EmbeddingSize; row++)
        {
            for (var column = 0; column < Vocabulary; column++)
            {
                projection[(row * Vocabulary) + column] = column / 1000f;
            }
        }

        var type = halfCache ? OnnxModelWriter.Float16 : OnnxModelWriter.Float;
        var writer = new OnnxModelWriter("language_model")
            .Input("inputs_embeds", halfEmbeddings ? OnnxModelWriter.Float16 : OnnxModelWriter.Float, "1", "s", "1024")
            .Input("attention_mask", OnnxModelWriter.Int64, "1", "a")
            .Input("position_ids", OnnxModelWriter.Int64, "1", "s")
            .FloatInitializer("projection", [EmbeddingSize, Vocabulary], projection);

        // Half-precision inputs and outputs are converted around the float32 matmul (CPU MatMul has no float16 kernel)
        var matmulInput = "inputs_embeds";
        if (halfEmbeddings)
        {
            writer.Node("Cast", ["inputs_embeds"], ["embeds_float"], OnnxModelWriter.IntAttribute("to", OnnxModelWriter.Float));
            matmulInput = "embeds_float";
        }

        if (halfLogits)
        {
            writer
                .Node("MatMul", [matmulInput, "projection"], ["logits_float"])
                .Node("Cast", ["logits_float"], ["logits"], OnnxModelWriter.IntAttribute("to", OnnxModelWriter.Float16))
                .Output("logits", OnnxModelWriter.Float16, "1", "s", Vocabulary.ToString());
        }
        else
        {
            writer
                .Node("MatMul", [matmulInput, "projection"], ["logits"])
                .Output("logits", OnnxModelWriter.Float, "1", "s", Vocabulary.ToString());
        }

        if (!withPastInputs)
        {
            return writer.Build();
        }

        if (halfCache)
        {
            writer.HalfInitializer("step", [1, 2, 1, 4], Enumerable.Repeat((Half)0.25f, 8).ToArray());
        }
        else
        {
            writer.FloatInitializer("step", [1, 2, 1, 4], Enumerable.Repeat(0.25f, 8).ToArray());
        }

        foreach (var kind in new[] { "key", "value" })
        {
            writer
                .Input($"past_key_values.0.{kind}", type, "1", "2", "p", "4")
                .Node("Concat", [$"past_key_values.0.{kind}", "step"], [$"present.0.{kind}"], OnnxModelWriter.IntAttribute("axis", 2))
                .Output($"present.0.{kind}", type, "1", "2", "q", "4");
        }

        return writer.Build();
    }

    /// <summary>
    ///     Builds the decoder: the waveform is the speech tokens cast to float, plus (when
    ///     <paramref name="speakerSensitive"/> is set) the sum of the speaker embeddings and the sum of the speaker
    ///     features, so the waveform proves the conditioning reached the decoder.
    /// </summary>
    /// <param name="half">True to emit a float16 waveform.</param>
    /// <param name="speakerSensitive">True to make the waveform depend on the speaker inputs.</param>
    /// <returns>The serialized graph.</returns>
    public static byte[] Decoder(bool half = false, bool speakerSensitive = false)
    {
        var writer = new OnnxModelWriter("decoder")
            .Input("speech_tokens", OnnxModelWriter.Int64, "1", "n")
            .Input("speaker_embeddings", OnnxModelWriter.Float, "1", "4")
            .Input("speaker_features", OnnxModelWriter.Float, "1", "f", "2");
        var floatType = half ? OnnxModelWriter.Float16 : OnnxModelWriter.Float;
        if (!speakerSensitive)
        {
            return writer
                .Node("Cast", ["speech_tokens"], ["waveform"], OnnxModelWriter.IntAttribute("to", floatType))
                .Output("waveform", floatType, "1", "n")
                .Build();
        }

        // waveform = tokens + sum(speaker_embeddings) + sum(speaker_features); the sums reduce over every axis
        return writer
            .Node("Cast", ["speech_tokens"], ["token_values"], OnnxModelWriter.IntAttribute("to", OnnxModelWriter.Float))
            .Node("ReduceSum", ["speaker_embeddings"], ["embedding_sum"], OnnxModelWriter.IntAttribute("keepdims", 0))
            .Node("ReduceSum", ["speaker_features"], ["feature_sum"], OnnxModelWriter.IntAttribute("keepdims", 0))
            .Node("Add", ["embedding_sum", "feature_sum"], ["speaker_sum"])
            .Node("Add", ["token_values", "speaker_sum"], ["sum_values"])
            .Node("Cast", ["sum_values"], ["waveform"], OnnxModelWriter.IntAttribute("to", floatType))
            .Output("waveform", floatType, "1", "n")
            .Build();
    }
}
