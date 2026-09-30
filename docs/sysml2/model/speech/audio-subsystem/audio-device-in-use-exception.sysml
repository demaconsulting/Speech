package Speech {
    part def AudioDeviceInUseException {
        doc /* Exception thrown when a PortAudio device-table refresh is refused because one or
             * more capture/playback devices created from the environment currently have an
             * open/started stream. The one deliberate exception to this library's "never throws
             * at composition" policy - caller-driven, recoverable misuse. */

        comment sourceRef /* Source: src/DemaConsulting.Speech/AudioSubsystem/AudioDeviceInUseException.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Tests/AudioSubsystem/AudioDeviceInUseExceptionTests.cs */
        comment designRef /* Design: docs/design/speech/audio-subsystem.md (documented as a supporting type within the subsystem doc) */
        comment verificationRef /* Verification: docs/verification/speech/audio-subsystem.md (documented as a supporting type within the subsystem doc) */
        comment reqRef /* Requirements: docs/reqstream/speech/audio-subsystem/audio-device-refresh.yaml */
    }
}
