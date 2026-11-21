package SpeechTextEn_EvaluationRecord {
    private import SoftwareItemMetadata::*;
    private import SpeechTextEn_HeteronymEvaluator::*;
    private import SpeechTextEn_TrainingRecord::*;

    part def EvaluationRecord {
        doc /* Renderer and verifier of the evaluation record: six deterministic tab-separated files (summary, gate,
             * by-source, per-reading, confidence, seeds) of integers and basis points with LF line ends and no time
             * stamps, written to a folder and compared with it byte for byte. Tab-separated rather than Markdown so
             * the repository formatters cannot reflow them. Also holds the EvaluationReport record. */

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
