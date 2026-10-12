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
        part app : App;
        part appLaunchOptions : AppLaunchOptions;
        part mirrorOptionsFactory : MirrorOptionsFactory;
        part program : Program;
        part avalonia : Avalonia;
        part communityToolkitMvvm : CommunityToolkitMvvm;
    }

    part def App {
        doc /* The Avalonia application object and the demo's manual composition root: builds
             * the library entry points and demo service adapters and wires them into the
             * shell. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Demo/App.axaml.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Demo.Tests/SpeechDemoTests.cs */
        comment designRef /* Design: docs/design/speech-demo/app.md */
        comment verificationRef /* Verification: docs/verification/speech-demo/app.md */
        comment reqRef /* Requirements: docs/reqstream/speech-demo/app.yaml */
    }

    part def AppLaunchOptions {
        doc /* The parser and holder of the demo's launch-time command-line options. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Demo/AppLaunchOptions.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Demo.Tests/AppLaunchOptionsTests.cs */
        comment designRef /* Design: docs/design/speech-demo/app-launch-options.md */
        comment verificationRef /* Verification: docs/verification/speech-demo/app-launch-options.md */
        comment reqRef /* Requirements: docs/reqstream/speech-demo/app-launch-options.yaml */
    }

    part def MirrorOptionsFactory {
        doc /* The shared validator that builds a download mirror from raw strings for both
             * launch-time and in-app mirror configuration. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Demo/MirrorOptionsFactory.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Demo.Tests/AppLaunchOptionsTests.cs (verified through its callers) */
        comment designRef /* Design: docs/design/speech-demo/mirror-options-factory.md */
        comment verificationRef /* Verification: docs/verification/speech-demo/mirror-options-factory.md */
        comment reqRef /* Requirements: docs/reqstream/speech-demo/mirror-options-factory.yaml */
    }

    part def Program {
        doc /* The process entry point: fails fast on invalid launch options and configures
             * the Avalonia application builder. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Demo/Program.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Demo.Tests/Ots/AvaloniaTests.cs */
        comment designRef /* Design: docs/design/speech-demo/program.md */
        comment verificationRef /* Verification: docs/verification/speech-demo/program.md */
        comment reqRef /* Requirements: docs/reqstream/speech-demo/program.yaml */
    }
}
