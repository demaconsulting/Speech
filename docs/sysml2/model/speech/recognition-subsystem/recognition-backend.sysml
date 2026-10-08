package Speech {
    part def RecognitionBackend {
        doc /* Internal mockable seam over one loaded speech-inference backend, together with its
             * factory and the default factory that asks a model to construct its own backend,
             * confining every inference interop call so the recognition pipeline is testable with
             * pure managed fakes. Concrete backends are supplied by sibling packages such as
             * SpeechSherpa. */

        comment sourceRef /* Source: src/DemaConsulting.Speech/RecognitionSubsystem/IRecognitionBackend.cs, IRecognitionBackendFactory.cs, DefaultRecognitionBackendFactory.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Tests/RecognitionSubsystem/RecognitionSessionTests.cs */
        comment designRef /* Design: docs/design/speech/recognition-subsystem/recognition-session.md (documented with the pipeline it serves) */
        comment verificationRef /* Verification: docs/verification/speech/recognition-subsystem/recognition-session.md (documented with the pipeline it serves) */
        comment reqRef /* Requirements: docs/reqstream/speech/recognition-subsystem/recognition-session.yaml */
    }
}
