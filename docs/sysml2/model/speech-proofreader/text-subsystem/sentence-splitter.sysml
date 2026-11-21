package SpeechProofreader_SentenceSplitter {
    private import SoftwareItemMetadata::*;
    private import SpeechProofreader_ProofreadSentence::*;

    part def SentenceSplitter {
        doc /* Splits long-form text into the sentences the Proofreader plays, using the English sentence analysis the
             * synthesis models use so the boundaries heard match what the library speaks. */

        metadata info : SoftwareInfo {
            sourceFiles = ("tools/DemaConsulting.Speech.Proofreader/Text/SentenceSplitter.cs");
            testFiles = ("test/DemaConsulting.Speech.Proofreader.Tests/SentenceSplitterTests.cs");
        }

        // Uses the English text analyzer to cut text into ProofreadSentence items with library-matching
        // breaks.
        dependency SentenceSplitter to ProofreadSentence;
        dependency SentenceSplitter to SpeechTextEn_EnglishTextAnalyzer::EnglishTextAnalyzer;
        dependency SentenceSplitter to SpeechTextEn_EnglishTextAnalyzerOptions::EnglishTextAnalyzerOptions;
    }
}
