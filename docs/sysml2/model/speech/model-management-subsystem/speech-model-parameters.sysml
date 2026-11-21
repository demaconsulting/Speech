package Speech_SpeechModelParameters {
    private import SoftwareItemMetadata::*;
    private import Speech_ISpeechDiagnostics::*;
    private import Speech_SpeechDiagnosticLevel::*;

    part def SpeechModelParameters {
        doc /* Typed, self-describing tunable-parameter descriptor hierarchy: the common ISpeechModelParameter
             * contract plus NumericParameter, ChoiceParameter (and its ChoiceParameterOption), and BooleanParameter,
             * each validating an internally consistent range/option-set/default eagerly at construction, plus the
             * shared SpeechModelParameterDiagnostics helper used by SpeechRecognizerFactory and
             * SpeechSynthesizerFactory to validate caller-supplied parameter values at composition time. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech/ModelManagementSubsystem/ISpeechModelParameter.cs", "src/DemaConsulting.Speech/ModelManagementSubsystem/NumericParameter.cs", "src/DemaConsulting.Speech/ModelManagementSubsystem/NumericParameterBounds.cs", "src/DemaConsulting.Speech/ModelManagementSubsystem/ChoiceParameter.cs", "src/DemaConsulting.Speech/ModelManagementSubsystem/BooleanParameter.cs", "src/DemaConsulting.Speech/ModelManagementSubsystem/SpeechModelParameterDiagnostics.cs");
            testFiles = ("test/DemaConsulting.Speech.Tests/ModelManagementSubsystem/NumericParameterTests.cs", "test/DemaConsulting.Speech.Tests/ModelManagementSubsystem/ChoiceParameterTests.cs", "test/DemaConsulting.Speech.Tests/ModelManagementSubsystem/BooleanParameterTests.cs", "test/DemaConsulting.Speech.Tests/ModelManagementSubsystem/SpeechModelParameterDiagnosticsTests.cs");
            designFiles = ("docs/design/speech/model-management-subsystem/speech-model-parameters.md");
            verificationFiles = ("docs/verification/speech/model-management-subsystem/speech-model-parameters.md");
            requirementFiles = ("docs/reqstream/speech/model-management-subsystem/speech-model-parameters.yaml");
        }

        // Enforces numeric parameter bounds and reports invalid values to diagnostics.
        dependency SpeechModelParameters to ISpeechDiagnostics;
        dependency SpeechModelParameters to SpeechDiagnosticLevel;
    }
}
