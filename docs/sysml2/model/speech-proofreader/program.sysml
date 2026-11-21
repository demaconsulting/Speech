package SpeechProofreader_Program {
    private import SoftwareItemMetadata::*;

    part def Program {
        doc /* Process entry point with the command-line launch options (the text file to open and the model choices). */

        metadata info : SoftwareInfo {
            sourceFiles = ("tools/DemaConsulting.Speech.Proofreader/Program.cs");
        }

        // Parses launch options and starts the Avalonia App.
        dependency Program to SpeechProofreader_App::App;
        dependency Program to OtsDependencies::Avalonia;
    }
}
