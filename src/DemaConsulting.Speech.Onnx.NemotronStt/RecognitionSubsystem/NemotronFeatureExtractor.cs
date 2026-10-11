namespace DemaConsulting.Speech.Onnx.NemotronStt.RecognitionSubsystem;

/// <summary>
///     Streaming log-mel feature extractor reproducing the NeMo front end the Nemotron model was
///     trained with.
/// </summary>
/// <remarks>
///     Pipeline (identical to the reference spike's <c>features()</c>): pre-emphasis
///     <c>y[n] = x[n] - 0.97 * x[n-1]</c> (<c>y[0] = x[0]</c>); centered framing with a 256-sample
///     reflect pad on both ends; a 512-point FFT of each frame multiplied by a 400-sample periodic
///     Hann window centered in the 512 samples; power spectrum; a 128-band Slaney-scale mel
///     filterbank (0 to 8000 Hz, Slaney area normalization); and <c>log(mel + 1e-10)</c> with no
///     further normalization. The hop is 160 samples (10 ms).
///     <para>
///     The extractor is incremental: samples can be supplied in blocks of any size and the
///     produced frames equal those of a one-shot computation over the concatenated input. The
///     left reflect pad needs the first 257 samples, so no frame is emitted before then;
///     <see cref="Flush"/> applies the right reflect pad and emits the remaining frames. An input
///     shorter than 257 samples is zero-extended to 257 at flush, since a reflect pad cannot be
///     formed from fewer.
///     </para>
/// </remarks>
internal sealed class NemotronFeatureExtractor
{
    /// <summary>The audio sample rate, in hertz.</summary>
    public const int SampleRate = 16000;

    /// <summary>The number of mel bands per frame.</summary>
    public const int MelBands = 128;

    /// <summary>The FFT size.</summary>
    public const int FftSize = 512;

    /// <summary>The Hann window length.</summary>
    public const int WindowLength = 400;

    /// <summary>The hop between frames, in samples.</summary>
    public const int HopLength = 160;

    /// <summary>The pre-emphasis coefficient.</summary>
    public const double PreEmphasis = 0.97;

    /// <summary>The value added to the mel energy before the logarithm.</summary>
    public const double LogEpsilon = 1e-10;

    /// <summary>The reflect pad applied to each end (half the FFT size).</summary>
    private const int Pad = FftSize / 2;

    /// <summary>The number of frequency bins of the real FFT.</summary>
    private const int Bins = (FftSize / 2) + 1;

    /// <summary>The 400-sample Hann window placed at an offset inside the 512-sample frame.</summary>
    private static readonly double[] Window = BuildWindow();

    /// <summary>The mel filterbank as one row per band.</summary>
    private static readonly MelFilter[] Filters = BuildFilterbank();

    /// <summary>The FFT twiddle factors.</summary>
    private static readonly double[] CosTable = BuildTwiddles(Math.Cos);

    /// <summary>The FFT twiddle factors (sine).</summary>
    private static readonly double[] SinTable = BuildTwiddles(Math.Sin);

    /// <summary>The bit-reversal permutation for the FFT.</summary>
    private static readonly int[] BitReverse = BuildBitReverse();

    /// <summary>The FFT real working buffer.</summary>
    private readonly double[] _re = new double[FftSize];

    /// <summary>The FFT imaginary working buffer.</summary>
    private readonly double[] _im = new double[FftSize];

    /// <summary>The power spectrum working buffer.</summary>
    private readonly double[] _power = new double[Bins];

    /// <summary>The first <see cref="Pad"/>+1 pre-emphasized samples, held until the left pad can be formed.</summary>
    private readonly List<float> _head = [];

    /// <summary>The pre-emphasized samples of the framing buffer (the padded signal from the next frame start).</summary>
    private float[] _buffer = new float[FftSize * 8];

    /// <summary>The number of valid samples in <see cref="_buffer"/>.</summary>
    private int _bufferLength;

    /// <summary>The queue of finished frames, <see cref="MelBands"/> floats each.</summary>
    private float[] _frames = new float[MelBands * 64];

    /// <summary>The number of finished frames in <see cref="_frames"/>.</summary>
    private int _frameCount;

    /// <summary>The previous raw sample, for pre-emphasis.</summary>
    private float _previousSample;

    /// <summary>The number of raw samples accepted so far.</summary>
    private long _samplesAccepted;

    /// <summary>The last <see cref="Pad"/>+1 pre-emphasized samples, used to form the right reflect pad.</summary>
    private readonly float[] _tail = new float[Pad + 1];

    /// <summary>Whether the left pad has been formed and framing has started.</summary>
    private bool _started;

    /// <summary>Gets the number of finished frames waiting to be dequeued.</summary>
    public int FrameCount => _frameCount;

    /// <summary>
    ///     Restores the start-of-stream state, discarding queued frames.
    /// </summary>
    public void Reset()
    {
        _head.Clear();
        _bufferLength = 0;
        _frameCount = 0;
        _previousSample = 0;
        _samplesAccepted = 0;
        _started = false;
        Array.Clear(_tail);
    }

    /// <summary>
    ///     Accepts samples, queueing every frame they complete.
    /// </summary>
    /// <param name="samples">Mono samples at 16 kHz in [-1, 1].</param>
    public void Accept(ReadOnlySpan<float> samples)
    {
        for (var i = 0; i < samples.Length; i++)
        {
            var x = samples[i];
            var y = _samplesAccepted == 0 ? x : (float)(x - (PreEmphasis * _previousSample));
            _previousSample = x;
            AppendEmphasized(y);
            _samplesAccepted++;
        }

        EmitReadyFrames();
    }

    /// <summary>
    ///     Applies the right reflect pad and queues every remaining frame. After this call the
    ///     extractor should be <see cref="Reset"/> before accepting more audio.
    /// </summary>
    public void Flush()
    {
        if (_samplesAccepted == 0)
        {
            return;
        }

        // Too short for a reflect pad: zero-extend to Pad + 1 samples, which starts framing.
        while (!_started)
        {
            AppendEmphasized(0f);
        }

        // Right reflect pad: padded[len + Pad + j] = y[len - 2 - j]. _tail holds the last Pad+1
        // emphasized samples in order, so y[len - 1 - k] = _tail[Pad - k].
        for (var j = 0; j < Pad; j++)
        {
            AppendRaw(_tail[Pad - (j + 1)]);
        }

        EmitReadyFrames();
    }

    /// <summary>
    ///     Removes the oldest <paramref name="count"/> queued frames, copying them to
    ///     <paramref name="destination"/> as consecutive rows of <see cref="MelBands"/> floats.
    /// </summary>
    /// <param name="count">The number of frames to dequeue; must not exceed <see cref="FrameCount"/>.</param>
    /// <param name="destination">Receives at least <c>count * MelBands</c> floats.</param>
    public void Dequeue(int count, Span<float> destination)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(count, _frameCount);

        var floats = count * MelBands;
        _frames.AsSpan(0, floats).CopyTo(destination);
        Array.Copy(_frames, floats, _frames, 0, (_frameCount - count) * MelBands);
        _frameCount -= count;
    }

    /// <summary>Appends one pre-emphasized sample, forming the left reflect pad on the 257th.</summary>
    private void AppendEmphasized(float y)
    {
        // Maintain the sliding window of the last Pad + 1 emphasized samples.
        Array.Copy(_tail, 1, _tail, 0, Pad);
        _tail[Pad] = y;

        if (_started)
        {
            AppendRaw(y);
            return;
        }

        _head.Add(y);
        if (_head.Count == Pad + 1)
        {
            // Left reflect pad: padded[i] = y[Pad - i] for i in [0, Pad).
            for (var i = 0; i < Pad; i++)
            {
                AppendRaw(_head[Pad - i]);
            }

            foreach (var sample in _head)
            {
                AppendRaw(sample);
            }

            _head.Clear();
            _started = true;
        }
    }

    /// <summary>Appends a sample of the padded signal to the framing buffer.</summary>
    private void AppendRaw(float value)
    {
        if (_bufferLength == _buffer.Length)
        {
            Array.Resize(ref _buffer, _buffer.Length * 2);
        }

        _buffer[_bufferLength++] = value;
    }

    /// <summary>Computes every frame whose 512 samples are available and discards consumed samples.</summary>
    private void EmitReadyFrames()
    {
        if (!_started)
        {
            return;
        }

        var start = 0;
        while (start + FftSize <= _bufferLength)
        {
            ComputeFrame(start);
            start += HopLength;
        }

        if (start > 0)
        {
            Array.Copy(_buffer, start, _buffer, 0, _bufferLength - start);
            _bufferLength -= start;
        }
    }

    /// <summary>Computes the log-mel vector of the 512 samples at <paramref name="start"/> and queues it.</summary>
    private void ComputeFrame(int start)
    {
        const int windowOffset = (FftSize - WindowLength) / 2;
        for (var i = 0; i < FftSize; i++)
        {
            var w = (i >= windowOffset && i < windowOffset + WindowLength) ? Window[i - windowOffset] : 0.0;
            _re[i] = _buffer[start + i] * w;
            _im[i] = 0.0;
        }

        Fft();

        for (var k = 0; k < Bins; k++)
        {
            _power[k] = (_re[k] * _re[k]) + (_im[k] * _im[k]);
        }

        if ((_frameCount + 1) * MelBands > _frames.Length)
        {
            Array.Resize(ref _frames, _frames.Length * 2);
        }

        var row = _frameCount * MelBands;
        for (var m = 0; m < MelBands; m++)
        {
            var filter = Filters[m];
            double energy = 0;
            for (var k = 0; k < filter.Weights.Length; k++)
            {
                energy += filter.Weights[k] * _power[filter.First + k];
            }

            _frames[row + m] = (float)Math.Log(energy + LogEpsilon);
        }

        _frameCount++;
    }

    /// <summary>In-place iterative radix-2 FFT of <see cref="_re"/>/<see cref="_im"/>.</summary>
    private void Fft()
    {
        for (var i = 0; i < FftSize; i++)
        {
            var j = BitReverse[i];
            if (j > i)
            {
                (_re[i], _re[j]) = (_re[j], _re[i]);
                (_im[i], _im[j]) = (_im[j], _im[i]);
            }
        }

        for (var size = 2; size <= FftSize; size <<= 1)
        {
            var half = size / 2;
            var step = FftSize / size;
            for (var block = 0; block < FftSize; block += size)
            {
                for (var k = 0; k < half; k++)
                {
                    var c = CosTable[k * step];
                    var s = SinTable[k * step];
                    var a = block + k;
                    var b = a + half;
                    var tr = (_re[b] * c) + (_im[b] * s);
                    var ti = (_im[b] * c) - (_re[b] * s);
                    _re[b] = _re[a] - tr;
                    _im[b] = _im[a] - ti;
                    _re[a] += tr;
                    _im[a] += ti;
                }
            }
        }
    }

    /// <summary>Builds the periodic Hann window (scipy <c>get_window("hann", 400, fftbins=True)</c>).</summary>
    private static double[] BuildWindow()
    {
        var window = new double[WindowLength];
        for (var n = 0; n < WindowLength; n++)
        {
            window[n] = 0.5 - (0.5 * Math.Cos(2.0 * Math.PI * n / WindowLength));
        }

        return window;
    }

    /// <summary>Builds <c>f(2*pi*k/N)</c> for k in [0, N/2).</summary>
    private static double[] BuildTwiddles(Func<double, double> function)
    {
        var table = new double[FftSize / 2];
        for (var k = 0; k < table.Length; k++)
        {
            table[k] = function(2.0 * Math.PI * k / FftSize);
        }

        return table;
    }

    /// <summary>Builds the bit-reversal permutation for <see cref="FftSize"/> points.</summary>
    private static int[] BuildBitReverse()
    {
        var bits = (int)Math.Log2(FftSize);
        var table = new int[FftSize];
        for (var i = 0; i < FftSize; i++)
        {
            var reversed = 0;
            for (var b = 0; b < bits; b++)
            {
                if ((i & (1 << b)) != 0)
                {
                    reversed |= 1 << (bits - 1 - b);
                }
            }

            table[i] = reversed;
        }

        return table;
    }

    /// <summary>Converts hertz to the Slaney mel scale.</summary>
    private static double HzToMel(double hz)
    {
        var logStep = Math.Log(6.4) / 27.0;
        return hz >= 1000.0 ? 15.0 + (Math.Log(Math.Max(hz, 1e-9) / 1000.0) / logStep) : hz / (200.0 / 3);
    }

    /// <summary>Converts the Slaney mel scale to hertz.</summary>
    private static double MelToHz(double mel)
    {
        var logStep = Math.Log(6.4) / 27.0;
        return mel >= 15.0 ? 1000.0 * Math.Exp(logStep * (mel - 15.0)) : mel * (200.0 / 3);
    }

    /// <summary>
    ///     Builds the Slaney-scale, Slaney-normalized triangular mel filterbank (librosa's default,
    ///     as used by NeMo), storing only each band's non-zero span of bins.
    /// </summary>
    private static MelFilter[] BuildFilterbank()
    {
        var fftFrequencies = new double[Bins];
        for (var k = 0; k < Bins; k++)
        {
            fftFrequencies[k] = (double)k * (SampleRate / 2.0) / (Bins - 1);
        }

        var melLow = HzToMel(0.0);
        var melHigh = HzToMel(SampleRate / 2.0);
        var hz = new double[MelBands + 2];
        for (var i = 0; i < hz.Length; i++)
        {
            hz[i] = MelToHz(melLow + ((melHigh - melLow) * i / (MelBands + 1)));
        }

        var filters = new MelFilter[MelBands];
        for (var m = 0; m < MelBands; m++)
        {
            var lowerDelta = hz[m + 1] - hz[m];
            var upperDelta = hz[m + 2] - hz[m + 1];
            var norm = 2.0 / (hz[m + 2] - hz[m]);
            var weights = new double[Bins];
            var first = -1;
            var last = -1;
            for (var k = 0; k < Bins; k++)
            {
                var lower = -(hz[m] - fftFrequencies[k]) / lowerDelta;
                var upper = (hz[m + 2] - fftFrequencies[k]) / upperDelta;
                var weight = Math.Max(0.0, Math.Min(lower, upper)) * norm;
                weights[k] = weight;
                if (weight > 0.0)
                {
                    first = first < 0 ? k : first;
                    last = k;
                }
            }

            filters[m] = first < 0
                ? new MelFilter(0, [])
                : new MelFilter(first, weights[first..(last + 1)]);
        }

        return filters;
    }

    /// <summary>One mel band's non-zero weights, starting at bin <paramref name="First"/>.</summary>
    private readonly record struct MelFilter(int First, double[] Weights);
}
