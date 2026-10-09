package SpeechOnnxKokoro {
    doc
    /*
     * SpeechOnnxKokoro is the DemaConsulting.Speech.Onnx.Kokoro library that supplies a concrete,
     * ONNX-Runtime-backed Kokoro v1.0 text-to-speech model and inference engine for the Speech
     * library through its ISynthesisModel extension seam. Speech must never depend on it. Unlike
     * SpeechSherpa, this system owns its own phonemization and tokenization pipeline, since the
     * Kokoro v1.0 ONNX export performs only the waveform-generation forward pass.
     */

    part def SpeechOnnxKokoroSystem {
        doc /* The SpeechOnnxKokoro library as a whole. */

        comment designRef /* Design: docs/design/speech-onnx-kokoro.md */
        comment verificationRef /* Verification: docs/verification/speech-onnx-kokoro.md */
        comment reqRef /* Requirements: docs/reqstream/speech-onnx-kokoro.yaml */

        part modelManagementSubsystem : ModelManagementSubsystem;
        part synthesisSubsystem : SynthesisSubsystem;
    }
}
