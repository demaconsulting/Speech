package SpeechOnnxNemotronStt_NemotronEngineOptions {
    private import SoftwareItemMetadata::*;
    private import SpeechOnnxNemotronStt_DitherNoise::*;
    private import SpeechOnnxNemotronStt_NemotronEndpointDetector::*;
    private import SpeechOnnxNemotronStt_SilenceRunLimiter::*;
    private import ScalarValues::*;

    part def NemotronEngineOptions {
        doc /* Supporting record holding the tunable constants of OnnxNemotronRecognitionEngine: chunk and
             * pre-encode sizes, dither amplitude and seed, flush tail chunks, the RNN-T blank penalty (default 1.0,
             * chosen by an evaluation-tool sweep), the endpoint rules (default 2.4 s, 1.2 s, 20 s), and the limiter
             * options. The defaults are the documented production values; tests and the non-shipped evaluation tool
             * supply others for edge cases and measurements. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech.Onnx.NemotronStt/RecognitionSubsystem/NemotronEngineOptions.cs");
            testFiles = ("test/DemaConsulting.Speech.Onnx.NemotronStt.Tests/RecognitionSubsystem/OnnxNemotronRecognitionEngineTests.cs");
            designFiles = ("docs/design/speech-onnx-nemotron-stt/recognition-subsystem/onnx-nemotron-recognition-engine.md");
            verificationFiles = ("docs/verification/speech-onnx-nemotron-stt/recognition-subsystem/onnx-nemotron-recognition-engine.md");
            requirementFiles = ("docs/reqstream/speech-onnx-nemotron-stt/recognition-subsystem/onnx-nemotron-recognition-engine.yaml");
        }

        attribute ditherAmplitude : Real;
        attribute ditherSeed : Integer;
        attribute flushTailChunks : Integer;
        attribute blankPenalty : Real;

        // Holds tuning settings for the dither noise, endpoint rules, and silence run limiter stages, and takes its
        // default blank penalty from the RNN-T network.
        dependency NemotronEngineOptions to DitherNoise;
        dependency NemotronEngineOptions to NemotronEndpointDetector;
        dependency NemotronEngineOptions to SpeechOnnxNemotronStt_NemotronRnntNetwork::NemotronRnntNetwork;
        dependency NemotronEngineOptions to SilenceRunLimiter;
    }
}
