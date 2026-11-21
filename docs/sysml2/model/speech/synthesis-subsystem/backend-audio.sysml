package Speech_BackendAudio {
    private import SoftwareItemMetadata::*;
    private import ScalarValues::*;

    part def BackendAudio {
        doc /* Supporting plain-data record carrying the mono samples and sample rate returned by one
             * ISynthesisBackend.Generate call from an engine to the shared SynthesisSession. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech/SynthesisSubsystem/BackendAudio.cs");
            testFiles = ("test/DemaConsulting.Speech.Tests/SynthesisSubsystem/SynthesisSessionTests.cs");
            designFiles = ("docs/design/speech/synthesis-subsystem/synthesis-backend.md");
            verificationFiles = ("docs/verification/speech/synthesis-subsystem/synthesis-backend.md");
            requirementFiles = ("docs/reqstream/speech/synthesis-subsystem/synthesis-backend.yaml");
        }

        attribute samples : Real [0..*];
        attribute sampleRate : Integer;
    }
}
