package SpeechTextEn {
    part def AlphanumericTokenRecognizer {
        doc /* Internal, stateless recognizer of tokens that mix letters and digits in the way of
             * identifiers and names: alternating runs of capital letters (spelled) and digits
             * (read as a cardinal up to three digits without a leading zero, digit by digit
             * otherwise) such as A4, H2O and MP3, and a name joined to one to three digits by a
             * hyphen (COVID-19, Catch-22), of which only the number is read. It leaves a word of
             * four or more capital letters before digits and every other shape alone, and the work
             * per token is bounded. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Text.En/NormalizationSubsystem/AlphanumericTokenRecognizer.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Text.En.Tests/NormalizationSubsystem/AlphanumericTokenRecognizerTests.cs */
        comment designRef /* Design: docs/design/speech-text-en/normalization-subsystem/alphanumeric-token-recognizer.md */
        comment verificationRef /* Verification: docs/verification/speech-text-en/normalization-subsystem/alphanumeric-token-recognizer.md */
        comment reqRef /* Requirements: docs/reqstream/speech-text-en/normalization-subsystem/alphanumeric-token-recognizer.yaml */
    }
}
