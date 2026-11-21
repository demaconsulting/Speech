package SpeechTextEn_ExternalSourceCache {
    private import SoftwareItemMetadata::*;

    part def ExternalSourceCache {
        doc /* The pins and the local cache of the external datasets: loads and cross-checks the committed pins
             * (external-sources.json and external-files.tsv), fetches a pinned archive over HTTPS only when asked,
             * writes only pinned paths into a partial folder, verifies every file by size and SHA-256 and publishes
             * the folder only when all files are exact, and reads cached files only after verifying them again. Also
             * holds the PinnedFile, ExternalSourcePin and ExternalSourcePins records and the default archive
             * download. */

        metadata info : SoftwareInfo {
            sourceFiles = ("tools/DemaConsulting.Speech.Text.En.Trainer/ExternalEvaluationSubsystem/ExternalSourceCache.cs");
            testFiles = ("test/DemaConsulting.Speech.Text.En.Trainer.Tests/ExternalEvaluationSubsystem/ExternalSourceCacheTests.cs");
            designFiles = ("docs/design/speech-text-en/external-evaluation-subsystem/external-source-cache.md");
            verificationFiles = ("docs/verification/speech-text-en/external-evaluation-subsystem/external-source-cache.md");
            requirementFiles = ("docs/reqstream/speech-text-en/external-evaluation-subsystem/external-source-cache.yaml");
        }
    }
}
