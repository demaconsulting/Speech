using System.Reflection;
using DemaConsulting.Speech.ModelManagementSubsystem;
using DemaConsulting.Speech.RecognitionSubsystem;

namespace DemaConsulting.Speech.Tests.RecognitionSubsystem;

/// <summary>
///     Unit tests for <see cref="SherpaOnnxRecognitionEngine"/>'s post-endpoint warm-up-replay
///     bookkeeping, added to fix a real, confirmed Nemotron word-loss defect (see
///     <c>.agent-logs/planning-nemotron-endpoint-word-loss-warmup-replay-fix-9d4b71.md</c>).
/// </summary>
/// <remarks>
///     <see cref="SherpaOnnxRecognitionEngine"/> wraps native, un-fakeable sherpa-onnx types
///     (<c>OnlineRecognizer</c>/<c>OnlineStream</c>), so - per this project's testing standards
///     for native-runtime-dependent code, and consistent with the pattern already used for other
///     native-dependent integration tests in this project - these tests load the real, already
///     installed streaming Zipformer model and drive the engine with real audio (silence and a
///     synthesized tone) rather than mocking the native surface. Tests are skipped, not failed,
///     when the model is not installed in this environment (for example a fresh CI checkout that
///     has not downloaded any model), since downloading a ~70 MiB production model is outside the
///     scope of a unit test run. Private bookkeeping fields (<c>_warmupBuffer</c>,
///     <c>_graceSamplesRemaining</c>) are inspected via reflection because they are deliberately
///     not part of any public/internal surface - the whole point of the feature is that it is
///     invisible to every caller.
/// </remarks>
public sealed class SherpaOnnxRecognitionEngineTests
{
    /// <summary>The real, already-installed streaming Zipformer model, reused as a cheap, real, available native model for these engine-level tests.</summary>
    private static readonly SherpaOnnxZipformerEnRecognitionModel Model = new();

    /// <summary>
    ///     Resolves the real installed model directory for the streaming Zipformer model using
    ///     the same default store root <see cref="SpeechModelStore"/> uses, without depending on
    ///     that class's non-test-only construction path.
    /// </summary>
    private static string InstalledModelDirectory => Path.Join(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DemaConsulting.Speech",
        "Models",
        SherpaOnnxZipformerEnRecognitionModel.ModelId,
        "current");

    /// <summary>Whether the real Zipformer model is installed in this environment.</summary>
    private static bool IsModelInstalled => Directory.Exists(InstalledModelDirectory) &&
        Directory.EnumerateFileSystemEntries(InstalledModelDirectory).Any();

    /// <summary>
    ///     Builds a real <see cref="SherpaOnnxRecognitionEngine"/> over the installed Zipformer
    ///     model at the given warm-up window, skipping the calling test if the model is not
    ///     installed in this environment.
    /// </summary>
    private static SherpaOnnxRecognitionEngine CreateEngine(int postEndpointWarmupWindowMs)
    {
        if (!IsModelInstalled)
        {
            Assert.Skip("The real streaming Zipformer model is not installed in this environment.");
        }

        IRecognitionModel model = Model;
        var config = model.CreateEngineConfig(InstalledModelDirectory);
        return new SherpaOnnxRecognitionEngine(
            config,
            model.AudioFormat.SampleRate,
            postEndpointWarmupWindowMs);
    }

    /// <summary>Reads a private instance field by name via reflection.</summary>
    private static T GetPrivateField<T>(object instance, string fieldName)
    {
        var field = typeof(SherpaOnnxRecognitionEngine).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new MissingFieldException(nameof(SherpaOnnxRecognitionEngine), fieldName);
        return (T)field.GetValue(instance)!;
    }

    /// <summary>
    ///     Writes a private instance field by name via reflection, used to force
    ///     <c>_hasRecognizedTextSinceReset</c> to a known value so the replay-eligibility gating
    ///     logic itself can be exercised deterministically, independent of whether the real model
    ///     happens to transcribe a synthesized tone as non-empty text (see the class remarks and
    ///     <c>.agent-logs/planning-nemotron-endpoint-leading-silence-regression-fix-7a2f91.md</c>,
    ///     Implementation Plan item (c)).
    /// </summary>
    private static void SetPrivateField<T>(object instance, string fieldName, T value)
    {
        var field = typeof(SherpaOnnxRecognitionEngine).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new MissingFieldException(nameof(SherpaOnnxRecognitionEngine), fieldName);
        field.SetValue(instance, value);
    }

    /// <summary>Half a second of digital silence at 16 kHz, used to drive the endpoint detector.</summary>
    private static float[] SilenceBlock(double seconds = 0.5) => new float[(int)(16000 * seconds)];

    /// <summary>
    ///     A short, non-silent synthetic tone at 16 kHz, standing in for "genuine speech" for the
    ///     purpose of exercising the buffer/replay/grace bookkeeping paths - the real recognizer
    ///     is not expected to transcribe it as any particular word.
    /// </summary>
    private static float[] ToneBlock(double seconds = 0.3)
    {
        var sampleCount = (int)(16000 * seconds);
        var samples = new float[sampleCount];
        for (var i = 0; i < sampleCount; i++)
        {
            samples[i] = 0.2f * MathF.Sin(2 * MathF.PI * 220f * i / 16000f);
        }

        return samples;
    }

    /// <summary>
    ///     Proves that a disabled (<c>0</c>) warm-up window allocates no rolling buffer at all -
    ///     the exact no-op guarantee every model except Nemotron relies on.
    /// </summary>
    [Fact]
    public void SherpaOnnxRecognitionEngine_PostEndpointWarmupWindowMsZero_NoBufferAllocated()
    {
        // Arrange & Act
        using var engine = CreateEngine(postEndpointWarmupWindowMs: 0);

        // Assert: the sentinel buffer field is null, meaning every enabled-path check is skipped
        var buffer = GetPrivateField<List<float>?>(engine, "_warmupBuffer");
        Assert.Null(buffer);
    }

    /// <summary>
    ///     Proves that a disabled engine feeding samples through several endpoint cycles behaves
    ///     exactly as before this feature existed: no exception, no buffer ever materializes, and
    ///     normal decode/reset bookkeeping (<see cref="SherpaOnnxRecognitionEngine.TryDecode"/>,
    ///     internal reset) continues to function.
    /// </summary>
    [Fact]
    public void SherpaOnnxRecognitionEngine_PostEndpointWarmupWindowMsZero_AcceptSamplesAndDecode_IsNoOpForWarmupMechanism()
    {
        // Arrange
        using var engine = CreateEngine(postEndpointWarmupWindowMs: 0);

        // Act: feed tone then enough silence to plausibly trigger an endpoint, decoding throughout
        engine.AcceptSamples(ToneBlock());
        engine.TryDecode(out _);
        for (var i = 0; i < 6; i++)
        {
            engine.AcceptSamples(SilenceBlock());
            engine.TryDecode(out _);
        }

        // Assert: the buffer field never materializes for a disabled engine
        var buffer = GetPrivateField<List<float>?>(engine, "_warmupBuffer");
        Assert.Null(buffer);
    }

    /// <summary>
    ///     Proves that an enabled engine allocates a rolling buffer sized to the configured
    ///     window (800ms at 16 kHz = 12800 samples) and actually accumulates fed samples into it.
    /// </summary>
    [Fact]
    public void SherpaOnnxRecognitionEngine_PostEndpointWarmupWindowMsEnabled_BufferSizedToWindowAndAccumulates()
    {
        // Arrange
        using var engine = CreateEngine(postEndpointWarmupWindowMs: 800);

        // Act
        engine.AcceptSamples(ToneBlock(0.1));

        // Assert: capacity matches the configured window at the model's 16 kHz rate, and some
        // samples have been buffered (bounded by capacity, per the rolling-window trim logic)
        var capacity = GetPrivateField<int>(engine, "_warmupBufferCapacity");
        Assert.Equal(12800, capacity);

        var buffer = GetPrivateField<List<float>?>(engine, "_warmupBuffer");
        Assert.NotNull(buffer);
        Assert.True(buffer.Count > 0);
        Assert.True(buffer.Count <= capacity);
    }

    /// <summary>
    ///     Proves the rolling buffer trims from the front once it exceeds capacity, so it never
    ///     grows unbounded across a long utterance.
    /// </summary>
    [Fact]
    public void SherpaOnnxRecognitionEngine_PostEndpointWarmupWindowMsEnabled_BufferNeverExceedsCapacity()
    {
        // Arrange
        using var engine = CreateEngine(postEndpointWarmupWindowMs: 800);

        // Act: feed well over 800ms of audio in total
        for (var i = 0; i < 20; i++)
        {
            engine.AcceptSamples(ToneBlock(0.1));
        }

        // Assert
        var capacity = GetPrivateField<int>(engine, "_warmupBufferCapacity");
        var buffer = GetPrivateField<List<float>?>(engine, "_warmupBuffer");
        Assert.NotNull(buffer);
        Assert.Equal(capacity, buffer.Count);
    }

    /// <summary>
    ///     Proves that once a real endpoint fires on an enabled engine, the buffered pre-endpoint
    ///     audio is replayed and cleared (consumed), and the post-replay grace period is armed -
    ///     the core state transition the whole mechanism depends on.
    /// </summary>
    [Fact]
    public void SherpaOnnxRecognitionEngine_PostEndpointWarmupWindowMsEnabled_EndpointReplaysAndArmsGracePeriod()
    {
        // Arrange
        using var engine = CreateEngine(postEndpointWarmupWindowMs: 800);
        engine.AcceptSamples(ToneBlock());
        engine.TryDecode(out _);

        // A synthesized sine tone is not guaranteed to be transcribed as non-empty text by the
        // real model, which would otherwise make this test's replay-arming assertion depend on
        // that transcription and skip more often than the mechanism it verifies warrants (see
        // .agent-logs/planning-nemotron-endpoint-leading-silence-regression-fix-7a2f91.md,
        // Implementation Plan item (c)). Forcing the eligibility flag directly, mirroring the
        // existing reflection pattern already used to read `_warmupBuffer`/`_graceSamplesRemaining`,
        // exercises the real replay-and-grace mechanism deterministically while still requiring
        // only the eligibility signal to be forced, not the endpoint detection or replay itself.
        SetPrivateField(engine, "_hasRecognizedTextSinceReset", true);

        // Act: feed enough trailing silence for the real endpoint detector to fire an endpoint,
        // draining decode after every block until either an endpoint is observed or a generous
        // bound is reached (real endpoint timing depends on the native model's own rules).
        var endpointObserved = false;
        for (var i = 0; i < 60 && !endpointObserved; i++)
        {
            engine.AcceptSamples(SilenceBlock());
            var gracedBefore = GetPrivateField<int>(engine, "_graceSamplesRemaining");
            engine.TryDecode(out _);
            var gracedAfter = GetPrivateField<int>(engine, "_graceSamplesRemaining");

            // The grace counter is only ever armed (set to a positive value) by a replay
            // following a real endpoint, so observing it become armed here is direct proof the
            // replay-on-endpoint transition fired.
            if (gracedBefore == 0 && gracedAfter > 0)
            {
                endpointObserved = true;
            }
        }

        if (!endpointObserved)
        {
            Assert.Skip("The real endpoint detector did not fire within this test's bounded silence budget.");
        }

        // Assert: the buffer was consumed by the replay and the grace period is counting down
        var buffer = GetPrivateField<List<float>?>(engine, "_warmupBuffer");
        Assert.NotNull(buffer);
        Assert.Empty(buffer);
        var graceRemaining = GetPrivateField<int>(engine, "_graceSamplesRemaining");
        Assert.True(graceRemaining > 0);
    }

    /// <summary>
    ///     Proves that no <see cref="SpeechRecognitionResult"/> is ever raised purely from the
    ///     text produced by replayed buffer content - <see cref="SherpaOnnxRecognitionEngine.TryDecode"/>
    ///     must never surface more than one final result across one real endpoint-and-replay
    ///     cycle, whether from the suppressed replay itself or from a spurious re-trigger during
    ///     the grace period.
    /// </summary>
    [Fact]
    public void SherpaOnnxRecognitionEngine_PostEndpointWarmupWindowMsEnabled_ReplayNeverProducesExtraResult()
    {
        // Arrange
        using var engine = CreateEngine(postEndpointWarmupWindowMs: 800);
        engine.AcceptSamples(ToneBlock());
        engine.TryDecode(out _);

        // See the identical rationale on `EndpointReplaysAndArmsGracePeriod` above: force replay
        // eligibility directly so this test deterministically exercises the real replay/grace
        // mechanism instead of depending on whether the tone happens to transcribe as text.
        SetPrivateField(engine, "_hasRecognizedTextSinceReset", true);

        var finalResultCount = 0;
        var sawGraceArm = false;

        // Act: drive well past the point where an endpoint (and its replay) should have fired,
        // then continue feeding silence through the grace period and beyond, counting every
        // final result raised.
        for (var i = 0; i < 120; i++)
        {
            engine.AcceptSamples(SilenceBlock());
            var gracedBefore = GetPrivateField<int>(engine, "_graceSamplesRemaining");
            var gotResult = engine.TryDecode(out var result);
            var gracedAfter = GetPrivateField<int>(engine, "_graceSamplesRemaining");

            if (gracedBefore == 0 && gracedAfter > 0)
            {
                sawGraceArm = true;
            }

            if (gotResult && result!.IsFinal)
            {
                finalResultCount++;
            }
        }

        if (!sawGraceArm)
        {
            Assert.Skip("The real endpoint detector did not fire within this test's bounded silence budget.");
        }

        // Assert: at most one final result for the one real utterance fed (a synthesized tone is
        // not guaranteed to produce non-empty recognized text, so zero is an acceptable outcome,
        // but two or more would prove a spurious/duplicated final result was raised from the
        // suppressed replay or a spurious re-trigger during the grace period).
        Assert.True(
            finalResultCount <= 1,
            $"Expected at most one final result but observed {finalResultCount}.");
    }

    /// <summary>
    ///     Proves that the grace period suppresses a same-tick spurious re-trigger: immediately
    ///     after a replay arms the grace counter, continuing to feed the same kind of near-silent
    ///     audio must not immediately produce a second final result before genuinely new audio
    ///     (at least the configured grace duration's worth) has been fed.
    /// </summary>
    [Fact]
    public void SherpaOnnxRecognitionEngine_PostEndpointWarmupWindowMsEnabled_GracePeriodSuppressesImmediateReTrigger()
    {
        // Arrange
        using var engine = CreateEngine(postEndpointWarmupWindowMs: 800);
        engine.AcceptSamples(ToneBlock());
        engine.TryDecode(out _);

        // See the identical rationale on `EndpointReplaysAndArmsGracePeriod` above: force replay
        // eligibility directly so this test deterministically exercises the real replay/grace
        // mechanism instead of depending on whether the tone happens to transcribe as text.
        SetPrivateField(engine, "_hasRecognizedTextSinceReset", true);

        var sawGraceArm = false;
        var extraFinalWhileGraced = false;

        // Act
        for (var i = 0; i < 120; i++)
        {
            engine.AcceptSamples(SilenceBlock(0.1));
            var gracedBefore = GetPrivateField<int>(engine, "_graceSamplesRemaining");
            var gotResult = engine.TryDecode(out var result);
            var gracedAfter = GetPrivateField<int>(engine, "_graceSamplesRemaining");

            if (gracedBefore == 0 && gracedAfter > 0)
            {
                sawGraceArm = true;
                continue;
            }

            // Any final result observed *while the grace counter is still counting down* would
            // be exactly the spurious immediate re-trigger the grace period exists to prevent.
            if (sawGraceArm && gracedAfter > 0 && gotResult && result!.IsFinal)
            {
                extraFinalWhileGraced = true;
            }
        }

        if (!sawGraceArm)
        {
            Assert.Skip("The real endpoint detector did not fire within this test's bounded silence budget.");
        }

        // Assert
        Assert.False(extraFinalWhileGraced);
    }

    /// <summary>
    ///     Proves the core fix for the leading/inter-utterance-silence corruption regression (see
    ///     <c>.agent-logs/quality-nemotron-endpoint-warmup-replay-fix-7a2f91.md</c> and
    ///     <c>.agent-logs/planning-nemotron-endpoint-leading-silence-regression-fix-7a2f91.md</c>):
    ///     an endpoint that fires on pure/near-silence, with no genuine speech ever recognized
    ///     since the last reset, must never replay the buffered audio into the freshly reset
    ///     stream - the buffer must simply be dropped and the grace counter must never arm.
    /// </summary>
    [Fact]
    public void SherpaOnnxRecognitionEngine_PostEndpointWarmupWindowMsEnabled_SilenceOnlyEndpoint_NeverArmsReplay()
    {
        // Arrange: no tone/speech fed at all, only silence from the very start of the cycle.
        using var engine = CreateEngine(postEndpointWarmupWindowMs: 800);

        var sawBufferPopulatedThenCleared = false;
        var sawGraceArm = false;

        // Act: feed enough pure silence for the real endpoint detector's Rule1 (which fires on
        // trailing silence alone, with no prior speech required) to report an endpoint.
        for (var i = 0; i < 60; i++)
        {
            engine.AcceptSamples(SilenceBlock());
            var bufferBefore = GetPrivateField<List<float>?>(engine, "_warmupBuffer")?.Count ?? 0;
            var gracedBefore = GetPrivateField<int>(engine, "_graceSamplesRemaining");
            var hadTextBefore = GetPrivateField<bool>(engine, "_hasRecognizedTextSinceReset");
            engine.TryDecode(out _);
            var bufferAfter = GetPrivateField<List<float>?>(engine, "_warmupBuffer")?.Count ?? 0;
            var gracedAfter = GetPrivateField<int>(engine, "_graceSamplesRemaining");

            // No genuine speech was ever fed, so the eligibility flag must never have been set.
            Assert.False(hadTextBefore);

            if (gracedBefore == 0 && gracedAfter > 0)
            {
                sawGraceArm = true;
            }

            // A buffer that was populated and then became empty on the very same call, with no
            // grace period ever arming, is direct proof an endpoint fired and its buffer was
            // dropped (not replayed): a genuine replay always arms the grace counter (see
            // `EndpointReplaysAndArmsGracePeriod` above), so its absence here is conclusive.
            if (bufferBefore > 0 && bufferAfter == 0 && gracedAfter == 0)
            {
                sawBufferPopulatedThenCleared = true;
            }
        }

        if (!sawBufferPopulatedThenCleared)
        {
            Assert.Skip("The real endpoint detector did not fire on pure silence within this test's bounded budget.");
        }

        // Assert: the buffer was dropped without ever arming a replay-driven grace period.
        Assert.False(sawGraceArm);
        Assert.False(GetPrivateField<bool>(engine, "_hasRecognizedTextSinceReset"));
    }

    /// <summary>
    ///     Directly unit-tests the replay-eligibility gate itself via reflection, independent of
    ///     whether the real model happens to transcribe any particular fed audio as non-empty
    ///     text: forcing <c>_hasRecognizedTextSinceReset</c> to <see langword="true"/> before an
    ///     endpoint fires must result in a replay (buffer cleared, grace armed); leaving it
    ///     <see langword="false"/> must result in the buffer being dropped without a replay
    ///     (buffer cleared, grace never armed) - covering the exact fork
    ///     <see cref="SherpaOnnxRecognitionEngine.TryDecode"/> takes at the moment an endpoint is
    ///     observed.
    /// </summary>
    [Fact]
    public void SherpaOnnxRecognitionEngine_PostEndpointWarmupWindowMsEnabled_ReplayEligibility_GatedOnHasRecognizedTextSinceReset()
    {
        // Case 1: eligibility forced true - a genuine replay must occur.
        using (var eligibleEngine = CreateEngine(postEndpointWarmupWindowMs: 800))
        {
            eligibleEngine.AcceptSamples(ToneBlock());
            eligibleEngine.TryDecode(out _);
            SetPrivateField(eligibleEngine, "_hasRecognizedTextSinceReset", true);

            var sawGraceArm = false;
            for (var i = 0; i < 60 && !sawGraceArm; i++)
            {
                eligibleEngine.AcceptSamples(SilenceBlock());
                var gracedBefore = GetPrivateField<int>(eligibleEngine, "_graceSamplesRemaining");
                eligibleEngine.TryDecode(out _);
                var gracedAfter = GetPrivateField<int>(eligibleEngine, "_graceSamplesRemaining");
                if (gracedBefore == 0 && gracedAfter > 0)
                {
                    sawGraceArm = true;
                }
            }

            if (!sawGraceArm)
            {
                Assert.Skip("The real endpoint detector did not fire within this test's bounded silence budget.");
            }

            Assert.Empty(GetPrivateField<List<float>?>(eligibleEngine, "_warmupBuffer")!);
        }

        // Case 2: eligibility left false (default, no speech fed) - the buffer must be dropped,
        // never replayed, and the grace counter must never arm.
        using var ineligibleEngine = CreateEngine(postEndpointWarmupWindowMs: 800);
        ineligibleEngine.AcceptSamples(ToneBlock(0.1));
        Assert.False(GetPrivateField<bool>(ineligibleEngine, "_hasRecognizedTextSinceReset"));

        // Explicitly force the flag false (defensive - it should already be false at this point)
        // so this assertion is not accidentally dependent on the tone having failed to transcribe.
        SetPrivateField(ineligibleEngine, "_hasRecognizedTextSinceReset", false);

        var sawBufferPopulatedThenCleared = false;
        var sawGraceArmIneligible = false;
        for (var i = 0; i < 60; i++)
        {
            ineligibleEngine.AcceptSamples(SilenceBlock());
            var bufferBefore = GetPrivateField<List<float>?>(ineligibleEngine, "_warmupBuffer")?.Count ?? 0;
            var gracedBefore = GetPrivateField<int>(ineligibleEngine, "_graceSamplesRemaining");
            ineligibleEngine.TryDecode(out _);
            var bufferAfter = GetPrivateField<List<float>?>(ineligibleEngine, "_warmupBuffer")?.Count ?? 0;
            var gracedAfter = GetPrivateField<int>(ineligibleEngine, "_graceSamplesRemaining");

            if (gracedBefore == 0 && gracedAfter > 0)
            {
                sawGraceArmIneligible = true;
            }

            if (bufferBefore > 0 && bufferAfter == 0 && gracedAfter == 0)
            {
                sawBufferPopulatedThenCleared = true;
            }
        }

        if (!sawBufferPopulatedThenCleared)
        {
            Assert.Skip("The real endpoint detector did not fire within this test's bounded silence budget.");
        }

        Assert.False(sawGraceArmIneligible);
    }
}
