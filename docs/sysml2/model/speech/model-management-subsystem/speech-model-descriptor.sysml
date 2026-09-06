package Speech {
    part def SpeechModelDescriptor {
        doc /* Immutable catalog read-model pairing one ISpeechModel with its current
             * SpeechModelState, plus Id/DisplayName/Role passthrough properties. */

        comment sourceRef /* Source: src/DemaConsulting.Speech/ModelManagementSubsystem/SpeechModelDescriptor.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Tests/ModelManagementSubsystem/SpeechModelDescriptorTests.cs */
        comment designRef /* Design: docs/design/speech/model-management-subsystem/speech-model-descriptor.md */
        comment verificationRef /* Verification: docs/verification/speech/model-management-subsystem/speech-model-descriptor.md */
        comment reqRef /* Requirements: docs/reqstream/speech/model-management-subsystem/speech-model-descriptor.yaml */
    }
}
