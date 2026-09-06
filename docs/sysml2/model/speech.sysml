package Speech {
    doc
    /*
     * Speech is a .NET library template demonstrating DEMA Consulting
     * best practices for structuring, documenting, and testing a DotNet library.
     */

    part def SpeechSystem {
        doc /* The Speech system as a whole. */

        comment testRef /* Test: test/DemaConsulting.Speech.Tests/SpeechTests.cs */
        comment designRef /* Design: docs/design/speech.md */
        comment verificationRef /* Verification: docs/verification/speech.md */
        comment reqRef /* Requirements: docs/reqstream/speech.yaml */

        part diagnostics : Diagnostics;
        part audioSubsystem : AudioSubsystem;
        part modelManagementSubsystem : ModelManagementSubsystem;
        part recognitionSubsystem : RecognitionSubsystem;
        part synthesisSubsystem : SynthesisSubsystem;
    }
}
