package SpeechTextEn_TrainingDataSubsystem {
    private import SoftwareItemMetadata::*;
    private import SpeechTextEn_CheckDataCommand::*;
    private import SpeechTextEn_CheckReport::*;
    private import SpeechTextEn_CorpusDataChecker::*;
    private import SpeechTextEn_DataManifest::*;
    private import SpeechTextEn_EvaluationOverlayChecker::*;
    private import SpeechTextEn_LeakageChecker::*;
    private import SpeechTextEn_LicenseChecker::*;
    private import SpeechTextEn_SchemaChecker::*;
    private import SpeechTextEn_SpanValidator::*;
    private import SpeechTextEn_TextEnDataSet::*;
    private import SpeechTextEn_TsvTable::*;

    part def TrainingDataSubsystem {
        doc /* The check-data command of the non-shipped Text.En trainer: the strict tab-separated reader (TsvTable),
             * the typed data loader with the closed source vocabulary (TextEnDataSet), the manifest verifier
             * (DataManifest), the whole-word span check (SpanValidator), the finding collector (CheckReport), the
             * schema, license and leakage checks (SchemaChecker, LicenseChecker, LeakageChecker), the check of the
             * regression overlay and the generalization set (EvaluationOverlayChecker), the offline check of the corpus
             * pins and removals (CorpusDataChecker) and the command line entry (CheckDataCommand). It reads the training and evaluation data in data/text-en/, which is a tool
             * input and not a software item, and uses only the .NET base class library. */

        metadata info : SoftwareInfo {
            testFiles = ("test/DemaConsulting.Speech.Text.En.Trainer.Tests/TrainingDataSubsystemTests.cs");
            designFiles = ("docs/design/speech-text-en/training-data-subsystem.md");
            verificationFiles = ("docs/verification/speech-text-en/training-data-subsystem.md");
            requirementFiles = ("docs/reqstream/speech-text-en/training-data-subsystem.yaml");
        }

        part tsvTable : TsvTable;
        part textEnDataSet : TextEnDataSet;
        part dataManifest : DataManifest;
        part spanValidator : SpanValidator;
        part checkReport : CheckReport;
        part schemaChecker : SchemaChecker;
        part licenseChecker : LicenseChecker;
        part leakageChecker : LeakageChecker;
        part checkDataCommand : CheckDataCommand;
        part evaluationOverlayChecker : EvaluationOverlayChecker;
        part corpusDataChecker : CorpusDataChecker;
    }
}
