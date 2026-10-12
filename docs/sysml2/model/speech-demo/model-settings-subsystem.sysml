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
}
