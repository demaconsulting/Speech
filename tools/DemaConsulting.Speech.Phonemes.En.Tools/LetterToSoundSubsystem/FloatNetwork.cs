using System.Runtime.Intrinsics;

namespace DemaConsulting.Speech.Phonemes.En.Tools.LetterToSoundSubsystem;

/// <summary>
///     The training data of one network: the input values of every sample and its label.
/// </summary>
/// <param name="Groups">The number of input groups per sample.</param>
/// <param name="Values">The input values, <paramref name="Groups"/> per sample, sample after sample.</param>
/// <param name="Labels">The class of every sample.</param>
internal sealed record TrainingSet(int Groups, int[] Values, int[] Labels)
{
    /// <summary>Gets the number of samples.</summary>
    public int Count => Labels.Length;
}

/// <summary>
///     The settings of one network.
/// </summary>
/// <param name="Hidden1">The width of the first hidden layer.</param>
/// <param name="Hidden2">The width of the second hidden layer.</param>
/// <param name="Epochs">The number of passes over the training data.</param>
/// <param name="LearningRate">The initial Adam learning rate (it decays linearly to a fiftieth).</param>
/// <param name="BatchSize">The number of samples per update (a multiple of 8).</param>
/// <param name="Seed">The seed of initialization and shuffling.</param>
/// <param name="Dropout">The fraction of the hidden units dropped during training (0 for none); the mask is a hash of the sample number, not a random stream, so it does not depend on the thread that computes the sample.</param>
internal sealed record NetworkSettings(int Hidden1, int Hidden2, int Epochs, float LearningRate, int BatchSize, ulong Seed, float Dropout = 0f);

/// <summary>
///     A 64-bit mixing generator (the SplitMix construction): tiny, fast and identical on every platform, so initialization and shuffling are reproducible.
/// </summary>
internal sealed class DeterministicRandom
{
    /// <summary>The state.</summary>
    private ulong _state;

    /// <summary>
    ///     Initializes a generator.
    /// </summary>
    /// <param name="seed">The seed.</param>
    public DeterministicRandom(ulong seed) => _state = seed;

    /// <summary>
    ///     Gets the next 64 random bits.
    /// </summary>
    /// <returns>The bits.</returns>
    public ulong NextUInt64()
    {
        _state += 0x9E3779B97F4A7C15UL;
        var z = _state;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }

    /// <summary>
    ///     Gets a float in <c>[-1, 1)</c>.
    /// </summary>
    /// <returns>The value.</returns>
    public float NextSignedFloat() => (((NextUInt64() >> 40) / 16777216f) * 2f) - 1f;

    /// <summary>
    ///     Gets an integer in <c>[0, bound)</c>.
    /// </summary>
    /// <param name="bound">The exclusive bound, positive.</param>
    /// <returns>The value.</returns>
    public int NextInt(int bound) => (int)(NextUInt64() % (ulong)bound);
}

/// <summary>
///     The floating-point twin of the shipped integer network: the same structure, trained here with Adam and then
///     quantized into the serialized form <c>LtsNetwork</c> reads.
/// </summary>
/// <remarks>
///     <para>
///     <b>Determinism.</b> The result must be a pure function of the data and the settings, because the committed
///     model is verified by retraining. Every random choice comes from <see cref="DeterministicRandom"/>; vector code
///     uses 128-bit vectors with a fixed reduction order (four lanes, summed first two and last two, then together);
///     <see cref="Exp"/> is a polynomial in double arithmetic, not the platform's library; the learning-rate decay is
///     linear. Training is parallel but order-independent: a batch is cut into a fixed number of shards, every shard
///     accumulates its own gradient sequentially, and the shard gradients are added in shard order, so the number of
///     processor cores has no effect on the result.
///     </para>
/// </remarks>
internal sealed class FloatNetwork
{
    /// <summary>The number of gradient shards of a batch (fixed, independent of the machine).</summary>
    private const int Shards = 8;

    /// <summary>The gradient components smaller than this in the output layer are skipped (speed; deterministic).</summary>
    private const float SkipBelow = 1e-5f;

    /// <summary>The cardinality of every input group.</summary>
    private readonly int[] _groups;

    /// <summary>The first embedding row of every group.</summary>
    private readonly int[] _groupRows;

    /// <summary>The embedding table.</summary>
    private readonly float[] _embedding;

    /// <summary>The first layer bias.</summary>
    private readonly float[] _bias1;

    /// <summary>The second layer weights, one row of <c>_hidden1</c> per unit.</summary>
    private readonly float[] _weights2;

    /// <summary>The second layer bias.</summary>
    private readonly float[] _bias2;

    /// <summary>The output weights, one row of <c>_hidden2</c> per output.</summary>
    private readonly float[] _weights3;

    /// <summary>The output bias.</summary>
    private readonly float[] _bias3;

    /// <summary>The width of the first hidden layer.</summary>
    private readonly int _hidden1;

    /// <summary>The width of the second hidden layer.</summary>
    private readonly int _hidden2;

    /// <summary>The number of outputs.</summary>
    private readonly int _outputs;

    /// <summary>
    ///     Initializes a network with small random weights.
    /// </summary>
    /// <param name="groups">The cardinality of every input group.</param>
    /// <param name="hidden1">The width of the first hidden layer.</param>
    /// <param name="hidden2">The width of the second hidden layer.</param>
    /// <param name="outputs">The number of outputs.</param>
    /// <param name="seed">The seed of the initial weights.</param>
    public FloatNetwork(int[] groups, int hidden1, int hidden2, int outputs, ulong seed)
    {
        _groups = groups;
        _hidden1 = hidden1;
        _hidden2 = hidden2;
        _outputs = outputs;
        _groupRows = new int[groups.Length + 1];
        for (var g = 0; g < groups.Length; g++)
        {
            _groupRows[g + 1] = _groupRows[g] + groups[g];
        }

        var random = new DeterministicRandom(seed);
        _embedding = Initialize(_groupRows[groups.Length] * hidden1, 0.3f / MathF.Sqrt(groups.Length), random);
        _bias1 = new float[hidden1];
        _weights2 = Initialize(hidden2 * hidden1, MathF.Sqrt(6f / hidden1), random);
        _bias2 = new float[hidden2];
        _weights3 = Initialize(outputs * hidden2, MathF.Sqrt(6f / (hidden2 + outputs)), random);
        _bias3 = new float[outputs];
    }

    /// <summary>
    ///     Computes <c>e^x</c> for <c>x &lt;= 0</c> in plain double arithmetic (identical on every platform).
    /// </summary>
    /// <param name="x">The exponent; values below <c>-60</c> give about zero.</param>
    /// <returns>The exponential.</returns>
    public static double Exp(double x)
    {
        if (x < -60)
        {
            return 0;
        }

        // e^x = 2^t with t = x * log2(e); split t into an integer k and a fraction f in [0, 1)
        var t = x * 1.4426950408889634;
        var k = Math.Floor(t);
        var f = t - k;

        // 2^f = e^(f ln 2), Taylor series to the tenth term (error below 1e-10)
        var y = f * 0.6931471805599453;
        var sum = 1.0;
        var term = 1.0;
        for (var n = 1; n <= 12; n++)
        {
            term *= y / n;
            sum += term;
        }

        return sum * BitConverter.Int64BitsToDouble((long)(k + 1023) << 52);
    }

    /// <summary>
    ///     Trains the network.
    /// </summary>
    /// <param name="data">The training samples.</param>
    /// <param name="settings">The settings; the hidden widths must be the ones of this instance.</param>
    /// <param name="validation">Samples to report accuracy on after every epoch, or <see langword="null"/>.</param>
    /// <param name="log">Receives one progress line per epoch.</param>
    public void Train(TrainingSet data, NetworkSettings settings, TrainingSet? validation, Action<string> log)
    {
        var random = new DeterministicRandom(settings.Seed ^ 0xA5A5A5A5UL);
        var workspaces = new Workspace[Shards];
        for (var s = 0; s < Shards; s++)
        {
            workspaces[s] = new Workspace(this);
        }

        var adam = new[]
        {
            new AdamState(_embedding.Length), new AdamState(_bias1.Length), new AdamState(_weights2.Length),
            new AdamState(_bias2.Length), new AdamState(_weights3.Length), new AdamState(_bias3.Length),
        };
        var parameters = new[] { _embedding, _bias1, _weights2, _bias2, _weights3, _bias3 };

        var order = new int[data.Count];
        for (var i = 0; i < order.Length; i++)
        {
            order[i] = i;
        }

        // The gradient arrays of every shard, by parameter
        var shardGradients = new float[parameters.Length][][];
        for (var p = 0; p < parameters.Length; p++)
        {
            shardGradients[p] = workspaces.Select(w => w.Gradients[p]).ToArray();
        }

        var batch = settings.BatchSize;
        var stepsPerEpoch = data.Count / batch;
        var totalSteps = (double)stepsPerEpoch * settings.Epochs;
        var step = 0;
        var moment1 = 1.0;
        var moment2 = 1.0;
        for (var epoch = 0; epoch < settings.Epochs; epoch++)
        {
            // Fisher-Yates shuffle
            for (var i = order.Length - 1; i > 0; i--)
            {
                var j = random.NextInt(i + 1);
                (order[i], order[j]) = (order[j], order[i]);
            }

            long correct = 0;
            for (var b = 0; b < stepsPerEpoch; b++, step++)
            {
                var offset = b * batch;
                var shardSize = batch / Shards;
                Parallel.For(0, Shards, s =>
                {
                    var ws = workspaces[s];
                    ws.Clear();
                    for (var k = 0; k < shardSize; k++)
                    {
                        var sample = order[offset + (s * shardSize) + k];
                        var stream = ((ulong)epoch * (ulong)data.Count) + (ulong)(offset + (s * shardSize) + k) + settings.Seed;
                        ws.Accumulate(data.Values.AsSpan(sample * data.Groups, data.Groups), data.Labels[sample], settings.Dropout, stream);
                    }
                });

                // Add the shard gradients in shard order, then update
                for (var s = 0; s < Shards; s++)
                {
                    correct += workspaces[s].Correct;
                }

                var learningRate = settings.LearningRate * Math.Max(0.02f, (float)(1.0 - (step / totalSteps)));
                moment1 *= 0.9;
                moment2 *= 0.999;
                var bias1 = (float)(1.0 - moment1);
                var bias2 = (float)(1.0 - moment2);
                for (var p = 0; p < parameters.Length; p++)
                {
                    var target = parameters[p];
                    var state = adam[p];
                    var shardsOfParameter = shardGradients[p];
                    Parallel.For(0, (target.Length + 16383) / 16384, chunk =>
                    {
                        var from = chunk * 16384;
                        var to = Math.Min(target.Length, from + 16384);
                        UpdateRange(target, shardsOfParameter, state, from, to, learningRate, 1f / batch, bias1, bias2);
                    });
                }
            }

            var message = $"  epoch {epoch + 1}/{settings.Epochs}: train accuracy {100.0 * correct / ((long)stepsPerEpoch * batch):F2}%";
            if (validation is not null)
            {
                message += $", validation accuracy {100.0 * Accuracy(validation):F2}%";
            }

            log(message);
        }
    }

    /// <summary>
    ///     Measures the floating-point accuracy on a set.
    /// </summary>
    /// <param name="data">The samples.</param>
    /// <returns>The fraction classified correctly.</returns>
    public double Accuracy(TrainingSet data)
    {
        var ws = new Workspace(this);
        var correct = 0;
        for (var i = 0; i < data.Count; i++)
        {
            if (ws.Predict(data.Values.AsSpan(i * data.Groups, data.Groups)) == data.Labels[i])
            {
                correct++;
            }
        }

        return data.Count == 0 ? 0 : (double)correct / data.Count;
    }

    /// <summary>
    ///     Quantizes the network and writes it in the form <c>LtsNetwork.Read</c> reads.
    /// </summary>
    /// <param name="writer">The writer.</param>
    /// <param name="calibration">Samples that set the activation scales (a representative subset of the training data).</param>
    public void WriteQuantized(BinaryWriter writer, TrainingSet calibration)
    {
        // Calibrate the activation ranges on a deterministic subset (99.9th percentile of the positive activations)
        var ws = new Workspace(this);
        var first = new List<float>();
        var second = new List<float>();
        var stride = Math.Max(1, calibration.Count / 4000);
        for (var i = 0; i < calibration.Count; i += stride)
        {
            ws.Forward(calibration.Values.AsSpan(i * calibration.Groups, calibration.Groups));
            first.AddRange(ws.Activation1.Where(a => a > 0));
            second.AddRange(ws.Activation2.Where(a => a > 0));
        }

        var scale1 = Percentile(first) / 2047.0;
        var scale2 = Percentile(second) / 2047.0;

        // Embedding rows and first bias in a common 16-bit scale
        var embeddingScale = Math.Max(_embedding.Max(v => Math.Abs(v)), _bias1.Max(v => Math.Abs(v))) / 32767.0;
        var weights2Scale = _weights2.Max(v => Math.Abs(v)) / 127.0;
        var weights3Scale = _weights3.Max(v => Math.Abs(v)) / 127.0;

        writer.Write((byte)_groups.Length);
        foreach (var group in _groups)
        {
            writer.Write((ushort)group);
        }

        writer.Write((ushort)_hidden1);
        writer.Write((ushort)_hidden2);
        writer.Write((ushort)_outputs);
        foreach (var value in _embedding)
        {
            writer.Write((short)Round(value / embeddingScale));
        }

        foreach (var value in _bias1)
        {
            writer.Write((int)Round(value / embeddingScale));
        }

        writer.Write((int)Round(embeddingScale / scale1 * (1 << 24)));
        foreach (var value in _weights2)
        {
            writer.Write((sbyte)Round(value / weights2Scale));
        }

        var accumulator2 = scale1 * weights2Scale;
        foreach (var value in _bias2)
        {
            writer.Write((int)Round(value / accumulator2));
        }

        writer.Write((int)Round(accumulator2 / scale2 * (1 << 24)));
        foreach (var value in _weights3)
        {
            writer.Write((sbyte)Round(value / weights3Scale));
        }

        var accumulator3 = scale2 * weights3Scale;
        foreach (var value in _bias3)
        {
            writer.Write((int)Round(value / accumulator3));
        }
    }

    /// <summary>
    ///     Rounds half away from zero.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The rounded value.</returns>
    private static double Round(double value) => Math.Round(value, MidpointRounding.AwayFromZero);

    /// <summary>
    ///     Finds the 99.9th percentile of a list of positive activations.
    /// </summary>
    /// <param name="values">The values.</param>
    /// <returns>The percentile, at least a small positive number.</returns>
    private static double Percentile(List<float> values)
    {
        if (values.Count == 0)
        {
            return 1;
        }

        values.Sort();
        return Math.Max(1e-3, values[(int)(0.999 * (values.Count - 1))]);
    }

    /// <summary>
    ///     Creates an array of uniform random values.
    /// </summary>
    /// <param name="count">The length.</param>
    /// <param name="bound">The values are in <c>[-bound, bound)</c>.</param>
    /// <param name="random">The generator.</param>
    /// <returns>The array.</returns>
    private static float[] Initialize(int count, float bound, DeterministicRandom random)
    {
        var values = new float[count];
        for (var i = 0; i < count; i++)
        {
            values[i] = random.NextSignedFloat() * bound;
        }

        return values;
    }

    /// <summary>
    ///     Sums the shard gradients of a range and applies one Adam step to it.
    /// </summary>
    /// <param name="weights">The parameter array.</param>
    /// <param name="shards">The gradient arrays of all shards, in shard order.</param>
    /// <param name="state">The Adam moments.</param>
    /// <param name="from">The first element.</param>
    /// <param name="to">The end of the range (exclusive).</param>
    /// <param name="learningRate">The learning rate.</param>
    /// <param name="scale">The gradient scale (one over the batch size).</param>
    /// <param name="bias1">The first moment bias correction.</param>
    /// <param name="bias2">The second moment bias correction.</param>
    private static void UpdateRange(float[] weights, float[][] shards, AdamState state, int from, int to, float learningRate, float scale, float bias1, float bias2)
    {
        for (var i = from; i < to; i++)
        {
            // Shard order, then the Adam step; every element is independent of every other
            var gradient = shards[0][i];
            for (var s = 1; s < shards.Length; s++)
            {
                gradient += shards[s][i];
            }

            gradient *= scale;
            var m = (0.9f * state.M[i]) + (0.1f * gradient);
            var v = (0.999f * state.V[i]) + (0.001f * gradient * gradient);
            state.M[i] = m;
            state.V[i] = v;
            weights[i] -= learningRate * (m / bias1) / (MathF.Sqrt(v / bias2) + 1e-8f);
        }
    }

    /// <summary>
    ///     The Adam moments of one parameter array.
    /// </summary>
    private sealed class AdamState
    {
        /// <summary>
        ///     Initializes zero moments.
        /// </summary>
        /// <param name="length">The length of the parameter array.</param>
        public AdamState(int length)
        {
            M = new float[length];
            V = new float[length];
        }

        /// <summary>Gets the first moments.</summary>
        public float[] M { get; }

        /// <summary>Gets the second moments.</summary>
        public float[] V { get; }
    }

    /// <summary>
    ///     The per-thread scratch memory and gradient accumulators of one shard.
    /// </summary>
    private sealed class Workspace
    {
        /// <summary>The network.</summary>
        private readonly FloatNetwork _net;

        /// <summary>The logits and then their gradient.</summary>
        private readonly float[] _logits;

        /// <summary>The gradient of the second hidden activations.</summary>
        private readonly float[] _gradient2;

        /// <summary>The gradient of the first hidden activations.</summary>
        private readonly float[] _gradient1;

        /// <summary>
        ///     Initializes a workspace.
        /// </summary>
        /// <param name="net">The network.</param>
        public Workspace(FloatNetwork net)
        {
            _net = net;
            Activation1 = new float[net._hidden1];
            Activation2 = new float[net._hidden2];
            _logits = new float[net._outputs];
            _gradient2 = new float[net._hidden2];
            _gradient1 = new float[net._hidden1];
            Gradients =
            [
                new float[net._embedding.Length], new float[net._bias1.Length], new float[net._weights2.Length],
                new float[net._bias2.Length], new float[net._weights3.Length], new float[net._bias3.Length],
            ];
        }

        /// <summary>Gets the first hidden activations of the last forward pass.</summary>
        public float[] Activation1 { get; }

        /// <summary>Gets the second hidden activations of the last forward pass.</summary>
        public float[] Activation2 { get; }

        /// <summary>
        ///     Gets the gradient accumulators in the order embedding, first bias, second weights, second bias, output
        ///     weights, output bias (the order of the network's parameter arrays).
        /// </summary>
        public float[][] Gradients { get; }

        /// <summary>Gets the number of samples of this batch the network classified correctly.</summary>
        public int Correct { get; private set; }

        /// <summary>
        ///     Zeroes the gradients and the counter for a new batch.
        /// </summary>
        public void Clear()
        {
            foreach (var gradient in Gradients)
            {
                Array.Clear(gradient);
            }

            Correct = 0;
        }

        /// <summary>
        ///     Runs the network on one input and returns the predicted class.
        /// </summary>
        /// <param name="values">The input values.</param>
        /// <returns>The class with the largest logit.</returns>
        public int Predict(ReadOnlySpan<int> values)
        {
            Forward(values);
            return Argmax();
        }

        /// <summary>
        ///     Runs the forward pass: leaves the activations and the logits in the workspace.
        /// </summary>
        /// <param name="values">The input values.</param>
        /// <param name="dropout">The dropout fraction of the hidden units (0 for none, as in prediction).</param>
        /// <param name="stream">The identity of the dropout mask.</param>
        public void Forward(ReadOnlySpan<int> values, float dropout = 0f, ulong stream = 0)
        {
            var n = _net;
            var keep = dropout > 0f ? 1f / (1f - dropout) : 1f;

            // First layer: bias plus the chosen embedding rows, rectified (and thinned by dropout)
            Array.Copy(n._bias1, Activation1, n._hidden1);
            for (var g = 0; g < values.Length; g++)
            {
                AddScaled(Activation1, 0, 1f, n._embedding, (n._groupRows[g] + values[g]) * n._hidden1, n._hidden1);
            }

            for (var j = 0; j < Activation1.Length; j++)
            {
                Activation1[j] = dropout > 0f && Dropped(stream, 0, j, dropout) ? 0f : Math.Max(0f, Activation1[j]) * keep;
            }

            // Second layer and outputs
            for (var i = 0; i < n._hidden2; i++)
            {
                var value = Math.Max(0f, n._bias2[i] + Dot(n._weights2, i * n._hidden1, Activation1, n._hidden1));
                Activation2[i] = dropout > 0f && Dropped(stream, 1, i, dropout) ? 0f : value * keep;
            }

            for (var o = 0; o < n._outputs; o++)
            {
                _logits[o] = n._bias3[o] + Dot(n._weights3, o * n._hidden2, Activation2, n._hidden2);
            }
        }

        /// <summary>
        ///     Decides whether a hidden unit is dropped for a sample: a hash of the sample's stream, layer and unit, so the
        ///     mask is the same whichever thread computes the sample.
        /// </summary>
        /// <param name="stream">The identity of the sample's mask.</param>
        /// <param name="layer">The hidden layer, 0 or 1.</param>
        /// <param name="unit">The unit.</param>
        /// <param name="dropout">The probability of dropping.</param>
        /// <returns><see langword="true"/> when the unit is dropped.</returns>
        private static bool Dropped(ulong stream, int layer, int unit, float dropout)
        {
            var z = (stream * 0x9E3779B97F4A7C15UL) + (((ulong)((unit * 2) + layer) + 1) * 0xD1B54A32D192ED03UL);
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            z ^= z >> 31;
            return ((z >> 40) / 16777216f) < dropout;
        }

        /// <summary>
        ///     Runs one sample forward and adds its gradient to this shard's accumulators.
        /// </summary>
        /// <param name="values">The input values.</param>
        /// <param name="label">The correct class.</param>
        /// <param name="dropout">The dropout fraction of the hidden units (0 for none).</param>
        /// <param name="stream">The identity of this sample's dropout mask (any number that differs between samples and epochs).</param>
        public void Accumulate(ReadOnlySpan<int> values, int label, float dropout, ulong stream)
        {
            var n = _net;
            Forward(values, dropout, stream);
            if (Argmax() == label)
            {
                Correct++;
            }

            // Softmax cross-entropy gradient: probability minus the one-hot label
            var max = float.MinValue;
            foreach (var logit in _logits)
            {
                max = Math.Max(max, logit);
            }

            double total = 0;
            for (var o = 0; o < n._outputs; o++)
            {
                total += Exp(_logits[o] - max);
            }

            for (var o = 0; o < n._outputs; o++)
            {
                _logits[o] = (float)(Exp(_logits[o] - max) / total);
            }

            _logits[label] -= 1f;

            // Output layer
            Array.Clear(_gradient2);
            for (var o = 0; o < n._outputs; o++)
            {
                var d = _logits[o];
                if (Math.Abs(d) < SkipBelow)
                {
                    continue;
                }

                AddScaled(Gradients[4], o * n._hidden2, d, Activation2, 0, n._hidden2);
                Gradients[5][o] += d;
                AddScaled(_gradient2, 0, d, n._weights3, o * n._hidden2, n._hidden2);
            }

            // Second layer (units with a zero activation pass no gradient; kept units carry the dropout scale)
            var keep = 1f / (1f - dropout);
            Array.Clear(_gradient1);
            for (var i = 0; i < n._hidden2; i++)
            {
                if (Activation2[i] <= 0f)
                {
                    continue;
                }

                var d = _gradient2[i] * (dropout > 0f ? keep : 1f);
                AddScaled(Gradients[2], i * n._hidden1, d, Activation1, 0, n._hidden1);
                Gradients[3][i] += d;
                AddScaled(_gradient1, 0, d, n._weights2, i * n._hidden1, n._hidden1);
            }

            // First layer: the gradient of the chosen embedding rows is the gradient of the layer's sum
            for (var j = 0; j < n._hidden1; j++)
            {
                if (Activation1[j] <= 0f)
                {
                    _gradient1[j] = 0f;
                }
                else if (dropout > 0f)
                {
                    _gradient1[j] *= keep;
                }
            }

            AddScaled(Gradients[1], 0, 1f, _gradient1, 0, n._hidden1);
            for (var g = 0; g < values.Length; g++)
            {
                AddScaled(Gradients[0], (n._groupRows[g] + values[g]) * n._hidden1, 1f, _gradient1, 0, n._hidden1);
            }
        }

        /// <summary>
        ///     Computes the dot product of two float ranges with a fixed summation order.
        /// </summary>
        /// <param name="a">The first array.</param>
        /// <param name="aOffset">The start in the first array.</param>
        /// <param name="b">The second array.</param>
        /// <param name="n">The length.</param>
        /// <returns>The sum of products.</returns>
        private static float Dot(float[] a, int aOffset, float[] b, int n)
        {
            var accumulator = Vector128<float>.Zero;
            var i = 0;
            for (; i <= n - 4; i += 4)
            {
                accumulator += Vector128.LoadUnsafe(ref a[aOffset + i]) * Vector128.LoadUnsafe(ref b[i]);
            }

            var sum = (accumulator.GetElement(0) + accumulator.GetElement(1)) + (accumulator.GetElement(2) + accumulator.GetElement(3));
            for (; i < n; i++)
            {
                sum += a[aOffset + i] * b[i];
            }

            return sum;
        }

        /// <summary>
        ///     Adds a multiple of a vector to a range: <c>y[yOffset + i] += factor * x[xOffset + i]</c>.
        /// </summary>
        /// <param name="y">The target array.</param>
        /// <param name="yOffset">The start in the target.</param>
        /// <param name="factor">The factor.</param>
        /// <param name="x">The source.</param>
        /// <param name="xOffset">The start in the source.</param>
        /// <param name="n">The length.</param>
        private static void AddScaled(float[] y, int yOffset, float factor, float[] x, int xOffset, int n)
        {
            var f = Vector128.Create(factor);
            var i = 0;
            for (; i <= n - 4; i += 4)
            {
                var updated = Vector128.LoadUnsafe(ref y[yOffset + i]) + (f * Vector128.LoadUnsafe(ref x[xOffset + i]));
                updated.StoreUnsafe(ref y[yOffset + i]);
            }

            for (; i < n; i++)
            {
                y[yOffset + i] += factor * x[xOffset + i];
            }
        }

        /// <summary>
        ///     Finds the largest logit.
        /// </summary>
        /// <returns>Its index; the first of equal logits.</returns>
        private int Argmax()
        {
            var best = 0;
            for (var o = 1; o < _logits.Length; o++)
            {
                if (_logits[o] > _logits[best])
                {
                    best = o;
                }
            }

            return best;
        }
    }
}
