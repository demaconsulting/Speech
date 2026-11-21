package Speech_SpeechParameterConventions {
    private import SoftwareItemMetadata::*;

    part def SpeechParameterConventions {
        doc /* Supporting constants holding the conservative, built-in naming conventions that
             * DefaultModelCapabilityProfile and SynthesisSession use to recognize which of a model's own numeric
             * parameters conventionally controls speaking rate or output volume. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech/SynthesisSubsystem/SpeechParameterConventions.cs");
            testFiles = ("test/DemaConsulting.Speech.Tests/SynthesisSubsystem/SynthesisSessionTests.cs");
            designFiles = ("docs/design/speech/synthesis-subsystem.md");
            verificationFiles = ("docs/verification/speech/synthesis-subsystem.md");
            requirementFiles = ("docs/reqstream/speech/synthesis-subsystem.yaml");
        }
    }
}
