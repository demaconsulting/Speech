package SpeechDemo {
    part def CaptureDebugRecorder {
        doc /* Opt-in, off-by-default diagnostic instrumentation that records the live-microphone capture
             * and recognition event stream to help investigate dropped words after a long pause. It is a
             * supporting diagnostic type, not a user-facing feature. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Demo/RecognitionPanelSubsystem/CaptureDebugRecorder.cs, src/DemaConsulting.Speech.Demo/RecognitionPanelSubsystem/WavFileWriter.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Demo.Tests/RecognitionPanelSubsystem/CaptureDebugRecorderTests.cs */
        comment designRef /* Design: docs/design/speech-demo/recognition-panel-subsystem.md (documented as a supporting type) */
                comment verificationRef /* Verification: docs/verification/speech-demo/recognition-panel-subsystem.md (verified within the subsystem doc; no dedicated unit doc) */
                comment reqRef /* Requirements: docs/reqstream/speech-demo/recognition-panel-subsystem.yaml (requirements held in the subsystem file; no dedicated unit file) */    }
}
