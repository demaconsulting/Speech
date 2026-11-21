package SpeechSherpa_RecognitionSubsystem {
    private import SoftwareItemMetadata::*;
    private import SpeechSherpa_SherpaOnnxRecognitionEngine::*;

    part def RecognitionSubsystem {
        doc /* The real sherpa-onnx implementation of Speech's public IRecognitionBackend seam over sherpa-onnx's
             * streaming recognizer. */

        metadata info : SoftwareInfo {
            designFiles = ("docs/design/speech-sherpa/recognition-subsystem.md");
            verificationFiles = ("docs/verification/speech-sherpa/recognition-subsystem.md");
            requirementFiles = ("docs/reqstream/speech-sherpa/recognition-subsystem.yaml");
        }

        part sherpaOnnxRecognitionEngine : SherpaOnnxRecognitionEngine;
    }
}
