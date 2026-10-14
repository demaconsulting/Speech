# Hot TTS/STT: Reusing an Instance Across Turns

`SpeechRecognizerFactory.LoadAsync(...)`/`SpeechSynthesizerFactory.LoadAsync(...)` are the
expensive step in either direction: on success, each loads a model into native memory. Beneath
that, there is a three-way cost split:

- **`LoadAsync`** (engine): expensive - do this once per model/parameter combination.
- **`CreateSessionAsync`** (engine → session): binds exactly one device for the session's entire
  life. Comparatively cheap, but exclusive - an engine leases its single `IRecognitionSession`/
  `ISynthesisSession` to only one live session at a time, and a concurrent `CreateSessionAsync`
  call while a lease is held fails fast with `RecognitionEngineBusyException`/
  `SynthesisEngineBusyException` rather than queueing.
- **`StartAsync`/`StopAsync`/`GetResultsAsync`** (recognition) and **`SpeakAsync`/
  `SynthesizeAsync`** (synthesis): per-turn, cheap, and fully repeatable - neither reloads the
  model nor rebinds the device.

For a low-latency, multi-turn scenario such as a voice conversation, load each engine **once**
and reuse it across many turns, rather than disposing and recreating it per turn. A synthesis
session may itself be reused across many `SpeakAsync` calls (it cycles back to `Starting` on each
new call). A recognition session is single-use - once it reaches `Stopped`, `StartAsync` throws
`InvalidOperationException` - so reuse the *engine* across turns and create a fresh session per
turn instead:

```csharp
// Synthesis: load the engine once, create the session once, speak many times.
await using var synthesizerEngine = await SpeechSynthesizerFactory.LoadAsync(model, catalog);
await using var synthesisSession = await synthesizerEngine.CreateSessionAsync(playbackDevice);
await synthesisSession.SpeakAsync("First turn.");
await synthesisSession.SpeakAsync("Second turn - neither the model nor the device were reloaded.");

// Recognition: load the engine once, create a fresh session per turn (sessions are single-use).
await using var recognizerEngine = await SpeechRecognizerFactory.LoadAsync(model, catalog);

await using (var turn1 = await recognizerEngine.CreateSessionAsync(captureDevice))
{
    await turn1.StartAsync();
    // ... wait for this turn's result via GetResultsAsync, then:
    await turn1.StopAsync();
}

await using (var turn2 = await recognizerEngine.CreateSessionAsync(captureDevice))
{
    // Creating turn2 only succeeds once turn1 has fully disposed and released the engine's lease.
    await turn2.StartAsync();
    // ... wait for this turn's result, then:
    await turn2.StopAsync();
}
```

Only dispose and reload an engine when you need to change its model or parameter values - not
simply to begin a new turn. `speech-cli ask` applies the same principle at the
process level: because a single CLI invocation only ever runs one turn, there is no instance to
reuse across turns, so instead `ask` pre-warms (constructs, and so loads) its STT engine
concurrently with speaking the prompt, rather than only afterward, removing the same avoidable
model-load latency a long-lived host would instead avoid by reusing one instance across many
turns. See the "SpeechCli" section's worked examples below for the exact command.
