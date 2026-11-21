package SpeechOnnxPiper_SynthesisSubsystem {
    private import SoftwareItemMetadata::*;
    private import SpeechOnnxPiper_OnnxPiperSynthesisEngine::*;
    private import SpeechOnnxPiper_PiperPhonemePipeline::*;

    part def SynthesisSubsystem {
        doc /* The real ONNX-Runtime-backed implementation of Speech's public ISynthesisBackend and
             * IAnnotatedSynthesisBackend seams over the Piper voice's ONNX graph: the phoneme pipeline that turns
             * text and the annotations of the English text analysis into framed phoneme ids (PiperPhonemePipeline)
             * and the engine that runs the graph over them (OnnxPiperSynthesisEngine). */

        metadata info : SoftwareInfo {
            testFiles = ("test/DemaConsulting.Speech.Onnx.Piper.Tests/SynthesisSubsystem/SynthesisSubsystemTests.cs");
            designFiles = ("docs/design/speech-onnx-piper/synthesis-subsystem.md");
            verificationFiles = ("docs/verification/speech-onnx-piper/synthesis-subsystem.md");
            requirementFiles = ("docs/reqstream/speech-onnx-piper/synthesis-subsystem.yaml");
        }

        part onnxPiperSynthesisEngine : OnnxPiperSynthesisEngine;
        part piperPhonemePipeline : PiperPhonemePipeline;
    }
}
