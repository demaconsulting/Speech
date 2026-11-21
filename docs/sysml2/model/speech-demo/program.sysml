package SpeechDemo_Program {
    private import SoftwareItemMetadata::*;
    private import SpeechDemo_App::*;
    private import SpeechDemo_AppLaunchOptions::*;

    part def Program {
        doc /* The process entry point: fails fast on invalid launch options and configures the Avalonia application
             * builder. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech.Demo/Program.cs");
            testFiles = ("test/DemaConsulting.Speech.Demo.Tests/Ots/AvaloniaTests.cs", "test/DemaConsulting.Speech.Demo.Tests/ProgramTests.cs");
            designFiles = ("docs/design/speech-demo/program.md");
            verificationFiles = ("docs/verification/speech-demo/program.md");
            requirementFiles = ("docs/reqstream/speech-demo/program.yaml");
        }

        // Checks launch options, then starts Avalonia with the demo App as the application.
        dependency Program to App;
        dependency Program to AppLaunchOptions;
        dependency Program to OtsDependencies::Avalonia;
    }
}
