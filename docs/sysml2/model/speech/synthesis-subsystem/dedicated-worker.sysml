package Speech {
    part def DedicatedWorker {
        doc /* Internal utility that runs a delegate on a dedicated, long-running background
             * thread, applying a cooperative-cancel-then-abandon policy for native calls that do
             * not honor cancellation promptly: on cancellation the worker gets a bounded grace
             * period to stop cooperatively, after which the awaited task completes as cancelled,
             * a Warning diagnostic is reported, and the worker thread is detached. This is the
             * synthesis subsystem's own copy, duplicated rather than shared with
             * RecognitionSubsystem's identically-shaped internal utility per this library's
             * subsystem test-dependency boundary. */

        comment sourceRef /* Source: src/DemaConsulting.Speech/SynthesisSubsystem/DedicatedWorker.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Tests/SynthesisSubsystem/DedicatedWorkerTests.cs */
        comment designRef /* Design: docs/design/speech/synthesis-subsystem/sherpa-onnx-synthesis-session.md (documented with the pipeline it serves) */
        comment verificationRef /* Verification: docs/verification/speech/synthesis-subsystem/sherpa-onnx-synthesis-session.md (documented with the pipeline it serves) */
        comment reqRef /* Requirements: docs/reqstream/speech/synthesis-subsystem/dedicated-worker.yaml */
    }
}
