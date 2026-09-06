namespace DemaConsulting.Speech.AudioSubsystem;

/// <summary>
///     Shared windowed-sinc Hamming lowpass FIR primitive used as the anti-aliasing stage before
///     downsampling.
/// </summary>
/// <remarks>
///     Both <see cref="RecognitionSubsystem.AudioFrameResampler"/> (capture-direction, downmix
///     then downsample) and <see cref="SynthesisSubsystem.PlaybackAudioResampler"/> (playback-
///     direction, downsample then upmix) need the exact same FIR kernel construction and
///     application logic immediately before their own direction-specific decimation step. Rather
///     than maintaining two byte-for-byte-identical copies of that DSP math, it lives once here as
///     a pure, dependency-free, direction-agnostic primitive that both resamplers call. This type
///     has no knowledge of channels, capture, or playback - it operates purely on mono sample
///     arrays and kernel coefficients, so it stays exhaustively testable in isolation from either
///     direction's resampler.
///     <para>
///     Stateless and thread-safe: every member is a pure function with no shared or per-call
///     state.
///     </para>
/// </remarks>
internal static class WindowedSincLowpassFilter
{
    /// <summary>
    ///     The odd-numbered FIR tap count used for the anti-aliasing lowpass filter applied
    ///     before downsampling.
    /// </summary>
    internal const int DownsamplingFilterTapCount = 33;

    /// <summary>
    ///     Builds a normalized Hamming-windowed sinc lowpass kernel for pre-downsampling
    ///     anti-aliasing.
    /// </summary>
    /// <param name="cutoffRatio">
    ///     The desired cutoff frequency expressed as a fraction of the source signal's Nyquist
    ///     frequency. Must be greater than zero and less than or equal to one.
    /// </param>
    /// <param name="tapCount">
    ///     The odd-numbered number of FIR taps to build. Must be greater than zero and odd so the
    ///     kernel is symmetric around one exact center sample.
    /// </param>
    /// <returns>
    ///     A normalized kernel whose coefficients sum to <c>1.0</c>.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown when <paramref name="cutoffRatio"/> lies outside <c>(0, 1]</c> or when
    ///     <paramref name="tapCount"/> is less than or equal to zero.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///     Thrown when <paramref name="tapCount"/> is even.
    /// </exception>
    internal static float[] BuildLowpassKernel(double cutoffRatio, int tapCount)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(cutoffRatio, 0.0d);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(cutoffRatio, 1.0d);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(tapCount, 0);
        if (tapCount % 2 == 0)
        {
            throw new ArgumentException("The tap count must be odd.", nameof(tapCount));
        }

        var radius = tapCount / 2;
        var kernel = new float[tapCount];
        var sum = 0.0;
        for (var tap = 0; tap < tapCount; tap++)
        {
            var offset = tap - radius;
            var window = 0.54d - (0.46d * Math.Cos((2.0d * Math.PI * tap) / (tapCount - 1)));
            var coefficient = cutoffRatio * Sinc(cutoffRatio * offset) * window;
            kernel[tap] = (float)coefficient;
            sum += coefficient;
        }

        for (var tap = 0; tap < kernel.Length; tap++)
        {
            kernel[tap] = (float)(kernel[tap] / sum);
        }

        return kernel;
    }

    /// <summary>
    ///     Applies one symmetric FIR filter to a mono signal, extending edges by replicating the
    ///     nearest endpoint sample so the output length matches the input length exactly.
    /// </summary>
    /// <param name="monoSamples">
    ///     The mono samples to filter. May be empty.
    /// </param>
    /// <param name="kernel">
    ///     The normalized FIR kernel to apply. Must not be null and should typically come from
    ///     <see cref="BuildLowpassKernel(double, int)"/>.
    /// </param>
    /// <returns>
    ///     A newly allocated filtered array of the same length as <paramref name="monoSamples"/>.
    /// </returns>
    internal static float[] ApplyLowpassFilter(ReadOnlySpan<float> monoSamples, float[] kernel)
    {
        ArgumentNullException.ThrowIfNull(kernel);

        if (monoSamples.IsEmpty)
        {
            return [];
        }

        var filtered = new float[monoSamples.Length];
        var radius = kernel.Length / 2;
        var lastIndex = monoSamples.Length - 1;
        for (var sampleIndex = 0; sampleIndex < monoSamples.Length; sampleIndex++)
        {
            var sum = 0.0;
            for (var tap = 0; tap < kernel.Length; tap++)
            {
                var sourceIndex = Math.Clamp(sampleIndex + tap - radius, 0, lastIndex);
                sum += monoSamples[sourceIndex] * kernel[tap];
            }

            filtered[sampleIndex] = (float)sum;
        }

        return filtered;
    }

    /// <summary>
    ///     Computes the normalized sinc function <c>sin(pi x) / (pi x)</c>.
    /// </summary>
    /// <param name="value">
    ///     The input value.
    /// </param>
    /// <returns>
    ///     The normalized sinc of <paramref name="value"/>, with the removable singularity at
    ///     zero defined as <c>1.0</c>.
    /// </returns>
    private static double Sinc(double value)
    {
        if (Math.Abs(value) < double.Epsilon)
        {
            return 1.0d;
        }

        var radians = Math.PI * value;
        return Math.Sin(radians) / radians;
    }
}
