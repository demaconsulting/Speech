package SpeechOnnxKokoro_OnnxKokoroEnglishSynthesisModel {
    private import SoftwareItemMetadata::*;
    private import SpeechOnnxKokoro_KokoroPhonemePipeline::*;
    private import SpeechOnnxKokoro_KokoroPhonemeVocabulary::*;
    private import SpeechOnnxKokoro_OnnxKokoroSynthesisEngine::*;

    part def OnnxKokoroEnglishSynthesisModel {
        doc /* Real, concrete ISynthesisModel backing the bare ONNX export onnx-community/Kokoro-82M-v1.0-ONNX
             * (Apache-2.0), run directly through ONNX Runtime with no bundled native inference engine. Declares 29
             * American/British English voices as a ChoiceParameter (the British voices speak American phonemes),
             * supplies the shared English text analysis (SpeechTextEn: normalization and heteronym readings) to the
             * session, downloads one SHA-256-pinned bundle from the project's models release (Bundles) whose 30 pinned
             * vendor files (the ONNX graph plus every voice's style-vector file) its InstallAsync extracts and verifies
             * through SpeechModelBundle, builds its ONNX Runtime session through the sibling SpeechOnnx system's
             * OnnxExecutionProviderSelector (supplying OnnxKokoroSynthesisEngine.RunProbeInference as the
             * session-validation probe for each accelerated candidate), and owns a confirmed voice-to-speaker-id
             * mapping via ISynthesisModel.ResolveSpeakerId. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech.Onnx.Kokoro/ModelManagementSubsystem/OnnxKokoroEnglishSynthesisModel.cs");
            testFiles = ("test/DemaConsulting.Speech.Onnx.Kokoro.Tests/ModelManagementSubsystem/OnnxKokoroEnglishSynthesisModelTests.cs");
            designFiles = ("docs/design/speech-onnx-kokoro/model-management-subsystem/onnx-kokoro-english-synthesis-model.md");
            verificationFiles = ("docs/verification/speech-onnx-kokoro/model-management-subsystem/onnx-kokoro-english-synthesis-model.md");
            requirementFiles = ("docs/reqstream/speech-onnx-kokoro/model-management-subsystem/onnx-kokoro-english-synthesis-model.yaml");
        }

        // Declares the model contract and downloads, then builds the Kokoro engine with text analysis and
        // phonemes.
        dependency OnnxKokoroEnglishSynthesisModel to KokoroPhonemePipeline;
        dependency OnnxKokoroEnglishSynthesisModel to KokoroPhonemeVocabulary;
        dependency OnnxKokoroEnglishSynthesisModel to OnnxKokoroSynthesisEngine;
        dependency OnnxKokoroEnglishSynthesisModel to SpeechOnnx_OnnxExecutionProviderSelector::OnnxExecutionProviderSelector;
        dependency OnnxKokoroEnglishSynthesisModel to SpeechTextEn_EnglishTextAnalyzer::EnglishTextAnalyzer;
        dependency OnnxKokoroEnglishSynthesisModel to SpeechTextEn_EnglishTextAnalyzerOptions::EnglishTextAnalyzerOptions;
        dependency OnnxKokoroEnglishSynthesisModel to Speech_AudioFormat::AudioFormat;
        dependency OnnxKokoroEnglishSynthesisModel to Speech_SpeechModelContract::SpeechModelContract;
        dependency OnnxKokoroEnglishSynthesisModel to Speech_SpeechModelBundle::SpeechModelBundle;
        dependency OnnxKokoroEnglishSynthesisModel to Speech_SpeechModelDescriptorEnums::SpeechModelDescriptorEnums;
        dependency OnnxKokoroEnglishSynthesisModel to Speech_SpeechModelDownloadDescriptor::SpeechModelDownloadDescriptor;
        dependency OnnxKokoroEnglishSynthesisModel to Speech_SpeechModelDownloadFile::SpeechModelDownloadFile;
        dependency OnnxKokoroEnglishSynthesisModel to Speech_SpeechModelParameters::SpeechModelParameters;
        dependency OnnxKokoroEnglishSynthesisModel to Speech_SynthesisBackend::SynthesisBackend;
        dependency OnnxKokoroEnglishSynthesisModel to Speech_TextAnalysis::TextAnalysis;
        dependency OnnxKokoroEnglishSynthesisModel to OtsDependencies::MicrosoftMlOnnxRuntime;
    }
}
