package SpeechDemo {
    part def ModelSettingsSubsystem {
        doc /* The generic per-model tunable-parameter settings panel: presenters for the
             * library's numeric, choice, and boolean parameter kinds, chosen by concrete type
             * alone, plus the honest empty-state shown with no model selected or a model
             * declaring no parameters. */

        comment designRef /* Design: docs/design/speech-demo/model-settings-subsystem.md */
        comment verificationRef /* Verification: docs/verification/speech-demo/model-settings-subsystem.md */
        comment reqRef /* Requirements: docs/reqstream/speech-demo/model-settings-subsystem.yaml */

        part parameterViewModelBase : ParameterViewModelBase;
        part numericParameterViewModel : NumericParameterViewModel;
        part choiceParameterViewModel : ChoiceParameterViewModel;
        part booleanParameterViewModel : BooleanParameterViewModel;
        part modelSettingsViewModel : ModelSettingsViewModel;
    }

    part def ParameterViewModelBase {
        doc /* Common presentation state shared by every parameter presenter kind: the
             * parameter's id, display name, description, and the boxed current value used to
             * assemble the untyped key-value bag. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Demo/ModelSettingsSubsystem/ParameterViewModelBase.cs */
        comment designRef /* Design: docs/design/speech-demo/model-settings-subsystem.md (documented as a subsystem unit) */
        comment verificationRef /* Verification: docs/verification/speech-demo/model-settings-subsystem.md (documented as a subsystem unit) */
        comment reqRef /* Requirements: docs/reqstream/speech-demo/model-settings-subsystem.yaml (consolidated at subsystem level) */
    }

    part def NumericParameterViewModel {
        doc /* Presentation state for one numeric parameter, starting at its declared default
             * and clamping every written value to its declared range. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Demo/ModelSettingsSubsystem/NumericParameterViewModel.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Demo.Tests/ModelSettingsSubsystem/ModelSettingsViewModelTests.cs */
        comment designRef /* Design: docs/design/speech-demo/model-settings-subsystem.md (documented as a subsystem unit) */
        comment verificationRef /* Verification: docs/verification/speech-demo/model-settings-subsystem.md (documented as a subsystem unit) */
        comment reqRef /* Requirements: docs/reqstream/speech-demo/model-settings-subsystem.yaml (consolidated at subsystem level) */
    }

    part def ChoiceParameterViewModel {
        doc /* Presentation state for one choice parameter, listing its declared options and
             * preselecting the option matching its declared default. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Demo/ModelSettingsSubsystem/ChoiceParameterViewModel.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Demo.Tests/ModelSettingsSubsystem/ModelSettingsViewModelTests.cs */
        comment designRef /* Design: docs/design/speech-demo/model-settings-subsystem.md (documented as a subsystem unit) */
        comment verificationRef /* Verification: docs/verification/speech-demo/model-settings-subsystem.md (documented as a subsystem unit) */
        comment reqRef /* Requirements: docs/reqstream/speech-demo/model-settings-subsystem.yaml (consolidated at subsystem level) */
    }

    part def BooleanParameterViewModel {
        doc /* Presentation state for one boolean parameter, starting at its declared default. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Demo/ModelSettingsSubsystem/BooleanParameterViewModel.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Demo.Tests/ModelSettingsSubsystem/ModelSettingsViewModelTests.cs */
        comment designRef /* Design: docs/design/speech-demo/model-settings-subsystem.md (documented as a subsystem unit) */
        comment verificationRef /* Verification: docs/verification/speech-demo/model-settings-subsystem.md (documented as a subsystem unit) */
        comment reqRef /* Requirements: docs/reqstream/speech-demo/model-settings-subsystem.yaml (consolidated at subsystem level) */
    }

    part def ModelSettingsViewModel {
        doc /* Presentation state for the whole settings panel: rebuilds one presenter per
             * declared parameter whenever the selected model changes, reports the honest empty
             * state with no model or no parameters, and assembles the untyped key-value bag. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Demo/ModelSettingsSubsystem/ModelSettingsViewModel.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Demo.Tests/ModelSettingsSubsystem/ModelSettingsViewModelTests.cs */
        comment designRef /* Design: docs/design/speech-demo/model-settings-subsystem.md (documented as a subsystem unit) */
        comment verificationRef /* Verification: docs/verification/speech-demo/model-settings-subsystem.md (documented as a subsystem unit) */
        comment reqRef /* Requirements: docs/reqstream/speech-demo/model-settings-subsystem.yaml (consolidated at subsystem level) */
    }
}
