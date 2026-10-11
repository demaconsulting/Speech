using System.Text;

namespace DemaConsulting.Speech.Onnx.NemotronStt.Tests.RecognitionSubsystem;

/// <summary>
///     Minimal ONNX protobuf writer used by the synthetic model fixtures: just enough of ModelProto,
///     GraphProto, NodeProto, TensorProto and ValueInfoProto to describe small operator graphs.
/// </summary>
internal sealed class OnnxModelWriter
{
    /// <summary>The ONNX element type id for float.</summary>
    public const int Float = 1;

    /// <summary>The ONNX element type id for int64.</summary>
    public const int Int64 = 7;

    /// <summary>The graph being assembled.</summary>
    private readonly List<byte> _graph = [];

    /// <summary>Initializes a new instance of the <see cref="OnnxModelWriter"/> class.</summary>
    /// <param name="name">The graph name.</param>
    public OnnxModelWriter(string name)
    {
        _name = name;
    }

    /// <summary>The graph name.</summary>
    private readonly string _name;

    /// <summary>Adds a node.</summary>
    /// <param name="op">The operator type.</param>
    /// <param name="inputs">The input names.</param>
    /// <param name="outputs">The output names.</param>
    /// <param name="attribute">An optional encoded attribute.</param>
    /// <returns>This writer.</returns>
    public OnnxModelWriter Node(string op, string[] inputs, string[] outputs, List<byte>? attribute = null)
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

        Field(_graph, 1, node);
        return this;
    }

    /// <summary>Adds an int64 initializer.</summary>
    /// <param name="name">The tensor name.</param>
    /// <param name="values">The values.</param>
    /// <returns>This writer.</returns>
    public OnnxModelWriter Int64Initializer(string name, long[] values) =>
        Initializer(name, Int64, values.Length, values.SelectMany(BitConverter.GetBytes).ToArray());

    /// <summary>Adds a float initializer.</summary>
    /// <param name="name">The tensor name.</param>
    /// <param name="values">The values.</param>
    /// <returns>This writer.</returns>
    public OnnxModelWriter FloatInitializer(string name, float[] values) =>
        Initializer(name, Float, values.Length, values.SelectMany(BitConverter.GetBytes).ToArray());

    /// <summary>Adds a graph input; non-numeric dimensions become symbolic.</summary>
    /// <param name="name">The input name.</param>
    /// <param name="elementType">The ONNX element type.</param>
    /// <param name="dimensions">The dimensions.</param>
    /// <returns>This writer.</returns>
    public OnnxModelWriter Input(string name, int elementType, params string[] dimensions)
    {
        Field(_graph, 11, ValueInfo(name, elementType, dimensions));
        return this;
    }

    /// <summary>Adds a graph output.</summary>
    /// <param name="name">The output name.</param>
    /// <param name="elementType">The ONNX element type.</param>
    /// <param name="dimensions">The dimensions.</param>
    /// <returns>This writer.</returns>
    public OnnxModelWriter Output(string name, int elementType, params string[] dimensions)
    {
        Field(_graph, 12, ValueInfo(name, elementType, dimensions));
        return this;
    }

    /// <summary>Encodes an integer node attribute.</summary>
    /// <param name="name">The attribute name.</param>
    /// <param name="value">The attribute value.</param>
    /// <returns>The encoded attribute.</returns>
    public static List<byte> IntAttribute(string name, long value)
    {
        var attribute = new List<byte>();
        Field(attribute, 1, Encoding.UTF8.GetBytes(name));
        WriteVarIntField(attribute, 3, value);
        WriteVarIntField(attribute, 20, 2);
        return attribute;
    }

    /// <summary>Serializes the model (IR version 8, operator set 13).</summary>
    /// <returns>The ONNX model bytes.</returns>
    public byte[] Build()
    {
        var graph = new List<byte>(_graph);
        Field(graph, 2, Encoding.UTF8.GetBytes(_name));

        var operatorSet = new List<byte>();
        WriteVarIntField(operatorSet, 2, 13);

        var model = new List<byte>();
        WriteVarIntField(model, 1, 8);
        Field(model, 8, operatorSet);
        Field(model, 7, graph);
        return [.. model];
    }

    /// <summary>Adds an initializer.</summary>
    private OnnxModelWriter Initializer(string name, int elementType, int count, byte[] raw)
    {
        var tensor = new List<byte>();
        WriteVarIntField(tensor, 1, count);
        WriteVarIntField(tensor, 2, elementType);
        Field(tensor, 8, Encoding.UTF8.GetBytes(name));
        Field(tensor, 9, raw);
        Field(_graph, 5, tensor);
        return this;
    }

    /// <summary>Encodes a base-128 variable-length integer.</summary>
    private static void WriteVarInt(List<byte> buffer, long value)
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
    private static void WriteVarIntField(List<byte> buffer, int field, long value)
    {
        WriteVarInt(buffer, field << 3);
        WriteVarInt(buffer, value);
    }

    /// <summary>Appends a length-delimited field.</summary>
    private static void Field(List<byte> buffer, int field, IReadOnlyCollection<byte> payload)
    {
        WriteVarInt(buffer, (field << 3) | 2);
        WriteVarInt(buffer, payload.Count);
        buffer.AddRange(payload);
    }

    /// <summary>Encodes a ValueInfoProto.</summary>
    private static List<byte> ValueInfo(string name, int elementType, string[] dimensions)
    {
        var shape = new List<byte>();
        foreach (var dimension in dimensions)
        {
            var dim = new List<byte>();
            if (long.TryParse(dimension, out var value))
            {
                WriteVarIntField(dim, 1, value);
            }
            else
            {
                Field(dim, 2, Encoding.UTF8.GetBytes(dimension));
            }

            Field(shape, 1, dim);
        }

        var tensorType = new List<byte>();
        WriteVarIntField(tensorType, 1, elementType);
        Field(tensorType, 2, shape);

        var type = new List<byte>();
        Field(type, 1, tensorType);

        var info = new List<byte>();
        Field(info, 1, Encoding.UTF8.GetBytes(name));
        Field(info, 2, type);
        return info;
    }
}
