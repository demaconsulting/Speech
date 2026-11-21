package Speech_DownloadMirror {
    private import SoftwareItemMetadata::*;
    private import ScalarValues::*;

    part def DownloadMirror {
        doc /* Describes an internal HTTP(S) mirror SpeechModelDownloader should fetch every model's declared files
             * from instead of each model's own hardcoded source URI, plus the single authentication mechanism
             * (NetworkCredential for preemptive HTTP Basic, or a bearer token) that mirror requires, if any. BaseUri
             * must be absolute with scheme http or https - a file://UNC mirror has no implementation path anywhere in
             * this library and is explicitly out of scope. Credentials and BearerToken are mutually exclusive by
             * design: supplying both is a configuration error, not a silent fallback. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech/ModelManagementSubsystem/DownloadMirror.cs");
            testFiles = ("test/DemaConsulting.Speech.Tests/ModelManagementSubsystem/DownloadMirrorTests.cs");
            designFiles = ("docs/design/speech/model-management-subsystem/download-mirror.md");
            verificationFiles = ("docs/verification/speech/model-management-subsystem/download-mirror.md");
            requirementFiles = ("docs/reqstream/speech/model-management-subsystem/download-mirror.yaml");
        }

        attribute baseUri : String;
        attribute bearerToken : String [0..1];
    }
}
