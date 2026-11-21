package Speech_ReferenceVoiceLibrary {
    private import SoftwareItemMetadata::*;
    private import Speech_AudioFileReader::*;
    private import Speech_IModelDownloadClient::*;
    private import Speech_ISpeechDiagnostics::*;
    private import Speech_NullSpeechDiagnostics::*;
    private import Speech_ReferenceVoicePack::*;
    private import Speech_ReferenceVoicePackInstaller::*;
    private import Speech_SpeechDiagnosticLevel::*;
    private import Speech_SpeechModelDescriptorEnums::*;
    private import Speech_SpeechModelDownloadProgress::*;
    private import Speech_SpeechModelDownloader::*;
    private import Speech_SpeechModelDownloaderOptions::*;
    private import Speech_SpeechModelStore::*;
    private import Speech_SpeechModelStoreException::*;
    private import Speech_SpeechModelStoreOptions::*;

    part def ReferenceVoiceLibrary {
        doc /* Model-agnostic reference-voice library: installs and removes downloadable voice packs (optionally with
             * emotion variants, exposed as MoodClips) through the hash-verified model downloader, keeps a user voice
             * folder, and lists all available voices merged and de-duplicated (first id wins). */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech/ModelManagementSubsystem/ReferenceVoiceLibrary.cs");
            testFiles = ("test/DemaConsulting.Speech.Tests/ModelManagementSubsystem/ReferenceVoiceLibraryTests.cs");
            designFiles = ("docs/design/speech/model-management-subsystem/reference-voice-library.md");
            verificationFiles = ("docs/verification/speech/model-management-subsystem/reference-voice-library.md");
            requirementFiles = ("docs/reqstream/speech/model-management-subsystem/reference-voice-library.yaml");
        }

        // Downloads voice packs into the model store via the downloader and installer, then reads their
        // clips.
        dependency ReferenceVoiceLibrary to AudioFileReader;
        dependency ReferenceVoiceLibrary to IModelDownloadClient;
        dependency ReferenceVoiceLibrary to ISpeechDiagnostics;
        dependency ReferenceVoiceLibrary to NullSpeechDiagnostics;
        dependency ReferenceVoiceLibrary to ReferenceVoicePack;
        dependency ReferenceVoiceLibrary to ReferenceVoicePackInstaller;
        dependency ReferenceVoiceLibrary to SpeechDiagnosticLevel;
        dependency ReferenceVoiceLibrary to SpeechModelDescriptorEnums;
        dependency ReferenceVoiceLibrary to SpeechModelDownloadProgress;
        dependency ReferenceVoiceLibrary to SpeechModelDownloader;
        dependency ReferenceVoiceLibrary to SpeechModelDownloaderOptions;
        dependency ReferenceVoiceLibrary to SpeechModelStore;
        dependency ReferenceVoiceLibrary to SpeechModelStoreException;
        dependency ReferenceVoiceLibrary to SpeechModelStoreOptions;
    }
}
