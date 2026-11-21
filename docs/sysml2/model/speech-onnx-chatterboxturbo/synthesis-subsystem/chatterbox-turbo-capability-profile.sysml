package SpeechOnnxChatterboxTurbo_ChatterboxTurboCapabilityProfile {
    private import SoftwareItemMetadata::*;

    enum def ChatterboxTurboTagKind {
        enum Native;
        enum Pause;
        enum Mood;
        enum Strip;
    }

    part def ChatterboxTurboCapabilityProfile {
        doc /* The audio-tag rendering policy for Chatterbox Turbo: native tokens, silence segments, omission,
             * trailing-tag handling and sentence segmentation. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech.Onnx.ChatterboxTurbo/SynthesisSubsystem/ChatterboxTurboCapabilityProfile.cs", "src/DemaConsulting.Speech.Onnx.ChatterboxTurbo/SynthesisSubsystem/ChatterboxTurboTagKind.cs", "src/DemaConsulting.Speech.Onnx.ChatterboxTurbo/SynthesisSubsystem/ChatterboxTurboTagTreatment.cs");
            testFiles = ("test/DemaConsulting.Speech.Onnx.ChatterboxTurbo.Tests/SynthesisSubsystem/ChatterboxTurboCapabilityProfileTests.cs");
            designFiles = ("docs/design/speech-onnx-chatterboxturbo/synthesis-subsystem/chatterbox-turbo-capability-profile.md");
            verificationFiles = ("docs/verification/speech-onnx-chatterboxturbo/synthesis-subsystem/chatterbox-turbo-capability-profile.md");
            requirementFiles = ("docs/reqstream/speech-onnx-chatterboxturbo/synthesis-subsystem/chatterbox-turbo-capability-profile.yaml");
        }

        // Applies the audio tag parser and Layer 2 rendering rules to define how Chatterbox renders tags.
        dependency ChatterboxTurboCapabilityProfile to SpeechOnnxChatterboxTurbo_OnnxChatterboxTurboEnglishSynthesisModel::OnnxChatterboxTurboEnglishSynthesisModel;
        dependency ChatterboxTurboCapabilityProfile to Speech_AudioTagDescriptor::AudioTagDescriptor;
        dependency ChatterboxTurboCapabilityProfile to Speech_AudioTagParser::AudioTagParser;
        dependency ChatterboxTurboCapabilityProfile to Speech_Layer2Rendering::Layer2Rendering;
        dependency ChatterboxTurboCapabilityProfile to Speech_ReferenceVoicePack::ReferenceVoicePack;
        dependency ChatterboxTurboCapabilityProfile to Speech_SpeechModelContract::SpeechModelContract;
    }
}
