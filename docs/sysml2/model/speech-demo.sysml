package SpeechDemo {
    doc
    /*
     * SpeechDemo is the Avalonia desktop application that demonstrates the Speech library
     * from the perspective of a real consuming application, and proves the library's public
     * API is genuinely usable without any internal access.
     */

    part def SpeechDemoSystem {
        doc /* The SpeechDemo application as a whole. */

        comment testRef /* Test: test/DemaConsulting.Speech.Demo.Tests/SpeechDemoTests.cs */
        comment designRef /* Design: docs/design/speech-demo.md */
        comment verificationRef /* Verification: docs/verification/speech-demo.md */
        comment reqRef /* Requirements: docs/reqstream/speech-demo.yaml */

        part shellSubsystem : ShellSubsystem;
        part deviceSelectionSubsystem : DeviceSelectionSubsystem;
        part modelCatalogSubsystem : ModelCatalogSubsystem;
        part modelSettingsSubsystem : ModelSettingsSubsystem;
        part synthesisPanelSubsystem : SynthesisPanelSubsystem;
        part recognitionPanelSubsystem : RecognitionPanelSubsystem;
    }
}
