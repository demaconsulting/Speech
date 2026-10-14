package Speech {
    part def NumericParameterBounds {
        doc /* Supporting value bundling the minimum, maximum, step and default of a NumericParameter so
             * its constructor stays small; NumericParameter still validates and exposes the four values. */

        comment sourceRef /* Source: src/DemaConsulting.Speech/ModelManagementSubsystem/NumericParameterBounds.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Tests/ModelManagementSubsystem/NumericParameterTests.cs */
        comment designRef /* Design: docs/design/speech/model-management-subsystem/speech-model-parameters.md (documented as a supporting type) */
                comment verificationRef /* Verification: docs/verification/speech/model-management-subsystem/speech-model-parameters.md (verified within the SpeechModelParameters unit doc; no dedicated doc) */
                comment reqRef /* Requirements: docs/reqstream/speech/model-management-subsystem/speech-model-parameters.yaml (requirements held in the SpeechModelParameters unit file; no dedicated file) */    }
}
