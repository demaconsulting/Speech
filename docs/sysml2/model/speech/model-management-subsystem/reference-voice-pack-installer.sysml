package Speech {
    part def ReferenceVoicePackInstaller {
        doc /* Internal downloader install hook that safely extracts only the voice clips and
             * license/readme files from a verified reference-voice pack archive, writes pack.json
             * and deletes the archive. */

        comment sourceRef /* Source: src/DemaConsulting.Speech/ModelManagementSubsystem/ReferenceVoicePackInstaller.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Tests/ModelManagementSubsystem/ReferenceVoicePackInstallerTests.cs */
        comment designRef /* Design: docs/design/speech/model-management-subsystem/reference-voice-pack-installer.md */
        comment verificationRef /* Verification: docs/verification/speech/model-management-subsystem/reference-voice-pack-installer.md */
        comment reqRef /* Requirements: docs/reqstream/speech/model-management-subsystem/reference-voice-pack-installer.yaml */
    }
}
