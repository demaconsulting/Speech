package Speech {
    part def AudioDeviceRefreshSeam {
        doc /* Cross-cutting seam capability jointly implemented by PortAudioEnvironment.Refresh()
             * and AudioDeviceFactory.RefreshDevices(): an on-demand PortAudio device-table
             * refresh that is refused atomically, without terminating the native runtime, while
             * any created device has an active stream, that never replaces explicitly injected
             * probes, and that invalidates every device instance created before it. It has no
             * type of its own: its behavior is designed and verified in the two owning units'
             * documents, and its four requirements live in a single seam-level requirements file
             * because splitting them across the two units would duplicate one justification and
             * test list. */

        comment sourceRef /* Source: src/DemaConsulting.Speech/AudioSubsystem/AudioDeviceFactory.cs, src/DemaConsulting.Speech/AudioSubsystem/PortAudio/PortAudioEnvironment.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Tests/AudioSubsystem/AudioDeviceFactoryTests.cs, test/DemaConsulting.Speech.Tests/AudioSubsystem/PortAudio/PortAudioEnvironmentTests.cs */
        comment designRef /* Design: docs/design/speech/audio-subsystem/audio-device-factory.md, docs/design/speech/audio-subsystem/port-audio/port-audio-environment.md (documented with the two owning units) */
        comment verificationRef /* Verification: docs/verification/speech/audio-subsystem/audio-device-factory.md, docs/verification/speech/audio-subsystem/port-audio/port-audio-environment.md (verified with the two owning units) */
        comment reqRef /* Requirements: docs/reqstream/speech/audio-subsystem/audio-device-refresh.yaml */
    }
}
