package SpeechTextEn_EvaluationOverlayChecker {
    private import SoftwareItemMetadata::*;
    private import SpeechTextEn_CheckReport::*;
    private import SpeechTextEn_LeakageChecker::*;
    private import SpeechTextEn_SpanValidator::*;
    private import SpeechTextEn_TextEnDataSet::*;

    part def EvaluationOverlayChecker {
        doc /* The check-data overlay check: keeps the regression overlay (references to owner-flagged, golden,
             * analyzer-test and existing training or development sentences, never a holdout, with correct
             * seen-in-training marks) and the blind generalization set (rows that stay novel against the training and
             * development data per lemma group, with balanced quotas) what they claim to be, and checks a staged
             * generalization set before it is committed with reasons that reveal no training data to the blind
             * writer. */

        metadata info : SoftwareInfo {
            sourceFiles = ("tools/DemaConsulting.Speech.Text.En.Trainer/TrainingDataSubsystem/EvaluationOverlayChecker.cs");
            testFiles = ("test/DemaConsulting.Speech.Text.En.Trainer.Tests/TrainingDataSubsystem/EvaluationOverlayCheckerTests.cs");
            designFiles = ("docs/design/speech-text-en/training-data-subsystem/evaluation-overlay-checker.md");
            verificationFiles = ("docs/verification/speech-text-en/training-data-subsystem/evaluation-overlay-checker.md");
            requirementFiles = ("docs/reqstream/speech-text-en/training-data-subsystem/evaluation-overlay-checker.yaml");
        }

        // Validates overlay spans and leakage against the data set and reports violations to the check
        // report.
        dependency EvaluationOverlayChecker to CheckReport;
        dependency EvaluationOverlayChecker to LeakageChecker;
        dependency EvaluationOverlayChecker to SpanValidator;
        dependency EvaluationOverlayChecker to TextEnDataSet;
    }
}
