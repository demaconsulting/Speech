package Speech_ModelManagementSubsystem {
    private import SoftwareItemMetadata::*;
    private import Speech_DownloadMirror::*;
    private import Speech_HttpModelDownloadClient::*;
    private import Speech_IModelDownloadClient::*;
    private import Speech_NumericParameterBounds::*;
    private import Speech_ReferenceVoiceLibrary::*;
    private import Speech_ReferenceVoicePack::*;
    private import Speech_ReferenceVoicePackInstaller::*;
    private import Speech_SpeechModelCatalog::*;
    private import Speech_SpeechModelContract::*;
    private import Speech_SpeechModelDescriptor::*;
    private import Speech_SpeechModelDescriptorEnums::*;
    private import Speech_SpeechModelDownloadDescriptor::*;
    private import Speech_SpeechModelDownloadFile::*;
    private import Speech_SpeechModelDownloadProgress::*;
    private import Speech_SpeechModelDownloader::*;
    private import Speech_SpeechModelDownloaderOptions::*;
    private import Speech_SpeechModelParameters::*;
    private import Speech_SpeechModelStore::*;
    private import Speech_SpeechModelStoreException::*;
    private import Speech_SpeechModelStoreOptions::*;

    part def ModelManagementSubsystem {
        doc /* Per-user speech model storage with atomic install/repair/uninstall, the queued download-verify-install
             * orchestration and mockable HTTP seam that fetch and install a model's files with honest failure states,
             * and the model catalog/contract seam: typed tunable-parameter descriptors, the common ISpeechModel
             * contract with its role-marker interfaces (including the ISynthesisModel ResolveSpeakerId hook), and
             * SpeechModelCatalog's enumeration of the models registered with it through AddModels alongside install
             * state. The library ships no concrete models itself; sibling packages such as SpeechSherpa supply them
             * through the IRecognitionModel/ISynthesisModel extension seam. */

        metadata info : SoftwareInfo {
            designFiles = ("docs/design/speech/model-management-subsystem.md");
            verificationFiles = ("docs/verification/speech/model-management-subsystem.md");
            requirementFiles = ("docs/reqstream/speech/model-management-subsystem.yaml");
        }

        part speechModelStoreOptions : SpeechModelStoreOptions;
        part speechModelStore : SpeechModelStore;
        part speechModelStoreException : SpeechModelStoreException;
        part speechModelDownloadFile : SpeechModelDownloadFile;
        part speechModelDownloadDescriptor : SpeechModelDownloadDescriptor;
        part speechModelDownloadProgress : SpeechModelDownloadProgress;
        part iModelDownloadClient : IModelDownloadClient;
        part httpModelDownloadClient : HttpModelDownloadClient;
        part downloadMirror : DownloadMirror;
        part speechModelDownloaderOptions : SpeechModelDownloaderOptions;
        part speechModelDownloader : SpeechModelDownloader;
        part speechModelDescriptorEnums : SpeechModelDescriptorEnums;
        part speechModelParameters : SpeechModelParameters;
        part speechModelContract : SpeechModelContract;
        part speechModelDescriptor : SpeechModelDescriptor;
        part speechModelCatalog : SpeechModelCatalog;
        part referenceVoicePack : ReferenceVoicePack;
        part referenceVoicePackInstaller : ReferenceVoicePackInstaller;
        part referenceVoiceLibrary : ReferenceVoiceLibrary;
        part numericParameterBounds : NumericParameterBounds;
    }
}
