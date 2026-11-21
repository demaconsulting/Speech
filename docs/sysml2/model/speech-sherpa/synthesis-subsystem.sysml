package SpeechSherpa_SynthesisSubsystem {
    private import SoftwareItemMetadata::*;
    private import SpeechSherpa_SherpaOnnxSynthesisEngine::*;

    part def SynthesisSubsystem {
        doc /* The real sherpa-onnx implementation of Speech's public ISynthesisBackend seam over sherpa-onnx's
             * offline text-to-speech API. */

        metadata info : SoftwareInfo {
            designFiles = ("docs/design/speech-sherpa/synthesis-subsystem.md");
            verificationFiles = ("docs/verification/speech-sherpa/synthesis-subsystem.md");
            requirementFiles = ("docs/reqstream/speech-sherpa/synthesis-subsystem.yaml");
        }

        part sherpaOnnxSynthesisEngine : SherpaOnnxSynthesisEngine;
    }
}
