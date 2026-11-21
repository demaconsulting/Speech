package Speech_SynthesisSubsystem {
    private import SoftwareItemMetadata::*;
    private import Speech_AudioTagDescriptor::*;
    private import Speech_AudioTagParser::*;
    private import Speech_BackendAudio::*;
    private import Speech_ISpeechSynthesizerEngine::*;
    private import Speech_ISynthesisSession::*;
    private import Speech_Layer2Rendering::*;
    private import Speech_PlaybackAudioResampler::*;
    private import Speech_SentenceChunker::*;
    private import Speech_SpeechParameterConventions::*;
    private import Speech_SpeechSynthesizerEngine::*;
    private import Speech_SpeechSynthesizerFactory::*;
    private import Speech_SpeechSynthesizerUnavailableException::*;
    private import Speech_SynthesisBackend::*;
    private import Speech_SynthesisDedicatedWorker::*;
    private import Speech_SynthesisEngineBusyException::*;
    private import Speech_SynthesisSession::*;
    private import Speech_SynthesisSessionFaultedException::*;
    private import Speech_SynthesisSessionState::*;
    private import Speech_SynthesisSessionStateChangedEventArgs::*;
    private import Speech_TextAnalysis::*;
    private import Speech_UnavailableSpeechSynthesizerEngine::*;
    private import Speech_UnavailableSynthesisSession::*;

    part def SynthesisSubsystem {
        doc /* Text-to-speech: the closed, fixed Natural Language Audio Tag vocabulary and the model-independent Layer
             * 1 parser, plus Layer 2 per-model rendering, sentence chunking, the public Layer 3/Layer 5
             * Engine/Session text-to-speech contract (ISpeechSynthesizerEngine/ISynthesisSession) with its
             * composition root and honest unavailable fallbacks at every layer, engine exclusivity leasing, a
             * cancel-then-abandon dedicated worker, the public mockable synthesis-backend seam (with its optional
             * annotation-accepting extension), the language-neutral text-analysis/annotation seam, and the
             * playback-format converter. */

        metadata info : SoftwareInfo {
            designFiles = ("docs/design/speech/synthesis-subsystem.md");
            verificationFiles = ("docs/verification/speech/synthesis-subsystem.md");
            requirementFiles = ("docs/reqstream/speech/synthesis-subsystem.yaml");
        }

        part audioTagParser : AudioTagParser;
        part iSpeechSynthesizerEngine : ISpeechSynthesizerEngine;
        part iSynthesisSession : ISynthesisSession;
        part speechSynthesizerFactory : SpeechSynthesizerFactory;
        part speechSynthesizerEngine : SpeechSynthesizerEngine;
        part synthesisSession : SynthesisSession;
        part synthesisBackend : SynthesisBackend;
        part layer2Rendering : Layer2Rendering;
        part sentenceChunker : SentenceChunker;
        part textAnalysis : TextAnalysis;
        part playbackAudioResampler : PlaybackAudioResampler;
        part unavailableSpeechSynthesizerEngine : UnavailableSpeechSynthesizerEngine;
        part unavailableSynthesisSession : UnavailableSynthesisSession;
        part speechSynthesizerUnavailableException : SpeechSynthesizerUnavailableException;
        part synthesisSessionState : SynthesisSessionState;
        part sessionStateChangedEventArgs : SynthesisSessionStateChangedEventArgs;
        part synthesisEngineBusyException : SynthesisEngineBusyException;
        part synthesisSessionFaultedException : SynthesisSessionFaultedException;
        part dedicatedWorker : SynthesisDedicatedWorker;
        part backendAudio : BackendAudio;
        part audioTagDescriptor : AudioTagDescriptor;
        part speechParameterConventions : SpeechParameterConventions;
    }
}
