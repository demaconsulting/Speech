package Speech {
    part def SpeechRecognitionResult {
        doc /* Supporting record carrying one recognition result (text, finality and timing) produced
             * while streaming audio through an IRecognitionSession. */

        comment sourceRef /* Source: src/DemaConsulting.Speech/RecognitionSubsystem/SpeechRecognitionResult.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Tests/RecognitionSubsystem/RecognitionResultBufferTests.cs */
        comment designRef /* Design: docs/design/speech/recognition-subsystem.md (documented as a supporting type) */
    }
}
