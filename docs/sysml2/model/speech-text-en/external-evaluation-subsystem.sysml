package SpeechTextEn_ExternalEvaluationSubsystem {
    private import SoftwareItemMetadata::*;
    private import SpeechTextEn_EvaluateExternalCommand::*;
    private import SpeechTextEn_ExternalEvaluationRecord::*;
    private import SpeechTextEn_ExternalRowConversion::*;
    private import SpeechTextEn_ExternalSourceCache::*;
    private import SpeechTextEn_HeteronymBenchAdapter::*;
    private import SpeechTextEn_WikipediaHomographAdapter::*;

    part def ExternalEvaluationSubsystem {
        doc /* The evaluate-external command of the non-shipped Text.En trainer: it measures the embedded heteronym
             * model on external, evaluation-only datasets (Google WikipediaHomographData and heteronym-bench) fetched
             * at evaluation time from pinned revisions into a git-ignored or out-of-repository cache. The pinned
             * source cache verifies every file by size and SHA-256 (ExternalSourceCache), the shared row conversion
             * maps byte offsets and dataset labels to readings and accounts for skipped rows (ExternalRowConversion),
             * the two adapters read the datasets (WikipediaHomographAdapter, HeteronymBenchAdapter), the record holds
             * only numbers, pins and our own reading identifiers (ExternalEvaluationRecord), and the command
             * orchestrates them (EvaluateExternalCommand). External rows are evaluated through the
             * TrainingSubsystem's evaluator and are never trained on or used to choose a setting. */

        metadata info : SoftwareInfo {
            testFiles = ("test/DemaConsulting.Speech.Text.En.Trainer.Tests/ExternalEvaluationSubsystemTests.cs");
            designFiles = ("docs/design/speech-text-en/external-evaluation-subsystem.md");
            verificationFiles = ("docs/verification/speech-text-en/external-evaluation-subsystem.md");
            requirementFiles = ("docs/reqstream/speech-text-en/external-evaluation-subsystem.yaml");
        }

        part externalSourceCache : ExternalSourceCache;
        part externalRowConversion : ExternalRowConversion;
        part wikipediaHomographAdapter : WikipediaHomographAdapter;
        part heteronymBenchAdapter : HeteronymBenchAdapter;
        part externalEvaluationRecord : ExternalEvaluationRecord;
        part evaluateExternalCommand : EvaluateExternalCommand;
    }
}
