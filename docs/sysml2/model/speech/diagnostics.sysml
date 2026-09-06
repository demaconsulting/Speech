package Speech {
    part def Diagnostics {
        doc /* Reports structural diagnostic events (e.g. device selected, fallback used) to an
             * optional host-supplied sink, carrying structural facts only, never raw audio or text. */

        comment designRef /* Design: docs/design/speech/diagnostics.md */
        comment verificationRef /* Verification: docs/verification/speech/diagnostics.md */
        comment reqRef /* Requirements: docs/reqstream/speech/diagnostics.yaml */

        part iSpeechDiagnostics : ISpeechDiagnostics;
        part nullSpeechDiagnostics : NullSpeechDiagnostics;
    }
}
