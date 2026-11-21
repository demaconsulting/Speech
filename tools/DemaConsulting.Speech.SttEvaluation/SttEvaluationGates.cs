using DemaConsulting.Speech.Onnx.NemotronStt.ModelManagementSubsystem;

namespace DemaConsulting.Speech.SttEvaluation;

/// <summary>
///     The accuracy gates of the speech-to-text evaluation: the single source of the thresholds
///     used by the evaluation tool's <c>--check</c> and by the real-model tests.
/// </summary>
/// <remarks>
///     Only corpus word error rate is gated. Segment counts, invented words, latency and real-time
///     factor are reported but never gated by these constants, because they depend on the scenario
///     (segments, invented words) or on the machine (latency, speed). Compiled (linked) into the
///     tests that apply the same gates.
/// </remarks>
internal static class SttEvaluationGates
{
    /// <summary>The id of the model the gates apply to: the ONNX Nemotron 3.5 streaming model.</summary>
    public const string GatedModelId = OnnxNemotronMultilingualRecognitionModel.ModelId;

    /// <summary>
    ///     The highest accepted corpus word error rate on the LibriSpeech test-clean subset
    ///     (<see cref="SttDataSet.LibriSpeechSet"/>), as a fraction.
    /// </summary>
    public const double LibriSpeechMaximumWordErrorRate = 0.06;

    /// <summary>
    ///     The highest accepted word error rate on the "Crossing the Bar" recitation
    ///     (<see cref="SttDataSet.CrossingTheBarSet"/>), as a fraction. Higher than the LibriSpeech
    ///     gate because the poem has archaic words ("crost", "bourne") a modern vocabulary may not
    ///     spell as the text does.
    /// </summary>
    public const double CrossingTheBarMaximumWordErrorRate = 0.10;
}
