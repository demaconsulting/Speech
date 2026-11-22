package SpeechSttEvaluation_SttTranscriber {
    private import SoftwareItemMetadata::*;

    part def SttTranscriber {
        doc /* Streams a recording through a recognition backend the way a recognition session does: 20 ms blocks,
             * decoding after each block, the model's text hook on every result, optional trailing silence, and one
             * flush at the end; records every result with its audio-clock position (SttTranscript, SttResultEvent)
             * and the backend's processing time. Shared: compiled (linked) into the SpeechOnnxNemotronStt tests. */

        metadata info : SoftwareInfo {
            sourceFiles = ("tools/DemaConsulting.Speech.SttEvaluation/SttTranscriber.cs");
            testFiles = ("test/DemaConsulting.Speech.Onnx.NemotronStt.Tests/Evaluation/SttTranscriberTests.cs");
        }

        // Drives the public recognition-backend seam and records the results it reports.
        dependency SttTranscriber to Speech_RecognitionBackend::RecognitionBackend;
        dependency SttTranscriber to Speech_SpeechRecognitionResult::SpeechRecognitionResult;
    }
}