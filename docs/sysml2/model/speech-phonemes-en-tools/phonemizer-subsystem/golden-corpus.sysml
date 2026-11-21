package SpeechPhonemesEnTools_GoldenCorpus {
    private import SoftwareItemMetadata::*;

    part def GoldenCorpus {
        doc /* Reader and writer of the golden corpora: reading cases, sentence cases and rendered cases. */

        metadata info : SoftwareInfo {
            sourceFiles = ("tools/DemaConsulting.Speech.Phonemes.En.Tools/PhonemizerSubsystem/GoldenCorpus.cs");
        }

        // Reads and writes golden cases using text-analysis annotations and EnglishPhonemizer outputs.
        dependency GoldenCorpus to SpeechPhonemesEn_EnglishPhonemizer::EnglishPhonemizer;
        dependency GoldenCorpus to SpeechTextEn_EnglishTextAnalyzer::EnglishTextAnalyzer;
        dependency GoldenCorpus to SpeechTextEn_TextEnAnnotations::TextEnAnnotations;
        dependency GoldenCorpus to Speech_TextAnalysis::TextAnalysis;
    }
}
