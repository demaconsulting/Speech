package SpeechTextEn_CorpusLeakageAudit {
    private import SoftwareItemMetadata::*;
    private import SpeechTextEn_CheckReport::*;
    private import SpeechTextEn_CorpusReader::*;
    private import SpeechTextEn_GutenbergBody::*;
    private import SpeechTextEn_LeakageChecker::*;
    private import SpeechTextEn_TextEnDataSet::*;

    part def CorpusLeakageAudit {
        doc /* Finds every corpus sentence that repeats, nearly repeats or quotes a sentence of an evaluation set
             * (dev, both holdouts, the regression overlay and the generalization set), with the data check's own
             * sentence keys and thresholds, so it is removed before use, and records the removals reproducibly in
             * cluster-removals.tsv (ids and hashes only). */

        metadata info : SoftwareInfo {
            sourceFiles = ("tools/DemaConsulting.Speech.Text.En.Trainer/CorpusSubsystem/CorpusLeakageAudit.cs");
            testFiles = ("test/DemaConsulting.Speech.Text.En.Trainer.Tests/CorpusSubsystem/CorpusLeakageAuditTests.cs");
            designFiles = ("docs/design/speech-text-en/corpus-subsystem/corpus-leakage-audit.md");
            verificationFiles = ("docs/verification/speech-text-en/corpus-subsystem/corpus-leakage-audit.md");
            requirementFiles = ("docs/reqstream/speech-text-en/corpus-subsystem/corpus-leakage-audit.yaml");
        }

        // Compares the tokenized corpus sentences with the evaluation sets using the data check's sentence keys and
        // thresholds, and hashes the removed sentences.
        dependency CorpusLeakageAudit to CheckReport;
        dependency CorpusLeakageAudit to CorpusReader;
        dependency CorpusLeakageAudit to GutenbergBody;
        dependency CorpusLeakageAudit to LeakageChecker;
        dependency CorpusLeakageAudit to TextEnDataSet;
    }
}
