package SpeechDemo {
    part def ModelSettingsViewModel {
        doc /* Presentation state for the whole settings panel: rebuilds one presenter per
             * declared parameter whenever the selected model changes, reports the honest empty
             * state with no model or no parameters, assembles the untyped key-value bag, and
             * restores a captured bag onto a replacement model instance. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Demo/ModelSettingsSubsystem/ModelSettingsViewModel.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Demo.Tests/ModelSettingsSubsystem/ModelSettingsViewModelTests.cs */
        comment designRef /* Design: docs/design/speech-demo/model-settings-subsystem/model-settings-view-model.md */
        comment verificationRef /* Verification: docs/verification/speech-demo/model-settings-subsystem/model-settings-view-model.md */
        comment reqRef /* Requirements: docs/reqstream/speech-demo/model-settings-subsystem/model-settings-view-model.yaml */
    }
}
