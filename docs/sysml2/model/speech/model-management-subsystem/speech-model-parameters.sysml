package Speech {
    part def SpeechModelParameters {
        doc /* Typed, self-describing tunable-parameter descriptor hierarchy: the common
             * ISpeechModelParameter contract plus NumericParameter, ChoiceParameter (and its
             * ChoiceParameterOption), and BooleanParameter, each validating an internally
             * consistent range/option-set/default eagerly at construction, plus the shared
             * SpeechModelParameterDiagnostics helper used by SpeechRecognizerFactory and
             * SpeechSynthesizerFactory to validate caller-supplied parameter values at
             * composition time. */

        comment sourceRef /* Source: src/DemaConsulting.Speech/ModelManagementSubsystem/ISpeechModelParameter.cs, NumericParameter.cs, ChoiceParameter.cs, BooleanParameter.cs, SpeechModelParameterDiagnostics.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Tests/ModelManagementSubsystem/NumericParameterTests.cs, ChoiceParameterTests.cs, BooleanParameterTests.cs, SpeechModelParameterDiagnosticsTests.cs */
        comment designRef /* Design: docs/design/speech/model-management-subsystem/speech-model-parameters.md */
        comment verificationRef /* Verification: docs/verification/speech/model-management-subsystem/speech-model-parameters.md */
        comment reqRef /* Requirements: docs/reqstream/speech/model-management-subsystem/speech-model-parameters.yaml */
    }
}

