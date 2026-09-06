package Speech {
    part def NullSpeechDiagnostics {
        doc /* Default no-op ISpeechDiagnostics sink that discards every event without throwing,
             * used when a host supplies no diagnostics implementation. */

        comment sourceRef /* Source: src/DemaConsulting.Speech/Diagnostics/NullSpeechDiagnostics.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Tests/Diagnostics/NullSpeechDiagnosticsTests.cs */
        comment designRef /* Design: docs/design/speech/diagnostics.md */
        comment verificationRef /* Verification: docs/verification/speech/diagnostics.md */
        comment reqRef /* Requirements: docs/reqstream/speech/diagnostics/null-speech-diagnostics.yaml */
    }
}
