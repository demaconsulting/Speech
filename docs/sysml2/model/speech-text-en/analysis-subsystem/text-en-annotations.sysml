package SpeechTextEn_TextEnAnnotations {
    private import SoftwareItemMetadata::*;
    private import SpeechTextEn_NormalizationAlignment::*;
    private import SpeechTextEn_PronunciationOverrides::*;
    private import SpeechTextEn_ReadingInventory::*;

    part def TextEnAnnotations {
        doc /* The annotation vocabulary of the English text analysis (six kind names: en.upos, en.norm, en.spell,
             * en.pron, en.reading and en.reading-confidence) and the typed readers that give a backend the part of
             * speech, the normalized or spelled spoken text (as one replacement family), the pronunciation scheme and
             * phonemes, or the reading id and its confidence out of an annotation without hand-parsing kind and
             * value. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech.Text.En/AnalysisSubsystem/TextEnAnnotations.cs");
            testFiles = ("test/DemaConsulting.Speech.Text.En.Tests/AnalysisSubsystem/TextEnAnnotationsTests.cs");
            designFiles = ("docs/design/speech-text-en/analysis-subsystem/text-en-annotations.md");
            verificationFiles = ("docs/verification/speech-text-en/analysis-subsystem/text-en-annotations.md");
            requirementFiles = ("docs/reqstream/speech-text-en/analysis-subsystem/text-en-annotations.yaml");
        }

        // Defines annotation payloads over the text analysis model, using normalization, override and
        // reading types.
        dependency TextEnAnnotations to NormalizationAlignment;
        dependency TextEnAnnotations to PronunciationOverrides;
        dependency TextEnAnnotations to ReadingInventory;
        dependency TextEnAnnotations to Speech_TextAnalysis::TextAnalysis;
    }
}
