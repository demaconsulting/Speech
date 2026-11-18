package SpeechTextEn {
    part def NumericExpressionRecognizer {
        doc /* Internal, stateless, regular-expression-free recognizer of the numeric expressions of
             * en-US English text: numbers, ordinals, decimals, fractions, years and decades,
             * currency, percent, units, times, dates and digit groups, plus the number of a
             * hyphenated compound (12-year-old) and a number with a capital letter glued to it
             * (10K). It classifies whole tokens by
             * shape with bounded look-behind and look-ahead, decomposes a construct into one span per
             * token (or a currency symbol or apostrophe with its adjacent token) so that no span
             * contains whitespace, reports readings chosen by default as ambiguous and numbers left
             * as written because of a documented limit as numeric fallbacks, and builds its words
             * with the NumberVerbalizer. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Text.En/NormalizationSubsystem/NumericExpressionRecognizer.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Text.En.Tests/NormalizationSubsystem/NumericExpressionRecognizerTests.cs */
        comment designRef /* Design: docs/design/speech-text-en/normalization-subsystem/numeric-expression-recognizer.md */
        comment verificationRef /* Verification: docs/verification/speech-text-en/normalization-subsystem/numeric-expression-recognizer.md */
        comment reqRef /* Requirements: docs/reqstream/speech-text-en/normalization-subsystem/numeric-expression-recognizer.yaml */
    }
}
