package Speech {
    part def RecognitionResultBuffer {
        doc /* Internal two-tier backpressure buffer used by RecognitionSession between
             * the pump thread and a possibly-slow GetResultsAsync consumer: one overwritable
             * "latest provisional" slot plus an unbounded FIFO of final results; a final result is
             * never dropped, and an unread provisional result is superseded by the next one. */

        comment sourceRef /* Source: src/DemaConsulting.Speech/RecognitionSubsystem/RecognitionResultBuffer.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Tests/RecognitionSubsystem/RecognitionSessionTests.cs */
        comment designRef /* Design: docs/design/speech/recognition-subsystem/recognition-result-buffer.md */
        comment verificationRef /* Verification: docs/verification/speech/recognition-subsystem/recognition-result-buffer.md */
        comment reqRef /* Requirements: docs/reqstream/speech/recognition-subsystem/recognition-result-buffer.yaml */
    }
}
