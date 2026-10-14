package SpeechDemo {
    part def IAudioDeviceService {
        doc /* Demo-owned seam for enumerating capture and playback devices, so the pickers
             * can be tested without audio hardware. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Demo/DeviceSelectionSubsystem/IAudioDeviceService.cs */
        comment designRef /* Design: docs/design/speech-demo/device-selection-subsystem/audio-device-service.md (documented together with its sole implementation, AudioDeviceService) */
        comment verificationRef /* Verification: docs/verification/speech-demo/device-selection-subsystem/audio-device-service.md (documented together with its sole implementation, AudioDeviceService) */
        comment reqRef /* Requirements: docs/reqstream/speech-demo/device-selection-subsystem/audio-device-service.yaml */
    }
}
