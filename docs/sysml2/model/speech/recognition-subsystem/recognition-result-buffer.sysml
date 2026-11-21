package Speech_RecognitionResultBuffer {
    private import SoftwareItemMetadata::*;
    private import Speech_RecognitionSessionFaultedException::*;
    private import Speech_SpeechRecognitionResult::*;

    part def RecognitionResultBuffer {
        doc /* Internal two-tier backpressure buffer used by RecognitionSession between the pump thread and a
             * possibly-slow GetResultsAsync consumer: one overwritable "latest provisional" slot plus an unbounded
             * FIFO of final results; a final result is never dropped, and an unread provisional result is superseded
             * by the next one. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech/RecognitionSubsystem/RecognitionResultBuffer.cs");
            testFiles = ("test/DemaConsulting.Speech.Tests/RecognitionSubsystem/RecognitionSessionTests.cs");
            designFiles = ("docs/design/speech/recognition-subsystem/recognition-result-buffer.md");
            verificationFiles = ("docs/verification/speech/recognition-subsystem/recognition-result-buffer.md");
            requirementFiles = ("docs/reqstream/speech/recognition-subsystem/recognition-result-buffer.yaml");
        }

        // Buffers recognition results and signals a faulted session when consumers fall too far behind.
        dependency RecognitionResultBuffer to RecognitionSessionFaultedException;
        dependency RecognitionResultBuffer to SpeechRecognitionResult;
    }
}
