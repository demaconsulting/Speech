package SpeechSherpa_SherpaOnnxRecognitionEngine {
    private import SoftwareItemMetadata::*;

    part def SherpaOnnxRecognitionEngine {
        doc /* Real IRecognitionBackend implementation wrapping one loaded sherpa-onnx streaming recognizer, confining
             * every recognition inference interop call behind Speech's public mockable seam. Constructed by the
             * recognition models' CreateBackend overrides. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech.Sherpa/RecognitionSubsystem/SherpaOnnxRecognitionEngine.cs");
            testFiles = ("test/DemaConsulting.Speech.Sherpa.Tests/RecognitionSubsystem/SherpaOnnxRecognitionEngineTests.cs", "test/DemaConsulting.Speech.Sherpa.Tests/RecognitionSubsystem/SherpaOnnxRecognitionEngineAccuracyTests.cs");
            designFiles = ("docs/design/speech-sherpa/recognition-subsystem/sherpa-onnx-recognition-engine.md");
            verificationFiles = ("docs/verification/speech-sherpa/recognition-subsystem/sherpa-onnx-recognition-engine.md");
            requirementFiles = ("docs/reqstream/speech-sherpa/recognition-subsystem/sherpa-onnx-recognition-engine.yaml");
        }

        // Wraps the sherpa-onnx recognizer behind the recognition backend contract and returns recognition
        // results.
        dependency SherpaOnnxRecognitionEngine to Speech_RecognitionBackend::RecognitionBackend;
        dependency SherpaOnnxRecognitionEngine to Speech_SpeechRecognitionResult::SpeechRecognitionResult;
        dependency SherpaOnnxRecognitionEngine to OtsDependencies::SherpaOnnx;
    }
}
