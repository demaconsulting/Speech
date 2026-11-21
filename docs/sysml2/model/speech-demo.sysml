package SpeechDemo {
    private import SoftwareItemMetadata::*;
    private import OtsDependencies::*;
    private import SpeechDemo_App::*;
    private import SpeechDemo_AppLaunchOptions::*;
    private import SpeechDemo_DeviceSelectionSubsystem::*;
    private import SpeechDemo_MirrorOptionsFactory::*;
    private import SpeechDemo_ModelCatalogSubsystem::*;
    private import SpeechDemo_ModelSettingsSubsystem::*;
    private import SpeechDemo_Program::*;
    private import SpeechDemo_RecognitionPanelSubsystem::*;
    private import SpeechDemo_ShellSubsystem::*;
    private import SpeechDemo_SynthesisPanelSubsystem::*;

    doc
    /*
     * SpeechDemo is the Avalonia desktop application that demonstrates the Speech library from the perspective of a
     * real consuming application, and proves the library's public API is genuinely usable without any internal
     * access.
     */

    part def SpeechDemoSystem {
        doc /* The SpeechDemo application as a whole. */

        metadata info : SoftwareInfo {
            testFiles = ("test/DemaConsulting.Speech.Demo.Tests/SpeechDemoTests.cs");
            designFiles = ("docs/design/speech-demo.md");
            verificationFiles = ("docs/verification/speech-demo.md");
            requirementFiles = ("docs/reqstream/speech-demo.yaml");
        }

        part shellSubsystem : ShellSubsystem;
        part deviceSelectionSubsystem : DeviceSelectionSubsystem;
        part modelCatalogSubsystem : ModelCatalogSubsystem;
        part modelSettingsSubsystem : ModelSettingsSubsystem;
        part synthesisPanelSubsystem : SynthesisPanelSubsystem;
        part recognitionPanelSubsystem : RecognitionPanelSubsystem;
        part app : App;
        part appLaunchOptions : AppLaunchOptions;
        part mirrorOptionsFactory : MirrorOptionsFactory;
        part program : Program;
        part avalonia : Avalonia;
        part communityToolkitMvvm : CommunityToolkitMvvm;
    }
}
