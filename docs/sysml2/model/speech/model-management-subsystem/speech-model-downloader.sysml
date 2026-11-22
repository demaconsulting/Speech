package Speech_SpeechModelDownloader {
    private import SoftwareItemMetadata::*;
    private import Speech_DownloadMirror::*;
    private import Speech_HttpModelDownloadClient::*;
    private import Speech_IModelDownloadClient::*;
    private import Speech_ISpeechDiagnostics::*;
    private import Speech_NullSpeechDiagnostics::*;
    private import Speech_SpeechDiagnosticLevel::*;
    private import Speech_SpeechModelContract::*;
    private import Speech_SpeechModelDownloadDescriptor::*;
    private import Speech_SpeechModelDownloadFile::*;
    private import Speech_SpeechModelDownloadProgress::*;
    private import Speech_SpeechModelDownloaderOptions::*;
    private import Speech_SpeechModelStore::*;

    enum def SpeechModelDownloadOutcome {
        enum Installed;
        enum ChecksumMismatch;
        enum Failed;
        enum NetworkBlocked;
        enum HttpError;
        enum IoFailure;
    }

    part def SpeechModelDownloader {
        doc /* Queued (one-at-a-time) fetch-verify-install orchestration for a model download: uses an injected
             * IModelDownloadClient to fetch each file to a staging directory, verifies each file's SHA-256 checksum,
             * and hands a fully-verified staging directory to SpeechModelStore for atomic swap - a corrupted,
             * partial, or checksum-mismatched download is never marked installed. An optional
             * SpeechModelDownloaderOptions.Mirror (a DownloadMirror) redirects every file's effective request URI
             * beneath a single internal mirror, resolved by an internal ResolveEffectiveUri helper, with the mirror's
             * authentication applied only to requests sent to it. A download/install failure beyond ChecksumMismatch
             * is further classified by an internal ClassifyFailure helper into HttpError, NetworkBlocked, IoFailure,
             * or the generic Failed fallback, while a genuine caller cancellation always propagates unchanged as
             * OperationCanceledException. With the built-in HttpModelDownloadClient each file is fetched into a
             * resumable partial file in the store's .partial directory and continued with an HTTP Range request on the
             * next call. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech/ModelManagementSubsystem/SpeechModelDownloader.cs");
            testFiles = ("test/DemaConsulting.Speech.Tests/ModelManagementSubsystem/SpeechModelDownloaderTests.cs");
            designFiles = ("docs/design/speech/model-management-subsystem/speech-model-downloader.md");
            verificationFiles = ("docs/verification/speech/model-management-subsystem/speech-model-downloader.md");
            requirementFiles = ("docs/reqstream/speech/model-management-subsystem/speech-model-downloader.yaml");
        }

        // Fetches files through the download client (resuming through the concrete HTTP client's ranged download),
        // verifies them, and installs them atomically in the store (keeping partial files in its partial directory).
        dependency SpeechModelDownloader to DownloadMirror;
        dependency SpeechModelDownloader to HttpModelDownloadClient;
        dependency SpeechModelDownloader to IModelDownloadClient;
        dependency SpeechModelDownloader to ISpeechDiagnostics;
        dependency SpeechModelDownloader to NullSpeechDiagnostics;
        dependency SpeechModelDownloader to SpeechDiagnosticLevel;
        dependency SpeechModelDownloader to SpeechModelContract;
        dependency SpeechModelDownloader to SpeechModelDownloadDescriptor;
        dependency SpeechModelDownloader to SpeechModelDownloadFile;
        dependency SpeechModelDownloader to SpeechModelDownloadProgress;
        dependency SpeechModelDownloader to SpeechModelDownloaderOptions;
        dependency SpeechModelDownloader to SpeechModelStore;
    }
}
