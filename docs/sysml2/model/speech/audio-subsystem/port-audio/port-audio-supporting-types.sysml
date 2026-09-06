package Speech {
    part def PortAudioDeviceInfo {
        doc /* Managed metadata record describing one PortAudio device without exposing native
             * binding structs to higher-level logic. */

        comment sourceRef /* Source: src/DemaConsulting.Speech/AudioSubsystem/PortAudio/PortAudioDeviceInfo.cs */
        comment designRef /* Design: docs/design/speech/audio-subsystem/port-audio/port-audio-supporting-types.md */
        comment verificationRef /* Verification: docs/verification/speech/audio-subsystem/port-audio/port-audio-supporting-types.md */
        comment reqRef /* Requirements: docs/reqstream/speech/audio-subsystem/port-audio/port-audio-supporting-types.yaml */
    }

    part def PortAudioHostApiInfo {
        doc /* Managed metadata record describing one PortAudio host API, including its stable type
             * identifier and default input/output device indices. */

        comment sourceRef /* Source: src/DemaConsulting.Speech/AudioSubsystem/PortAudio/PortAudioHostApiInfo.cs */
        comment designRef /* Design: docs/design/speech/audio-subsystem/port-audio/port-audio-supporting-types.md */
        comment verificationRef /* Verification: docs/verification/speech/audio-subsystem/port-audio/port-audio-supporting-types.md */
        comment reqRef /* Requirements: docs/reqstream/speech/audio-subsystem/port-audio/port-audio-supporting-types.yaml */
    }

    part def PortAudioHostApiType {
        doc /* Internal enum carrying the stable PortAudio host-API constants used by platform
             * policy before runtime host-API indices are resolved. */

        comment sourceRef /* Source: src/DemaConsulting.Speech/AudioSubsystem/PortAudio/PortAudioHostApiType.cs */
        comment designRef /* Design: docs/design/speech/audio-subsystem/port-audio/port-audio-supporting-types.md */
        comment verificationRef /* Verification: docs/verification/speech/audio-subsystem/port-audio/port-audio-supporting-types.md */
        comment reqRef /* Requirements: docs/reqstream/speech/audio-subsystem/port-audio/port-audio-supporting-types.yaml */
    }

    part def PortAudioNativeMethods {
        doc /* Repository-local P/Invoke declarations for PortAudio host-API functions absent from
             * PortAudioSharp2. */

        comment sourceRef /* Source: src/DemaConsulting.Speech/AudioSubsystem/PortAudio/PortAudioNativeMethods.cs */
        comment designRef /* Design: docs/design/speech/audio-subsystem/port-audio/port-audio-supporting-types.md */
        comment verificationRef /* Verification: docs/verification/speech/audio-subsystem/port-audio/port-audio-supporting-types.md */
        comment reqRef /* Requirements: docs/reqstream/speech/audio-subsystem/port-audio/port-audio-supporting-types.yaml */
    }
}
