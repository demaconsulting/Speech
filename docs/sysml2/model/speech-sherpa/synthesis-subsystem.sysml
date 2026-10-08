package SpeechSherpa {
    part def SynthesisSubsystem {
        doc /* The real sherpa-onnx implementation of Speech's public ISynthesisBackend seam
             * over sherpa-onnx's offline text-to-speech API. */

        comment designRef /* Design: docs/design/speech-sherpa/synthesis-subsystem.md */
        comment verificationRef /* Verification: docs/verification/speech-sherpa/synthesis-subsystem.md */
        comment reqRef /* Requirements: docs/reqstream/speech-sherpa/synthesis-subsystem.yaml */

        part sherpaOnnxSynthesisEngine : SherpaOnnxSynthesisEngine;
    }
}
