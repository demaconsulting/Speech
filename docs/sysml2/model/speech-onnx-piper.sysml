package SpeechOnnxPiper {
    private import SoftwareItemMetadata::*;
    private import OtsDependencies::*;
    private import SpeechOnnxPiper_ModelManagementSubsystem::*;
    private import SpeechOnnxPiper_SynthesisSubsystem::*;

    doc
    /*
     * SpeechOnnxPiper is the DemaConsulting.Speech.Onnx.Piper library that supplies the Piper (VITS) LibriTTS-R
     * English voice en_US-libritts_r-medium (904 speakers) as a synthesis model for the Speech library through its
     * ISynthesisModel extension seam, run directly through ONNX Runtime with no bundled native inference engine and
     * no espeak-ng. Speech must never depend on it. The system owns the whole text-to-speech pipeline of the voice:
     * it uses the English text analysis of the sibling SpeechTextEn system (the model's ITextAnalyzer: spoken forms
     * of numbers and abbreviations, the reading of each heteronym) and the phonemizer and espeak-style IPA renderer
     * of the sibling SpeechPhonemesEn system, encodes the phonemes with the voice's own symbol table, and builds its
     * inference session through the sibling SpeechOnnx system's execution-provider selector. The voice is installed
     * from the sherpa-onnx project's .tar.bz2 archive of the same voice, pinned by its SHA-256, from which only the
     * graph and the configuration are kept; SharpCompress reads that archive. SpeechTextEn and SpeechPhonemesEn are
     * sibling systems whose own models are not part of this system, so they appear here only in this text.
     */

    part def SpeechOnnxPiperSystem {
        doc /* The SpeechOnnxPiper library as a whole. */

        metadata info : SoftwareInfo {
            testFiles = ("test/DemaConsulting.Speech.Onnx.Piper.Tests/SpeechOnnxPiperTests.cs");
            designFiles = ("docs/design/speech-onnx-piper.md");
            verificationFiles = ("docs/verification/speech-onnx-piper.md");
            requirementFiles = ("docs/reqstream/speech-onnx-piper.yaml");
        }

        part modelManagementSubsystem : ModelManagementSubsystem;
        part synthesisSubsystem : SynthesisSubsystem;
        part microsoftMlOnnxRuntime : MicrosoftMlOnnxRuntime;
        part sharpCompress : SharpCompress;
    }
}
