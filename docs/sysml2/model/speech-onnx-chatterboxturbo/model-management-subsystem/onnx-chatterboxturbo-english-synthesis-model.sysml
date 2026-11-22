package SpeechOnnxChatterboxTurbo_OnnxChatterboxTurboEnglishSynthesisModel {
    private import SoftwareItemMetadata::*;
    private import SpeechOnnxChatterboxTurbo_ByteLevelBpeTokenizer::*;
    private import SpeechOnnxChatterboxTurbo_ChatterboxTurboCapabilityProfile::*;
    private import SpeechOnnxChatterboxTurbo_ChatterboxTurboReferenceVoice::*;
    private import SpeechOnnxChatterboxTurbo_OnnxChatterboxTurboNetworks::*;
    private import SpeechOnnxChatterboxTurbo_OnnxChatterboxTurboSynthesisEngine::*;

    enum def ChatterboxTurboAuxiliaryModelVariant {
        enum Q4;
        enum Fp16;
        enum Fp32;
    }

    enum def ChatterboxTurboLanguageModelVariant {
        enum Fp16;
        enum Fp32;
    }

    enum def ChatterboxTurboEndOfSentenceTagPolicy {
        enum MoveBeforeFinalWord;
        enum Suffix;
        enum Strip;
    }

    part def OnnxChatterboxTurboEnglishSynthesisModel {
        doc /* The ISynthesisModel for Chatterbox Turbo English: identity, precision-variant file set with checksums
             * (downloaded as one language-model and one auxiliary SHA-256-pinned bundle from the project's models
             * release when both are published, extracted and verified through SpeechModelBundle, and otherwise as
             * individual Hugging Face files), voice choice over caller-supplied reference voices, and backend construction from an installed model
             * directory. Includes its ChatterboxTurboModelOptions and variant enums. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech.Onnx.ChatterboxTurbo/ModelManagementSubsystem/OnnxChatterboxTurboEnglishSynthesisModel.cs", "src/DemaConsulting.Speech.Onnx.ChatterboxTurbo/ModelManagementSubsystem/ChatterboxTurboModelOptions.cs", "src/DemaConsulting.Speech.Onnx.ChatterboxTurbo/ModelManagementSubsystem/ChatterboxTurboAuxiliaryModelVariant.cs", "src/DemaConsulting.Speech.Onnx.ChatterboxTurbo/ModelManagementSubsystem/ChatterboxTurboLanguageModelVariant.cs", "src/DemaConsulting.Speech.Onnx.ChatterboxTurbo/ModelManagementSubsystem/ChatterboxTurboEndOfSentenceTagPolicy.cs");
            testFiles = ("test/DemaConsulting.Speech.Onnx.ChatterboxTurbo.Tests/ModelManagementSubsystem/OnnxChatterboxTurboEnglishSynthesisModelTests.cs");
            designFiles = ("docs/design/speech-onnx-chatterboxturbo/model-management-subsystem/onnx-chatterboxturbo-english-synthesis-model.md");
            verificationFiles = ("docs/verification/speech-onnx-chatterboxturbo/model-management-subsystem/onnx-chatterboxturbo-english-synthesis-model.md");
            requirementFiles = ("docs/reqstream/speech-onnx-chatterboxturbo/model-management-subsystem/onnx-chatterboxturbo-english-synthesis-model.yaml");
        }

        // Describes the model contract and download files, then builds the synthesis engine from networks
        // and voices.
        dependency OnnxChatterboxTurboEnglishSynthesisModel to ByteLevelBpeTokenizer;
        dependency OnnxChatterboxTurboEnglishSynthesisModel to ChatterboxTurboCapabilityProfile;
        dependency OnnxChatterboxTurboEnglishSynthesisModel to ChatterboxTurboReferenceVoice;
        dependency OnnxChatterboxTurboEnglishSynthesisModel to OnnxChatterboxTurboNetworks;
        dependency OnnxChatterboxTurboEnglishSynthesisModel to OnnxChatterboxTurboSynthesisEngine;
        dependency OnnxChatterboxTurboEnglishSynthesisModel to Speech_AudioFileReader::AudioFileReader;
        dependency OnnxChatterboxTurboEnglishSynthesisModel to Speech_AudioFormat::AudioFormat;
        dependency OnnxChatterboxTurboEnglishSynthesisModel to Speech_ISpeechDiagnostics::ISpeechDiagnostics;
        dependency OnnxChatterboxTurboEnglishSynthesisModel to Speech_Layer2Rendering::Layer2Rendering;
        dependency OnnxChatterboxTurboEnglishSynthesisModel to Speech_NullSpeechDiagnostics::NullSpeechDiagnostics;
        dependency OnnxChatterboxTurboEnglishSynthesisModel to Speech_ReferenceVoiceLibrary::ReferenceVoiceLibrary;
        dependency OnnxChatterboxTurboEnglishSynthesisModel to Speech_ReferenceVoicePack::ReferenceVoicePack;
        dependency OnnxChatterboxTurboEnglishSynthesisModel to Speech_SpeechDiagnosticLevel::SpeechDiagnosticLevel;
        dependency OnnxChatterboxTurboEnglishSynthesisModel to Speech_SpeechModelContract::SpeechModelContract;
        dependency OnnxChatterboxTurboEnglishSynthesisModel to Speech_SpeechModelBundle::SpeechModelBundle;
        dependency OnnxChatterboxTurboEnglishSynthesisModel to Speech_SpeechModelDescriptorEnums::SpeechModelDescriptorEnums;
        dependency OnnxChatterboxTurboEnglishSynthesisModel to Speech_SpeechModelDownloadDescriptor::SpeechModelDownloadDescriptor;
        dependency OnnxChatterboxTurboEnglishSynthesisModel to Speech_SpeechModelDownloadFile::SpeechModelDownloadFile;
        dependency OnnxChatterboxTurboEnglishSynthesisModel to Speech_SpeechModelParameters::SpeechModelParameters;
        dependency OnnxChatterboxTurboEnglishSynthesisModel to Speech_SynthesisBackend::SynthesisBackend;
    }
}
