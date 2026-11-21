package SpeechTextEn_EvaluateCommand {
    private import SoftwareItemMetadata::*;
    private import SpeechTextEn_CheckDataCommand::*;
    private import SpeechTextEn_EvaluationOverlay::*;
    private import SpeechTextEn_EvaluationRecord::*;
    private import SpeechTextEn_HeteronymEvaluator::*;
    private import SpeechTextEn_ModelSelection::*;
    private import SpeechTextEn_SparsePerceptronModel::*;
    private import SpeechTextEn_TrainCommand::*;
    private import SpeechTextEn_TrainingCorpus::*;
    private import SpeechTextEn_TrainingRecord::*;

    part def EvaluateCommand {
        doc /* Command line entry of the trainer's evaluate command: evaluates the model on the development and
             * holdout sets and the evaluation overlays, decides the statistical quality gate over the regression
             * overlay, both holdouts and the generalization set against the recorded thresholds, prints the flips of
             * the model under test against the committed record, runs the multi-seed study over the gate sets, and
             * writes or verifies the evaluation record. A holdout file is read only when the gate, an explicit
             * holdout set or the record asks for it. Exit codes: 0 done or gate passed, 1 gate failed or record
             * stale, 2 usage or input error or a missing gate set. The Program.cs entry point only dispatches to this
             * unit. */

        metadata info : SoftwareInfo {
            sourceFiles = ("tools/DemaConsulting.Speech.Text.En.Trainer/TrainingSubsystem/EvaluateCommand.cs", "tools/DemaConsulting.Speech.Text.En.Trainer/Program.cs");
            testFiles = ("test/DemaConsulting.Speech.Text.En.Trainer.Tests/TrainingSubsystem/EvaluateCommandTests.cs");
            designFiles = ("docs/design/speech-text-en/training-subsystem/evaluate-command.md");
            verificationFiles = ("docs/verification/speech-text-en/training-subsystem/evaluate-command.md");
            requirementFiles = ("docs/reqstream/speech-text-en/training-subsystem/evaluate-command.yaml");
        }

        // Loads the model and corpus, evaluates them with overlays, applies the quality gate and writes
        // the evaluation record.
        dependency EvaluateCommand to CheckDataCommand;
        dependency EvaluateCommand to EvaluationOverlay;
        dependency EvaluateCommand to EvaluationRecord;
        dependency EvaluateCommand to HeteronymEvaluator;
        dependency EvaluateCommand to ModelSelection;
        dependency EvaluateCommand to SparsePerceptronModel;
        dependency EvaluateCommand to TrainCommand;
        dependency EvaluateCommand to TrainingCorpus;
        dependency EvaluateCommand to TrainingRecord;
    }
}
