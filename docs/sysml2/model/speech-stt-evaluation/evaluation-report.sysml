package SpeechSttEvaluation_EvaluationReport {
    private import SoftwareItemMetadata::*;
    private import SpeechSttEvaluation_SttEvaluationGates::*;
    private import SpeechSttEvaluation_SttTranscriber::*;
    private import SpeechSttEvaluation_WordErrorRateCalculator::*;

    part def EvaluationReport {
        doc /* Formats evaluation results: applies the accuracy gates of --check (GateResult), prints the console
             * summary, and writes the per-model summary, item and transcript files and the comparison with a cross
             * word error rate, tab-separated with invariant numbers, only below the resolved output folder inside the
             * git-ignored artifacts folder. */

        metadata info : SoftwareInfo {
            sourceFiles = ("tools/DemaConsulting.Speech.SttEvaluation/EvaluationReport.cs");
        }

        // Reads the runner's results and transcripts, gates them, and aligns compared transcripts.
        dependency EvaluationReport to SpeechSttEvaluation_EvaluationRunner::EvaluationRunner;
        dependency EvaluationReport to SttEvaluationGates;
        dependency EvaluationReport to SttTranscriber;
        dependency EvaluationReport to WordErrorRateCalculator;
    }
}