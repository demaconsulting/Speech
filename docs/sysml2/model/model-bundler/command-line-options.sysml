package ModelBundler_CommandLineOptions {
    private import SoftwareItemMetadata::*;

    part def CommandLineOptions {
        doc /* The parsed command line: build (--all or --bundle, --output, --source-store, --cache,
             * --skip-unavailable), verify (--input, --source-store, --cache) or upload (--input, --repo, --dry-run).
             * Rejects an unknown command or option, a missing value, or an option the command does not take. */

        metadata info : SoftwareInfo {
            sourceFiles = ("tools/ModelBundler/CommandLineOptions.cs");
            testFiles = ("test/DemaConsulting.Speech.ModelBundler.Tests/CommandLineOptionsTests.cs");
        }
    }
}