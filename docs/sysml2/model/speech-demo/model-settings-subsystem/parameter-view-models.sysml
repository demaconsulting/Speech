package SpeechDemo {
    part def ParameterViewModelBase {
        doc /* Common presentation state shared by every parameter presenter kind: the
             * parameter's id, display name, description, and the boxed current value used to
             * assemble the untyped key-value bag. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Demo/ModelSettingsSubsystem/ParameterViewModelBase.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Demo.Tests/ModelSettingsSubsystem/ParameterViewModelsTests.cs */
        comment designRef /* Design: docs/design/speech-demo/model-settings-subsystem/parameter-view-models.md */
        comment verificationRef /* Verification: docs/verification/speech-demo/model-settings-subsystem/parameter-view-models.md */
        comment reqRef /* Requirements: docs/reqstream/speech-demo/model-settings-subsystem/parameter-view-models.yaml */
    }

    part def NumericParameterViewModel {
        doc /* Presentation state for one numeric parameter, starting at its declared default
             * and clamping every written value to its declared range. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Demo/ModelSettingsSubsystem/NumericParameterViewModel.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Demo.Tests/ModelSettingsSubsystem/ParameterViewModelsTests.cs */
        comment designRef /* Design: docs/design/speech-demo/model-settings-subsystem/parameter-view-models.md */
        comment verificationRef /* Verification: docs/verification/speech-demo/model-settings-subsystem/parameter-view-models.md */
        comment reqRef /* Requirements: docs/reqstream/speech-demo/model-settings-subsystem/parameter-view-models.yaml */
    }

    part def ChoiceParameterViewModel {
        doc /* Presentation state for one choice parameter, listing its declared options and
             * preselecting the option matching its declared default. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Demo/ModelSettingsSubsystem/ChoiceParameterViewModel.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Demo.Tests/ModelSettingsSubsystem/ParameterViewModelsTests.cs */
        comment designRef /* Design: docs/design/speech-demo/model-settings-subsystem/parameter-view-models.md */
        comment verificationRef /* Verification: docs/verification/speech-demo/model-settings-subsystem/parameter-view-models.md */
        comment reqRef /* Requirements: docs/reqstream/speech-demo/model-settings-subsystem/parameter-view-models.yaml */
    }

    part def BooleanParameterViewModel {
        doc /* Presentation state for one boolean parameter, starting at its declared default. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Demo/ModelSettingsSubsystem/BooleanParameterViewModel.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Demo.Tests/ModelSettingsSubsystem/ParameterViewModelsTests.cs */
        comment designRef /* Design: docs/design/speech-demo/model-settings-subsystem/parameter-view-models.md */
        comment verificationRef /* Verification: docs/verification/speech-demo/model-settings-subsystem/parameter-view-models.md */
        comment reqRef /* Requirements: docs/reqstream/speech-demo/model-settings-subsystem/parameter-view-models.yaml */
    }
}
