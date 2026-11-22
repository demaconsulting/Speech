package ModelBundler_VendorFileSource {
    private import SoftwareItemMetadata::*;

    part def VendorFileSource {
        doc /* Finds a local copy of each vendor file - the installed copy in the per-user model store for each of
             * the bundle's model ids, then the SHA-keyed cache, then an HTTPS download of the original URL into the
             * cache - and hashes every copy against its compiled SHA-256 pin before use. Every source-store path is
             * confined beneath the store root (a model id must be one safe directory name). A copy that differs from
             * its pin always fails; a file no source holds is reported as unavailable. */

        metadata info : SoftwareInfo {
            sourceFiles = ("tools/ModelBundler/VendorFileSource.cs");
            testFiles = ("test/DemaConsulting.Speech.ModelBundler.Tests/VendorFileSourceTests.cs");
        }

        // Resolves the declared vendor files (original URL, pin and install path).
        dependency VendorFileSource to Speech_SpeechModelDownloadFile::SpeechModelDownloadFile;
    }
}