package SpeechTextEn_TrainCommand {
    private import SoftwareItemMetadata::*;
    private import SpeechTextEn_CheckDataCommand::*;
    private import SpeechTextEn_ModelSelection::*;
    private import SpeechTextEn_SparsePerceptronModel::*;
    private import SpeechTextEn_TrainingCorpus::*;
    private import SpeechTextEn_TrainingRecord::*;

    part def TrainCommand {
        doc /* Command line entry of the trainer's train command: trains the model from the training and development
             * data and writes the model file and the training record, or with --check verifies that the committed
             * model and record are what retraining produces. Experiment options need --model-dir so the committed
             * files cannot be overwritten. Exit codes: 0 trained or current, 1 stale, 2 usage or input error. The
             * Program.cs entry point only dispatches to this unit. */

        metadata info : SoftwareInfo {
            sourceFiles = ("tools/DemaConsulting.Speech.Text.En.Trainer/TrainingSubsystem/TrainCommand.cs", "tools/DemaConsulting.Speech.Text.En.Trainer/Program.cs");
            testFiles = ("test/DemaConsulting.Speech.Text.En.Trainer.Tests/TrainingSubsystem/TrainCommandTests.cs");
            designFiles = ("docs/design/speech-text-en/training-subsystem/train-command.md");
            verificationFiles = ("docs/verification/speech-text-en/training-subsystem/train-command.md");
            requirementFiles = ("docs/reqstream/speech-text-en/training-subsystem/train-command.yaml");
        }

        // Loads the training corpus, builds the model via model selection and writes the model and
        // training record.
        dependency TrainCommand to CheckDataCommand;
        dependency TrainCommand to ModelSelection;
        dependency TrainCommand to SparsePerceptronModel;
        dependency TrainCommand to TrainingCorpus;
        dependency TrainCommand to TrainingRecord;
    }
}
