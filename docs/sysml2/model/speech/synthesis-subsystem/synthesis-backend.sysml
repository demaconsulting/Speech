package Speech {
    part def SynthesisBackend {
        doc /* Internal mockable seam over one loaded speech-synthesis engine, together with its
             * factory and their real sherpa-onnx implementations, confining every inference
             * interop call so the synthesis pipeline is testable with pure managed fakes. Renamed
             * from ISynthesisEngine/ISynthesisEngineFactory so the "engine" vocabulary is reserved
             * for the public Layer 3 ISpeechSynthesizerEngine contract; members unchanged. */

        comment sourceRef /* Source: src/DemaConsulting.Speech/SynthesisSubsystem/ISynthesisBackend.cs, ISynthesisBackendFactory.cs, SherpaOnnxSynthesisEngine.cs, SherpaOnnxSynthesisEngineFactory.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Tests/SynthesisSubsystem/SherpaOnnxSynthesisSessionTests.cs */
        comment designRef /* Design: docs/design/speech/synthesis-subsystem/sherpa-onnx-synthesis-session.md (documented with the pipeline it serves) */
        comment verificationRef /* Verification: docs/verification/speech/synthesis-subsystem/sherpa-onnx-synthesis-session.md (documented with the pipeline it serves) */
        comment reqRef /* Requirements: docs/reqstream/speech/synthesis-subsystem/sherpa-onnx-synthesis-session.yaml */
    }
}
