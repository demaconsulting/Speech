package SpeechTextEn_EvaluationOverlay {
    private import SoftwareItemMetadata::*;
    private import SpeechTextEn_CheckReport::*;
    private import SpeechTextEn_TextEnDataSet::*;
    private import SpeechTextEn_TrainingCorpus::*;

    part def EvaluationOverlay {
        doc /* Loader of the two evaluation overlays of the data folder, the regression overlay (regression.tsv) and
             * the blind generalization set (generalization.tsv), as rows the evaluator can score, each with its
             * stratum (seen or unseen regression origin, cue-solvable or cue-unsolvable generalization kind), its
             * report slices and its bootstrap cluster (the family of a generalization row, the row itself for a
             * regression row). The overlays are not splits: only the evaluation reads them, never training or model
             * selection. */

        metadata info : SoftwareInfo {
            sourceFiles = ("tools/DemaConsulting.Speech.Text.En.Trainer/TrainingSubsystem/EvaluationOverlay.cs");
            testFiles = ("test/DemaConsulting.Speech.Text.En.Trainer.Tests/TrainingSubsystem/EvaluationOverlayTests.cs");
            designFiles = ("docs/design/speech-text-en/training-subsystem/evaluation-overlay.md");
            verificationFiles = ("docs/verification/speech-text-en/training-subsystem/evaluation-overlay.md");
            requirementFiles = ("docs/reqstream/speech-text-en/training-subsystem/evaluation-overlay.yaml");
        }

        // Loads regression and generalization overlay tables into corpus-ready rows, reporting errors to
        // the check report.
        dependency EvaluationOverlay to CheckReport;
        dependency EvaluationOverlay to TextEnDataSet;
        dependency EvaluationOverlay to TrainingCorpus;
    }
}
