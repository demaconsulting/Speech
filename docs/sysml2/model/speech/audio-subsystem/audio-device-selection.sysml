package Speech {
    part def AudioDeviceSelection {
        doc /* Immutable, persistable reference to a chosen audio device identified by name alone,
             * with never-throwing resolution against a probe's current enumeration. */

        comment sourceRef /* Source: src/DemaConsulting.Speech/AudioSubsystem/AudioDeviceSelection.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Tests/AudioSubsystem/AudioDeviceSelectionTests.cs */
        comment designRef /* Design: docs/design/speech/audio-subsystem/audio-device-selection.md */
        comment verificationRef /* Verification: docs/verification/speech/audio-subsystem/audio-device-selection.md */
        comment reqRef /* Requirements: docs/reqstream/speech/audio-subsystem/audio-device-selection.yaml */
    }
}
