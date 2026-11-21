package SpeechPhonemesEnTools_RegenerateGoldensCommand {
    private import SoftwareItemMetadata::*;
    private import SpeechPhonemesEnTools_GoldenCorpus::*;
    private import SpeechPhonemesEnTools_SymbolInventory::*;
    private import SpeechPhonemesEnTools_ToolPaths::*;

    part def RegenerateGoldensCommand {
        doc /* The regenerate-goldens command: regenerates the generated columns of the phonemizer golden corpora and
             * reports how well the real text analysis labels the readings corpus. */

        metadata info : SoftwareInfo {
            sourceFiles = ("tools/DemaConsulting.Speech.Phonemes.En.Tools/PhonemizerSubsystem/RegenerateGoldensCommand.cs");
        }

        // Reruns the phonemizer, LTS, and eSpeak/Misaki renderers to rewrite GoldenCorpus columns.
        dependency RegenerateGoldensCommand to GoldenCorpus;
        dependency RegenerateGoldensCommand to SymbolInventory;
        dependency RegenerateGoldensCommand to ToolPaths;
        dependency RegenerateGoldensCommand to SpeechPhonemesEn_EnglishPhonemizer::EnglishPhonemizer;
        dependency RegenerateGoldensCommand to SpeechPhonemesEn_EspeakIpaRenderer::EspeakIpaRenderer;
        dependency RegenerateGoldensCommand to SpeechPhonemesEn_LetterToSound::LetterToSound;
        dependency RegenerateGoldensCommand to SpeechPhonemesEn_MisakiRenderer::MisakiRenderer;
    }
}
