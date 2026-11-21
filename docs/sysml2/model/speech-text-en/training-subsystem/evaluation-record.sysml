package SpeechTextEn_EvaluationRecord {
    private import SoftwareItemMetadata::*;
    private import SpeechTextEn_HeteronymEvaluator::*;
    private import SpeechTextEn_TrainingRecord::*;

    part def EvaluationRecord {
        doc /* Renderer and verifier of the evaluation record: ten deterministic tab-separated files (summary, gate,
             * by-source, per-reading, confidence, seeds, overlay-rows, curated-ablation, gate-rows, flips) of
             * integers, basis points and reading ids with LF line ends and no time stamps, written to a folder and
             * compared with it byte for byte. The previous column of the gate rows rotates from the committed record:
             * equal to the current one on a first write, kept for the same model image, and taken from the committed
             * current column for a new image. Tab-separated rather than Markdown so the repository formatters cannot
             * reflow them. Also holds the EvaluationReport, GateRowState and CommittedGate records. */

        metadata info : SoftwareInfo {
            sourceFiles = ("tools/DemaConsulting.Speech.Text.En.Trainer/TrainingSubsystem/EvaluationRecord.cs");
            testFiles = ("test/DemaConsulting.Speech.Text.En.Trainer.Tests/TrainingSubsystem/EvaluationRecordTests.cs");
            designFiles = ("docs/design/speech-text-en/training-subsystem/evaluation-record.md");
            verificationFiles = ("docs/verification/speech-text-en/training-subsystem/evaluation-record.md");
            requirementFiles = ("docs/reqstream/speech-text-en/training-subsystem/evaluation-record.yaml");
        }

        // Renders evaluator results as deterministic files and verifies them against training record
        // conventions.
        dependency EvaluationRecord to HeteronymEvaluator;
        dependency EvaluationRecord to TrainingRecord;
    }
}
