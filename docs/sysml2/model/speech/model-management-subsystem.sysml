package Speech {
    part def ModelManagementSubsystem {
        doc /* Per-user speech model storage with atomic install/repair/uninstall, the
             * queued download-verify-install orchestration and mockable HTTP seam that fetch
             * and install a model's files with honest failure states, and the model
             * catalog/contract seam: typed tunable-parameter descriptors, the common
             * ISpeechModel contract with its role-marker interfaces (including the
             * ISynthesisModel ResolveSpeakerId hook), and SpeechModelCatalog's enumeration of
             * the models registered with it through AddModels alongside install state. The
             * library ships no concrete models itself; sibling packages such as SpeechSherpa
             * supply them through the IRecognitionModel/ISynthesisModel extension seam. */

        comment designRef /* Design: docs/design/speech/model-management-subsystem.md */
        comment verificationRef /* Verification: docs/verification/speech/model-management-subsystem.md */
        comment reqRef /* Requirements: docs/reqstream/speech/model-management-subsystem.yaml */

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
    }
}
