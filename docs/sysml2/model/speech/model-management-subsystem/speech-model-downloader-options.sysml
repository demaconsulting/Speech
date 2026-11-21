package Speech_SpeechModelDownloaderOptions {
    private import SoftwareItemMetadata::*;
    private import Speech_DownloadMirror::*;

    part def SpeechModelDownloaderOptions {
        doc /* Host-configurable options for SpeechModelDownloader, currently limited to an optional DownloadMirror
             * that, when supplied, redirects every declared file's effective download request beneath that mirror
             * instead of its own declared URI. A null Mirror (the default) preserves SpeechModelDownloader's exact
             * pre-mirror behavior with zero change for existing callers. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech/ModelManagementSubsystem/SpeechModelDownloaderOptions.cs");
            designFiles = ("docs/design/speech/model-management-subsystem/speech-model-downloader-options.md");
            verificationFiles = ("docs/verification/speech/model-management-subsystem/speech-model-downloader-options.md");
            requirementFiles = ("docs/reqstream/speech/model-management-subsystem/speech-model-downloader-options.yaml");
        }

        // Holds the optional download mirror that the downloader applies.
        dependency SpeechModelDownloaderOptions to DownloadMirror;
    }
}
