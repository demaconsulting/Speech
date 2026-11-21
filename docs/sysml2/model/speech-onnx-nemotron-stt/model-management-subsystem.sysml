package SpeechOnnxNemotronStt_ModelManagementSubsystem {
    private import SoftwareItemMetadata::*;
    private import SpeechOnnxNemotronStt_NemotronVocabulary::*;
    private import SpeechOnnxNemotronStt_OnnxNemotronMultilingualRecognitionModel::*;
    private import SpeechOnnxNemotronStt_SpeechModelCatalogNemotronSttExtensions::*;

    part def ModelManagementSubsystem {
        doc /* The AddNemotronSttModels() catalog registration entry point, the one concrete ONNX-Runtime-backed
             * OnnxNemotronMultilingualRecognitionModel implementing Speech's IRecognitionModel contract, and the
             * NemotronVocabulary unit that derives the language id and blank id and detokenizes emitted token ids. */

        metadata info : SoftwareInfo {
            designFiles = ("docs/design/speech-onnx-nemotron-stt/model-management-subsystem.md");
            verificationFiles = ("docs/verification/speech-onnx-nemotron-stt/model-management-subsystem.md");
            requirementFiles = ("docs/reqstream/speech-onnx-nemotron-stt/model-management-subsystem.yaml");
        }

        part speechModelCatalogNemotronSttExtensions : SpeechModelCatalogNemotronSttExtensions;
        part onnxNemotronMultilingualRecognitionModel : OnnxNemotronMultilingualRecognitionModel;
        part nemotronVocabulary : NemotronVocabulary;
    }
}
