using DemaConsulting.Speech.SynthesisSubsystem;

namespace DemaConsulting.Speech.Tests.SynthesisSubsystem;

/// <summary>
///     Unit tests for the <see cref="SynthesizedSpeech"/> record.
/// </summary>
public class SynthesizedSpeechTests
{
    /// <summary>
    ///     Proves that valid, non-empty samples with a positive sample rate and non-negative
    ///     silences construct successfully and expose the supplied values.
    /// </summary>
    [Fact]
    public void SynthesizedSpeech_Constructor_ValidValues_ExposesValues()
    {
        // Arrange
        float[] samples = [0.1f, -0.2f];

        // Act
        var speech = new SynthesizedSpeech(samples, 16000, TimeSpan.FromMilliseconds(10), TimeSpan.FromMilliseconds(20));

        // Assert
        Assert.Equal(samples, speech.Samples);
        Assert.Equal(16000, speech.SampleRate);
        Assert.Equal(TimeSpan.FromMilliseconds(10), speech.PreSilence);
        Assert.Equal(TimeSpan.FromMilliseconds(20), speech.PostSilence);
    }

    /// <summary>
    ///     Proves that an empty samples buffer (a pure-pause segment) is accepted regardless of
    ///     sample rate, since a pause's sample rate is documented as meaningless and ignored by
    ///     playback.
    /// </summary>
    [Fact]
    public void SynthesizedSpeech_Constructor_EmptySamples_AcceptsAnySampleRate()
    {
        // Act
        var speech = new SynthesizedSpeech([], 0, TimeSpan.Zero, TimeSpan.Zero);

        // Assert
        Assert.Empty(speech.Samples);
    }

    /// <summary>
    ///     Proves that a null samples buffer is rejected with <see cref="ArgumentNullException"/>.
    /// </summary>
    [Fact]
    public void SynthesizedSpeech_Constructor_NullSamples_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(
            () => new SynthesizedSpeech(null!, 16000, TimeSpan.Zero, TimeSpan.Zero));
    }

    /// <summary>
    ///     Proves that a non-positive sample rate is rejected when samples are non-empty, since
    ///     playback cannot interpret real audio without a meaningful rate.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void SynthesizedSpeech_Constructor_NonEmptySamplesWithNonPositiveSampleRate_ThrowsArgumentException(int sampleRate)
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(
            () => new SynthesizedSpeech([0.1f], sampleRate, TimeSpan.Zero, TimeSpan.Zero));
    }

    /// <summary>
    ///     Proves that a negative pre-silence or post-silence duration is rejected, since negative
    ///     silence is not a meaningful playback instruction.
    /// </summary>
    [Fact]
    public void SynthesizedSpeech_Constructor_NegativeSilence_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(
            () => new SynthesizedSpeech([], 16000, TimeSpan.FromMilliseconds(-1), TimeSpan.Zero));
        Assert.Throws<ArgumentException>(
            () => new SynthesizedSpeech([], 16000, TimeSpan.Zero, TimeSpan.FromMilliseconds(-1)));
    }
}
