package Speech_SynthesisDedicatedWorker {
    private import SoftwareItemMetadata::*;
    private import Speech_ISpeechDiagnostics::*;
    private import Speech_NullSpeechDiagnostics::*;
    private import Speech_SpeechDiagnosticLevel::*;

    part def SynthesisDedicatedWorker {
        doc /* Internal utility that runs a delegate on a dedicated, long-running background thread, applying a
             * cooperative-cancel-then-abandon policy for native calls that do not honor cancellation promptly: on
             * cancellation the worker gets a bounded grace period to stop cooperatively, after which the awaited task
             * completes as canceled, a Warning diagnostic is reported, and the worker thread is detached. This is the
             * synthesis subsystem's own copy, duplicated rather than shared with RecognitionSubsystem's
             * identically-shaped internal utility per this library's subsystem test-dependency boundary. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech/SynthesisSubsystem/DedicatedWorker.cs");
            testFiles = ("test/DemaConsulting.Speech.Tests/SynthesisSubsystem/DedicatedWorkerTests.cs");
            designFiles = ("docs/design/speech/synthesis-subsystem/dedicated-worker.md");
            verificationFiles = ("docs/verification/speech/synthesis-subsystem/dedicated-worker.md");
            requirementFiles = ("docs/reqstream/speech/synthesis-subsystem/dedicated-worker.yaml");
        }

        // Reports worker thread faults to diagnostics.
        dependency SynthesisDedicatedWorker to ISpeechDiagnostics;
        dependency SynthesisDedicatedWorker to NullSpeechDiagnostics;
        dependency SynthesisDedicatedWorker to SpeechDiagnosticLevel;
    }
}
