package Speech {
    part def WavFileAudioPlaybackDevice {
        doc /* File-backed IAudioPlaybackDevice implementation that writes queued samples to a
             * WAV file as 16-bit PCM instead of rendering them through real playback hardware,
             * always available and clamping samples before scaling to match the byte layout
             * promoted from the demo's temporary WavFileWriter. */

        comment sourceRef /* Source: src/DemaConsulting.Speech/AudioSubsystem/WavFileAudioPlaybackDevice.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Tests/AudioSubsystem/WavFileAudioPlaybackDeviceTests.cs */
        comment designRef /* Design: docs/design/speech/audio-subsystem/wav-file-audio-playback-device.md */
        comment verificationRef /* Verification: docs/verification/speech/audio-subsystem/wav-file-audio-playback-device.md */
        comment reqRef /* Requirements: docs/reqstream/speech/audio-subsystem/wav-file-audio-playback-device.yaml */
    }
}
