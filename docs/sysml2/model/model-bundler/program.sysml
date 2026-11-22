package ModelBundler_Program {
    private import SoftwareItemMetadata::*;
    private import ModelBundler_BundleBuilder::*;
    private import ModelBundler_BundleCatalog::*;
    private import ModelBundler_BundleDefinitions::*;
    private import ModelBundler_CommandLineOptions::*;
    private import ModelBundler_ReleaseUploader::*;
    private import ModelBundler_VendorFileSource::*;

    part def Program {
        doc /* Entry point of the maintainer-only model bundling tool: parses the build, verify and upload commands,
             * loads bundles.json and the compiled bundle contents, creates the HTTP client and vendor file source,
             * and runs the bundle builder or the release uploader. Exit codes: 0 success, 1 a failed build,
             * verification or upload, 2 an invalid command line. */

        metadata info : SoftwareInfo {
            sourceFiles = ("tools/ModelBundler/Program.cs");
        }

        // Dispatches each command to the builder or the uploader over the definitions and the compiled contents.
        dependency Program to BundleBuilder;
        dependency Program to BundleCatalog;
        dependency Program to BundleDefinitions;
        dependency Program to CommandLineOptions;
        dependency Program to ReleaseUploader;
        dependency Program to VendorFileSource;
    }
}