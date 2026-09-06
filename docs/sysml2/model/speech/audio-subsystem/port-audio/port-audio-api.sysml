package Speech {
    part def PortAudioApi {
        doc /* Concrete production adapter that combines PortAudioSharp2-managed calls with
             * repository-local native host-API bindings behind the IPortAudioApi seam. */

        comment sourceRef /* Source: src/DemaConsulting.Speech/AudioSubsystem/PortAudio/PortAudioApi.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Tests/AudioSubsystem/PortAudio/PortAudioEnvironmentTests.cs (singleton exposure exercised) */
        comment designRef /* Design: docs/design/speech/audio-subsystem/port-audio/port-audio-api.md */
        comment verificationRef /* Verification: docs/verification/speech/audio-subsystem/port-audio/port-audio-api.md */
        comment reqRef /* Requirements: docs/reqstream/speech/audio-subsystem/port-audio/port-audio-api.yaml */
    }
}
