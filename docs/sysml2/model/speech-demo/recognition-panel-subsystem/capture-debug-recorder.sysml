package SpeechDemo {
    part def CaptureDebugRecorder {
        doc /* Opt-in, off-by-default diagnostic instrumentation that records the live-microphone capture
             * and recognition event stream to help investigate dropped words after a long pause. It is a
             * supporting diagnostic type, not a user-facing feature. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Demo/RecognitionPanelSubsystem/CaptureDebugRecorder.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Demo.Tests/RecognitionPanelSubsystem/CaptureDebugRecorderTests.cs */
        comment designRef /* Design: docs/design/speech-demo/recognition-panel-subsystem.md (documented as a supporting type) */
    }
}
