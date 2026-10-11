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
    /// <returns>The speech encoder, embedder, language model and decoder paths.</returns>
    public static (string Encoder, string Embedder, string LanguageModel, string Decoder) Write(
        string directory,
        string languageModelName = "language_model_fp16",
        string auxiliarySuffix = "_q4",
        bool halfCache = true,
        bool namedEncoderOutputs = true)
    {
        Directory.CreateDirectory(directory);
        var encoder = Path.Combine(directory, "speech_encoder" + auxiliarySuffix + ".onnx");
        var embedder = Path.Combine(directory, "embed_tokens" + auxiliarySuffix + ".onnx");
        var languageModel = Path.Combine(directory, languageModelName + ".onnx");
        var decoder = Path.Combine(directory, "conditional_decoder" + auxiliarySuffix + ".onnx");

        File.WriteAllBytes(encoder, Encoder(namedEncoderOutputs));
        File.WriteAllBytes(embedder, Embedder());
        File.WriteAllBytes(languageModel, LanguageModel(halfCache, withPastInputs: true));
        File.WriteAllBytes(decoder, Decoder());
        foreach (var path in new[] { encoder, embedder, languageModel, decoder })
        {
            File.WriteAllBytes(path + "_data", []);
        }

        return (encoder, embedder, languageModel, decoder);
    }

    /// <summary>Builds the speech encoder: constants for the four outputs.</summary>
    public static byte[] Encoder(bool namedOutputs)
    {
        var names = namedOutputs
            ? new[] { "audio_features", "audio_tokens", "speaker_embeddings", "speaker_features" }
            : ["out_a", "out_b", "out_c", "out_d"];
        return new OnnxModelWriter("encoder")
            .Input("audio_values", OnnxModelWriter.Float, "1", "n")
            .FloatInitializer("c_features", [1, 2, EmbeddingSize], Enumerable.Repeat(0.5f, 2 * EmbeddingSize).ToArray())
            .Int64Initializer("c_tokens", [1, PromptTokens.Length], PromptTokens)
            .FloatInitializer("c_embeddings", [1, 4], [1f, 2f, 3f, 4f])
            .FloatInitializer("c_speaker", [1, 3, 2], [1f, 2f, 3f, 4f, 5f, 6f])
            .Node("Identity", ["c_features"], [names[0]])
            .Node("Identity", ["c_tokens"], [names[1]])
            .Node("Identity", ["c_embeddings"], [names[2]])
            .Node("Identity", ["c_speaker"], [names[3]])
            .Output(names[0], OnnxModelWriter.Float, "1", "2", "1024")
            .Output(names[1], OnnxModelWriter.Int64, "1", "3")
            .Output(names[2], OnnxModelWriter.Float, "1", "4")
            .Output(names[3], OnnxModelWriter.Float, "1", "3", "2")
            .Build();
    }

    /// <summary>Builds the embedding graph: ids wrap modulo the table size; row <c>i</c> holds <c>i / 1000</c> in every column.</summary>
    public static byte[] Embedder()
    {
        var table = new float[TableRows * EmbeddingSize];
        for (var row = 0; row < TableRows; row++)
        {
            Array.Fill(table, row / 1000f, row * EmbeddingSize, EmbeddingSize);
        }

        return new OnnxModelWriter("embedder")
            .Input("input_ids", OnnxModelWriter.Int64, "1", "s")
            .FloatInitializer("table", [TableRows, EmbeddingSize], table)
            .Int64Initializer("rows", [1], [TableRows])
            .Node("Mod", ["input_ids", "rows"], ["wrapped_ids"])
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
    public static byte[] LanguageModel(bool halfCache, bool withPastInputs)
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
            .Input("inputs_embeds", OnnxModelWriter.Float, "1", "s", "1024")
            .Input("attention_mask", OnnxModelWriter.Int64, "1", "a")
            .Input("position_ids", OnnxModelWriter.Int64, "1", "s")
            .FloatInitializer("projection", [EmbeddingSize, Vocabulary], projection)
            .Node("MatMul", ["inputs_embeds", "projection"], ["logits"])
            .Output("logits", OnnxModelWriter.Float, "1", "s", Vocabulary.ToString());

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

    /// <summary>Builds the decoder: the waveform is the speech tokens cast to float.</summary>
    public static byte[] Decoder() =>
        new OnnxModelWriter("decoder")
            .Input("speech_tokens", OnnxModelWriter.Int64, "1", "n")
            .Input("speaker_embeddings", OnnxModelWriter.Float, "1", "4")
            .Input("speaker_features", OnnxModelWriter.Float, "1", "f", "2")
            .Node("Cast", ["speech_tokens"], ["waveform"], OnnxModelWriter.IntAttribute("to", OnnxModelWriter.Float))
            .Output("waveform", OnnxModelWriter.Float, "1", "n")
            .Build();
}
