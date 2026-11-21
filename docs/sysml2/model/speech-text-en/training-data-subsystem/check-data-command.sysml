package SpeechTextEn_CheckDataCommand {
    private import SoftwareItemMetadata::*;
    private import SpeechTextEn_CheckReport::*;
    private import SpeechTextEn_DataManifest::*;
    private import SpeechTextEn_EvaluationOverlayChecker::*;
    private import SpeechTextEn_LeakageChecker::*;
    private import SpeechTextEn_LicenseChecker::*;
    private import SpeechTextEn_SchemaChecker::*;
    private import SpeechTextEn_TextEnDataSet::*;

    part def CheckDataCommand {
        doc /* Command line entry of the trainer's check-data command: parses the options, finds the data folder, runs
             * the manifest, load, schema, license and leakage checks in a fixed order and returns the exit code (0
             * pass, 1 findings, 2 usage or input error). The Program.cs entry point is glue that dispatches to this
             * unit. */

        metadata info : SoftwareInfo {
            sourceFiles = ("tools/DemaConsulting.Speech.Text.En.Trainer/TrainingDataSubsystem/CheckDataCommand.cs", "tools/DemaConsulting.Speech.Text.En.Trainer/Program.cs");
            testFiles = ("test/DemaConsulting.Speech.Text.En.Trainer.Tests/TrainingDataSubsystem/CheckDataCommandTests.cs");
            designFiles = ("docs/design/speech-text-en/training-data-subsystem/check-data-command.md");
            verificationFiles = ("docs/verification/speech-text-en/training-data-subsystem/check-data-command.md");
            requirementFiles = ("docs/reqstream/speech-text-en/training-data-subsystem/check-data-command.yaml");
        }

        // Loads the data set and runs manifest, schema, license, leakage and overlay checks into a check
        // report.
        dependency CheckDataCommand to CheckReport;
        dependency CheckDataCommand to DataManifest;
        dependency CheckDataCommand to EvaluationOverlayChecker;
        dependency CheckDataCommand to LeakageChecker;
        dependency CheckDataCommand to LicenseChecker;
        dependency CheckDataCommand to SchemaChecker;
        dependency CheckDataCommand to TextEnDataSet;
    }
}
