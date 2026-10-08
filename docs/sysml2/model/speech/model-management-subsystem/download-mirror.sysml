package Speech {
    part def DownloadMirror {
        doc /* Describes an internal HTTP(S) mirror SpeechModelDownloader should fetch every
             * model's declared files from instead of each model's own hardcoded source URI,
             * plus the single authentication mechanism (NetworkCredential for Basic/NTLM, or
             * a bearer token) that mirror requires, if any. BaseUri must be absolute with
             * scheme http or https - a file://UNC mirror has no implementation path anywhere
             * in this library and is explicitly out of scope. Credentials and BearerToken are
             * mutually exclusive by design: supplying both is a configuration error, not a
             * silent fallback. */

        comment sourceRef /* Source: src/DemaConsulting.Speech/ModelManagementSubsystem/DownloadMirror.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Tests/ModelManagementSubsystem/DownloadMirrorTests.cs */
        comment designRef /* Design: docs/design/speech/model-management-subsystem/download-mirror.md */
        comment verificationRef /* Verification: docs/verification/speech/model-management-subsystem/download-mirror.md */
        comment reqRef /* Requirements: docs/reqstream/speech/model-management-subsystem/download-mirror.yaml */
    }
}
