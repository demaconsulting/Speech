package Speech_RecognitionDedicatedWorker {
    private import SoftwareItemMetadata::*;
    private import Speech_ISpeechDiagnostics::*;
    private import Speech_NullSpeechDiagnostics::*;
    private import Speech_SpeechDiagnosticLevel::*;

    part def RecognitionDedicatedWorker {
        doc /* Internal long-running-task helper that runs one blocking delegate on its own dedicated thread
             * (TaskCreationOptions.LongRunning) and applies a cooperative-cancel-then-abandon policy: requests
             * cancellation first, then abandons the delegate after a bounded timeout (default 2 seconds) if it has
             * not observed cancellation, reporting the abandonment through ISpeechDiagnostics at Warning level rather
             * than blocking the caller forever on a stuck native call. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech/RecognitionSubsystem/DedicatedWorker.cs");
            testFiles = ("test/DemaConsulting.Speech.Tests/RecognitionSubsystem/DedicatedWorkerTests.cs");
            designFiles = ("docs/design/speech/recognition-subsystem/dedicated-worker.md");
            verificationFiles = ("docs/verification/speech/recognition-subsystem/dedicated-worker.md");
            requirementFiles = ("docs/reqstream/speech/recognition-subsystem/dedicated-worker.yaml");
        }

        // Reports worker thread faults to diagnostics.
        dependency RecognitionDedicatedWorker to ISpeechDiagnostics;
        dependency RecognitionDedicatedWorker to NullSpeechDiagnostics;
        dependency RecognitionDedicatedWorker to SpeechDiagnosticLevel;
    }
}
