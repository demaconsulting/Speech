package Speech_IModelDownloadClient {
    private import SoftwareItemMetadata::*;
    private import Speech_SpeechModelDownloadProgress::*;

    part def IModelDownloadClient {
        doc /* Mockable seam for fetching one downloadable file's bytes to a local destination stream, isolating
             * SpeechModelDownloader's orchestration logic from any real HTTP client so it can be unit tested without
             * real network access. This four-parameter interface carries no authentication parameter of its own and
             * is unchanged by mirror support - a credentialed DownloadMirror's authentication is applied only by the
             * concrete HttpModelDownloadClient at construction time, never through this generic seam, so every
             * host-supplied implementation remains source-compatible. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech/ModelManagementSubsystem/IModelDownloadClient.cs");
            testFiles = ("test/DemaConsulting.Speech.Tests/ModelManagementSubsystem/SpeechModelDownloaderTests.cs");
            designFiles = ("docs/design/speech/model-management-subsystem/i-model-download-client.md");
            verificationFiles = ("docs/verification/speech/model-management-subsystem/i-model-download-client.md");
            requirementFiles = ("docs/reqstream/speech/model-management-subsystem/i-model-download-client.yaml");
        }

        // Reports file transfer progress through the download progress type.
        dependency IModelDownloadClient to SpeechModelDownloadProgress;
    }
}
