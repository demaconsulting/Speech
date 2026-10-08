package Speech {
    part def SynthesisBackend {
        doc /* Internal mockable seam over one loaded speech-synthesis engine, together with its
             * factory and the default factory that asks a model to construct its own backend,
             * confining every inference interop call so the synthesis pipeline is testable with
             * pure managed fakes. Renamed from ISynthesisEngine/ISynthesisEngineFactory so the
             * "engine" vocabulary is reserved for the public Layer 3 ISpeechSynthesizerEngine
             * contract; members unchanged. Concrete backends are supplied by sibling packages
             * such as SpeechSherpa. */

        comment sourceRef /* Source: src/DemaConsulting.Speech/SynthesisSubsystem/ISynthesisBackend.cs, ISynthesisBackendFactory.cs, DefaultSynthesisBackendFactory.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Tests/SynthesisSubsystem/SynthesisSessionTests.cs */
        comment designRef /* Design: docs/design/speech/synthesis-subsystem/synthesis-session.md (documented with the pipeline it serves) */
        comment verificationRef /* Verification: docs/verification/speech/synthesis-subsystem/synthesis-session.md (documented with the pipeline it serves) */
        comment reqRef /* Requirements: docs/reqstream/speech/synthesis-subsystem/synthesis-session.yaml */
    }
}
