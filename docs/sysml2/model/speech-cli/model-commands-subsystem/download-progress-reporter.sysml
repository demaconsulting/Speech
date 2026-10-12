package SpeechCli {
    part def DownloadProgressReporter {
        doc /* Creates the throttled live progress reporter shared by download and
             * voices install: one line per whole percent (or whole kilobyte when the total is
             * unknown), repeats suppressed, label supplied by the caller. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Cli/Commands/ModelCommandsSubsystem/DownloadProgressReporter.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Cli.Tests/Commands/ModelCommandsSubsystem/DownloadProgressReporterTests.cs */
    }
}
