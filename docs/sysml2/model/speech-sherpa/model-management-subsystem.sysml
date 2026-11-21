package SpeechSherpa_ModelManagementSubsystem {
    private import SoftwareItemMetadata::*;
    private import SpeechSherpa_SherpaOnnxKokoroEnglishSynthesisModel::*;
    private import SpeechSherpa_SherpaOnnxNemotronStreamingEnRecognitionModel::*;
    private import SpeechSherpa_SherpaOnnxVitsLibriTtsEnglishSynthesisModel::*;
    private import SpeechSherpa_SherpaOnnxZipformerEnRecognitionModel::*;
    private import SpeechSherpa_SpeechModelCatalogSherpaExtensions::*;
    private import SpeechSherpa_TarBz2ArchiveExtractor::*;
    private import SpeechSherpa_UppercaseTranscriptRestorer::*;

    part def ModelManagementSubsystem {
        doc /* The AddSherpaModels() catalog registration entry point, the four concrete sherpa-onnx-backed models
             * (Zipformer and NVIDIA Nemotron streaming recognition models, VITS/Piper LibriTTS-R and Kokoro synthesis
             * models) implementing Speech's IRecognitionModel/ISynthesisModel contract, and the shared .tar.bz2
             * archive-extraction and uppercase-transcript-restoration helpers they use. */

        metadata info : SoftwareInfo {
            designFiles = ("docs/design/speech-sherpa/model-management-subsystem.md");
            verificationFiles = ("docs/verification/speech-sherpa/model-management-subsystem.md");
            requirementFiles = ("docs/reqstream/speech-sherpa/model-management-subsystem.yaml");
        }

        part speechModelCatalogSherpaExtensions : SpeechModelCatalogSherpaExtensions;
        part sherpaOnnxZipformerEnRecognitionModel : SherpaOnnxZipformerEnRecognitionModel;
        part sherpaOnnxNemotronStreamingEnRecognitionModel : SherpaOnnxNemotronStreamingEnRecognitionModel;
        part tarBz2ArchiveExtractor : TarBz2ArchiveExtractor;
        part uppercaseTranscriptRestorer : UppercaseTranscriptRestorer;
        part sherpaOnnxVitsLibriTtsEnglishSynthesisModel : SherpaOnnxVitsLibriTtsEnglishSynthesisModel;
        part sherpaOnnxKokoroEnglishSynthesisModel : SherpaOnnxKokoroEnglishSynthesisModel;
    }
}
