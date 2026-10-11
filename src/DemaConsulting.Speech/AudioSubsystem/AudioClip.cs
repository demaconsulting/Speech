namespace DemaConsulting.Speech.AudioSubsystem;

/// <summary>
///     An immutable, fully decoded audio clip: interleaved floating-point samples plus the format they were
///     decoded from.
/// </summary>
/// <remarks>
///     Produced by <see cref="AudioFileReader"/>. Samples are scaled to the range [-1, 1). The sample array is
///     shared, not copied, so callers must not modify it. Instances are safe to share across threads as long as
///     nobody writes to <see cref="Samples"/>.
/// </remarks>
/// <param name="Samples">The interleaved samples, <c>frame * ChannelCount + channel</c>.</param>
/// <param name="SampleRate">The sample rate in Hz; must be positive.</param>
/// <param name="ChannelCount">The number of interleaved channels; must be at least one.</param>
/// <param name="BitsPerSample">The bit depth of the source file (informational).</param>
public sealed record AudioClip(float[] Samples, int SampleRate, int ChannelCount, int BitsPerSample)
{
    /// <summary>Gets the number of sample frames (samples per channel).</summary>
    public int FrameCount => Samples.Length / ChannelCount;

    /// <summary>Gets the clip length in seconds.</summary>
    public double Duration => (double)FrameCount / SampleRate;

    /// <summary>
    ///     Averages all channels into one mono stream.
    /// </summary>
    /// <returns>A new mono sample array (a copy when the clip is already mono).</returns>
    public float[] ToMono() => AudioResampler.DownMix(Samples, ChannelCount);

    /// <summary>
    ///     Converts the interleaved samples to 16-bit PCM, clamping out-of-range values.
    /// </summary>
    /// <returns>A new array of interleaved 16-bit samples.</returns>
    public short[] ToPcm16()
    {
        var pcm = new short[Samples.Length];
        for (var i = 0; i < pcm.Length; i++)
        {
            var scaled = Math.Round(Samples[i] * 32768.0);
            pcm[i] = (short)Math.Clamp(scaled, short.MinValue, short.MaxValue);
        }

        return pcm;
    }
}
