package SpeechSherpa {
    part def ModelManagementSubsystem {
        doc /* The AddSherpaModels() catalog registration entry point, the four concrete
             * sherpa-onnx-backed models (Zipformer and NVIDIA Nemotron streaming recognition
             * models, VITS/Piper LibriTTS-R and Kokoro synthesis models) implementing Speech's
             * IRecognitionModel/ISynthesisModel contract, and the shared .tar.bz2
             * archive-extraction and uppercase-transcript-restoration helpers they use. */

        comment designRef /* Design: docs/design/speech-sherpa/model-management-subsystem.md */
        comment verificationRef /* Verification: docs/verification/speech-sherpa/model-management-subsystem.md */
        comment reqRef /* Requirements: docs/reqstream/speech-sherpa/model-management-subsystem.yaml */

        part speechModelCatalogSherpaExtensions : SpeechModelCatalogSherpaExtensions;
        part sherpaOnnxZipformerEnRecognitionModel : SherpaOnnxZipformerEnRecognitionModel;
        part sherpaOnnxNemotronStreamingEnRecognitionModel : SherpaOnnxNemotronStreamingEnRecognitionModel;
        part tarBz2ArchiveExtractor : TarBz2ArchiveExtractor;
        part uppercaseTranscriptRestorer : UppercaseTranscriptRestorer;
        part sherpaOnnxVitsLibriTtsEnglishSynthesisModel : SherpaOnnxVitsLibriTtsEnglishSynthesisModel;
        part sherpaOnnxKokoroEnglishSynthesisModel : SherpaOnnxKokoroEnglishSynthesisModel;
    }
}
