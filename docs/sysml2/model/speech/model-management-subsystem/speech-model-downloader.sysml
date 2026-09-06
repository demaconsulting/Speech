package Speech {
    part def SpeechModelDownloader {
        doc /* Queued (one-at-a-time) fetch-verify-install orchestration for a model download:
             * uses an injected IModelDownloadClient to fetch each file to a staging directory,
             * verifies each file's SHA-256 checksum, and hands a fully-verified staging
             * directory to SpeechModelStore for atomic swap - a corrupted, partial, or
             * checksum-mismatched download is never marked installed. */

        comment sourceRef /* Source: src/DemaConsulting.Speech/ModelManagementSubsystem/SpeechModelDownloader.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Tests/ModelManagementSubsystem/SpeechModelDownloaderTests.cs */
        comment designRef /* Design: docs/design/speech/model-management-subsystem/speech-model-downloader.md */
        comment verificationRef /* Verification: docs/verification/speech/model-management-subsystem/speech-model-downloader.md */
        comment reqRef /* Requirements: docs/reqstream/speech/model-management-subsystem/speech-model-downloader.yaml */
    }
}
