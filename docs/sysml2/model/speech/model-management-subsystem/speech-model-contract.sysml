package Speech_SpeechModelContract {
    private import SoftwareItemMetadata::*;
    private import Speech_AudioFormat::*;
    private import Speech_Layer2Rendering::*;
    private import Speech_RecognitionBackend::*;
    private import Speech_SpeechModelDescriptorEnums::*;
    private import Speech_SpeechModelDownloadDescriptor::*;
    private import Speech_SpeechModelParameters::*;
    private import Speech_SynthesisBackend::*;
    private import Speech_TextAnalysis::*;

    part def SpeechModelContract {
        doc /* The common per-model contract (ISpeechModel: Id, DisplayName, Role, Parameters, AudioTagSupport,
             * DownloadDescriptor) and its two role-specific interfaces, IRecognitionModel and ISynthesisModel. Both
             * interfaces are fully public, including their engine-construction members, so a third-party package can
             * implement either to add its own recognition/synthesis backend. Recognition exposes a public AudioFormat
             * plus public engine construction; synthesis exposes a public best-effort PreferredAudioFormat plus
             * public engine construction and a default-hook CapabilityProfile member and a default-hook TextAnalyzer
             * member (defaulting to none) through which a language-aware model can supply a text analyzer. It also
             * provides a recognition-only IRecognitionModel.NormalizeText(text, isFinal) default hook, distinct from
             * the shared ISpeechModel.NormalizeText(text) used on the synthesis side. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech/ModelManagementSubsystem/ISpeechModel.cs", "src/DemaConsulting.Speech/ModelManagementSubsystem/IRecognitionModel.cs", "src/DemaConsulting.Speech/ModelManagementSubsystem/ISynthesisModel.cs");
            testFiles = ("test/DemaConsulting.Speech.Tests/ModelManagementSubsystem/SpeechModelContractTests.cs");
            designFiles = ("docs/design/speech/model-management-subsystem/speech-model-contract.md");
            verificationFiles = ("docs/verification/speech/model-management-subsystem/speech-model-contract.md");
            requirementFiles = ("docs/reqstream/speech/model-management-subsystem/speech-model-contract.yaml");
        }

        // Defines each model's audio format, parameters, download files and the backends and text analysis
        // it plugs in.
        dependency SpeechModelContract to AudioFormat;
        dependency SpeechModelContract to Layer2Rendering;
        dependency SpeechModelContract to RecognitionBackend;
        dependency SpeechModelContract to SpeechModelDescriptorEnums;
        dependency SpeechModelContract to SpeechModelDownloadDescriptor;
        dependency SpeechModelContract to SpeechModelParameters;
        dependency SpeechModelContract to SynthesisBackend;
        dependency SpeechModelContract to TextAnalysis;
    }
}
