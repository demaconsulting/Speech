package Speech_SynthesisEngineBusyException {
    private import SoftwareItemMetadata::*;

    part def SynthesisEngineBusyException {
        doc /* Exception thrown when ISpeechSynthesizerEngine.CreateSessionAsync is called while this engine's
             * exclusivity lease is already held by another session. Fails fast rather than queueing or awaiting
             * release, since waiting would make this call's latency depend on an unrelated session's teardown with no
             * caller-visible way to bound that wait. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech/SynthesisSubsystem/SynthesisEngineBusyException.cs");
            testFiles = ("test/DemaConsulting.Speech.Tests/SynthesisSubsystem/SpeechSynthesizerEngineTests.cs", "test/DemaConsulting.Speech.Tests/SynthesisSubsystem/SynthesisEngineBusyExceptionTests.cs");
            designFiles = ("docs/design/speech/synthesis-subsystem/synthesis-engine-busy-exception.md");
            verificationFiles = ("docs/verification/speech/synthesis-subsystem/synthesis-engine-busy-exception.md");
            requirementFiles = ("docs/reqstream/speech/synthesis-subsystem/synthesis-engine-busy-exception.yaml");
        }
    }
}
