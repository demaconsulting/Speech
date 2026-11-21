package SpeechTextEn_ExternalEvaluationRecord {
    private import SoftwareItemMetadata::*;
    private import SpeechTextEn_EvaluationRecord::*;
    private import SpeechTextEn_ExternalRowConversion::*;
    private import SpeechTextEn_ExternalSourceCache::*;
    private import SpeechTextEn_HeteronymEvaluator::*;
    private import SpeechTextEn_TrainingRecord::*;

    part def ExternalEvaluationRecord {
        doc /* Renderer and verifier of the external evaluation record: seven deterministic tab-separated files
             * (sources, conversion, summary, by-source, per-reading, confidence, seeds) of integers and basis points
             * with LF line ends and no time stamps, written to a folder and compared with it byte for byte. They hold
             * only numbers, pins and our own reading identifiers, never a sentence, word or label of an external
             * dataset. Also holds the ExternalEvaluationReport, ExternalSetResult and ExternalSeedResult records. */

        metadata info : SoftwareInfo {
            sourceFiles = ("tools/DemaConsulting.Speech.Text.En.Trainer/ExternalEvaluationSubsystem/ExternalEvaluationRecord.cs");
            testFiles = ("test/DemaConsulting.Speech.Text.En.Trainer.Tests/ExternalEvaluationSubsystem/ExternalEvaluationRecordTests.cs");
            designFiles = ("docs/design/speech-text-en/external-evaluation-subsystem/external-evaluation-record.md");
            verificationFiles = ("docs/verification/speech-text-en/external-evaluation-subsystem/external-evaluation-record.md");
            requirementFiles = ("docs/reqstream/speech-text-en/external-evaluation-subsystem/external-evaluation-record.yaml");
        }

        // Renders external evaluator results with source-cache and conversion details, reusing record
        // formats.
        dependency ExternalEvaluationRecord to EvaluationRecord;
        dependency ExternalEvaluationRecord to ExternalRowConversion;
        dependency ExternalEvaluationRecord to ExternalSourceCache;
        dependency ExternalEvaluationRecord to HeteronymEvaluator;
        dependency ExternalEvaluationRecord to TrainingRecord;
    }
}
