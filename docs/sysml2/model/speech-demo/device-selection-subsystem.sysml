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

    part def IAudioDeviceService {
        doc /* Demo-owned seam for enumerating capture and playback devices, so the pickers
             * can be tested without audio hardware. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Demo/DeviceSelectionSubsystem/IAudioDeviceService.cs */
        comment designRef /* Design: docs/design/speech-demo/device-selection-subsystem.md (documented as a subsystem unit) */
        comment verificationRef /* Verification: docs/verification/speech-demo/device-selection-subsystem.md (documented as a subsystem unit) */
        comment reqRef /* Requirements: docs/reqstream/speech-demo/device-selection-subsystem.yaml (consolidated at subsystem level) */
    }

    part def AudioDeviceService {
        doc /* Real enumeration seam implementation, delegating to the library's
             * AudioDeviceFactory capture and playback probes. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Demo/DeviceSelectionSubsystem/AudioDeviceService.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Demo.Tests/DeviceSelectionSubsystem/AudioDeviceServiceTests.cs */
        comment designRef /* Design: docs/design/speech-demo/device-selection-subsystem.md (documented as a subsystem unit) */
        comment verificationRef /* Verification: docs/verification/speech-demo/device-selection-subsystem.md (documented as a subsystem unit) */
        comment reqRef /* Requirements: docs/reqstream/speech-demo/device-selection-subsystem.yaml (consolidated at subsystem level) */
    }

    part def DeviceSelectionViewModel {
        doc /* Presentation state for both device pickers: the listed devices, the chosen
             * device in each direction expressed as the library's name-based selection, the
             * refresh command, and the honest empty-machine status text. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Demo/DeviceSelectionSubsystem/DeviceSelectionViewModel.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Demo.Tests/DeviceSelectionSubsystem/DeviceSelectionViewModelTests.cs */
        comment designRef /* Design: docs/design/speech-demo/device-selection-subsystem.md (documented as a subsystem unit) */
        comment verificationRef /* Verification: docs/verification/speech-demo/device-selection-subsystem.md (documented as a subsystem unit) */
        comment reqRef /* Requirements: docs/reqstream/speech-demo/device-selection-subsystem.yaml (consolidated at subsystem level) */
    }
}
