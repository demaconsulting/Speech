package SpeechCli_SpeechModelCatalogAdapter {
    private import SoftwareItemMetadata::*;
    private import SpeechCli_ICliModelCatalog::*;

    part def SpeechModelCatalogAdapter {
        doc /* Real catalog seam implementation, owning and delegating to a real SpeechModelCatalog (and, for
             * uninstall/cleanup, its Store) without exposing the library's internal-only constructor. Forwards
             * GetPreferredAudioFormat/CreateSynthesizerEngineAsync and CreateRecognizerEngineAsync unchanged to the
             * resolved model's real SpeechSynthesizerFactory/SpeechRecognizerFactory, preserving each factory's own
             * honestly-unavailable fallback contract rather than masking it. Chains AddChatterboxTurboModels after
             * the Sherpa/Kokoro/Piper/Nemotron models, sharing one ReferenceVoiceLibrary with the voice members and
             * passing --voices-dir as the extra reference-voice directory. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech.Cli/Commands/ModelCommandsSubsystem/SpeechModelCatalogAdapter.cs");
            testFiles = ("test/DemaConsulting.Speech.Cli.Tests/Commands/ModelCommandsSubsystem/SpeechModelCatalogAdapterTests.cs");
            designFiles = ("docs/design/speech-cli/model-commands-subsystem/speech-model-catalog-adapter.md");
            verificationFiles = ("docs/verification/speech-cli/model-commands-subsystem/speech-model-catalog-adapter.md");
            requirementFiles = ("docs/reqstream/speech-cli/model-commands-subsystem/speech-model-catalog-adapter.yaml");
        }

        // Implements the CLI seam, delegating to SpeechModelCatalog, its store, downloader and engine
        // factories.
        dependency SpeechModelCatalogAdapter to ICliModelCatalog;
        dependency SpeechModelCatalogAdapter to SpeechOnnxChatterboxTurbo_SpeechModelCatalogChatterboxTurboExtensions::SpeechModelCatalogChatterboxTurboExtensions;
        dependency SpeechModelCatalogAdapter to SpeechOnnxKokoro_SpeechModelCatalogKokoroExtensions::SpeechModelCatalogKokoroExtensions;
        dependency SpeechModelCatalogAdapter to SpeechOnnxNemotronStt_SpeechModelCatalogNemotronSttExtensions::SpeechModelCatalogNemotronSttExtensions;
        dependency SpeechModelCatalogAdapter to SpeechOnnxPiper_SpeechModelCatalogPiperExtensions::SpeechModelCatalogPiperExtensions;
        dependency SpeechModelCatalogAdapter to SpeechSherpa_SpeechModelCatalogSherpaExtensions::SpeechModelCatalogSherpaExtensions;
        dependency SpeechModelCatalogAdapter to Speech_AudioFormat::AudioFormat;
        dependency SpeechModelCatalogAdapter to Speech_ISpeechRecognizerEngine::ISpeechRecognizerEngine;
        dependency SpeechModelCatalogAdapter to Speech_ISpeechSynthesizerEngine::ISpeechSynthesizerEngine;
        dependency SpeechModelCatalogAdapter to Speech_ReferenceVoiceLibrary::ReferenceVoiceLibrary;
        dependency SpeechModelCatalogAdapter to Speech_ReferenceVoicePack::ReferenceVoicePack;
        dependency SpeechModelCatalogAdapter to Speech_SpeechModelCatalog::SpeechModelCatalog;
        dependency SpeechModelCatalogAdapter to Speech_SpeechModelContract::SpeechModelContract;
        dependency SpeechModelCatalogAdapter to Speech_SpeechModelDescriptor::SpeechModelDescriptor;
        dependency SpeechModelCatalogAdapter to Speech_SpeechModelDescriptorEnums::SpeechModelDescriptorEnums;
        dependency SpeechModelCatalogAdapter to Speech_SpeechModelDownloadProgress::SpeechModelDownloadProgress;
        dependency SpeechModelCatalogAdapter to Speech_SpeechModelDownloader::SpeechModelDownloader;
        dependency SpeechModelCatalogAdapter to Speech_SpeechModelDownloaderOptions::SpeechModelDownloaderOptions;
        dependency SpeechModelCatalogAdapter to Speech_SpeechModelStoreOptions::SpeechModelStoreOptions;
        dependency SpeechModelCatalogAdapter to Speech_SpeechRecognizerFactory::SpeechRecognizerFactory;
        dependency SpeechModelCatalogAdapter to Speech_SpeechSynthesizerFactory::SpeechSynthesizerFactory;
    }
}
