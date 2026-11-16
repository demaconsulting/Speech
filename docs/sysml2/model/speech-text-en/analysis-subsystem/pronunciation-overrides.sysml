package SpeechTextEn {
    part def PronunciationOverrides {
        doc /* The immutable, thread-safe dictionary of host-supplied pronunciation overrides
             * (with the PronunciationOverrideEntry record, the OverrideAction and
             * PronunciationScheme enumerations and the internal OverrideSyntax grammars). It is built
             * from entries or from a tab-separated file with validation and size limits that report a
             * position and a fixed reason, never content, and it resolves the tokens of a text to token
             * directives (speak as written, replace with plain words, spell by letter name, attach a
             * pronunciation) by whole-word, case-sensitive-then-case-insensitive matching with no
             * culture dependency. It rewrites nothing and decides nothing about spans: the
             * NormalizationSubsystem applies the directives before its built-in rules. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Text.En/AnalysisSubsystem/PronunciationOverrides.cs */
        comment sourceRef /* Source: src/DemaConsulting.Speech.Text.En/AnalysisSubsystem/PronunciationOverrideEntry.cs */
        comment sourceRef /* Source: src/DemaConsulting.Speech.Text.En/AnalysisSubsystem/OverrideSyntax.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Text.En.Tests/AnalysisSubsystem/PronunciationOverridesTests.cs */
        comment designRef /* Design: docs/design/speech-text-en/analysis-subsystem/pronunciation-overrides.md */
        comment verificationRef /* Verification: docs/verification/speech-text-en/analysis-subsystem/pronunciation-overrides.md */
        comment reqRef /* Requirements: docs/reqstream/speech-text-en/analysis-subsystem/pronunciation-overrides.yaml */
    }
}
