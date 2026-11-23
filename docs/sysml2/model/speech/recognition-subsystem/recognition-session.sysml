package Speech_RecognitionSession {
    private import SoftwareItemMetadata::*;
    private import Speech_AudioFrameResampler::*;
    private import Speech_IAudioCaptureDevice::*;
    private import Speech_IRecognitionSession::*;
    private import Speech_ISpeechDiagnostics::*;
    private import Speech_RecognitionBackend::*;
    private import Speech_RecognitionResultBuffer::*;
    private import Speech_RecognitionSessionState::*;
    private import Speech_SpeechDiagnosticLevel::*;
    private import Speech_SpeechModelContract::*;
    private import Speech_SpeechRecognitionResult::*;
    private import Speech_SpeechRecognizerUnavailableException::*;
    private import Speech_WavFileAudioCaptureDevice::*;

    part def RecognitionSession {
        doc /* Real streaming IRecognitionSession implementation: enqueues captured audio off the audio callback
             * thread via a lossless channel (unbounded for a live device, with a reported one-minute safety cap;
             * bounded with backpressure for a file), resamples it to the model's required format, drains it on a
             * DedicatedWorker pump thread through the leased IRecognitionBackend, normalizes each result's text via
             * the owning IRecognitionModel.NormalizeText, buffers ordered provisional and final results in a
             * RecognitionResultBuffer, and streams them through GetResultsAsync. A graceful stop decodes all queued
             * audio, flushes the backend, and buffers every final before resetting the backend and returning; only a
             * canceled stop discards. The backend stays owned and "hot" on the owning SpeechRecognizerEngine across
             * sessions. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech/RecognitionSubsystem/RecognitionSession.cs");
            testFiles = ("test/DemaConsulting.Speech.Tests/RecognitionSubsystem/RecognitionSessionTests.cs");
            designFiles = ("docs/design/speech/recognition-subsystem/recognition-session.md");
            verificationFiles = ("docs/verification/speech/recognition-subsystem/recognition-session.md");
            requirementFiles = ("docs/reqstream/speech/recognition-subsystem/recognition-session.yaml");
        }

        // Resamples captured audio, feeds it to the backend and buffers the results.
        dependency RecognitionSession to AudioFrameResampler;
        dependency RecognitionSession to IAudioCaptureDevice;
        dependency RecognitionSession to IRecognitionSession;
        dependency RecognitionSession to ISpeechDiagnostics;
        dependency RecognitionSession to RecognitionBackend;
        dependency RecognitionSession to RecognitionResultBuffer;
        dependency RecognitionSession to RecognitionSessionState;
        dependency RecognitionSession to SpeechDiagnosticLevel;
        dependency RecognitionSession to SpeechModelContract;
        dependency RecognitionSession to SpeechRecognitionResult;
        dependency RecognitionSession to SpeechRecognizerUnavailableException;
        dependency RecognitionSession to WavFileAudioCaptureDevice;
    }
}
