namespace DemaConsulting.Speech.AudioSubsystem;

/// <summary>
///     Down-mixes and resamples decoded audio, for example to the mono stream a speech model consumes.
/// </summary>
/// <remarks>
///     Channels are down-mixed by averaging, then resampled with a Hann-windowed sinc kernel whose cutoff is
///     lowered when down-sampling. A band-limited kernel is used instead of linear interpolation because clips
///     are typically recorded at 44.1/48 kHz and linear interpolation would alias high frequencies. The kernel
///     is normalized so DC passes with unity gain, including at the clip edges. Stateless and thread-safe.
/// </remarks>
public static class AudioResampler
{
    /// <summary>The one-sided kernel width, in zero crossings of the (cutoff-scaled) sinc.</summary>
    private const int KernelLobes = 16;

    /// <summary>
    ///     Averages interleaved channels into one mono stream.
    /// </summary>
    /// <param name="interleaved">Interleaved samples. Length should be a multiple of <paramref name="channelCount"/>.</param>
    /// <param name="channelCount">The channel count; must be at least one.</param>
    /// <returns>The mono samples (a trailing partial frame is ignored).</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="channelCount"/> is below one.</exception>
    public static float[] DownMix(ReadOnlySpan<float> interleaved, int channelCount)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(channelCount, 1);

        // Mono input needs no work beyond a copy so the caller's buffer is never aliased
        if (channelCount == 1)
        {
            return interleaved.ToArray();
        }

        var frames = interleaved.Length / channelCount;
        var mono = new float[frames];
        for (var frame = 0; frame < frames; frame++)
        {
            var sum = 0f;
            for (var channel = 0; channel < channelCount; channel++)
            {
                sum += interleaved[(frame * channelCount) + channel];
            }

            mono[frame] = sum / channelCount;
        }

        return mono;
    }

    /// <summary>
    ///     Resamples mono audio from <paramref name="inputRate"/> to <paramref name="outputRate"/>.
    /// </summary>
    /// <param name="input">The mono samples.</param>
    /// <param name="inputRate">The input sample rate in Hz; must be positive.</param>
    /// <param name="outputRate">The output sample rate in Hz; must be positive.</param>
    /// <returns>The resampled samples (a copy of the input when the rates are equal).</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when either rate is not positive.</exception>
    public static float[] Resample(ReadOnlySpan<float> input, int inputRate, int outputRate)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(inputRate, 0);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(outputRate, 0);

        if (inputRate == outputRate || input.IsEmpty)
        {
            return input.ToArray();
        }

        // Down-sampling lowers the kernel cutoff to the output Nyquist; up-sampling keeps it at the input's
        var step = (double)inputRate / outputRate;
        var cutoff = Math.Min(1.0, (double)outputRate / inputRate);
        var halfWidth = KernelLobes / cutoff;
        var outputLength = (int)Math.Floor(input.Length / step);
        var output = new float[outputLength];

        for (var i = 0; i < outputLength; i++)
        {
            output[i] = InterpolateAt(input, i * step, cutoff, halfWidth);
        }

        return output;
    }

    /// <summary>
    ///     Down-mixes a clip to mono and resamples it to <paramref name="outputRate"/>.
    /// </summary>
    /// <param name="clip">The decoded clip. Must not be null.</param>
    /// <param name="outputRate">The output sample rate in Hz; must be positive.</param>
    /// <returns>The mono samples at <paramref name="outputRate"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="clip"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="outputRate"/> is not positive.</exception>
    public static float[] ToMonoResampled(AudioClip clip, int outputRate)
    {
        ArgumentNullException.ThrowIfNull(clip);
        return Resample(clip.ToMono(), clip.SampleRate, outputRate);
    }

    /// <summary>Evaluates the band-limited interpolation of <paramref name="input"/> at the fractional index <paramref name="center"/>.</summary>
    private static float InterpolateAt(ReadOnlySpan<float> input, double center, double cutoff, double halfWidth)
    {
        var first = Math.Max(0, (int)Math.Ceiling(center - halfWidth));
        var last = Math.Min(input.Length - 1, (int)Math.Floor(center + halfWidth));
        double sum = 0;
        double weightSum = 0;
        for (var n = first; n <= last; n++)
        {
            var distance = n - center;
            var window = 0.5 * (1 + Math.Cos(Math.PI * distance / halfWidth));
            var x = Math.PI * distance * cutoff;
            var sinc = Math.Abs(x) < 1e-12 ? 1.0 : Math.Sin(x) / x;
            var weight = cutoff * sinc * window;
            sum += input[n] * weight;
            weightSum += weight;
        }

        // Normalizing by the weight sum keeps unity DC gain, including at the clip edges. When the truncated
        // kernel has almost no weight (it should not happen: a full kernel sums to about one), dividing would
        // amplify noise, so fall back to the nearest input sample instead.
        if (weightSum > 0.05)
        {
            return (float)(sum / weightSum);
        }

        return input[Math.Clamp((int)Math.Round(center), 0, input.Length - 1)];
    }
}
