namespace DemaConsulting.Speech.Onnx.NemotronStt.Tests.RecognitionSubsystem;

/// <summary>
///     Builds a tiny ONNX model that mimics the Nemotron encoder's tensor contract (names, ranks,
///     cache shapes) with trivial operators, so the production cache wiring runs in tests without
///     the real ~790 MB model.
/// </summary>
/// <remarks>
///     Behavior: the two big caches are passed through unchanged, <c>cache_last_channel_len</c>
///     is incremented on every call, <c>encoded_lengths</c> is <c>length / 9</c> (7 for the real
///     65-frame input), and every element of <c>outputs</c> ([1, 7, 1024]) equals
///     <c>cache_last_channel_len_next + 100 * lang_id</c>. The output therefore reveals how many
///     calls the caches have flowed through and which language id was fed.
/// </remarks>
internal static class NemotronEncoderFixture
{
    /// <summary>The weight applied to the language id in every output element.</summary>
    public const int LanguageWeight = 100;

    /// <summary>Builds the serialized model.</summary>
    /// <returns>The ONNX model bytes.</returns>
    public static byte[] Build() =>
        new OnnxModelWriter("nemotron_encoder_fixture")
            .Node("Identity", ["cache_last_channel"], ["cache_last_channel_next"])
            .Node("Identity", ["cache_last_time"], ["cache_last_time_next"])
            .Node("Mul", ["lang_id", "weight"], ["language_score"])
            .Node("Add", ["cache_last_channel_len", "one"], ["cache_last_channel_len_next"])
            .Node("Add", ["cache_last_channel_len_next", "language_score"], ["total"])
            .Node("Cast", ["total"], ["total_float"], OnnxModelWriter.IntAttribute("to", OnnxModelWriter.Float))
            .Node("Reshape", ["total_float", "scalar_shape"], ["scalar"])
            .Node("Expand", ["scalar", "output_shape"], ["outputs"])
            .Node("Div", ["length", "nine"], ["encoded_lengths"])
            .Int64Initializer("weight", [LanguageWeight])
            .Int64Initializer("one", [1])
            .Int64Initializer("nine", [9])
            .Int64Initializer("scalar_shape", [1, 1, 1])
            .Int64Initializer("output_shape", [1, 7, 1024])
            .Input("audio_signal", OnnxModelWriter.Float, "1", "frames", "128")
            .Input("length", OnnxModelWriter.Int64, "1")
            .Input("cache_last_channel", OnnxModelWriter.Float, "1", "24", "56", "1024")
            .Input("cache_last_time", OnnxModelWriter.Float, "1", "24", "1024", "8")
            .Input("cache_last_channel_len", OnnxModelWriter.Int64, "1")
            .Input("lang_id", OnnxModelWriter.Int64, "1")
            .Output("outputs", OnnxModelWriter.Float, "1", "7", "1024")
            .Output("encoded_lengths", OnnxModelWriter.Int64, "1")
            .Output("cache_last_channel_next", OnnxModelWriter.Float, "1", "24", "56", "1024")
            .Output("cache_last_time_next", OnnxModelWriter.Float, "1", "24", "1024", "8")
            .Output("cache_last_channel_len_next", OnnxModelWriter.Int64, "1")
            .Build();
}
