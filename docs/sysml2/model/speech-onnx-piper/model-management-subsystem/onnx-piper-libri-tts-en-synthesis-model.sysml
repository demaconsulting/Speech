package SpeechOnnxPiper_OnnxPiperLibriTtsEnSynthesisModel {
    private import SoftwareItemMetadata::*;
    private import SpeechOnnxPiper_OnnxPiperSynthesisEngine::*;
    private import SpeechOnnxPiper_PiperPhonemePipeline::*;
    private import SpeechOnnxPiper_PiperVoiceConfig::*;

    part def OnnxPiperLibriTtsEnSynthesisModel {
        doc /* Real, concrete ISynthesisModel for the Piper English (US) voice en_US-libritts_r-medium (CC BY 4.0, 904
             * speakers, 22,050 Hz), run directly through ONNX Runtime. Declares the stable identifier, the license,
             * the shared English text analyzer, the capability profile that joins the clause pieces of a sentence
             * into one segment (PiperCapabilityProfile) and one integer speaker parameter (0 to 903, default 0), owns
             * the SHA-256-pinned models-release bundle of the voice's three original files (graph, configuration and
             * model card, each with its own pin) and its installation through SpeechModelBundle, and builds the
             * synthesis engine from the installed files through the sibling SpeechOnnx system's
             * OnnxExecutionProviderSelector, after the voice configuration has been read and checked. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech.Onnx.Piper/ModelManagementSubsystem/OnnxPiperLibriTtsEnSynthesisModel.cs", "src/DemaConsulting.Speech.Onnx.Piper/ModelManagementSubsystem/PiperCapabilityProfile.cs");
            testFiles = ("test/DemaConsulting.Speech.Onnx.Piper.Tests/ModelManagementSubsystem/OnnxPiperLibriTtsEnSynthesisModelTests.cs");
            designFiles = ("docs/design/speech-onnx-piper/model-management-subsystem/onnx-piper-libri-tts-en-synthesis-model.md");
            verificationFiles = ("docs/verification/speech-onnx-piper/model-management-subsystem/onnx-piper-libri-tts-en-synthesis-model.md");
            requirementFiles = ("docs/reqstream/speech-onnx-piper/model-management-subsystem/onnx-piper-libri-tts-en-synthesis-model.yaml");
        }

        // Declares the contract and the voice's bundle, installs it, then builds the Piper engine with text
        // analysis.
        dependency OnnxPiperLibriTtsEnSynthesisModel to OnnxPiperSynthesisEngine;
        dependency OnnxPiperLibriTtsEnSynthesisModel to PiperPhonemePipeline;
        dependency OnnxPiperLibriTtsEnSynthesisModel to PiperVoiceConfig;
        dependency OnnxPiperLibriTtsEnSynthesisModel to SpeechOnnx_OnnxExecutionProviderSelector::OnnxExecutionProviderSelector;
        dependency OnnxPiperLibriTtsEnSynthesisModel to SpeechTextEn_EnglishTextAnalyzer::EnglishTextAnalyzer;
        dependency OnnxPiperLibriTtsEnSynthesisModel to SpeechTextEn_EnglishTextAnalyzerOptions::EnglishTextAnalyzerOptions;
        dependency OnnxPiperLibriTtsEnSynthesisModel to Speech_AudioFormat::AudioFormat;
        dependency OnnxPiperLibriTtsEnSynthesisModel to Speech_AudioTagParser::AudioTagParser;
        dependency OnnxPiperLibriTtsEnSynthesisModel to Speech_Layer2Rendering::Layer2Rendering;
        dependency OnnxPiperLibriTtsEnSynthesisModel to Speech_NumericParameterBounds::NumericParameterBounds;
        dependency OnnxPiperLibriTtsEnSynthesisModel to Speech_SpeechModelBundle::SpeechModelBundle;
        dependency OnnxPiperLibriTtsEnSynthesisModel to Speech_SpeechModelContract::SpeechModelContract;
        dependency OnnxPiperLibriTtsEnSynthesisModel to Speech_SpeechModelDescriptorEnums::SpeechModelDescriptorEnums;
        dependency OnnxPiperLibriTtsEnSynthesisModel to Speech_SpeechModelDownloadDescriptor::SpeechModelDownloadDescriptor;
        dependency OnnxPiperLibriTtsEnSynthesisModel to Speech_SpeechModelDownloadFile::SpeechModelDownloadFile;
        dependency OnnxPiperLibriTtsEnSynthesisModel to Speech_SpeechModelParameters::SpeechModelParameters;
        dependency OnnxPiperLibriTtsEnSynthesisModel to Speech_SynthesisBackend::SynthesisBackend;
        dependency OnnxPiperLibriTtsEnSynthesisModel to Speech_TextAnalysis::TextAnalysis;
    }
}
