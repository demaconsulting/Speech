package SpeechOnnxNemotronStt_SilenceRunLimiter {
    private import SoftwareItemMetadata::*;

    part def SilenceRunLimiter {
        doc /* Stateful streaming limiter that truncates quiet runs longer than 400 ms to 400 ms using an adaptive
             * frame-RMS threshold calibrated from the first 400 ms of the stream, preserving all speech samples. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech.Onnx.NemotronStt/RecognitionSubsystem/SilenceRunLimiter.cs");
            testFiles = ("test/DemaConsulting.Speech.Onnx.NemotronStt.Tests/RecognitionSubsystem/SilenceRunLimiterTests.cs");
            designFiles = ("docs/design/speech-onnx-nemotron-stt/recognition-subsystem/silence-run-limiter.md");
            verificationFiles = ("docs/verification/speech-onnx-nemotron-stt/recognition-subsystem/silence-run-limiter.md");
            requirementFiles = ("docs/reqstream/speech-onnx-nemotron-stt/recognition-subsystem/silence-run-limiter.yaml");
        }
    }
}
