package Speech {
    part def DedicatedWorker {
        doc /* Internal long-running-task helper that runs one blocking delegate on its own
             * dedicated thread (TaskCreationOptions.LongRunning) and applies a
             * cooperative-cancel-then-abandon policy: requests cancellation first, then abandons
             * the delegate after a bounded timeout (default 2 seconds) if it has not observed
             * cancellation, reporting the abandonment through ISpeechDiagnostics at Warning level
             * rather than blocking the caller forever on a stuck native call. */

        comment sourceRef /* Source: src/DemaConsulting.Speech/RecognitionSubsystem/DedicatedWorker.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Tests/RecognitionSubsystem/DedicatedWorkerTests.cs */
        comment designRef /* Design: docs/design/speech/recognition-subsystem/sherpa-onnx-recognition-session.md (documented with the pipeline it serves) */
        comment verificationRef /* Verification: docs/verification/speech/recognition-subsystem/sherpa-onnx-recognition-session.md (documented with the pipeline it serves) */
        comment reqRef /* Requirements: docs/reqstream/speech/recognition-subsystem/dedicated-worker.yaml */
    }
}
