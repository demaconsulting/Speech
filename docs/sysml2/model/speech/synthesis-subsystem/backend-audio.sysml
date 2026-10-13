package Speech {
    part def BackendAudio {
        doc /* Supporting plain-data record carrying the mono samples and sample rate returned by one
             * ISynthesisBackend.Generate call from an engine to the shared SynthesisSession. */

        comment sourceRef /* Source: src/DemaConsulting.Speech/SynthesisSubsystem/BackendAudio.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Tests/SynthesisSubsystem/SynthesisSessionTests.cs */
        comment designRef /* Design: docs/design/speech/synthesis-subsystem/synthesis-backend.md (documented as a supporting type) */
    }
}
