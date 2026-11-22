package ModelBundler_ReleaseUploader {
    private import SoftwareItemMetadata::*;
    private import ModelBundler_ReleaseNotesWriter::*;

    part def ReleaseUploader {
        doc /* Publishes a build to the append-only models release with gh through an injectable process runner:
             * checks every local zip against SHA256SUMS, lists the release assets, skips a name published with the
             * same digest, refuses the whole upload when a name is published with another digest, and uploads the
             * rest with gh release upload, never with --clobber. It never deletes or replaces an asset. */

        metadata info : SoftwareInfo {
            sourceFiles = ("tools/ModelBundler/ReleaseUploader.cs", "tools/ModelBundler/ProcessRunner.cs");
            testFiles = ("test/DemaConsulting.Speech.ModelBundler.Tests/ReleaseUploaderTests.cs");
        }

        // Reads the digests the build listed.
        dependency ReleaseUploader to ReleaseNotesWriter;
    }
}