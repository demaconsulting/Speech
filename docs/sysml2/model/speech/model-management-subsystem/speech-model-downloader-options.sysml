package Speech {
    part def SpeechModelDownloaderOptions {
        doc /* Host-configurable options for SpeechModelDownloader, currently limited to an
             * optional DownloadMirror that, when supplied, redirects every declared file's
             * effective download request beneath that mirror instead of its own declared URI.
             * A null Mirror (the default) preserves SpeechModelDownloader's exact pre-mirror
             * behavior with zero change for existing callers. */

        comment sourceRef /* Source: src/DemaConsulting.Speech/ModelManagementSubsystem/SpeechModelDownloaderOptions.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Tests/ModelManagementSubsystem/SpeechModelDownloaderTests.cs */
        comment designRef /* Design: docs/design/speech/model-management-subsystem/speech-model-downloader-options.md */
        comment verificationRef /* Verification: docs/verification/speech/model-management-subsystem/speech-model-downloader-options.md */
        comment reqRef /* Requirements: docs/reqstream/speech/model-management-subsystem/speech-model-downloader-options.yaml */
    }
}
