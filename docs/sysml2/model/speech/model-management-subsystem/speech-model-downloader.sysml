package Speech {
    part def SpeechModelDownloader {
        doc /* Queued (one-at-a-time) fetch-verify-install orchestration for a model download:
             * uses an injected IModelDownloadClient to fetch each file to a staging directory,
             * verifies each file's SHA-256 checksum, and hands a fully-verified staging
             * directory to SpeechModelStore for atomic swap - a corrupted, partial, or
             * checksum-mismatched download is never marked installed. An optional
             * SpeechModelDownloaderOptions.Mirror (a DownloadMirror) redirects every file's
             * effective request URI beneath a single internal mirror, resolved by an internal
             * ResolveEffectiveUri helper, with the mirror's authentication applied only to
             * requests sent to it. A download/install failure beyond ChecksumMismatch is
             * further classified by an internal ClassifyFailure helper into HttpError,
             * NetworkBlocked, IoFailure, or the generic Failed fallback, while a genuine
             * caller cancellation always propagates unchanged as OperationCanceledException. */

        comment sourceRef /* Source: src/DemaConsulting.Speech/ModelManagementSubsystem/SpeechModelDownloader.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Tests/ModelManagementSubsystem/SpeechModelDownloaderTests.cs */
        comment designRef /* Design: docs/design/speech/model-management-subsystem/speech-model-downloader.md */
        comment verificationRef /* Verification: docs/verification/speech/model-management-subsystem/speech-model-downloader.md */
        comment reqRef /* Requirements: docs/reqstream/speech/model-management-subsystem/speech-model-downloader.yaml */
    }
}
