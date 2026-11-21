package SpeechCli_DownloadProgressReporter {
    private import SoftwareItemMetadata::*;
    private import SpeechCli_Context::*;

    part def DownloadProgressReporter {
        doc /* Creates the throttled live progress reporter shared by download and voices install: one line per whole
             * percent (or whole kilobyte when the total is unknown), repeats suppressed, label supplied by the
             * caller. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech.Cli/Commands/ModelCommandsSubsystem/DownloadProgressReporter.cs");
            testFiles = ("test/DemaConsulting.Speech.Cli.Tests/Commands/ModelCommandsSubsystem/DownloadProgressReporterTests.cs");
            designFiles = ("docs/design/speech-cli/model-commands-subsystem/download-progress-reporter.md");
            verificationFiles = ("docs/verification/speech-cli/model-commands-subsystem/download-progress-reporter.md");
            requirementFiles = ("docs/reqstream/speech-cli/model-commands-subsystem/download-progress-reporter.yaml");
        }

        // Turns downloader progress updates into limited console progress lines, shaped by Context
        // options.
        dependency DownloadProgressReporter to Context;
        dependency DownloadProgressReporter to Speech_SpeechModelDownloadProgress::SpeechModelDownloadProgress;
    }
}
