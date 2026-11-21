package SpeechOnnxPiper_OnnxPiperSynthesisEngine {
    private import SoftwareItemMetadata::*;
    private import SpeechOnnxPiper_PiperPhonemePipeline::*;
    private import SpeechOnnxPiper_PiperVoiceConfig::*;

    part def OnnxPiperSynthesisEngine {
        doc /* Production IAnnotatedSynthesisBackend that runs the Piper voice's ONNX graph directly through ONNX
             * Runtime (inputs input, input_lengths, scales and sid) over the phoneme ids of the PiperPhonemePipeline:
             * the voice's own noise scales, the length scale divided by the requested speed, the speaker clamped into
             * the voice's range, one run per utterance with the audio joined, empty audio for a text with nothing to
             * say, the count and list of symbols the voice's table lacks, the probe inference for execution-provider
             * selection, and ownership of the session. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech.Onnx.Piper/SynthesisSubsystem/OnnxPiperSynthesisEngine.cs");
            testFiles = ("test/DemaConsulting.Speech.Onnx.Piper.Tests/SynthesisSubsystem/OnnxPiperSynthesisEngineTests.cs");
            designFiles = ("docs/design/speech-onnx-piper/synthesis-subsystem/onnx-piper-synthesis-engine.md");
            verificationFiles = ("docs/verification/speech-onnx-piper/synthesis-subsystem/onnx-piper-synthesis-engine.md");
            requirementFiles = ("docs/reqstream/speech-onnx-piper/synthesis-subsystem/onnx-piper-synthesis-engine.yaml");
        }

        // Converts text to phoneme ids with the pipeline and runs the Piper graph in ONNX Runtime for
        // backend audio.
        dependency OnnxPiperSynthesisEngine to PiperPhonemePipeline;
        dependency OnnxPiperSynthesisEngine to PiperVoiceConfig;
        dependency OnnxPiperSynthesisEngine to Speech_BackendAudio::BackendAudio;
        dependency OnnxPiperSynthesisEngine to Speech_SynthesisBackend::SynthesisBackend;
        dependency OnnxPiperSynthesisEngine to Speech_TextAnalysis::TextAnalysis;
        dependency OnnxPiperSynthesisEngine to OtsDependencies::MicrosoftMlOnnxRuntime;
    }
}
