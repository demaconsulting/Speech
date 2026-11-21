package Speech_HttpModelDownloadClient {
    private import SoftwareItemMetadata::*;
    private import Speech_DownloadMirror::*;
    private import Speech_IModelDownloadClient::*;
    private import Speech_SpeechModelDownloadProgress::*;

    part def HttpModelDownloadClient {
        doc /* Real HttpClient-backed implementation of IModelDownloadClient that streams a file from an HTTPS URI to
             * a destination stream, reporting progress as bytes arrive. Constructed with an optional DownloadMirror;
             * applies its Credentials as a preemptive Basic Authorization header, or its BearerToken as a Bearer
             * Authorization header, on every request whose URI falls within the configured mirror's base URI - sent
             * through the single HttpClient this instance was constructed with (never a second, separately configured
             * client), so the caller-supplied client's own configuration always applies. No NTLM challenge/response
             * handshake is supported. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech/ModelManagementSubsystem/HttpModelDownloadClient.cs");
            testFiles = ("test/DemaConsulting.Speech.Tests/ModelManagementSubsystem/HttpModelDownloadClientTests.cs");
            designFiles = ("docs/design/speech/model-management-subsystem/http-model-download-client.md");
            verificationFiles = ("docs/verification/speech/model-management-subsystem/http-model-download-client.md");
            requirementFiles = ("docs/reqstream/speech/model-management-subsystem/http-model-download-client.yaml");
        }

        // Implements the download seam over HTTP, applying the mirror and reporting progress.
        dependency HttpModelDownloadClient to DownloadMirror;
        dependency HttpModelDownloadClient to IModelDownloadClient;
        dependency HttpModelDownloadClient to SpeechModelDownloadProgress;
    }
}
