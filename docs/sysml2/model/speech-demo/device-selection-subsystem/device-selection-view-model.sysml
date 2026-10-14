package SpeechDemo {
    part def DeviceSelectionViewModel {
        doc /* Presentation state for both device pickers: the listed devices, the chosen
             * device in each direction expressed as the library's name-based selection, the
             * refresh command, and the honest empty-machine status text. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Demo/DeviceSelectionSubsystem/DeviceSelectionViewModel.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Demo.Tests/DeviceSelectionSubsystem/DeviceSelectionViewModelTests.cs */
        comment designRef /* Design: docs/design/speech-demo/device-selection-subsystem/device-selection-view-model.md */
        comment verificationRef /* Verification: docs/verification/speech-demo/device-selection-subsystem/device-selection-view-model.md */
        comment reqRef /* Requirements: docs/reqstream/speech-demo/device-selection-subsystem/device-selection-view-model.yaml */
    }
}
