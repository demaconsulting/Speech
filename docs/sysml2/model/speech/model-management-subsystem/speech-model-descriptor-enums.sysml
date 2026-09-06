package Speech {
    part def SpeechModelDescriptorEnums {
        doc /* The fixed, small value-set enums a model and the catalog are built around:
             * SpeechModelRole (Recognition/Synthesis), SpeechModelState (NotDownloaded/
             * Downloading/Downloaded/FailedOrCorrupt), and SpeechModelAudioTagSupport (None/
             * ParameterMapped/Native) - a declaration shape only; Layer 2 rendering is Phase 4. */

        comment sourceRef /* Source: src/DemaConsulting.Speech/ModelManagementSubsystem/SpeechModelRole.cs, SpeechModelState.cs, SpeechModelAudioTagSupport.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Tests/ModelManagementSubsystem/SpeechModelDescriptorEnumsTests.cs */
        comment designRef /* Design: docs/design/speech/model-management-subsystem/speech-model-descriptor-enums.md */
        comment verificationRef /* Verification: docs/verification/speech/model-management-subsystem/speech-model-descriptor-enums.md */
        comment reqRef /* Requirements: docs/reqstream/speech/model-management-subsystem/speech-model-descriptor-enums.yaml */
    }
}
