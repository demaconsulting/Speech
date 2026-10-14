package Speech {
    part def SpeechRecognitionResult {
        doc /* Supporting record carrying one recognition result (text, finality and timing) produced
             * while streaming audio through an IRecognitionSession. */

        comment sourceRef /* Source: src/DemaConsulting.Speech/RecognitionSubsystem/SpeechRecognitionResult.cs, src/DemaConsulting.Speech/RecognitionSubsystem/SpeechRecognitionEvent.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Tests/RecognitionSubsystem/RecognitionResultBufferTests.cs */
        comment designRef /* Design: docs/design/speech/recognition-subsystem.md (documented as a supporting type) */
                comment verificationRef /* Verification: docs/verification/speech/recognition-subsystem.md (verified within the subsystem doc; no dedicated unit doc) */
                comment reqRef /* Requirements: docs/reqstream/speech/recognition-subsystem.yaml (requirements held in the subsystem file; no dedicated unit file) */    }
}
