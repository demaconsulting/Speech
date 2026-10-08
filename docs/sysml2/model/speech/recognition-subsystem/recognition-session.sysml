package Speech {
    part def RecognitionSession {
        doc /* Real streaming IRecognitionSession implementation: enqueues captured audio off the
             * audio callback thread via a bounded drop-oldest channel, resamples it to the model's
             * required format, drains it on a DedicatedWorker pump thread through the leased
             * IRecognitionBackend, normalizes each result's text via the owning
             * IRecognitionModel.NormalizeText, buffers ordered provisional and final results in a
             * RecognitionResultBuffer, and streams them through GetResultsAsync. The backend stays
             * owned and "hot" on the owning SpeechRecognizerEngine across sessions. */

        comment sourceRef /* Source: src/DemaConsulting.Speech/RecognitionSubsystem/RecognitionSession.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Tests/RecognitionSubsystem/RecognitionSessionTests.cs */
        comment designRef /* Design: docs/design/speech/recognition-subsystem/recognition-session.md */
        comment verificationRef /* Verification: docs/verification/speech/recognition-subsystem/recognition-session.md */
        comment reqRef /* Requirements: docs/reqstream/speech/recognition-subsystem/recognition-session.yaml */
    }
}