package SpeechOnnxKokoro_ModelManagementSubsystem {
    private import SoftwareItemMetadata::*;
    private import SpeechOnnxKokoro_KokoroPhonemeVocabulary::*;
    private import SpeechOnnxKokoro_OnnxKokoroEnglishSynthesisModel::*;
    private import SpeechOnnxKokoro_SpeechModelCatalogKokoroExtensions::*;

    part def ModelManagementSubsystem {
        doc /* The AddKokoroModels() catalog registration entry point, the one concrete ONNX-Runtime-backed
             * OnnxKokoroEnglishSynthesisModel implementing Speech's ISynthesisModel contract (including the shared
             * English text analysis it hands to the session), and the supporting KokoroPhonemeVocabulary unit that
             * converts Kokoro phoneme symbols to token ids. */

        metadata info : SoftwareInfo {
            designFiles = ("docs/design/speech-onnx-kokoro/model-management-subsystem.md");
            verificationFiles = ("docs/verification/speech-onnx-kokoro/model-management-subsystem.md");
            requirementFiles = ("docs/reqstream/speech-onnx-kokoro/model-management-subsystem.yaml");
        }

        part speechModelCatalogKokoroExtensions : SpeechModelCatalogKokoroExtensions;
        part onnxKokoroEnglishSynthesisModel : OnnxKokoroEnglishSynthesisModel;
        part kokoroPhonemeVocabulary : KokoroPhonemeVocabulary;
    }
}
