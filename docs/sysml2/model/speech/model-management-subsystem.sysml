package Speech {
    part def ModelManagementSubsystem {
        doc /* Per-user speech model storage with atomic install/repair/uninstall, the
             * queued download-verify-install orchestration and mockable HTTP seam that fetch
             * and install a model's files with honest failure states, and the model
             * catalog/contract seam: typed tunable-parameter descriptors, the common
             * ISpeechModel contract with its role-marker interfaces, and SpeechModelCatalog's
             * enumeration of the compiled-in known-model registry alongside install state.
             * Phase 7a added the library's first two real, concrete IRecognitionModel classes
             * (Zipformer and NVIDIA Nemotron streaming ASR models) plus a shared .tar.bz2
             * archive-extraction helper. Phase 7b added the library's first real, concrete
             * ISynthesisModel class (VITS/Piper LibriTTS-R English), completing the catalog
             * with a shippable model of every role, though voice selection remained out of
             * scope. Phase 10 adds a second ISynthesisModel (Kokoro, 11 genuinely distinct
             * voices) and extends ISynthesisModel with a ResolveSpeakerId hook, closing the
             * voice-selection gap for any model that owns real per-voice knowledge. */

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
        part speechModelDownloader : SpeechModelDownloader;
        part speechModelDescriptorEnums : SpeechModelDescriptorEnums;
        part speechModelParameters : SpeechModelParameters;
        part speechModelContract : SpeechModelContract;
        part speechModelDescriptor : SpeechModelDescriptor;
        part speechModelCatalog : SpeechModelCatalog;
        part sherpaOnnxZipformerEnRecognitionModel : SherpaOnnxZipformerEnRecognitionModel;
        part sherpaOnnxNemotronStreamingEnRecognitionModel : SherpaOnnxNemotronStreamingEnRecognitionModel;
        part tarBz2ArchiveExtractor : TarBz2ArchiveExtractor;
        part uppercaseTranscriptRestorer : UppercaseTranscriptRestorer;
        part sherpaOnnxVitsLibriTtsEnglishSynthesisModel : SherpaOnnxVitsLibriTtsEnglishSynthesisModel;
        part sherpaOnnxKokoroEnglishSynthesisModel : SherpaOnnxKokoroEnglishSynthesisModel;
    }
}
