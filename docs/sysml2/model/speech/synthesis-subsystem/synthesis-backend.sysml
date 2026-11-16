package Speech {
    part def SynthesisBackend {
        doc /* Public mockable seam over one loaded speech-synthesis engine, together with its
             * factory and the default factory that asks a model to construct its own backend,
             * confining every inference interop call so the synthesis pipeline is testable with
             * pure managed fakes. Renamed from ISynthesisEngine/ISynthesisEngineFactory so the
             * "engine" vocabulary is reserved for the public Layer 3 ISpeechSynthesizerEngine
             * contract; members unchanged. A backend may additionally implement the optional
             * IAnnotatedSynthesisBackend extension to receive a segment's annotations with its
             * text; a backend that does not is never given annotations. Concrete backends are
             * supplied by sibling packages such as SpeechSherpa. */

        comment sourceRef /* Source: src/DemaConsulting.Speech/SynthesisSubsystem/ISynthesisBackend.cs, IAnnotatedSynthesisBackend.cs, ISynthesisBackendFactory.cs, DefaultSynthesisBackendFactory.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Tests/SynthesisSubsystem/SynthesisSessionTests.cs */
        comment designRef /* Design: docs/design/speech/synthesis-subsystem/synthesis-backend.md */
        comment verificationRef /* Verification: docs/verification/speech/synthesis-subsystem/synthesis-backend.md */
        comment reqRef /* Requirements: docs/reqstream/speech/synthesis-subsystem/synthesis-backend.yaml */
    }
}
