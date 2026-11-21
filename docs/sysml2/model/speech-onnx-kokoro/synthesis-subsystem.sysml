package SpeechOnnxKokoro_SynthesisSubsystem {
    private import SoftwareItemMetadata::*;
    private import SpeechOnnxKokoro_KokoroPhonemePipeline::*;
    private import SpeechOnnxKokoro_OnnxKokoroSynthesisEngine::*;

    part def SynthesisSubsystem {
        doc /* The real ONNX-Runtime-backed implementation of Speech's public ISynthesisBackend seam (and its
             * annotated derivative) over Kokoro v1.0's bare ONNX graph, owning the conversion of text to phoneme
             * token ids entirely in managed code: the KokoroPhonemePipeline unit (shared English phonemizer, Kokoro
             * phoneme symbols, token ids, parts for long text) and the OnnxKokoroSynthesisEngine unit (one graph run
             * per part). */

        metadata info : SoftwareInfo {
            designFiles = ("docs/design/speech-onnx-kokoro/synthesis-subsystem.md");
            verificationFiles = ("docs/verification/speech-onnx-kokoro/synthesis-subsystem.md");
            requirementFiles = ("docs/reqstream/speech-onnx-kokoro/synthesis-subsystem.yaml");
        }

        part kokoroPhonemePipeline : KokoroPhonemePipeline;
        part onnxKokoroSynthesisEngine : OnnxKokoroSynthesisEngine;
    }
}
