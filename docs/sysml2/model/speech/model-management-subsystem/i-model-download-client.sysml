package Speech {
    part def IModelDownloadClient {
        doc /* Mockable seam for fetching one downloadable file's bytes to a local destination
             * stream, isolating SpeechModelDownloader's orchestration logic from any real HTTP
             * client so it can be unit tested without real network access. This four-parameter
             * interface carries no authentication parameter of its own and is unchanged by
             * mirror support - a credentialed DownloadMirror's authentication is applied
             * only by the concrete HttpModelDownloadClient at construction time, never
             * through this generic seam, so every host-supplied implementation remains
             * source-compatible. */

        comment sourceRef /* Source: src/DemaConsulting.Speech/ModelManagementSubsystem/IModelDownloadClient.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Tests/ModelManagementSubsystem/SpeechModelDownloaderTests.cs */
        comment designRef /* Design: docs/design/speech/model-management-subsystem/i-model-download-client.md */
        comment verificationRef /* Verification: docs/verification/speech/model-management-subsystem/i-model-download-client.md */
        comment reqRef /* Requirements: docs/reqstream/speech/model-management-subsystem/i-model-download-client.yaml */
    }
}
