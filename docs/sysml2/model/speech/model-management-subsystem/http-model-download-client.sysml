package Speech {
    part def HttpModelDownloadClient {
        doc /* Real HttpClient-backed implementation of IModelDownloadClient that streams a file
             * from an HTTPS URI to a destination stream, reporting progress as bytes arrive.
             * Applies a supplied mirrorAuth's BearerToken as an explicit Authorization: Bearer
             * header, or its Credentials via a lazily-created, always internally-owned second
             * HttpClient whose HttpClientHandler.Credentials negotiates Basic/NTLM, without
             * disturbing the primary HttpClient's caller-supplied-vs-internally-owned
             * ownership contract. */

        comment sourceRef /* Source: src/DemaConsulting.Speech/ModelManagementSubsystem/HttpModelDownloadClient.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Tests/ModelManagementSubsystem/HttpModelDownloadClientTests.cs */
        comment designRef /* Design: docs/design/speech/model-management-subsystem/http-model-download-client.md */
        comment verificationRef /* Verification: docs/verification/speech/model-management-subsystem/http-model-download-client.md */
        comment reqRef /* Requirements: docs/reqstream/speech/model-management-subsystem/http-model-download-client.yaml */
    }
}
