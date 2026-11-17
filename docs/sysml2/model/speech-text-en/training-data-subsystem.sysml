package SpeechTextEn {
    part def TrainingDataSubsystem {
        doc /* The check-data command of the non-shipped Text.En trainer: the strict tab-separated reader
             * (TsvTable), the typed data loader with the closed source vocabulary (TextEnDataSet), the
             * manifest verifier (DataManifest), the whole-word span check (SpanValidator), the finding
             * collector (CheckReport), the schema, license and leakage checks (SchemaChecker,
             * LicenseChecker, LeakageChecker) and the command line entry (CheckDataCommand). It reads
             * the training and evaluation data in data/text-en/, which is a tool input and not a
             * software item, and uses only the .NET base class library. */

        comment designRef /* Design: docs/design/speech-text-en/training-data-subsystem.md */
        comment verificationRef /* Verification: docs/verification/speech-text-en/training-data-subsystem.md */
        comment reqRef /* Requirements: docs/reqstream/speech-text-en/training-data-subsystem.yaml */
        comment testRef /* Test: test/DemaConsulting.Speech.Text.En.Trainer.Tests/TrainingDataSubsystemTests.cs */

        part tsvTable : TsvTable;
        part textEnDataSet : TextEnDataSet;
        part dataManifest : DataManifest;
        part spanValidator : SpanValidator;
        part checkReport : CheckReport;
        part schemaChecker : SchemaChecker;
        part licenseChecker : LicenseChecker;
        part leakageChecker : LeakageChecker;
        part checkDataCommand : CheckDataCommand;
    }
}
