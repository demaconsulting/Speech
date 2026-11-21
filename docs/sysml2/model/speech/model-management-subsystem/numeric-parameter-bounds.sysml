package Speech_NumericParameterBounds {
    private import SoftwareItemMetadata::*;
    private import ScalarValues::*;

    part def NumericParameterBounds {
        doc /* Supporting value bundling the minimum, maximum, step and default of a NumericParameter so its
             * constructor stays small; NumericParameter still validates and exposes the four values. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech/ModelManagementSubsystem/NumericParameterBounds.cs");
            testFiles = ("test/DemaConsulting.Speech.Tests/ModelManagementSubsystem/NumericParameterTests.cs");
            designFiles = ("docs/design/speech/model-management-subsystem/speech-model-parameters.md");
            verificationFiles = ("docs/verification/speech/model-management-subsystem/speech-model-parameters.md");
            requirementFiles = ("docs/reqstream/speech/model-management-subsystem/speech-model-parameters.yaml");
        }

        attribute minimum : Real;
        attribute maximum : Real;
        attribute step : Real;
        attribute 'default' : Real;
    }
}
