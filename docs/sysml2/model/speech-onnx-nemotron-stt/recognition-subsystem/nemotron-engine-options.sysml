package SpeechOnnxNemotronStt_NemotronEngineOptions {
    private import SoftwareItemMetadata::*;
    private import SpeechOnnxNemotronStt_DitherNoise::*;
    private import SpeechOnnxNemotronStt_SilenceRunLimiter::*;
    private import ScalarValues::*;

    part def NemotronEngineOptions {
        doc /* Supporting record holding the tunable constants of OnnxNemotronRecognitionEngine; the defaults are the
             * documented production values and tests supply others for edge cases. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech.Onnx.NemotronStt/RecognitionSubsystem/NemotronEngineOptions.cs");
            testFiles = ("test/DemaConsulting.Speech.Onnx.NemotronStt.Tests/RecognitionSubsystem/OnnxNemotronRecognitionEngineTests.cs");
            designFiles = ("docs/design/speech-onnx-nemotron-stt/recognition-subsystem/onnx-nemotron-recognition-engine.md");
            verificationFiles = ("docs/verification/speech-onnx-nemotron-stt/recognition-subsystem/onnx-nemotron-recognition-engine.md");
            requirementFiles = ("docs/reqstream/speech-onnx-nemotron-stt/recognition-subsystem/onnx-nemotron-recognition-engine.yaml");
        }

        attribute ditherAmplitude : Real;
        attribute ditherSeed : Integer;
        attribute endpointQuietMs : Integer;
        attribute endpointEmptyChunks : Integer;
        attribute flushTailChunks : Integer;

        // Holds tuning settings for the dither noise and silence run limiter stages.
        dependency NemotronEngineOptions to DitherNoise;
        dependency NemotronEngineOptions to SilenceRunLimiter;
    }
}
