using System.Numerics;

namespace DemaConsulting.Speech.Phonemes.En.LetterToSoundSubsystem;

/// <summary>
///     A small integer-only neural network: sparse categorical inputs, two hidden layers, an argmax-able output
///     layer. The runtime of the letter-to-sound model.
/// </summary>
/// <remarks>
///     <para>
///     <b>Shape.</b> The input is a fixed number of categorical <em>groups</em> (for example "the letter two places
///     to the left"), each with its own number of values. Every group value owns a row of the embedding table; the
///     first layer is the sum of the chosen rows plus a bias (a sparse one-hot matrix product), then a rectifier, a
///     dense second layer and rectifier, and a dense output layer whose largest score wins.
///     </para>
///     <para>
///     <b>Why integers.</b> The weights were trained in floating point by the tools project and quantized there
///     (embedding rows and biases as 16-bit fixed point, dense weights as 8-bit, activations 0 to
///     <see cref="ActivationMax"/> between layers with an integer rescale of <c>multiplier / 2^24</c>). All the
///     arithmetic here is exact integer arithmetic, which is associative, so the result is bit-identical on every
///     platform and with every SIMD width: a pronunciation never depends on the machine, and the committed golden
///     of out-of-vocabulary words detects any change of the weights.
///     </para>
///     <para>
///     Immutable after construction; <see cref="Score"/> only reads the weights and writes the caller's buffers, so
///     one instance is safe for concurrent use.
///     </para>
/// </remarks>
internal sealed class LtsNetwork
{
    /// <summary>The largest activation between layers; the rescale clamps to <c>0..ActivationMax</c>.</summary>
    public const int ActivationMax = 2047;

    /// <summary>The number of fractional bits of the rescale multipliers.</summary>
    public const int RescaleShift = 24;

    /// <summary>The first row of every group in the embedding table.</summary>
    private readonly int[] _groupRows;

    /// <summary>The embedding table, row-major: one row of <c>_hidden1</c> values per group value.</summary>
    private readonly int[] _embedding;

    /// <summary>The first layer bias, in the embedding scale.</summary>
    private readonly int[] _bias1;

    /// <summary>The rescale multiplier of the first layer's output.</summary>
    private readonly int _multiplier1;

    /// <summary>The second layer weights, one row of <c>_hidden1</c> weights per hidden unit.</summary>
    private readonly int[] _weights2;

    /// <summary>The second layer bias, in the accumulator scale.</summary>
    private readonly int[] _bias2;

    /// <summary>The rescale multiplier of the second layer's output.</summary>
    private readonly int _multiplier2;

    /// <summary>The output layer weights, one row of <c>_hidden2</c> weights per output.</summary>
    private readonly int[] _weights3;

    /// <summary>The output layer bias, in the accumulator scale.</summary>
    private readonly int[] _bias3;

    /// <summary>The width of the first hidden layer.</summary>
    private readonly int _hidden1;

    /// <summary>The width of the second hidden layer.</summary>
    private readonly int _hidden2;

    /// <summary>
    ///     Initializes a network from its serialized form (see <see cref="Read"/>).
    /// </summary>
    /// <param name="reader">The reader positioned at the network.</param>
    /// <param name="expectedGroups">The group cardinalities the model's feature extraction produces.</param>
    /// <exception cref="InvalidDataException">Thrown when the serialized groups differ from <paramref name="expectedGroups"/> or a size is invalid.</exception>
    private LtsNetwork(BinaryReader reader, IReadOnlyList<int> expectedGroups)
    {
        // The groups must be exactly the ones the feature extraction fills
        var groupCount = reader.ReadByte();
        if (groupCount != expectedGroups.Count)
        {
            throw new InvalidDataException("The network has " + groupCount + " input groups instead of " + expectedGroups.Count + ".");
        }

        _groupRows = new int[groupCount + 1];
        for (var g = 0; g < groupCount; g++)
        {
            var cardinality = reader.ReadUInt16();
            if (cardinality != expectedGroups[g])
            {
                throw new InvalidDataException("Input group " + g + " has " + cardinality + " values instead of " + expectedGroups[g] + ".");
            }

            _groupRows[g + 1] = _groupRows[g] + cardinality;
        }

        _hidden1 = reader.ReadUInt16();
        _hidden2 = reader.ReadUInt16();
        Outputs = reader.ReadUInt16();
        if (_hidden1 == 0 || _hidden2 == 0 || Outputs == 0)
        {
            throw new InvalidDataException("The network has an empty layer.");
        }

        // First layer: embedding rows and biases, then the rescale
        _embedding = new int[_groupRows[groupCount] * _hidden1];
        for (var i = 0; i < _embedding.Length; i++)
        {
            _embedding[i] = reader.ReadInt16();
        }

        _bias1 = ReadInt32s(reader, _hidden1);
        _multiplier1 = reader.ReadInt32();

        // Second and output layers
        _weights2 = ReadSBytes(reader, _hidden2 * _hidden1);
        _bias2 = ReadInt32s(reader, _hidden2);
        _multiplier2 = reader.ReadInt32();
        _weights3 = ReadSBytes(reader, Outputs * _hidden2);
        _bias3 = ReadInt32s(reader, Outputs);
    }

    /// <summary>Gets the number of output scores.</summary>
    public int Outputs { get; }

    /// <summary>Gets the number of scratch values <see cref="Score"/> needs.</summary>
    public int ScratchSize => _hidden1 + _hidden2;

    /// <summary>
    ///     Reads a network.
    /// </summary>
    /// <param name="reader">The reader positioned at the network.</param>
    /// <param name="expectedGroups">The cardinality of every input group the feature extraction produces.</param>
    /// <returns>The network.</returns>
    /// <exception cref="InvalidDataException">Thrown when the data does not describe a network of the expected shape.</exception>
    public static LtsNetwork Read(BinaryReader reader, IReadOnlyList<int> expectedGroups)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(expectedGroups);
        return new LtsNetwork(reader, expectedGroups);
    }

    /// <summary>
    ///     Computes the output scores of one input.
    /// </summary>
    /// <param name="values">The chosen value of every group (the group index is the position in this span).</param>
    /// <param name="scratch">A buffer of at least <see cref="ScratchSize"/> values.</param>
    /// <param name="scores">Receives the <see cref="Outputs"/> scores; the largest wins and scores of different inputs are comparable.</param>
    public void Score(ReadOnlySpan<int> values, Span<int> scratch, Span<int> scores)
    {
        var hidden1 = scratch[.._hidden1];
        var hidden2 = scratch.Slice(_hidden1, _hidden2);

        // First layer: sum of the chosen embedding rows plus the bias, rectified and rescaled
        _bias1.CopyTo(hidden1);
        for (var g = 0; g < values.Length; g++)
        {
            Add(hidden1, _embedding.AsSpan((_groupRows[g] + values[g]) * _hidden1, _hidden1));
        }

        for (var j = 0; j < hidden1.Length; j++)
        {
            hidden1[j] = Rescale(hidden1[j], _multiplier1);
        }

        // Second layer
        for (var i = 0; i < _hidden2; i++)
        {
            hidden2[i] = Rescale(_bias2[i] + Dot(_weights2.AsSpan(i * _hidden1, _hidden1), hidden1), _multiplier2);
        }

        // Output layer
        for (var o = 0; o < Outputs; o++)
        {
            scores[o] = _bias3[o] + Dot(_weights3.AsSpan(o * _hidden2, _hidden2), hidden2);
        }
    }

    /// <summary>
    ///     Rectifies a value and rescales it to the activation range.
    /// </summary>
    /// <param name="value">The accumulator value.</param>
    /// <param name="multiplier">The multiplier of <c>1 / 2^24</c> units.</param>
    /// <returns>The activation, <c>0</c> to <see cref="ActivationMax"/>.</returns>
    private static int Rescale(int value, int multiplier)
    {
        if (value <= 0)
        {
            return 0;
        }

        var scaled = (((long)value * multiplier) + (1L << (RescaleShift - 1))) >> RescaleShift;
        return scaled > ActivationMax ? ActivationMax : (int)scaled;
    }

    /// <summary>
    ///     Adds a row to an accumulator, element by element.
    /// </summary>
    /// <param name="target">The accumulator.</param>
    /// <param name="row">The row to add; the same length as <paramref name="target"/>.</param>
    private static void Add(Span<int> target, ReadOnlySpan<int> row)
    {
        var i = 0;
        if (Vector.IsHardwareAccelerated)
        {
            for (; i <= target.Length - Vector<int>.Count; i += Vector<int>.Count)
            {
                (new Vector<int>(target[i..]) + new Vector<int>(row[i..])).CopyTo(target[i..]);
            }
        }

        for (; i < target.Length; i++)
        {
            target[i] += row[i];
        }
    }

    /// <summary>
    ///     Computes the dot product of two integer vectors; exact, so independent of the SIMD width.
    /// </summary>
    /// <param name="weights">The weights.</param>
    /// <param name="activations">The activations; the same length as <paramref name="weights"/>.</param>
    /// <returns>The sum of the products.</returns>
    private static int Dot(ReadOnlySpan<int> weights, ReadOnlySpan<int> activations)
    {
        var sum = 0;
        var i = 0;
        if (Vector.IsHardwareAccelerated && weights.Length >= Vector<int>.Count)
        {
            var accumulator = Vector<int>.Zero;
            for (; i <= weights.Length - Vector<int>.Count; i += Vector<int>.Count)
            {
                accumulator += new Vector<int>(weights[i..]) * new Vector<int>(activations[i..]);
            }

            sum = Vector.Sum(accumulator);
        }

        for (; i < weights.Length; i++)
        {
            sum += weights[i] * activations[i];
        }

        return sum;
    }

    /// <summary>
    ///     Reads little-endian 32-bit integers.
    /// </summary>
    /// <param name="reader">The reader.</param>
    /// <param name="count">The number of values.</param>
    /// <returns>The values.</returns>
    private static int[] ReadInt32s(BinaryReader reader, int count)
    {
        var values = new int[count];
        for (var i = 0; i < count; i++)
        {
            values[i] = reader.ReadInt32();
        }

        return values;
    }

    /// <summary>
    ///     Reads signed bytes into integers.
    /// </summary>
    /// <param name="reader">The reader.</param>
    /// <param name="count">The number of values.</param>
    /// <returns>The values.</returns>
    private static int[] ReadSBytes(BinaryReader reader, int count)
    {
        var values = new int[count];
        for (var i = 0; i < count; i++)
        {
            values[i] = reader.ReadSByte();
        }

        return values;
    }
}
