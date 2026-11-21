package SpeechProofreader_SentenceDiagnostics {
    private import SoftwareItemMetadata::*;
    private import SpeechProofreader_SentenceAudio::*;

    part def SentenceDiagnostics {
        doc /* Builds the per-sentence diagnostics: the text analysis (normalization, readings, sentence breaks) and
             * the phonemes and words the model consumed per segment. */

        metadata info : SoftwareInfo {
            sourceFiles = ("tools/DemaConsulting.Speech.Proofreader/Diagnostics/SentenceDiagnostics.cs");
        }

        // Uses the text analyzer, phonemizer, and renderers to explain what each model consumed per
        // sentence.
        dependency SentenceDiagnostics to SentenceAudio;
        dependency SentenceDiagnostics to SpeechOnnxKokoro_OnnxKokoroEnglishSynthesisModel::OnnxKokoroEnglishSynthesisModel;
        dependency SentenceDiagnostics to SpeechOnnxPiper_OnnxPiperLibriTtsEnSynthesisModel::OnnxPiperLibriTtsEnSynthesisModel;
        dependency SentenceDiagnostics to SpeechPhonemesEn_EnglishPhonemizer::EnglishPhonemizer;
        dependency SentenceDiagnostics to SpeechPhonemesEn_EspeakIpaRenderer::EspeakIpaRenderer;
        dependency SentenceDiagnostics to SpeechPhonemesEn_MisakiRenderer::MisakiRenderer;
        dependency SentenceDiagnostics to SpeechTextEn_EnglishTextAnalyzer::EnglishTextAnalyzer;
        dependency SentenceDiagnostics to SpeechTextEn_EnglishTextAnalyzerOptions::EnglishTextAnalyzerOptions;
        dependency SentenceDiagnostics to SpeechTextEn_TextEnAnnotations::TextEnAnnotations;
        dependency SentenceDiagnostics to Speech_SpeechModelContract::SpeechModelContract;
        dependency SentenceDiagnostics to Speech_TextAnalysis::TextAnalysis;
    }
}
