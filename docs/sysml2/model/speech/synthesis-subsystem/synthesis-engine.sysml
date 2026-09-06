package Speech {
    part def SynthesisEngine {
        doc /* Internal mockable seam over one loaded speech-synthesis engine, together with its
             * factory and their real sherpa-onnx implementations, confining every inference
             * interop call so the synthesis pipeline is testable with pure managed fakes. */

        comment sourceRef /* Source: src/DemaConsulting.Speech/SynthesisSubsystem/ISynthesisEngine.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Tests/SynthesisSubsystem/SherpaOnnxSpeechSynthesizerTests.cs */
        comment designRef /* Design: docs/design/speech/synthesis-subsystem/sherpa-onnx-speech-synthesizer.md (documented with the pipeline it serves) */
        comment verificationRef /* Verification: docs/verification/speech/synthesis-subsystem/sherpa-onnx-speech-synthesizer.md (documented with the pipeline it serves) */
        comment reqRef /* Requirements: docs/reqstream/speech/synthesis-subsystem/sherpa-onnx-speech-synthesizer.yaml */
    }
}
