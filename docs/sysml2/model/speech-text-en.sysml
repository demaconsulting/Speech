package SpeechTextEn {
    private import SoftwareItemMetadata::*;
    private import OtsDependencies::*;
    private import SpeechTextEn_AnalysisSubsystem::*;
    private import SpeechTextEn_CorpusSubsystem::*;
    private import SpeechTextEn_ExternalEvaluationSubsystem::*;
    private import SpeechTextEn_HeteronymSubsystem::*;
    private import SpeechTextEn_NormalizationSubsystem::*;
    private import SpeechTextEn_TrainingDataSubsystem::*;
    private import SpeechTextEn_TrainingSubsystem::*;

    doc
    /*
     * SpeechTextEn is the DemaConsulting.Speech.Text.En library that supplies an English implementation of the Speech
     * library's ITextAnalyzer seam: part-of-speech annotation of the words of a speech request, normalization of
     * abbreviations, symbols, apostrophes and initialisms into aligned spoken forms, host-supplied pronunciation
     * overrides, letter sequences marked to be spelled, heteronym reading ids with a confidence (from a host reading
     * override, or from the trained model that the package embeds), and authoritative sentence boundaries, delivered
     * to an annotation-aware synthesis backend as language-neutral annotations. The Analysis subsystem depends on the
     * Normalization subsystem and the Heteronym subsystem, never the reverse. It is a preview. Speech must never
     * depend on it. It depends on the Speech library's public API and on the DemaConsulting.PartOfSpeech package (an
     * OTS item), and on no inference runtime. The TrainingDataSubsystem is the non-shipped check-data command of the
     * trainer tool that verifies the training and evaluation data in data/text-en/, and the TrainingSubsystem is the
     * non-shipped train and evaluate commands of the same tool that train and measure the heteronym model with the
     * library's own internal feature extractor, scoring and model format (through InternalsVisibleTo), and the
     * ExternalEvaluationSubsystem is the non-shipped evaluate-external command of the same tool that measures the
     * embedded model on pinned, evaluation-only external datasets fetched into a cache that is never committed, and
     * the CorpusSubsystem is the non-shipped corpus command of the same tool that fetches a pinned public-domain book
     * corpus from an official mirror and audits it against every evaluation set (no model uses the corpus yet); none
     * is part of the package and the runtime subsystems do not know them.
     */

    part def SpeechTextEnSystem {
        doc /* The SpeechTextEn library as a whole, with the non-shipped training data and training tooling. */

        metadata info : SoftwareInfo {
            testFiles = ("test/DemaConsulting.Speech.Text.En.Tests/SpeechTextEnTests.cs");
            designFiles = ("docs/design/speech-text-en.md");
            verificationFiles = ("docs/verification/speech-text-en.md");
            requirementFiles = ("docs/reqstream/speech-text-en.yaml");
        }

        part analysisSubsystem : AnalysisSubsystem;
        part normalizationSubsystem : NormalizationSubsystem;
        part heteronymSubsystem : HeteronymSubsystem;
        part trainingDataSubsystem : TrainingDataSubsystem;
        part trainingSubsystem : TrainingSubsystem;
        part externalEvaluationSubsystem : ExternalEvaluationSubsystem;
        part partOfSpeech : PartOfSpeech;
        part corpusSubsystem : CorpusSubsystem;
    }
}
