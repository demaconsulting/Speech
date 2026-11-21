using DemaConsulting.Speech.Onnx.NemotronStt.RecognitionSubsystem;

namespace DemaConsulting.Speech.Onnx.NemotronStt.Tests.RecognitionSubsystem;

/// <summary>
///     Unit tests for <see cref="NemotronEndpointDetector"/>: each Sherpa-equivalent rule, disabled
///     rules, padding and removed-silence accounting, and rule validation. The detector is pure, so
///     the tests drive it with frame and silence events directly.
/// </summary>
public sealed class NemotronEndpointDetectorTests
{
    /// <summary>The input duration of one real-model encoder frame (80 ms at 16 kHz).</summary>
    private const long Frame = 1280;

    /// <summary>Converts milliseconds to samples at 16 kHz.</summary>
    /// <param name="milliseconds">The duration.</param>
    /// <returns>The sample count.</returns>
    private static long Samples(int milliseconds) => (long)milliseconds * 16;

    /// <summary>Feeds <paramref name="count"/> blank real frames.</summary>
    /// <param name="detector">The detector.</param>
    /// <param name="count">The number of frames.</param>
    private static void Blanks(NemotronEndpointDetector detector, int count)
    {
        for (var i = 0; i < count; i++)
        {
            detector.AdvanceFrame(Frame, emittedToken: false);
        }
    }

    /// <summary>Proves a fresh detector reports no endpoint.</summary>
    [Fact]
    public void NemotronEndpointDetector_Check_NoFrames_ReturnsNone()
    {
        // Arrange: a detector with the default rules
        var detector = new NemotronEndpointDetector();

        // Act: check before any frame
        var reason = detector.Check();

        // Assert: nothing has ended and the clocks are at zero
        Assert.Equal(NemotronEndpointReason.None, reason);
        Assert.False(detector.HasSpeech);
        Assert.Equal(0, detector.TrailingSilenceSamples);
        Assert.Equal(0, detector.UtteranceSamples);
    }

    /// <summary>Proves 1.2 s of blank frames after a token ends the utterance by rule 2.</summary>
    [Fact]
    public void NemotronEndpointDetector_Check_TrailingBlanksAfterTokenReachAfterSpeechRule_ReturnsTrailingSilenceAfterSpeech()
    {
        // Arrange: a detector that has seen a token
        var detector = new NemotronEndpointDetector();
        detector.AdvanceFrame(Frame, emittedToken: true);

        // Act: fifteen blank 80 ms frames (exactly 1.2 s) follow
        Blanks(detector, 15);

        // Assert: rule 2 fires
        Assert.Equal(NemotronEndpointReason.TrailingSilenceAfterSpeech, detector.Check());
        Assert.Equal(Samples(1200), detector.TrailingSilenceSamples);
    }

    /// <summary>Proves trailing silence shorter than 1.2 s after a token does not end the utterance.</summary>
    [Fact]
    public void NemotronEndpointDetector_Check_TrailingBlanksAfterTokenBelowRule_ReturnsNone()
    {
        // Arrange: a detector that has seen a token
        var detector = new NemotronEndpointDetector();
        detector.AdvanceFrame(Frame, emittedToken: true);

        // Act: fourteen blank frames (1.12 s) follow
        Blanks(detector, 14);

        // Assert: no rule fires yet
        Assert.Equal(NemotronEndpointReason.None, detector.Check());
    }

    /// <summary>Proves silence without speech does not use the shorter after-speech rule.</summary>
    [Fact]
    public void NemotronEndpointDetector_Check_SilenceWithoutTokenBelowLongRule_ReturnsNone()
    {
        // Arrange: a detector that has seen no token
        var detector = new NemotronEndpointDetector();

        // Act: 2.32 s of blank frames, past rule 2 but short of rule 1
        Blanks(detector, 29);

        // Assert: no rule fires
        Assert.Equal(NemotronEndpointReason.None, detector.Check());
    }

    /// <summary>Proves 2.4 s of silence without speech ends the segment by rule 1.</summary>
    [Fact]
    public void NemotronEndpointDetector_Check_SilenceWithoutTokenReachesLongRule_ReturnsTrailingSilence()
    {
        // Arrange: a detector that has seen no token
        var detector = new NemotronEndpointDetector();

        // Act: thirty blank frames (2.4 s)
        Blanks(detector, 30);

        // Assert: rule 1 fires
        Assert.Equal(NemotronEndpointReason.TrailingSilence, detector.Check());
    }

    /// <summary>Proves 20 s of continuous speech ends the utterance by rule 3.</summary>
    [Fact]
    public void NemotronEndpointDetector_Check_UtteranceReachesMaximum_ReturnsMaxUtteranceLength()
    {
        // Arrange: a detector with the default rules
        var detector = new NemotronEndpointDetector();

        // Act: 249 token frames (19.92 s) then one more (20.0 s), checking in between
        for (var i = 0; i < 249; i++)
        {
            detector.AdvanceFrame(Frame, emittedToken: true);
        }

        var before = detector.Check();
        detector.AdvanceFrame(Frame, emittedToken: true);

        // Assert: rule 3 fires exactly at the maximum
        Assert.Equal(NemotronEndpointReason.None, before);
        Assert.Equal(NemotronEndpointReason.MaxUtteranceLength, detector.Check());
    }

    /// <summary>Proves padding frames alone never satisfy any rule.</summary>
    [Fact]
    public void NemotronEndpointDetector_AdvancePaddingFrame_OnlyPadding_NeverEndpoints()
    {
        // Arrange: a detector that has seen a token
        var detector = new NemotronEndpointDetector();
        detector.AdvanceFrame(Frame, emittedToken: true);

        // Act: a thousand blank padding frames (80 s if they counted)
        for (var i = 0; i < 1000; i++)
        {
            detector.AdvancePaddingFrame(emittedToken: false);
        }

        // Assert: neither clock moved, so no rule fires
        Assert.Equal(NemotronEndpointReason.None, detector.Check());
        Assert.Equal(0, detector.TrailingSilenceSamples);
        Assert.Equal(Frame, detector.UtteranceSamples);
    }

    /// <summary>Proves a token decoded on padding counts as speech and restarts the trailing silence.</summary>
    [Fact]
    public void NemotronEndpointDetector_AdvancePaddingFrame_TokenOnPadding_CountsAsSpeech()
    {
        // Arrange: a detector with one second of blank frames and no speech
        var detector = new NemotronEndpointDetector();
        Blanks(detector, 12);

        // Act: a padding frame emits a token
        detector.AdvancePaddingFrame(emittedToken: true);

        // Assert: the segment holds speech and the trailing silence restarted
        Assert.True(detector.HasSpeech);
        Assert.Equal(0, detector.TrailingSilenceSamples);
    }

    /// <summary>Proves quiet audio removed by the limiter counts toward the trailing silence and the length.</summary>
    [Fact]
    public void NemotronEndpointDetector_AdvanceSilence_DroppedQuietAfterToken_CountsTowardTrailingSilence()
    {
        // Arrange: a token followed by 400 ms of blank frames (the limiter's passed quiet)
        var detector = new NemotronEndpointDetector();
        detector.AdvanceFrame(Frame, emittedToken: true);
        Blanks(detector, 5);

        // Act: 800 ms of removed quiet audio is credited
        detector.AdvanceSilence(Samples(800));

        // Assert: 1.2 s of silence in total ends the utterance, and the length includes the removed audio
        Assert.Equal(NemotronEndpointReason.TrailingSilenceAfterSpeech, detector.Check());
        Assert.Equal(Frame * 6 + Samples(800), detector.UtteranceSamples);
    }

    /// <summary>Proves a token after silence restarts the trailing-silence clock.</summary>
    [Fact]
    public void NemotronEndpointDetector_AdvanceFrame_TokenAfterSilence_RestartsTrailingSilence()
    {
        // Arrange: a token followed by 1.12 s of blank frames
        var detector = new NemotronEndpointDetector();
        detector.AdvanceFrame(Frame, emittedToken: true);
        Blanks(detector, 14);

        // Act: another token, then one blank frame
        detector.AdvanceFrame(Frame, emittedToken: true);
        Blanks(detector, 1);

        // Assert: only the last blank frame is trailing silence, so no rule fires
        Assert.Equal(Frame, detector.TrailingSilenceSamples);
        Assert.Equal(NemotronEndpointReason.None, detector.Check());
    }

    /// <summary>Proves Reset starts a new, empty segment and the carried-speech overload keeps speech.</summary>
    [Fact]
    public void NemotronEndpointDetector_Reset_AfterEndpoint_StartsNewSegment()
    {
        // Arrange: a detector at a rule-2 endpoint
        var detector = new NemotronEndpointDetector();
        detector.AdvanceFrame(Frame, emittedToken: true);
        Blanks(detector, 15);
        var carrying = new NemotronEndpointDetector();
        carrying.AdvanceFrame(Frame, emittedToken: true);

        // Act: start new segments, one carrying speech
        detector.Reset();
        carrying.Reset(carriesSpeech: true);

        // Assert: the clocks restart; speech is kept only when carried
        Assert.Equal(NemotronEndpointReason.None, detector.Check());
        Assert.False(detector.HasSpeech);
        Assert.Equal(0, detector.TrailingSilenceSamples);
        Assert.Equal(0, detector.UtteranceSamples);
        Assert.True(carrying.HasSpeech);
        Assert.Equal(0, carrying.UtteranceSamples);
    }

    /// <summary>Proves a rule set to zero is disabled while the others still apply.</summary>
    [Fact]
    public void NemotronEndpointDetector_Check_RuleSetToZero_DisablesRule()
    {
        // Arrange: rule 2 and rule 3 disabled, rule 1 at 2.4 s
        var detector = new NemotronEndpointDetector(new NemotronEndpointRules(TrailingSilenceAfterSpeechMs: 0, MaxUtteranceMs: 0));
        for (var i = 0; i < 300; i++)
        {
            detector.AdvanceFrame(Frame, emittedToken: true);
        }

        // Act: 2.32 s and then 2.4 s of blank frames after speech
        Blanks(detector, 29);
        var before = detector.Check();
        Blanks(detector, 1);

        // Assert: neither the 24 s length nor the 1.2 s silence fired; rule 1 fires at 2.4 s
        Assert.Equal(NemotronEndpointReason.None, before);
        Assert.Equal(NemotronEndpointReason.TrailingSilence, detector.Check());
    }

    /// <summary>Proves custom rule durations are honored.</summary>
    [Fact]
    public void NemotronEndpointDetector_Check_CustomAfterSpeechRule_FiresAtConfiguredSilence()
    {
        // Arrange: rule 2 at 400 ms
        var detector = new NemotronEndpointDetector(new NemotronEndpointRules(TrailingSilenceAfterSpeechMs: 400));
        detector.AdvanceFrame(Frame, emittedToken: true);

        // Act: four, then five blank frames
        Blanks(detector, 4);
        var before = detector.Check();
        Blanks(detector, 1);

        // Assert: the rule fires at 400 ms
        Assert.Equal(NemotronEndpointReason.None, before);
        Assert.Equal(NemotronEndpointReason.TrailingSilenceAfterSpeech, detector.Check());
    }

    /// <summary>Proves negative rule durations and a non-positive sample rate are rejected.</summary>
    [Fact]
    public void NemotronEndpointDetector_Constructor_NegativeRule_ThrowsArgumentOutOfRangeException()
    {
        // Act / Assert: every negative rule and a zero rate are rejected
        Assert.Throws<ArgumentOutOfRangeException>(() => new NemotronEndpointDetector(new NemotronEndpointRules(TrailingSilenceMs: -1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new NemotronEndpointDetector(new NemotronEndpointRules(TrailingSilenceAfterSpeechMs: -1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new NemotronEndpointDetector(new NemotronEndpointRules(MaxUtteranceMs: -1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new NemotronEndpointDetector(null, 0));
    }

    /// <summary>Proves negative frame and silence durations are rejected.</summary>
    [Fact]
    public void NemotronEndpointDetector_AdvanceFrame_NegativeDuration_ThrowsArgumentOutOfRangeException()
    {
        // Arrange: a detector with the default rules
        var detector = new NemotronEndpointDetector();

        // Act / Assert: negative durations are rejected
        Assert.Throws<ArgumentOutOfRangeException>(() => detector.AdvanceFrame(-1, emittedToken: false));
        Assert.Throws<ArgumentOutOfRangeException>(() => detector.AdvanceSilence(-1));
    }

    /// <summary>Proves the default rules are the Sherpa-equivalent 2.4 s, 1.2 s and 20 s.</summary>
    [Fact]
    public void NemotronEndpointDetector_Rules_Default_AreSherpaEquivalent()
    {
        // Arrange / Act: a detector with no explicit rules
        var rules = new NemotronEndpointDetector().Rules;

        // Assert: the defaults
        Assert.Equal(2400, rules.TrailingSilenceMs);
        Assert.Equal(1200, rules.TrailingSilenceAfterSpeechMs);
        Assert.Equal(20000, rules.MaxUtteranceMs);
    }
}
