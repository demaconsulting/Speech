package SpeechOnnxNemotronStt_NemotronVocabulary {
    private import SoftwareItemMetadata::*;

    part def NemotronVocabulary {
        doc /* Loads the model's vocab.txt, derives the encoder language id from the locale marker tokens and the
             * RNN-T blank token id, and converts emitted token ids into normalized text. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech.Onnx.NemotronStt/ModelManagementSubsystem/NemotronVocabulary.cs");
            testFiles = ("test/DemaConsulting.Speech.Onnx.NemotronStt.Tests/ModelManagementSubsystem/NemotronVocabularyTests.cs");
            designFiles = ("docs/design/speech-onnx-nemotron-stt/model-management-subsystem/nemotron-vocabulary.md");
            verificationFiles = ("docs/verification/speech-onnx-nemotron-stt/model-management-subsystem/nemotron-vocabulary.md");
            requirementFiles = ("docs/reqstream/speech-onnx-nemotron-stt/model-management-subsystem/nemotron-vocabulary.yaml");
        }
    }
}
