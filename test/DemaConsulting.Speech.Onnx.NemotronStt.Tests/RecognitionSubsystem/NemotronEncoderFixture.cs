using System.Text;

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
    /// <summary>The ONNX element type id for float.</summary>
    private const int Float = 1;

    /// <summary>The ONNX element type id for int64.</summary>
    private const int Int64 = 7;

    /// <summary>The weight applied to the language id in every output element.</summary>
    public const int LanguageWeight = 100;

    /// <summary>Builds the serialized model.</summary>
    /// <returns>The ONNX model bytes.</returns>
    public static byte[] Build()
    {
        var graph = new List<byte>();
        Field(graph, 1, Node("Identity", ["cache_last_channel"], ["cache_last_channel_next"]));
        Field(graph, 1, Node("Identity", ["cache_last_time"], ["cache_last_time_next"]));
        Field(graph, 1, Node("Mul", ["lang_id", "weight"], ["language_score"]));
        Field(graph, 1, Node("Add", ["cache_last_channel_len", "one"], ["cache_last_channel_len_next"]));
        Field(graph, 1, Node("Add", ["cache_last_channel_len_next", "language_score"], ["total"]));
        Field(graph, 1, Node("Cast", ["total"], ["total_float"], CastToFloat()));
        Field(graph, 1, Node("Reshape", ["total_float", "scalar_shape"], ["scalar"]));
        Field(graph, 1, Node("Expand", ["scalar", "output_shape"], ["outputs"]));
        Field(graph, 1, Node("Div", ["length", "nine"], ["encoded_lengths"]));
        Field(graph, 2, Encoding.UTF8.GetBytes("nemotron_encoder_fixture"));
        Field(graph, 5, Int64Tensor("weight", [LanguageWeight]));
        Field(graph, 5, Int64Tensor("one", [1]));
        Field(graph, 5, Int64Tensor("nine", [9]));
        Field(graph, 5, Int64Tensor("scalar_shape", [1, 1, 1]));
        Field(graph, 5, Int64Tensor("output_shape", [1, 7, 1024]));

        Field(graph, 11, ValueInfo("audio_signal", Float, ["1", "frames", "128"]));
        Field(graph, 11, ValueInfo("length", Int64, ["1"]));
        Field(graph, 11, ValueInfo("cache_last_channel", Float, ["1", "24", "56", "1024"]));
        Field(graph, 11, ValueInfo("cache_last_time", Float, ["1", "24", "1024", "8"]));
        Field(graph, 11, ValueInfo("cache_last_channel_len", Int64, ["1"]));
        Field(graph, 11, ValueInfo("lang_id", Int64, ["1"]));

        Field(graph, 12, ValueInfo("outputs", Float, ["1", "7", "1024"]));
        Field(graph, 12, ValueInfo("encoded_lengths", Int64, ["1"]));
        Field(graph, 12, ValueInfo("cache_last_channel_next", Float, ["1", "24", "56", "1024"]));
        Field(graph, 12, ValueInfo("cache_last_time_next", Float, ["1", "24", "1024", "8"]));
        Field(graph, 12, ValueInfo("cache_last_channel_len_next", Int64, ["1"]));

        var OperatorSet = new List<byte>();
        WriteVariableField(OperatorSet, 2, 13);

        var model = new List<byte>();
        WriteVariableField(model, 1, 8);
        Field(model, 8, OperatorSet);
        Field(model, 7, graph);
        return [.. model];
    }

    /// <summary>Encodes a base-128 WriteVariableField.</summary>
    private static void WriteVariableLengthInt(List<byte> buffer, long value)
    {
        var v = (ulong)value;
        while (v >= 0x80)
        {
            buffer.Add((byte)(v | 0x80));
            v >>= 7;
        }

        buffer.Add((byte)v);
    }

    /// <summary>Appends a variable-length integer field.</summary>
    private static void WriteVariableField(List<byte> buffer, int field, long value)
    {
        WriteVariableLengthInt(buffer, field << 3);
        WriteVariableLengthInt(buffer, value);
    }

    /// <summary>Appends a length-delimited field.</summary>
    private static void Field(List<byte> buffer, int field, IReadOnlyCollection<byte> payload)
    {
        WriteVariableLengthInt(buffer, (field << 3) | 2);
        WriteVariableLengthInt(buffer, payload.Count);
        buffer.AddRange(payload);
    }

    /// <summary>Encodes a NodeProto.</summary>
    private static List<byte> Node(string op, string[] inputs, string[] outputs, List<byte>? attribute = null)
    {
        var node = new List<byte>();
        foreach (var input in inputs)
        {
            Field(node, 1, Encoding.UTF8.GetBytes(input));
        }

        foreach (var output in outputs)
        {
            Field(node, 2, Encoding.UTF8.GetBytes(output));
        }

        Field(node, 3, Encoding.UTF8.GetBytes(outputs[0] + "_node"));
        Field(node, 4, Encoding.UTF8.GetBytes(op));
        if (attribute is not null)
        {
            Field(node, 5, attribute);
        }

        return node;
    }

    /// <summary>Encodes the Cast node's <c>to = float</c> attribute.</summary>
    private static List<byte> CastToFloat()
    {
        var attribute = new List<byte>();
        Field(attribute, 1, Encoding.UTF8.GetBytes("to"));
        WriteVariableField(attribute, 3, Float);
        WriteVariableField(attribute, 20, 2);
        return attribute;
    }

    /// <summary>Encodes a one-dimensional int64 initializer.</summary>
    private static List<byte> Int64Tensor(string name, long[] values)
    {
        var tensor = new List<byte>();
        WriteVariableField(tensor, 1, values.Length);
        WriteVariableField(tensor, 2, Int64);
        Field(tensor, 8, Encoding.UTF8.GetBytes(name));
        Field(tensor, 9, values.SelectMany(BitConverter.GetBytes).ToArray());
        return tensor;
    }

    /// <summary>Encodes a ValueInfoProto; non-numeric dimensions become symbolic.</summary>
    private static List<byte> ValueInfo(string name, int elementType, string[] dimensions)
    {
        var shape = new List<byte>();
        foreach (var dimension in dimensions)
        {
            var dim = new List<byte>();
            if (long.TryParse(dimension, out var value))
            {
                WriteVariableField(dim, 1, value);
            }
            else
            {
                Field(dim, 2, Encoding.UTF8.GetBytes(dimension));
            }

            Field(shape, 1, dim);
        }

        var tensorType = new List<byte>();
        WriteVariableField(tensorType, 1, elementType);
        Field(tensorType, 2, shape);

        var type = new List<byte>();
        Field(type, 1, tensorType);

        var info = new List<byte>();
        Field(info, 1, Encoding.UTF8.GetBytes(name));
        Field(info, 2, type);
        return info;
    }
}
