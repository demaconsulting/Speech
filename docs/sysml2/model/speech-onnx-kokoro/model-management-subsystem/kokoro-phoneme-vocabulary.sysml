package SpeechOnnxKokoro_KokoroPhonemeVocabulary {
    private import SoftwareItemMetadata::*;

    part def KokoroPhonemeVocabulary {
        doc /* Loads Kokoro v1.0's own character-level IPA phoneme vocabulary from an embedded copy of its
             * tokenizer.json, and converts a phoneme string into the integer token id sequence the ONNX model
             * expects, dropping any unmapped character and truncating to MaxPhonemeTokens (510) entries. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech.Onnx.Kokoro/ModelManagementSubsystem/KokoroPhonemeVocabulary.cs");
            testFiles = ("test/DemaConsulting.Speech.Onnx.Kokoro.Tests/ModelManagementSubsystem/KokoroPhonemeVocabularyTests.cs");
            designFiles = ("docs/design/speech-onnx-kokoro/model-management-subsystem/kokoro-phoneme-vocabulary.md");
            verificationFiles = ("docs/verification/speech-onnx-kokoro/model-management-subsystem/kokoro-phoneme-vocabulary.md");
            requirementFiles = ("docs/reqstream/speech-onnx-kokoro/model-management-subsystem/kokoro-phoneme-vocabulary.yaml");
        }
    }
}
