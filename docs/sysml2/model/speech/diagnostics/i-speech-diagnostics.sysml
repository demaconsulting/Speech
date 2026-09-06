package Speech {
    part def ISpeechDiagnostics {
        doc /* Contract for reporting structural diagnostic events to an optional host-supplied
             * sink, independent of which library component raises the event. */

        comment sourceRef /* Source: src/DemaConsulting.Speech/Diagnostics/ISpeechDiagnostics.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Tests/SpeechTests.cs (exercised via system integration tests) */
        comment designRef /* Design: docs/design/speech/diagnostics.md */
        comment verificationRef /* Verification: docs/verification/speech/diagnostics.md */
        comment reqRef /* Requirements: docs/reqstream/speech/diagnostics/i-speech-diagnostics.yaml */
    }
}
