package Speech {
    part def HttpModelDownloadClient {
        doc /* Real HttpClient-backed implementation of IModelDownloadClient that streams a file
             * from an HTTPS URI to a destination stream, reporting progress as bytes arrive.
             * Constructed with an optional DownloadMirror; applies its Credentials as a
             * preemptive Basic Authorization header, or its BearerToken as a Bearer
             * Authorization header, on every request whose URI falls within the configured
             * mirror's base URI - sent through the single HttpClient this instance was
             * constructed with (never a second, separately configured client), so the
             * caller-supplied client's own configuration always applies. No NTLM
             * challenge/response handshake is supported. */

        comment sourceRef /* Source: src/DemaConsulting.Speech/ModelManagementSubsystem/HttpModelDownloadClient.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Tests/ModelManagementSubsystem/HttpModelDownloadClientTests.cs */
        comment designRef /* Design: docs/design/speech/model-management-subsystem/http-model-download-client.md */
        comment verificationRef /* Verification: docs/verification/speech/model-management-subsystem/http-model-download-client.md */
        comment reqRef /* Requirements: docs/reqstream/speech/model-management-subsystem/http-model-download-client.yaml */
    }
}
