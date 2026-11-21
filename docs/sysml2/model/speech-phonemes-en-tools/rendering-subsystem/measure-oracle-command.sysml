package SpeechPhonemesEnTools_MeasureOracleCommand {
    private import SoftwareItemMetadata::*;
    private import SpeechPhonemesEnTools_GoldenCorpus::*;
    private import SpeechPhonemesEnTools_ToolPaths::*;

    part def MeasureOracleCommand {
        doc /* The measure-oracle command: exports the renderings of the measurement corpora so a script kept outside
             * the repository can compare them with the reference engines on this machine. */

        metadata info : SoftwareInfo {
            sourceFiles = ("tools/DemaConsulting.Speech.Phonemes.En.Tools/RenderingSubsystem/MeasureOracleCommand.cs");
        }

        // Renders GoldenCorpus cases through the phonemizer and eSpeak/Misaki renderers for external
        // comparison.
        dependency MeasureOracleCommand to GoldenCorpus;
        dependency MeasureOracleCommand to ToolPaths;
        dependency MeasureOracleCommand to SpeechPhonemesEn_CmuLexicon::CmuLexicon;
        dependency MeasureOracleCommand to SpeechPhonemesEn_EnglishPhonemizer::EnglishPhonemizer;
        dependency MeasureOracleCommand to SpeechPhonemesEn_EspeakIpaRenderer::EspeakIpaRenderer;
        dependency MeasureOracleCommand to SpeechPhonemesEn_MisakiRenderer::MisakiRenderer;
        dependency MeasureOracleCommand to SpeechPhonemesEn_WeakForms::WeakForms;
    }
}
