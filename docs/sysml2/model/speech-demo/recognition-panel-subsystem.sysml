package SpeechDemo {
    part def RecognitionPanelSubsystem {
        doc /* The speech-to-text panel: a demo-owned session seam over the library's
             * SpeechRecognizerFactory narrowing a public model to the library's recognition
             * role, and the Start/Stop streaming lifecycle with progressive partial-then-final
             * transcript rendering and honest reporting of every unavailable state. */

        comment designRef /* Design: docs/design/speech-demo/recognition-panel-subsystem.md */
        comment verificationRef /* Verification: docs/verification/speech-demo/recognition-panel-subsystem.md */
        comment reqRef /* Requirements: docs/reqstream/speech-demo/recognition-panel-subsystem.yaml */

        part iRecognizerSessionFactory : IRecognizerSessionFactory;
        part recognizerSessionFactory : RecognizerSessionFactory;
        part recognitionPanelViewModel : RecognitionPanelViewModel;
        part captureDebugRecorder : CaptureDebugRecorder;
    }
}
