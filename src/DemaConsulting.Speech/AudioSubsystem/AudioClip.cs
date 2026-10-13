namespace DemaConsulting.Speech.AudioSubsystem;

/// <summary>
///     An immutable, fully decoded audio clip: interleaved floating-point samples plus the format they were
///     decoded from.
/// </summary>
/// <remarks>
///     Produced by <see cref="AudioFileReader"/>. Samples are scaled to the range [-1, 1]. The sample array is
///     shared, not copied, so callers must not modify it. Instances are safe to share across threads as long as
///     nobody writes to <see cref="Samples"/>. Construction (and a <c>with</c> expression) validates the values like
///     <see cref="AudioFormat"/> does: <see cref="Samples"/> must not be <see langword="null"/>, and
///     <see cref="SampleRate"/> and <see cref="ChannelCount"/> must be greater than zero, so <see cref="Duration"/>
///     and <see cref="ToMono"/> are always well defined. Equality compares the <see cref="Samples"/> array by
///     reference, not by content.
/// </remarks>
/// <param name="Samples">The interleaved samples, <c>frame * ChannelCount + channel</c>.</param>
/// <param name="SampleRate">The sample rate in Hz; must be positive.</param>
/// <param name="ChannelCount">The number of interleaved channels; must be at least one.</param>
/// <param name="BitsPerSample">The bit depth of the source file (informational).</param>
/// <exception cref="ArgumentNullException">Thrown when <paramref name="Samples"/> is <see langword="null"/>.</exception>
/// <exception cref="ArgumentOutOfRangeException">
///     Thrown when <paramref name="SampleRate"/> or <paramref name="ChannelCount"/> is less than or equal to zero.
/// </exception>
public sealed record AudioClip(float[] Samples, int SampleRate, int ChannelCount, int BitsPerSample)
{
    /// <summary>Backing store for <see cref="Samples"/>; rejects <see langword="null"/>.</summary>
    private readonly float[] _samples = Samples ?? throw new ArgumentNullException(nameof(Samples));

    /// <summary>Backing store for <see cref="SampleRate"/>; validated to be positive.</summary>
    private readonly int _sampleRate = Positive(SampleRate, nameof(SampleRate));

    /// <summary>Backing store for <see cref="ChannelCount"/>; validated to be positive.</summary>
    private readonly int _channelCount = Positive(ChannelCount, nameof(ChannelCount));

    /// <summary>Gets the interleaved samples, <c>frame * ChannelCount + channel</c>.</summary>
    public float[] Samples
    {
        get => _samples;
        init => _samples = value ?? throw new ArgumentNullException(nameof(Samples));
    }

    /// <summary>Gets the sample rate in Hz; always positive.</summary>
    public int SampleRate
    {
        get => _sampleRate;
        init => _sampleRate = Positive(value, nameof(SampleRate));
    }

    /// <summary>Gets the number of interleaved channels; always at least one.</summary>
    public int ChannelCount
    {
        get => _channelCount;
        init => _channelCount = Positive(value, nameof(ChannelCount));
    }

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
            var scaled = double.IsNaN(Samples[i]) ? 0.0 : Math.Round(Samples[i] * 32768.0);
            pcm[i] = (short)Math.Clamp(scaled, short.MinValue, short.MaxValue);
        }

        return pcm;
    }

    /// <summary>Returns <paramref name="value"/> when it is positive; otherwise throws, naming <paramref name="name"/>.</summary>
    private static int Positive(int value, string name)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(name, value, $"{name} must be greater than zero.");
        }

        return value;
    }
}
