package Speech {
    part def RecognitionResultBuffer {
        doc /* Internal two-tier backpressure buffer used by RecognitionSession between
             * the pump thread and a possibly-slow GetResultsAsync consumer: one overwritable
             * "latest provisional" slot plus a byte-capped (64 KiB) FIFO of final results, evicting
             * the oldest final only as a last resort and reporting that eviction as a Warning
             * diagnostic. */

        comment sourceRef /* Source: src/DemaConsulting.Speech/RecognitionSubsystem/RecognitionResultBuffer.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Tests/RecognitionSubsystem/RecognitionSessionTests.cs */
        comment designRef /* Design: docs/design/speech/recognition-subsystem/recognition-session.md (documented with the pipeline it serves) */
        comment verificationRef /* Verification: docs/verification/speech/recognition-subsystem/recognition-session.md (documented with the pipeline it serves) */
        comment reqRef /* Requirements: docs/reqstream/speech/recognition-subsystem/recognition-session.yaml */
    }
}
