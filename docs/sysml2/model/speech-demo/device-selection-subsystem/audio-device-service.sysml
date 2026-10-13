package SpeechDemo {
    part def AudioDeviceService {
        doc /* Real enumeration seam implementation, delegating to the library's
             * AudioDeviceFactory capture and playback probes. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Demo/DeviceSelectionSubsystem/AudioDeviceService.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Demo.Tests/DeviceSelectionSubsystem/AudioDeviceServiceTests.cs */
        comment designRef /* Design: docs/design/speech-demo/device-selection-subsystem/audio-device-service.md */
        comment verificationRef /* Verification: docs/verification/speech-demo/device-selection-subsystem/audio-device-service.md */
        comment reqRef /* Requirements: docs/reqstream/speech-demo/device-selection-subsystem/audio-device-service.yaml */
    }
}
