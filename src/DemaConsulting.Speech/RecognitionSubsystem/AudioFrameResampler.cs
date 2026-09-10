using System.Buffers;
using DemaConsulting.Speech.AudioSubsystem;

namespace DemaConsulting.Speech.RecognitionSubsystem;

/// <summary>
///     Converts one block of interleaved, multi-channel capture audio into the single-channel
///     (mono) audio at the model-declared sample rate that a recognition engine requires.
/// </summary>
/// <remarks>
///     A capture device resolves whatever channel count and sample rate its hardware defaults to
///     (often stereo at 44.1 or 48 kHz), while a streaming recognition model is trained at one
///     fixed mono rate. Something must bridge the two, and doing it here - in a pure, dependency-
///     free type - keeps that conversion fully unit-testable with plain <see cref="float"/>
///     arrays, with no capture device, engine, or native runtime involved.
///     <para>
///     <b>Deliberate quality trade-off.</b> Downmixing is still a straight arithmetic mean across
///     channels, and rate conversion still uses simple linear interpolation between adjacent
///     samples. Downsampling now inserts one small, dependency-free windowed-sinc FIR lowpass
///     stage first, which attenuates above-target-Nyquist content before decimation and
///     materially reduces aliasing. The type remains intentionally much smaller than a full
///     polyphase resampler, and because each block is converted independently the interpolation
///     restarts at every block boundary. This balance keeps the implementation small enough to
///     review and test exhaustively while addressing the most significant quality gap in the
///     original downsampling path. See the recognition subsystem's design documentation for the
///     full rationale.
///     </para>
///     <para>
///     Instances are immutable and carry no per-block state, so a single instance is safe to
///     reuse for every block of a session and safe to share across threads.
///     </para>
/// </remarks>
internal sealed class AudioFrameResampler
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="AudioFrameResampler"/> class for one
    ///     fixed source format and target rate.
    /// </summary>
    /// <param name="sourceSampleRate">The capture device's resolved sample rate, in Hz. Must be greater than zero.</param>
    /// <param name="sourceChannelCount">The capture device's resolved channel count. Must be greater than zero.</param>
    /// <param name="targetSampleRate">The model-declared engine input rate, in Hz. Must be greater than zero.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown when any argument is less than or equal to zero, because no meaningful
    ///     conversion exists for a zero or negative rate or channel count.
    /// </exception>
    internal AudioFrameResampler(int sourceSampleRate, int sourceChannelCount, int targetSampleRate)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(sourceSampleRate, 0);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(sourceChannelCount, 0);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(targetSampleRate, 0);

        SourceSampleRate = sourceSampleRate;
        SourceChannelCount = sourceChannelCount;
        TargetSampleRate = targetSampleRate;
    }

    /// <summary>Gets the capture device's resolved sample rate, in Hz.</summary>
    internal int SourceSampleRate { get; }

    /// <summary>Gets the capture device's resolved channel count.</summary>
    internal int SourceChannelCount { get; }

    /// <summary>Gets the model-declared engine input rate, in Hz.</summary>
    internal int TargetSampleRate { get; }

    /// <summary>
    ///     Converts one interleaved capture block into mono samples at
    ///     <see cref="TargetSampleRate"/>.
    /// </summary>
    /// <param name="interleavedSamples">
    ///     The captured block, interleaved by channel with a stride of
    ///     <see cref="SourceChannelCount"/>. May be empty. A trailing partial frame (fewer than
    ///     <see cref="SourceChannelCount"/> samples) is discarded, since a partially captured
    ///     frame cannot be downmixed correctly.
    /// </param>
    /// <returns>
    ///     A newly allocated array of mono samples at <see cref="TargetSampleRate"/>. Empty when
    ///     the input contains no complete frame.
    /// </returns>
    /// <remarks>Allocates one intermediate array and one result array; performs no I/O.</remarks>
    internal float[] Convert(ReadOnlySpan<float> interleavedSamples)
    {
        // Collapse channels first: downmixing before resampling means the interpolation runs
        // over one signal instead of once per channel, which is both cheaper and avoids any
        // chance of the channels drifting out of alignment with each other.
        var mono = DownmixToMono(interleavedSamples, SourceChannelCount);

        return Resample(mono, SourceSampleRate, TargetSampleRate);
    }

    /// <summary>
    ///     Collapses interleaved multi-channel samples into mono by averaging each frame's
    ///     channels.
    /// </summary>
    /// <param name="interleavedSamples">
    ///     The interleaved samples. A trailing partial frame is discarded.
    /// </param>
    /// <param name="channelCount">The interleaving stride. Must be greater than zero.</param>
    /// <returns>
    ///     A newly allocated array with one sample per complete input frame. Empty when the input
    ///     contains no complete frame.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown when <paramref name="channelCount"/> is less than or equal to zero.
    /// </exception>
    /// <remarks>
    ///     Averaging (rather than picking a single channel) is used because a speaker is
    ///     typically present in every channel of a desktop microphone array, so the mean both
    ///     preserves the voice and partially cancels uncorrelated per-channel noise. The mean of
    ///     values in <c>[-1.0, 1.0]</c> stays in that range, so no clipping step is needed.
    /// </remarks>
    internal static float[] DownmixToMono(ReadOnlySpan<float> interleavedSamples, int channelCount)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(channelCount, 0);

        // A single-channel source is already mono; copying it avoids the per-frame division
        // entirely and keeps the common 1-channel microphone case exact.
        if (channelCount == 1)
        {
            return interleavedSamples.ToArray();
        }

        var frameCount = interleavedSamples.Length / channelCount;
        if (frameCount == 0)
        {
            return [];
        }

        var mono = new float[frameCount];
        for (var frame = 0; frame < frameCount; frame++)
        {
            // Accumulate in double precision so a high channel count cannot lose low-level
            // detail to repeated single-precision rounding before the division.
            var sum = 0.0;
            var offset = frame * channelCount;
            for (var channel = 0; channel < channelCount; channel++)
            {
                sum += interleavedSamples[offset + channel];
            }

            mono[frame] = (float)(sum / channelCount);
        }

        return mono;
    }

    /// <summary>
    ///     Resamples mono audio from one rate to another using linear interpolation between adjacent
    ///     samples, with an anti-aliasing lowpass filter applied first when downsampling.
    /// </summary>
    /// <param name="monoSamples">The single-channel input samples. May be empty.</param>
    /// <param name="sourceSampleRate">The input rate, in Hz. Must be greater than zero.</param>
    /// <param name="targetSampleRate">The output rate, in Hz. Must be greater than zero.</param>
    /// <returns>
    ///     A newly allocated array holding the resampled signal, of length
    ///     <c>floor(monoSamples.Length * targetSampleRate / sourceSampleRate)</c>. Empty when the
    ///     input is empty or the conversion rounds down to zero output samples.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown when either rate is less than or equal to zero.
    /// </exception>
    /// <remarks>
    ///     When the two rates are equal the input is copied unchanged, so the identity case costs
    ///     nothing and introduces no interpolation error at all. When downsampling, the input is
    ///     first low-pass filtered with a small Hamming-windowed sinc FIR kernel so energy above the
    ///     target Nyquist frequency is attenuated before decimation. The actual resampling step
    ///     remains the same linear blend of the two input samples straddling each output position,
    ///     with the final position clamped to the last input sample so the loop never reads past the
    ///     end.
    /// </remarks>
    internal static float[] Resample(ReadOnlySpan<float> monoSamples, int sourceSampleRate, int targetSampleRate)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(sourceSampleRate, 0);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(targetSampleRate, 0);

        if (monoSamples.IsEmpty || sourceSampleRate == targetSampleRate)
        {
            return monoSamples.ToArray();
        }

        // Compute the output length in 64-bit arithmetic: at 48 kHz a multi-second block would
        // otherwise be able to overflow the intermediate product on a 32-bit multiply.
        var outputLength = (int)(monoSamples.Length * (long)targetSampleRate / sourceSampleRate);
        if (outputLength == 0)
        {
            return [];
        }

        ReadOnlySpan<float> samplesToResample = monoSamples;
        float[]? rentedFilterBuffer = null;
        try
        {
            if (targetSampleRate < sourceSampleRate)
            {
                var cutoffRatio = (double)targetSampleRate / sourceSampleRate;
                var kernel = WindowedSincLowpassFilter.BuildLowpassKernel(cutoffRatio, WindowedSincLowpassFilter.DownsamplingFilterTapCount);

                // The filtered signal is only ever read by the interpolation loop immediately
                // below and then discarded, so a pooled buffer avoids allocating a full-size
                // array for what is a purely transient intermediate result.
                rentedFilterBuffer = ArrayPool<float>.Shared.Rent(monoSamples.Length);
                var filteredDestination = rentedFilterBuffer.AsSpan(0, monoSamples.Length);
                WindowedSincLowpassFilter.ApplyLowpassFilter(monoSamples, kernel, filteredDestination);
                samplesToResample = filteredDestination;
            }

            var resampled = new float[outputLength];
            var step = (double)sourceSampleRate / targetSampleRate;
            var lastIndex = samplesToResample.Length - 1;
            for (var i = 0; i < outputLength; i++)
            {
                // Locate this output sample's fractional position in the input signal, then blend
                // the two input samples on either side of it.
                var position = i * step;
                var lowerIndex = (int)position;
                if (lowerIndex >= lastIndex)
                {
                    resampled[i] = samplesToResample[lastIndex];
                    continue;
                }

                var fraction = position - lowerIndex;
                var lower = samplesToResample[lowerIndex];
                var upper = samplesToResample[lowerIndex + 1];
                resampled[i] = (float)(lower + ((upper - lower) * fraction));
            }

            return resampled;
        }
        finally
        {
            if (rentedFilterBuffer is not null)
            {
                ArrayPool<float>.Shared.Return(rentedFilterBuffer);
            }
        }
    }
}
