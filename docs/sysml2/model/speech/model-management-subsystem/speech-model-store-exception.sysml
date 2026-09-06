package Speech {
    part def SpeechModelStoreException {
        doc /* Signals a SpeechModelStore operation could not complete, such as an uninstall
             * blocked by an open file handle within current/. */

        comment sourceRef /* Source: src/DemaConsulting.Speech/ModelManagementSubsystem/SpeechModelStoreException.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Tests/ModelManagementSubsystem/SpeechModelStoreExceptionTests.cs */
        comment designRef /* Design: docs/design/speech/model-management-subsystem/speech-model-store-exception.md */
        comment verificationRef /* Verification: docs/verification/speech/model-management-subsystem/speech-model-store-exception.md */
        comment reqRef /* Requirements: docs/reqstream/speech/model-management-subsystem/speech-model-store-exception.yaml */
    }
}
