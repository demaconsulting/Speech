package SpeechDemo {
    part def DeviceSelectionSubsystem {
        doc /* The capture/playback device pickers: a demo-owned enumeration seam over the
             * library's audio device factory and probes, and the presentation state that
             * lists devices, tracks the chosen device by name, and explains an empty
             * machine honestly. */

        comment designRef /* Design: docs/design/speech-demo/device-selection-subsystem.md */
        comment verificationRef /* Verification: docs/verification/speech-demo/device-selection-subsystem.md */
        comment reqRef /* Requirements: docs/reqstream/speech-demo/device-selection-subsystem.yaml */

        part iAudioDeviceService : IAudioDeviceService;
        part audioDeviceService : AudioDeviceService;
        part deviceSelectionViewModel : DeviceSelectionViewModel;
    }
}
