package SpeechTextEn {
    part def ExternalSourceCache {
        doc /* The pins and the local cache of the external datasets: loads and cross-checks the committed pins
             * (external-sources.json and external-files.tsv), fetches a pinned archive over HTTPS only when
             * asked, writes only pinned paths into a partial folder, verifies every file by size and SHA-256
             * and publishes the folder only when all files are exact, and reads cached files only after
             * verifying them again. Also holds the PinnedFile, ExternalSourcePin and ExternalSourcePins
             * records and the default archive download. */

        comment sourceRef /* Source: tools/DemaConsulting.Speech.Text.En.Trainer/ExternalEvaluationSubsystem/ExternalSourceCache.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Text.En.Trainer.Tests/ExternalEvaluationSubsystem/ExternalSourceCacheTests.cs */
        comment designRef /* Design: docs/design/speech-text-en/external-evaluation-subsystem/external-source-cache.md */
        comment verificationRef /* Verification: docs/verification/speech-text-en/external-evaluation-subsystem/external-source-cache.md */
        comment reqRef /* Requirements: docs/reqstream/speech-text-en/external-evaluation-subsystem/external-source-cache.yaml */
    }
}
