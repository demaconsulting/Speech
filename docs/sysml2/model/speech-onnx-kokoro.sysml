package SpeechOnnxKokoro {
    private import SoftwareItemMetadata::*;
    private import OtsDependencies::*;
    private import SpeechOnnxKokoro_ModelManagementSubsystem::*;
    private import SpeechOnnxKokoro_SynthesisSubsystem::*;

    doc
    /*
     * SpeechOnnxKokoro is the DemaConsulting.Speech.Onnx.Kokoro library that supplies a concrete, ONNX-Runtime-backed
     * Kokoro v1.0 text-to-speech model and inference engine for the Speech library through its ISynthesisModel
     * extension seam. Speech must never depend on it. Unlike SpeechSherpa, this system owns its own text-to-token
     * pipeline, since the Kokoro v1.0 ONNX export performs only the waveform-generation forward pass: it uses the
     * English text analysis of SpeechTextEn (spoken forms of numbers and abbreviations, the reading of each
     * heteronym), the phonemizer and the Kokoro symbol renderer of SpeechPhonemesEn (CMU Pronouncing Dictionary plus
     * a letter-to-sound model), and Kokoro's own embedded token vocabulary. It depends on the Speech, SpeechOnnx,
     * SpeechTextEn and SpeechPhonemesEn libraries.
     */

    part def SpeechOnnxKokoroSystem {
        doc /* The SpeechOnnxKokoro library as a whole. */

        metadata info : SoftwareInfo {
            testFiles = ("test/DemaConsulting.Speech.Onnx.Kokoro.Tests/SpeechOnnxKokoroTests.cs");
            designFiles = ("docs/design/speech-onnx-kokoro.md");
            verificationFiles = ("docs/verification/speech-onnx-kokoro.md");
            requirementFiles = ("docs/reqstream/speech-onnx-kokoro.yaml");
        }

        part modelManagementSubsystem : ModelManagementSubsystem;
        part synthesisSubsystem : SynthesisSubsystem;
        part microsoftMlOnnxRuntime : MicrosoftMlOnnxRuntime;
    }
}
