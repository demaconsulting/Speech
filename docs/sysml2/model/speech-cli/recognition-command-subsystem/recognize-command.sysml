package SpeechCli_RecognizeCommand {
    private import SoftwareItemMetadata::*;
    private import SpeechCli_AudioDeviceFactoryCaptureDeviceSource::*;
    private import SpeechCli_CliModelCatalogFactory::*;
    private import SpeechCli_ConsoleCancellation::*;
    private import SpeechCli_Context::*;
    private import SpeechCli_DevicesTestCommand::*;
    private import SpeechCli_ICliCaptureDeviceSource::*;
    private import SpeechCli_ICliModelCatalog::*;
    private import SpeechCli_ParameterBagParser::*;
    private import SpeechCli_SilenceTimeoutRecognizerSession::*;

    part def RecognizeCommand {
        doc /* Implements recognize: resolves exactly one of --input/--mic, resolves and validates the requested
             * recognition model, validates --stt-param values via the reused ParameterBagParser, loads an
             * ISpeechRecognizerEngine via ICliModelCatalog.CreateRecognizerEngineAsync and binds an
             * IRecognitionSession to the resolved capture device, drives file-input mode to completion via
             * StartAsync's blocking file delivery followed by an explicit drain StopAsync, wires mic-input mode to
             * Ctrl+C (session.StopAsync) and an always-constructed SilenceTimeoutRecognizerSession decorator around
             * GetResultsAsync (file mode pumps the session directly, with no decorator needed), filters console
             * output per --interim/--final-only, writes only final results to --output-text, and disposes the engine
             * and session (await using) on every exit path. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech.Cli/Commands/RecognitionCommandSubsystem/RecognizeCommand.cs");
            testFiles = ("test/DemaConsulting.Speech.Cli.Tests/Commands/RecognitionCommandSubsystem/RecognizeCommandTests.cs");
            designFiles = ("docs/design/speech-cli/recognition-command-subsystem/recognize-command.md");
            verificationFiles = ("docs/verification/speech-cli/recognition-command-subsystem/recognize-command.md");
            requirementFiles = ("docs/reqstream/speech-cli/recognition-command-subsystem/recognize-command.yaml");
        }

        // Gets a recognizer from the catalog, takes audio from a WAV file or mic source, and stops on
        // silence.
        dependency RecognizeCommand to AudioDeviceFactoryCaptureDeviceSource;
        dependency RecognizeCommand to CliModelCatalogFactory;
        dependency RecognizeCommand to ConsoleCancellation;
        dependency RecognizeCommand to Context;
        dependency RecognizeCommand to DevicesTestCommand;
        dependency RecognizeCommand to ICliCaptureDeviceSource;
        dependency RecognizeCommand to ICliModelCatalog;
        dependency RecognizeCommand to ParameterBagParser;
        dependency RecognizeCommand to SilenceTimeoutRecognizerSession;
        dependency RecognizeCommand to Speech_AudioDeviceFactory::AudioDeviceFactory;
        dependency RecognizeCommand to Speech_IAudioCaptureDevice::IAudioCaptureDevice;
        dependency RecognizeCommand to Speech_SpeechModelDescriptor::SpeechModelDescriptor;
        dependency RecognizeCommand to Speech_SpeechModelDescriptorEnums::SpeechModelDescriptorEnums;
        dependency RecognizeCommand to Speech_SpeechRecognitionResult::SpeechRecognitionResult;
        dependency RecognizeCommand to Speech_WavFileAudioCaptureDevice::WavFileAudioCaptureDevice;
    }
}
