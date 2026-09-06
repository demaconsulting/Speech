package Speech {
    part def SpeechModelDownloadProgress {
        doc /* Reports progress of a single file transfer within a model download - file index,
             * file count, bytes transferred, and optional total bytes - suitable for
             * IProgress<T> consumption by a GUI progress bar. */

        comment sourceRef /* Source: src/DemaConsulting.Speech/ModelManagementSubsystem/SpeechModelDownloadProgress.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Tests/ModelManagementSubsystem/SpeechModelDownloadProgressTests.cs */
        comment designRef /* Design: docs/design/speech/model-management-subsystem/speech-model-download-progress.md */
        comment verificationRef /* Verification: docs/verification/speech/model-management-subsystem/speech-model-download-progress.md */
        comment reqRef /* Requirements: docs/reqstream/speech/model-management-subsystem/speech-model-download-progress.yaml */
    }
}
