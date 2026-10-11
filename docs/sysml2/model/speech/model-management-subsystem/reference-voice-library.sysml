package Speech {
    part def ReferenceVoiceLibrary {
        doc /* Model-agnostic reference-voice library: installs and removes downloadable voice
             * packs through the hash-verified model downloader, keeps a user voice folder, and
             * lists all available voices merged and de-duplicated (first id wins). */

        comment sourceRef /* Source: src/DemaConsulting.Speech/ModelManagementSubsystem/ReferenceVoiceLibrary.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Tests/ModelManagementSubsystem/ReferenceVoiceLibraryTests.cs */
        comment designRef /* Design: docs/design/speech/model-management-subsystem/reference-voice-library.md */
        comment verificationRef /* Verification: docs/verification/speech/model-management-subsystem/reference-voice-library.md */
        comment reqRef /* Requirements: docs/reqstream/speech/model-management-subsystem/reference-voice-library.yaml */
    }
}
