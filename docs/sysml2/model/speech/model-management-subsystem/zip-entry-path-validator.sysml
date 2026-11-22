package Speech_ZipEntryPathValidator {
    private import SoftwareItemMetadata::*;

    part def ZipEntryPathValidator {
        doc /* Internal, shared zip-slip and name-portability guard: rejects archive entry names that could escape the
             * extraction directory (parent or current-directory segments, rooted names, backslashes, colons, NULs) or
             * that are not valid on every platform, and proves each resolved destination lies strictly inside the
             * extraction root. Used by every installer that extracts a downloaded archive. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech/ModelManagementSubsystem/ZipEntryPathValidator.cs");
            testFiles = ("test/DemaConsulting.Speech.Tests/ModelManagementSubsystem/ZipEntryPathValidatorTests.cs");
            designFiles = ("docs/design/speech/model-management-subsystem/zip-entry-path-validator.md");
            verificationFiles = ("docs/verification/speech/model-management-subsystem/zip-entry-path-validator.md");
            requirementFiles = ("docs/reqstream/speech/model-management-subsystem/zip-entry-path-validator.yaml");
        }
    }
}
