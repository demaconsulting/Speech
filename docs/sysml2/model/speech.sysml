package Speech {
    doc
    /*
     * Speech is a cross-platform .NET library providing local, offline speech-to-text (STT)
     * and text-to-speech (TTS) services for desktop applications, with mockable audio
     * interfaces, on-demand model download and verification, and Natural Language Audio Tags.
     */

    part def SpeechSystem {
        doc /* The core Speech library: engine-neutral audio, model management, recognition and synthesis. */

        comment testRef /* Test: test/DemaConsulting.Speech.Tests/SpeechTests.cs */
        comment designRef /* Design: docs/design/speech.md */
        comment verificationRef /* Verification: docs/verification/speech.md */
        comment reqRef /* Requirements: docs/reqstream/speech.yaml */

        part diagnostics : Diagnostics;
        part audioSubsystem : AudioSubsystem;
        part modelManagementSubsystem : ModelManagementSubsystem;
        part recognitionSubsystem : RecognitionSubsystem;
        part synthesisSubsystem : SynthesisSubsystem;
        part systemNumericsTensors : SystemNumericsTensors;
    }
}
