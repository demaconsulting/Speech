package SpeechSherpa_SherpaOnnxSynthesisEngine {
    private import SoftwareItemMetadata::*;

    part def SherpaOnnxSynthesisEngine {
        doc /* Real ISynthesisBackend implementation wrapping one loaded sherpa-onnx offline text-to-speech engine,
             * confining every synthesis inference interop call behind Speech's public mockable seam. Constructed by
             * the synthesis models' CreateBackend overrides. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech.Sherpa/SynthesisSubsystem/SherpaOnnxSynthesisEngine.cs");
            testFiles = ("test/DemaConsulting.Speech.Sherpa.Tests/SynthesisSubsystem/SherpaOnnxSynthesisEngineTests.cs");
            designFiles = ("docs/design/speech-sherpa/synthesis-subsystem/sherpa-onnx-synthesis-engine.md");
            verificationFiles = ("docs/verification/speech-sherpa/synthesis-subsystem/sherpa-onnx-synthesis-engine.md");
            requirementFiles = ("docs/reqstream/speech-sherpa/synthesis-subsystem/sherpa-onnx-synthesis-engine.yaml");
        }

        // Wraps sherpa-onnx text-to-speech behind the synthesis backend contract, returning backend audio.
        dependency SherpaOnnxSynthesisEngine to Speech_BackendAudio::BackendAudio;
        dependency SherpaOnnxSynthesisEngine to Speech_SynthesisBackend::SynthesisBackend;
        dependency SherpaOnnxSynthesisEngine to OtsDependencies::SherpaOnnx;
    }
}
