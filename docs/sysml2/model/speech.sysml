package Speech {
    private import SoftwareItemMetadata::*;
    private import OtsDependencies::*;
    private import Speech_AudioSubsystem::*;
    private import Speech_Diagnostics::*;
    private import Speech_ModelManagementSubsystem::*;
    private import Speech_RecognitionSubsystem::*;
    private import Speech_SynthesisSubsystem::*;

    doc
    /*
     * Speech is a cross-platform .NET library providing local, offline speech-to-text (STT) and text-to-speech (TTS)
     * services for desktop applications, with mockable audio interfaces, on-demand model download and verification,
     * and Natural Language Audio Tags.
     */

    part def SpeechSystem {
        doc /* The core Speech library: engine-neutral audio, model management, recognition and synthesis. */

        metadata info : SoftwareInfo {
            testFiles = ("test/DemaConsulting.Speech.Tests/SpeechTests.cs", "test/DemaConsulting.Speech.Tests/PlatformSupportTests.cs");
            designFiles = ("docs/design/speech.md");
            verificationFiles = ("docs/verification/speech.md");
            requirementFiles = ("docs/reqstream/speech.yaml");
        }

        part diagnostics : Diagnostics;
        part audioSubsystem : AudioSubsystem;
        part modelManagementSubsystem : ModelManagementSubsystem;
        part recognitionSubsystem : RecognitionSubsystem;
        part synthesisSubsystem : SynthesisSubsystem;
        part systemNumericsTensors : SystemNumericsTensors;
    }
}
