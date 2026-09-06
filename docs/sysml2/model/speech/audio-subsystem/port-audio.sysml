package Speech {
    part def PortAudio {
        doc /* Internal child subsystem that isolates PortAudioSharp2 and supplementary native
             * host-API interop behind mockable managed abstractions for deterministic testing. */

        comment designRef /* Design: docs/design/speech/audio-subsystem/port-audio.md */
        comment verificationRef /* Verification: docs/verification/speech/audio-subsystem/port-audio.md */
        comment reqRef /* Requirements: docs/reqstream/speech/audio-subsystem/port-audio.yaml */

        part iPortAudioApi : IPortAudioApi;
        part iPortAudioStream : IPortAudioStream;
        part portAudioEnvironment : PortAudioEnvironment;
        part portAudioApi : PortAudioApi;
        part portAudioDeviceInfo : PortAudioDeviceInfo;
        part portAudioHostApiInfo : PortAudioHostApiInfo;
        part portAudioHostApiType : PortAudioHostApiType;
        part portAudioNativeMethods : PortAudioNativeMethods;
        part portAudioSharp2 : PortAudioSharp2;
    }
}
