using System.Buffers;
using DemaConsulting.Speech.AudioSubsystem;

namespace DemaConsulting.Speech.SynthesisSubsystem;

/// <summary>
///     Converts mono synthesized audio at an engine's declared sample rate into interleaved audio
///     at a playback device's resolved sample rate and channel count.
/// </summary>
/// <remarks>
///     A synthesis engine produces mono audio at whatever rate its model was trained at, while a
///     playback device resolves whatever channel count and sample rate its hardware defaults to.
///     This is the playback-direction counterpart of
///     <see cref="RecognitionSubsystem.AudioFrameResampler"/> - a new, separate type rather than a
///     reuse of that already quality-passed capture-direction/downmix-only type, since the two
///     directions convert in opposite ways (upmix mono to N channels here, versus downmix N
///     channels to mono there) and combining them would blur that already-reviewed unit's scope.
///     <para>
///     <b>Deliberate quality trade-off.</b> As with <see cref="RecognitionSubsystem.AudioFrameResampler"/>,
///     rate conversion still uses simple linear interpolation between adjacent samples and each
///     block is converted independently. Downsampling now inserts one small, dependency-free
///     windowed-sinc FIR lowpass stage first, materially reducing aliasing while keeping this
///     direction-specific implementation small enough to review and test exhaustively; see the
///     synthesis subsystem's design documentation for the full rationale.
///     </para>
///     <para>
///     Instances are immutable and carry no per-block state, so a single instance is safe to
///     reuse for every segment of a session and safe to share across threads.
///     </para>
/// </remarks>
internal sealed class PlaybackAudioResampler
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="PlaybackAudioResampler"/> class for one
    ///     fixed source rate and target playback format.
    /// </summary>
    /// <param name="sourceSampleRate">The synthesis engine's declared output rate, in Hz. Must be greater than zero.</param>
    /// <param name="targetSampleRate">The playback device's resolved sample rate, in Hz. Must be greater than zero.</param>
    /// <param name="targetChannelCount">The playback device's resolved channel count. Must be greater than zero.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown when any argument is less than or equal to zero, because no meaningful
    ///     conversion exists for a zero or negative rate or channel count.
    /// </exception>
    internal PlaybackAudioResampler(int sourceSampleRate, int targetSampleRate, int targetChannelCount)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(sourceSampleRate, 0);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(targetSampleRate, 0);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(targetChannelCount, 0);

        SourceSampleRate = sourceSampleRate;
        TargetSampleRate = targetSampleRate;
        TargetChannelCount = targetChannelCount;
    }

    /// <summary>Gets the synthesis engine's declared output rate, in Hz.</summary>
    internal int SourceSampleRate { get; }

    /// <summary>Gets the playback device's resolved sample rate, in Hz.</summary>
    internal int TargetSampleRate { get; }

    /// <summary>Gets the playback device's resolved channel count.</summary>
    internal int TargetChannelCount { get; }

    /// <summary>
    ///     Converts mono synthesized samples into interleaved samples at
    ///     <see cref="TargetSampleRate"/>/<see cref="TargetChannelCount"/>.
    /// </summary>
    /// <param name="monoSamples">The synthesized mono samples at <see cref="SourceSampleRate"/>. May be empty.</param>
    /// <returns>
    ///     A newly allocated array of interleaved samples, with a stride of
    ///     <see cref="TargetChannelCount"/>. Empty when the input is empty.
    /// </returns>
    internal float[] Convert(ReadOnlySpan<float> monoSamples)
    {
        var resampled = Resample(monoSamples, SourceSampleRate, TargetSampleRate);
        return UpmixToChannels(resampled, TargetChannelCount);
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
    ///     When the two rates are equal the input is copied unchanged, so the identity case (the
    ///     common case where a model's declared rate already matches the playback device) costs
    ///     nothing and introduces no interpolation error at all. When downsampling, the input is
    ///     first low-pass filtered with a small Hamming-windowed sinc FIR kernel so energy above the
    ///     target Nyquist frequency is attenuated before decimation.
    /// </remarks>
    internal static float[] Resample(ReadOnlySpan<float> monoSamples, int sourceSampleRate, int targetSampleRate)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(sourceSampleRate, 0);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(targetSampleRate, 0);

        if (monoSamples.IsEmpty || sourceSampleRate == targetSampleRate)
        {
            return monoSamples.ToArray();
        }

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

    /// <summary>
    ///     Replicates mono samples across every output channel by interleaving the same value
    ///     <paramref name="channelCount"/> times per frame.
    /// </summary>
    /// <param name="monoSamples">The single-channel samples to replicate. May be empty.</param>
    /// <param name="channelCount">The number of output channels. Must be greater than zero.</param>
    /// <returns>
    ///     A newly allocated array of length <c>monoSamples.Length * channelCount</c>. Empty when
    ///     the input is empty.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown when <paramref name="channelCount"/> is less than or equal to zero.
    /// </exception>
    /// <remarks>
    ///     A single output channel is copied unchanged, keeping the common single-channel
    ///     playback device case exact and allocation-minimal.
    /// </remarks>
    internal static float[] UpmixToChannels(ReadOnlySpan<float> monoSamples, int channelCount)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(channelCount, 0);

        if (channelCount == 1)
        {
            return monoSamples.ToArray();
        }

        if (monoSamples.IsEmpty)
        {
            return [];
        }

        var interleaved = new float[monoSamples.Length * channelCount];
        for (var frame = 0; frame < monoSamples.Length; frame++)
        {
            var offset = frame * channelCount;
            interleaved.AsSpan(offset, channelCount).Fill(monoSamples[frame]);
        }

        return interleaved;
    }
}
