namespace DemaConsulting.Speech.Onnx.NemotronStt.RecognitionSubsystem;

/// <summary>
///     Seeded, deterministic Gaussian dither source added to the audio fed to the feature
///     extractor.
/// </summary>
/// <remarks>
///     Digital silence (exact zeros) drives the log-mel features to the constant
///     <c>log(1e-10)</c> floor, an input the streaming encoder was never trained on and which
///     can destabilize its caches. Adding noise far below audibility (default amplitude
///     <c>1e-5</c>, about -100 dBFS) keeps the features in a realistic range. The sequence is
///     seeded so a given input always produces the same transcript, which keeps tests and
///     support investigations reproducible. Gaussian samples use the Box-Muller transform over
///     <see cref="Random"/>.
/// </remarks>
internal sealed class DitherNoise
{
    /// <summary>The default dither standard deviation (about -100 dBFS).</summary>
    public const float DefaultAmplitude = 1e-5f;

    /// <summary>The default random seed.</summary>
    public const int DefaultSeed = 20260101;

    /// <summary>The dither standard deviation; <c>0</c> disables dither.</summary>
    private readonly float _amplitude;

    /// <summary>The seed restored by <see cref="Reset"/>.</summary>
    private readonly int _seed;

#pragma warning disable S2245 // Deterministic, seeded pseudo-randomness is the point: dither is not security sensitive.
    /// <summary>The pseudo-random generator.</summary>
    private Random _random;
#pragma warning restore S2245

    /// <summary>The second value of the last Box-Muller pair, when one is cached.</summary>
    private double? _spare;

    /// <summary>
    ///     Initializes a new instance of the <see cref="DitherNoise"/> class.
    /// </summary>
    /// <param name="amplitude">The Gaussian standard deviation; must be non-negative. Zero disables dither.</param>
    /// <param name="seed">The random seed.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="amplitude"/> is negative or not finite.</exception>
    public DitherNoise(float amplitude = DefaultAmplitude, int seed = DefaultSeed)
    {
        if (amplitude < 0 || !float.IsFinite(amplitude))
        {
            throw new ArgumentOutOfRangeException(nameof(amplitude), amplitude, "Dither amplitude must be a non-negative finite number.");
        }

        _amplitude = amplitude;
        _seed = seed;
#pragma warning disable S2245 // See above.
        _random = new Random(seed);
#pragma warning restore S2245
    }

    /// <summary>
    ///     Adds dither in place to <paramref name="samples"/>.
    /// </summary>
    /// <param name="samples">The samples to dither.</param>
    public void Apply(Span<float> samples)
    {
        if (_amplitude == 0f)
        {
            return;
        }

        for (var i = 0; i < samples.Length; i++)
        {
            samples[i] += (float)(_amplitude * NextGaussian());
        }
    }

    /// <summary>
    ///     Restarts the noise sequence from the original seed.
    /// </summary>
    public void Reset()
    {
#pragma warning disable S2245 // See above.
        _random = new Random(_seed);
#pragma warning restore S2245
        _spare = null;
    }

    /// <summary>Draws one standard-normal value using the Box-Muller transform.</summary>
    private double NextGaussian()
    {
        if (_spare is { } cached)
        {
            _spare = null;
            return cached;
        }

        // 1 - NextDouble() lies in (0, 1], so the logarithm is always finite.
        var u1 = 1.0 - _random.NextDouble();
        var u2 = _random.NextDouble();
        var radius = Math.Sqrt(-2.0 * Math.Log(u1));
        _spare = radius * Math.Sin(2.0 * Math.PI * u2);
        return radius * Math.Cos(2.0 * Math.PI * u2);
    }
}
