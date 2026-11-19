package SpeechTextEn {
    part def WebAddressRecognizer {
        doc /* Internal, stateless recognizer of web addresses inside one token: a URL with an
             * http, https or ftp scheme or a leading www, an e-mail address, or a bare domain whose
             * last label is a known top-level domain. It spells the scheme letters and www, drops
             * the slashes after the scheme and gives a spoken word to every separator (dot, slash,
             * at, dash, underscore, question mark, equals, and, hash, plus, percent, tilde), while
             * the words between the separators are left to the phonemizer. No span contains a
             * colon or whitespace, a file name or dotted name with an unknown ending is never read
             * as an address, and the work per token is bounded. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Text.En/NormalizationSubsystem/WebAddressRecognizer.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Text.En.Tests/NormalizationSubsystem/WebAddressRecognizerTests.cs */
        comment designRef /* Design: docs/design/speech-text-en/normalization-subsystem/web-address-recognizer.md */
        comment verificationRef /* Verification: docs/verification/speech-text-en/normalization-subsystem/web-address-recognizer.md */
        comment reqRef /* Requirements: docs/reqstream/speech-text-en/normalization-subsystem/web-address-recognizer.yaml */
    }
}
