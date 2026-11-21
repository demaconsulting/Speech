package Speech_SynthesisSession {
    private import SoftwareItemMetadata::*;
    private import Speech_AudioTagParser::*;
    private import Speech_IAudioPlaybackDevice::*;
    private import Speech_ISpeechDiagnostics::*;
    private import Speech_ISpeechSynthesizerEngine::*;
    private import Speech_ISynthesisSession::*;
    private import Speech_Layer2Rendering::*;
    private import Speech_PlaybackAudioResampler::*;
    private import Speech_SpeechDiagnosticLevel::*;
    private import Speech_SpeechModelContract::*;
    private import Speech_SpeechModelParameters::*;
    private import Speech_SpeechParameterConventions::*;
    private import Speech_SynthesisBackend::*;
    private import Speech_SynthesisDedicatedWorker::*;
    private import Speech_SynthesisSessionFaultedException::*;
    private import Speech_SynthesisSessionState::*;
    private import Speech_TextAnalysis::*;

    part def SynthesisSession {
        doc /* Real ISynthesisSession implementation: normalizes and tag-parses input text, runs the model's optional
             * text analysis once per request over the whole text on a thread-pool thread, never the thread that
             * called SpeakAsync/SynthesizeAsync (with each recognized tag replaced by a placeholder, containing
             * analyzer failures), Layer 2 renders it into an ordered SpeechPlan, synthesizes each segment on a
             * dedicated worker thread (through the annotation-accepting backend seam when the segment has
             * annotations; bounding how long a non-cooperative native call is waited on before being abandoned), and
             * - for SpeakAsync - plays the resulting audio through this session's bound playback device while waiting
             * for genuine playback drain before stopping it. An explicit SynthesisSessionState state machine and an
             * overlap guard (at most one SpeakAsync/SynthesizeAsync call in flight at a time) wrap this pipeline;
             * StopAsync cancels an in-flight operation deterministically and a failed operation transitions the
             * session to the terminal Faulted state. Resolves the speaker id passed to each segment's Generate call
             * from the constructor-supplied parameterValues bag via ISynthesisModel.ResolveSpeakerId once per
             * segment, independent of the per-segment ParameterOverrides speed/volume mechanism. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech/SynthesisSubsystem/SynthesisSession.cs");
            testFiles = ("test/DemaConsulting.Speech.Tests/SynthesisSubsystem/SynthesisSessionTests.cs");
            designFiles = ("docs/design/speech/synthesis-subsystem/synthesis-session.md");
            verificationFiles = ("docs/verification/speech/synthesis-subsystem/synthesis-session.md");
            requirementFiles = ("docs/reqstream/speech/synthesis-subsystem/synthesis-session.yaml");
        }

        // Parses tags, renders text, synthesizes on a worker, resamples and sends the audio to the
        // playback device.
        dependency SynthesisSession to AudioTagParser;
        dependency SynthesisSession to IAudioPlaybackDevice;
        dependency SynthesisSession to ISpeechDiagnostics;
        dependency SynthesisSession to ISpeechSynthesizerEngine;
        dependency SynthesisSession to ISynthesisSession;
        dependency SynthesisSession to Layer2Rendering;
        dependency SynthesisSession to PlaybackAudioResampler;
        dependency SynthesisSession to SpeechDiagnosticLevel;
        dependency SynthesisSession to SpeechModelContract;
        dependency SynthesisSession to SpeechModelParameters;
        dependency SynthesisSession to SpeechParameterConventions;
        dependency SynthesisSession to SynthesisBackend;
        dependency SynthesisSession to SynthesisDedicatedWorker;
        dependency SynthesisSession to SynthesisSessionFaultedException;
        dependency SynthesisSession to SynthesisSessionState;
        dependency SynthesisSession to TextAnalysis;
        dependency SynthesisSession to OtsDependencies::SystemNumericsTensors;
    }
}
