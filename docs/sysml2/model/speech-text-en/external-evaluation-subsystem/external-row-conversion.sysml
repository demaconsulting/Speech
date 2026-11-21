package SpeechTextEn_ExternalRowConversion {
    private import SoftwareItemMetadata::*;
    private import SpeechTextEn_LeakageChecker::*;
    private import SpeechTextEn_ReadingInventory::*;
    private import SpeechTextEn_TrainingCorpus::*;

    part def ExternalRowConversion {
        doc /* The conversion shared by the dataset adapters: UTF-8 byte offsets to UTF-16 character offsets
             * (ByteOffsets), the strict id-only mapping of dataset labels to reading identifiers (ReadingIdMap), and
             * the conversion of adapter records to evaluator rows with a count of every skipped row by reason and of
             * rows that equal a training or development sentence (ExternalRowConverter). Also holds the
             * ExternalRecord, ExternalReadResult, ConversionCounts, ExternalRowSet and SkipReasons types. */

        metadata info : SoftwareInfo {
            sourceFiles = ("tools/DemaConsulting.Speech.Text.En.Trainer/ExternalEvaluationSubsystem/ExternalRowConversion.cs");
            testFiles = ("test/DemaConsulting.Speech.Text.En.Trainer.Tests/ExternalEvaluationSubsystem/ExternalRowConversionTests.cs");
            designFiles = ("docs/design/speech-text-en/external-evaluation-subsystem/external-row-conversion.md");
            verificationFiles = ("docs/verification/speech-text-en/external-evaluation-subsystem/external-row-conversion.md");
            requirementFiles = ("docs/reqstream/speech-text-en/external-evaluation-subsystem/external-row-conversion.yaml");
        }

        // Maps dataset labels to inventory readings and rows to corpus form, with leakage checks against
        // our data.
        dependency ExternalRowConversion to LeakageChecker;
        dependency ExternalRowConversion to ReadingInventory;
        dependency ExternalRowConversion to TrainingCorpus;
    }
}
