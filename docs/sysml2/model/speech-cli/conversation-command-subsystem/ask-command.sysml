package SpeechCli_AskCommand {
    private import SoftwareItemMetadata::*;
    private import SpeechCli_AudioDeviceFactoryCaptureDeviceSource::*;
    private import SpeechCli_AudioDeviceFactoryPlaybackDeviceSource::*;
    private import SpeechCli_CliModelCatalogFactory::*;
    private import SpeechCli_ConsoleCancellation::*;
    private import SpeechCli_Context::*;
    private import SpeechCli_DevicesTestCommand::*;
    private import SpeechCli_ICliCaptureDeviceSource::*;
    private import SpeechCli_ICliModelCatalog::*;
    private import SpeechCli_ICliPlaybackDeviceSource::*;
    private import SpeechCli_ParameterBagParser::*;
    private import SpeechCli_SilenceTimeoutRecognizerSession::*;

    part def AskCommand {
        doc /* Implements ask: resolves prompt text from --text/--file/piped stdin, resolves and validates both a
             * synthesis-role --tts-model and a recognition-role --stt-model, validates --tts-param/--stt-param values
             * via the reused ParameterBagParser, speaks the resolved text through a real playback device via an
             * ISpeechSynthesizerEngine/ISynthesisSession, then - once playback finishes - listens through a capture
             * device resolved via ICliCaptureDeviceSource using an ISpeechRecognizerEngine/IRecognitionSession
             * wrapped in a reused SilenceTimeoutRecognizerSession, until the first final recognition result arrives,
             * a start or silence timeout elapses, or Ctrl+C is pressed. The recognizer engine and session are created
             * while the prompt plays, so the recognizer is ready when playback ends; microphone capture starts only
             * in the listen phase. If the prompt is canceled or fails, the recognizer is disposed in the background
             * without delaying the caller. A Ctrl+C during the listen phase is distinguished from a timeout by the
             * shared cancellation token and is reported as a cancellation, printing or writing no text. Otherwise the
             * final recognized text is printed (or written to --output-text), and every engine, session, and device
             * is disposed on every exit path. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech.Cli/Commands/ConversationCommandSubsystem/AskCommand.cs");
            testFiles = ("test/DemaConsulting.Speech.Cli.Tests/Commands/ConversationCommandSubsystem/AskCommandTests.cs");
            designFiles = ("docs/design/speech-cli/conversation-command-subsystem/ask-command.md");
            verificationFiles = ("docs/verification/speech-cli/conversation-command-subsystem/ask-command.md");
            requirementFiles = ("docs/reqstream/speech-cli/conversation-command-subsystem/ask-command.yaml");
        }

        // Uses the catalog for TTS/STT models, device sources for playback and capture, and silence
        // timeouts to stop.
        dependency AskCommand to AudioDeviceFactoryCaptureDeviceSource;
        dependency AskCommand to AudioDeviceFactoryPlaybackDeviceSource;
        dependency AskCommand to CliModelCatalogFactory;
        dependency AskCommand to ConsoleCancellation;
        dependency AskCommand to Context;
        dependency AskCommand to DevicesTestCommand;
        dependency AskCommand to ICliCaptureDeviceSource;
        dependency AskCommand to ICliModelCatalog;
        dependency AskCommand to ICliPlaybackDeviceSource;
        dependency AskCommand to ParameterBagParser;
        dependency AskCommand to SilenceTimeoutRecognizerSession;
        dependency AskCommand to Speech_AudioDeviceFactory::AudioDeviceFactory;
        dependency AskCommand to Speech_IRecognitionSession::IRecognitionSession;
        dependency AskCommand to Speech_ISpeechRecognizerEngine::ISpeechRecognizerEngine;
        dependency AskCommand to Speech_SpeechModelDescriptor::SpeechModelDescriptor;
        dependency AskCommand to Speech_SpeechModelDescriptorEnums::SpeechModelDescriptorEnums;
    }
}
