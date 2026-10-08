package SpeechSherpa {
    doc
    /*
     * SpeechSherpa is the DemaConsulting.Speech.Sherpa library that supplies concrete,
     * sherpa-onnx-backed speech models and inference backends for the Speech library through
     * its IRecognitionModel/ISynthesisModel extension seam. Speech must never depend on it.
     */

    part def SpeechSherpaSystem {
        doc /* The SpeechSherpa library as a whole. */

        comment designRef /* Design: docs/design/speech-sherpa.md */
        comment verificationRef /* Verification: docs/verification/speech-sherpa.md */
        comment reqRef /* Requirements: docs/reqstream/speech-sherpa.yaml */

        part modelManagementSubsystem : ModelManagementSubsystem;
        part recognitionSubsystem : RecognitionSubsystem;
        part synthesisSubsystem : SynthesisSubsystem;
    }
}
