package SpeechSherpa {
    part def SherpaOnnxSynthesisEngine {
        doc /* Real ISynthesisBackend implementation wrapping one loaded sherpa-onnx offline
             * text-to-speech engine, confining every synthesis inference interop call behind
             * Speech's public mockable seam. Constructed by the synthesis models'
             * CreateBackend overrides. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Sherpa/SynthesisSubsystem/SherpaOnnxSynthesisEngine.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Sherpa.Tests/SynthesisSubsystem/SherpaOnnxSynthesisEngineTests.cs */
        comment designRef /* Design: docs/design/speech-sherpa/synthesis-subsystem/sherpa-onnx-synthesis-engine.md */
        comment verificationRef /* Verification: docs/verification/speech-sherpa/synthesis-subsystem/sherpa-onnx-synthesis-engine.md */
        comment reqRef /* Requirements: docs/reqstream/speech-sherpa/synthesis-subsystem/sherpa-onnx-synthesis-engine.yaml */
    }
}
