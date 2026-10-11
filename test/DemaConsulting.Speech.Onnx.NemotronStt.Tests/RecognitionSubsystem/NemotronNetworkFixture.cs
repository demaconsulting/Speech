namespace DemaConsulting.Speech.Onnx.NemotronStt.Tests.RecognitionSubsystem;

/// <summary>
///     Builds tiny ONNX models that mimic the Nemotron decoder and joint networks' tensor contracts
///     (names, ranks, state shapes) with trivial operators, so the production RNN-T wiring runs in
///     tests without the real model.
/// </summary>
/// <remarks>
///     Decoder: every element of <c>decoder_output</c> equals
///     <c>target + max(h_in) + 10 * max(c_in)</c>; <c>h_out = h_in + 2</c> and <c>c_out = c_in + 1</c>.
///     The output therefore reveals the fed token and how far each recurrent state has flowed.
///     Joint: the logits over a <see cref="Vocabulary"/>-token vocabulary are
///     <c>-(index - s)^2</c> with <c>s = max(encoder_output) + max(decoder_output)</c>, so the
///     arg-max (before the blank penalty) is the integer value of <c>s</c>.
/// </remarks>
internal static class NemotronNetworkFixture
{
    /// <summary>The ReduceMax attribute that collapses the reduced axes.</summary>
    private static readonly List<byte> NoKeepDimensions = OnnxModelWriter.IntAttribute("keepdims", 0); // cspell:disable-line

    /// <summary>The joint vocabulary size.</summary>
    public const int Vocabulary = 64;

    /// <summary>Builds the serialized decoder model.</summary>
    /// <returns>The ONNX model bytes.</returns>
    public static byte[] BuildDecoder() =>
        new OnnxModelWriter("nemotron_decoder_fixture")
            .Node("ReduceMax", ["h_in"], ["max_h"], NoKeepDimensions)
            .Node("ReduceMax", ["c_in"], ["max_c"], NoKeepDimensions)
            .Node("Mul", ["max_c", "ten"], ["cell_score"])
            .Node("Add", ["max_h", "cell_score"], ["state_score"])
            .Node("Cast", ["targets"], ["target_float"], OnnxModelWriter.IntAttribute("to", OnnxModelWriter.Float))
            .Node("Add", ["target_float", "state_score"], ["total"])
            .Node("Reshape", ["total", "scalar_shape"], ["scalar"])
            .Node("Expand", ["scalar", "output_shape"], ["decoder_output"])
            .Node("Add", ["h_in", "two"], ["h_out"])
            .Node("Add", ["c_in", "one"], ["c_out"])
            .FloatInitializer("ten", [10f])
            .FloatInitializer("two", [2f])
            .FloatInitializer("one", [1f])
            .Int64Initializer("scalar_shape", [1, 1, 1])
            .Int64Initializer("output_shape", [1, 640, 1])
            .Input("targets", OnnxModelWriter.Int64, "1", "1")
            .Input("h_in", OnnxModelWriter.Float, "2", "1", "640")
            .Input("c_in", OnnxModelWriter.Float, "2", "1", "640")
            .Output("decoder_output", OnnxModelWriter.Float, "1", "640", "1")
            .Output("h_out", OnnxModelWriter.Float, "2", "1", "640")
            .Output("c_out", OnnxModelWriter.Float, "2", "1", "640")
            .Build();

    /// <summary>Builds the serialized joint model.</summary>
    /// <returns>The ONNX model bytes.</returns>
    public static byte[] BuildJoint() =>
        new OnnxModelWriter("nemotron_joint_fixture")
            .Node("ReduceMax", ["encoder_output"], ["max_e"], NoKeepDimensions)
            .Node("ReduceMax", ["decoder_output"], ["max_d"], NoKeepDimensions)
            .Node("Add", ["max_e", "max_d"], ["s"])
            .Node("Sub", ["index", "s"], ["distance"])
            .Node("Mul", ["distance", "distance"], ["square"])
            .Node("Neg", ["square"], ["flat_logits"])
            .Node("Reshape", ["flat_logits", "logits_shape"], ["joint_output"])
            .FloatInitializer("index", [.. Enumerable.Range(0, Vocabulary).Select(i => (float)i)])
            .Int64Initializer("logits_shape", [1, 1, 1, Vocabulary])
            .Input("encoder_output", OnnxModelWriter.Float, "1", "1", "1024")
            .Input("decoder_output", OnnxModelWriter.Float, "1", "1", "640")
            .Output("joint_output", OnnxModelWriter.Float, "1", "1", "1", Vocabulary.ToString())
            .Build();
}
