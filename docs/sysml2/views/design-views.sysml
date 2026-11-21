// Views rendered into docs/design/generated/ and embedded in the Design document.
// The repository contains several systems (one per package or tool), so the overview is a general view that
// exposes each system and subsystem part def by name (not every unit, which would be unreadable) plus the OTS
// items the systems use, with documentation filtered out. Every unit appears in its system or subsystem view
// below, and its dependencies are consumed with 'sysml2tools query dependencies' rather than a diagram.
// Per SysML2Tools syntax, 'expose' is only valid inside a named 'view' usage (not a 'view def' definition),
// and it - not the view definition - is what scopes the rendered diagram content.
// View names are a flat namespace (render writes {ViewName}.svg), so the subsystem views of the sibling
// systems carry a short system prefix (Sherpa, Kokoro, Piper, ChatterboxTurbo, Nemotron, TextEn, PhonemesEn,
// PhonemesEnTools, Proofreader) to keep same-named subsystems distinct; the core Speech system and the
// SpeechDemo, SpeechCli and SpeechOnnx systems, whose subsystem names are unique, are unprefixed.
package SpeechDesignViews {
    private import StandardViewDefinitions::*;

    view SoftwareStructureView : GeneralView {
        expose Speech::SpeechSystem;
        expose Speech_Diagnostics::Diagnostics;
        expose Speech_AudioSubsystem::AudioSubsystem;
        expose Speech_PortAudio::PortAudio;
        expose Speech_ModelManagementSubsystem::ModelManagementSubsystem;
        expose Speech_RecognitionSubsystem::RecognitionSubsystem;
        expose Speech_SynthesisSubsystem::SynthesisSubsystem;
        expose SpeechDemo::SpeechDemoSystem;
        expose SpeechDemo_ShellSubsystem::ShellSubsystem;
        expose SpeechDemo_DeviceSelectionSubsystem::DeviceSelectionSubsystem;
        expose SpeechDemo_ModelCatalogSubsystem::ModelCatalogSubsystem;
        expose SpeechDemo_ModelSettingsSubsystem::ModelSettingsSubsystem;
        expose SpeechDemo_SynthesisPanelSubsystem::SynthesisPanelSubsystem;
        expose SpeechDemo_RecognitionPanelSubsystem::RecognitionPanelSubsystem;
        expose SpeechCli::SpeechCliSystem;
        expose SpeechCli_ModelCommandsSubsystem::ModelCommandsSubsystem;
        expose SpeechCli_DeviceCommandsSubsystem::DeviceCommandsSubsystem;
        expose SpeechCli_SynthesisCommandSubsystem::SynthesisCommandSubsystem;
        expose SpeechCli_RecognitionCommandSubsystem::RecognitionCommandSubsystem;
        expose SpeechCli_ConversationCommandSubsystem::ConversationCommandSubsystem;
        expose SpeechSherpa::SpeechSherpaSystem;
        expose SpeechSherpa_ModelManagementSubsystem::ModelManagementSubsystem;
        expose SpeechSherpa_RecognitionSubsystem::RecognitionSubsystem;
        expose SpeechSherpa_SynthesisSubsystem::SynthesisSubsystem;
        expose SpeechOnnx::SpeechOnnxSystem;
        expose SpeechOnnx_OnnxRuntimeSubsystem::OnnxRuntimeSubsystem;
        expose SpeechOnnxKokoro::SpeechOnnxKokoroSystem;
        expose SpeechOnnxKokoro_ModelManagementSubsystem::ModelManagementSubsystem;
        expose SpeechOnnxKokoro_SynthesisSubsystem::SynthesisSubsystem;
        expose SpeechOnnxPiper::SpeechOnnxPiperSystem;
        expose SpeechOnnxPiper_ModelManagementSubsystem::ModelManagementSubsystem;
        expose SpeechOnnxPiper_SynthesisSubsystem::SynthesisSubsystem;
        expose SpeechOnnxChatterboxTurbo::SpeechOnnxChatterboxTurboSystem;
        expose SpeechOnnxChatterboxTurbo_ModelManagementSubsystem::ModelManagementSubsystem;
        expose SpeechOnnxChatterboxTurbo_SynthesisSubsystem::SynthesisSubsystem;
        expose SpeechOnnxNemotronStt::SpeechOnnxNemotronSttSystem;
        expose SpeechOnnxNemotronStt_ModelManagementSubsystem::ModelManagementSubsystem;
        expose SpeechOnnxNemotronStt_RecognitionSubsystem::RecognitionSubsystem;
        expose SpeechTextEn::SpeechTextEnSystem;
        expose SpeechTextEn_AnalysisSubsystem::AnalysisSubsystem;
        expose SpeechTextEn_NormalizationSubsystem::NormalizationSubsystem;
        expose SpeechTextEn_HeteronymSubsystem::HeteronymSubsystem;
        expose SpeechTextEn_TrainingDataSubsystem::TrainingDataSubsystem;
        expose SpeechTextEn_TrainingSubsystem::TrainingSubsystem;
        expose SpeechTextEn_ExternalEvaluationSubsystem::ExternalEvaluationSubsystem;
        expose SpeechTextEn_ClusteringSubsystem::ClusteringSubsystem;
        expose SpeechPhonemesEn::SpeechPhonemesEnSystem;
        expose SpeechPhonemesEn_LexiconSubsystem::LexiconSubsystem;
        expose SpeechPhonemesEn_LetterToSoundSubsystem::LetterToSoundSubsystem;
        expose SpeechPhonemesEn_PhonemizerSubsystem::PhonemizerSubsystem;
        expose SpeechPhonemesEn_RenderingSubsystem::RenderingSubsystem;
        expose SpeechPhonemesEnTools::SpeechPhonemesEnToolsSystem;
        expose SpeechPhonemesEnTools_LexiconSubsystem::LexiconSubsystem;
        expose SpeechPhonemesEnTools_LetterToSoundSubsystem::LetterToSoundSubsystem;
        expose SpeechPhonemesEnTools_MorphologySubsystem::MorphologySubsystem;
        expose SpeechPhonemesEnTools_PhonemizerSubsystem::PhonemizerSubsystem;
        expose SpeechPhonemesEnTools_ReadingSubsystem::ReadingSubsystem;
        expose SpeechPhonemesEnTools_RenderingSubsystem::RenderingSubsystem;
        expose SpeechProofreader::SpeechProofreaderSystem;
        expose SpeechProofreader_DiagnosticsSubsystem::DiagnosticsSubsystem;
        expose SpeechProofreader_PlaybackSubsystem::PlaybackSubsystem;
        expose SpeechProofreader_SessionSubsystem::SessionSubsystem;
        expose SpeechProofreader_SettingsSubsystem::SettingsSubsystem;
        expose SpeechProofreader_SynthesisSubsystem::SynthesisSubsystem;
        expose SpeechProofreader_TextSubsystem::TextSubsystem;
        expose ModelDownloader::ModelDownloaderSystem;
        expose KokoroOnnxBenchmark::KokoroOnnxBenchmarkSystem;
        expose OtsDependencies::PortAudioSharp2;
        expose OtsDependencies::SystemNumericsTensors;
        expose OtsDependencies::MicrosoftMlOnnxRuntime;
        expose OtsDependencies::TestResults;
        expose OtsDependencies::PartOfSpeech;
        expose OtsDependencies::Avalonia;
        expose OtsDependencies::CommunityToolkitMvvm;
        expose OtsDependencies::SherpaOnnx;
        expose OtsDependencies::SharpCompress;
        filter not (@SysML::Documentation);
    }

    view SpeechView {
        expose Speech::SpeechSystem;
    }

    view DiagnosticsView {
        expose Speech_Diagnostics::Diagnostics;
    }

    view AudioSubsystemView {
        expose Speech_AudioSubsystem::AudioSubsystem;
    }

    view PortAudioView {
        expose Speech_PortAudio::PortAudio;
    }

    view ModelManagementSubsystemView {
        expose Speech_ModelManagementSubsystem::ModelManagementSubsystem;
    }

    view RecognitionSubsystemView {
        expose Speech_RecognitionSubsystem::RecognitionSubsystem;
    }

    view SynthesisSubsystemView {
        expose Speech_SynthesisSubsystem::SynthesisSubsystem;
    }

    view SpeechDemoView {
        expose SpeechDemo::SpeechDemoSystem;
    }

    view ShellSubsystemView {
        expose SpeechDemo_ShellSubsystem::ShellSubsystem;
    }

    view DeviceSelectionSubsystemView {
        expose SpeechDemo_DeviceSelectionSubsystem::DeviceSelectionSubsystem;
    }

    view ModelCatalogSubsystemView {
        expose SpeechDemo_ModelCatalogSubsystem::ModelCatalogSubsystem;
    }

    view ModelSettingsSubsystemView {
        expose SpeechDemo_ModelSettingsSubsystem::ModelSettingsSubsystem;
    }

    view SynthesisPanelSubsystemView {
        expose SpeechDemo_SynthesisPanelSubsystem::SynthesisPanelSubsystem;
    }

    view RecognitionPanelSubsystemView {
        expose SpeechDemo_RecognitionPanelSubsystem::RecognitionPanelSubsystem;
    }

    view SpeechCliView {
        expose SpeechCli::SpeechCliSystem;
    }

    view ModelCommandsSubsystemView {
        expose SpeechCli_ModelCommandsSubsystem::ModelCommandsSubsystem;
    }

    view DeviceCommandsSubsystemView {
        expose SpeechCli_DeviceCommandsSubsystem::DeviceCommandsSubsystem;
    }

    view SynthesisCommandSubsystemView {
        expose SpeechCli_SynthesisCommandSubsystem::SynthesisCommandSubsystem;
    }

    view RecognitionCommandSubsystemView {
        expose SpeechCli_RecognitionCommandSubsystem::RecognitionCommandSubsystem;
    }

    view ConversationCommandSubsystemView {
        expose SpeechCli_ConversationCommandSubsystem::ConversationCommandSubsystem;
    }

    view SpeechSherpaView {
        expose SpeechSherpa::SpeechSherpaSystem;
    }

    view SherpaModelManagementSubsystemView {
        expose SpeechSherpa_ModelManagementSubsystem::ModelManagementSubsystem;
    }

    view SherpaRecognitionSubsystemView {
        expose SpeechSherpa_RecognitionSubsystem::RecognitionSubsystem;
    }

    view SherpaSynthesisSubsystemView {
        expose SpeechSherpa_SynthesisSubsystem::SynthesisSubsystem;
    }

    view SpeechOnnxView {
        expose SpeechOnnx::SpeechOnnxSystem;
    }

    view OnnxRuntimeSubsystemView {
        expose SpeechOnnx_OnnxRuntimeSubsystem::OnnxRuntimeSubsystem;
    }

    view SpeechOnnxKokoroView {
        expose SpeechOnnxKokoro::SpeechOnnxKokoroSystem;
    }

    view KokoroModelManagementSubsystemView {
        expose SpeechOnnxKokoro_ModelManagementSubsystem::ModelManagementSubsystem;
    }

    view KokoroSynthesisSubsystemView {
        expose SpeechOnnxKokoro_SynthesisSubsystem::SynthesisSubsystem;
    }

    view SpeechOnnxPiperView {
        expose SpeechOnnxPiper::SpeechOnnxPiperSystem;
    }

    view PiperModelManagementSubsystemView {
        expose SpeechOnnxPiper_ModelManagementSubsystem::ModelManagementSubsystem;
    }

    view PiperSynthesisSubsystemView {
        expose SpeechOnnxPiper_SynthesisSubsystem::SynthesisSubsystem;
    }

    view SpeechOnnxChatterboxTurboView {
        expose SpeechOnnxChatterboxTurbo::SpeechOnnxChatterboxTurboSystem;
    }

    view ChatterboxTurboModelManagementSubsystemView {
        expose SpeechOnnxChatterboxTurbo_ModelManagementSubsystem::ModelManagementSubsystem;
    }

    view ChatterboxTurboSynthesisSubsystemView {
        expose SpeechOnnxChatterboxTurbo_SynthesisSubsystem::SynthesisSubsystem;
    }

    view SpeechOnnxNemotronSttView {
        expose SpeechOnnxNemotronStt::SpeechOnnxNemotronSttSystem;
    }

    view NemotronModelManagementSubsystemView {
        expose SpeechOnnxNemotronStt_ModelManagementSubsystem::ModelManagementSubsystem;
    }

    view NemotronRecognitionSubsystemView {
        expose SpeechOnnxNemotronStt_RecognitionSubsystem::RecognitionSubsystem;
    }

    view SpeechTextEnView {
        expose SpeechTextEn::SpeechTextEnSystem;
    }

    view TextEnAnalysisSubsystemView {
        expose SpeechTextEn_AnalysisSubsystem::AnalysisSubsystem;
    }

    view TextEnNormalizationSubsystemView {
        expose SpeechTextEn_NormalizationSubsystem::NormalizationSubsystem;
    }

    view TextEnHeteronymSubsystemView {
        expose SpeechTextEn_HeteronymSubsystem::HeteronymSubsystem;
    }

    view TextEnTrainingDataSubsystemView {
        expose SpeechTextEn_TrainingDataSubsystem::TrainingDataSubsystem;
    }

    view TextEnTrainingSubsystemView {
        expose SpeechTextEn_TrainingSubsystem::TrainingSubsystem;
    }

    view TextEnExternalEvaluationSubsystemView {
        expose SpeechTextEn_ExternalEvaluationSubsystem::ExternalEvaluationSubsystem;
    }

    view SpeechPhonemesEnView {
        expose SpeechPhonemesEn::SpeechPhonemesEnSystem;
    }

    view PhonemesEnLexiconSubsystemView {
        expose SpeechPhonemesEn_LexiconSubsystem::LexiconSubsystem;
    }

    view PhonemesEnLetterToSoundSubsystemView {
        expose SpeechPhonemesEn_LetterToSoundSubsystem::LetterToSoundSubsystem;
    }

    view PhonemesEnPhonemizerSubsystemView {
        expose SpeechPhonemesEn_PhonemizerSubsystem::PhonemizerSubsystem;
    }

    view PhonemesEnRenderingSubsystemView {
        expose SpeechPhonemesEn_RenderingSubsystem::RenderingSubsystem;
    }

    view SpeechPhonemesEnToolsView {
        expose SpeechPhonemesEnTools::SpeechPhonemesEnToolsSystem;
    }

    view PhonemesEnToolsLexiconSubsystemView {
        expose SpeechPhonemesEnTools_LexiconSubsystem::LexiconSubsystem;
    }

    view PhonemesEnToolsLetterToSoundSubsystemView {
        expose SpeechPhonemesEnTools_LetterToSoundSubsystem::LetterToSoundSubsystem;
    }

    view PhonemesEnToolsMorphologySubsystemView {
        expose SpeechPhonemesEnTools_MorphologySubsystem::MorphologySubsystem;
    }

    view PhonemesEnToolsPhonemizerSubsystemView {
        expose SpeechPhonemesEnTools_PhonemizerSubsystem::PhonemizerSubsystem;
    }

    view PhonemesEnToolsReadingSubsystemView {
        expose SpeechPhonemesEnTools_ReadingSubsystem::ReadingSubsystem;
    }

    view PhonemesEnToolsRenderingSubsystemView {
        expose SpeechPhonemesEnTools_RenderingSubsystem::RenderingSubsystem;
    }

    view SpeechProofreaderView {
        expose SpeechProofreader::SpeechProofreaderSystem;
    }

    view ProofreaderDiagnosticsSubsystemView {
        expose SpeechProofreader_DiagnosticsSubsystem::DiagnosticsSubsystem;
    }

    view ProofreaderPlaybackSubsystemView {
        expose SpeechProofreader_PlaybackSubsystem::PlaybackSubsystem;
    }

    view ProofreaderSessionSubsystemView {
        expose SpeechProofreader_SessionSubsystem::SessionSubsystem;
    }

    view ProofreaderSettingsSubsystemView {
        expose SpeechProofreader_SettingsSubsystem::SettingsSubsystem;
    }

    view ProofreaderSynthesisSubsystemView {
        expose SpeechProofreader_SynthesisSubsystem::SynthesisSubsystem;
    }

    view ProofreaderTextSubsystemView {
        expose SpeechProofreader_TextSubsystem::TextSubsystem;
    }

    view ModelDownloaderView {
        expose ModelDownloader::ModelDownloaderSystem;
    }

    view KokoroOnnxBenchmarkView {
        expose KokoroOnnxBenchmark::KokoroOnnxBenchmarkSystem;
    }
}
