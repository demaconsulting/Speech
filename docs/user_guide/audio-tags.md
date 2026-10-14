# Natural Language Audio Tags

The library also provides the closed, fixed Natural Language Audio Tag vocabulary and its
model-independent Layer 1 parser from the `SynthesisSubsystem`
(`DemaConsulting.Speech.SynthesisSubsystem`), together with the Layer 2 rendering and streaming
synthesis pipeline described below. Inline bracket tags let you request expressive delivery
without SSML:

| Kind | Tags |
| --- | --- |
| Emotion | `[excited]`/`[excitedly]`, `[serious]`, `[sarcastic]` |
| Emotion | `[panicked]`/`[shocked]`, `[bored]`/`[tired]`, `[sad]`/`[crying]` |
| Mood | `[angry]`/`[anger]`, `[calm]`, `[happy]`, `[enthused]`/`[enthusiastic]` |
| Mood | `[surprised]`, `[worried]`/`[anxious]`, `[confused]`, `[frustrated]`, `[neutral]` |
| Pace | `[slow]`, `[very slow]`, `[fast]`, `[very fast]` |
| Pause | `[short pause]`, `[long pause]` |
| Emphasis | `[emphasis]` |
| Delivery/Volume | `[whispers]`/`[whispering]`, `[soft]`, `[loud]`/`[shouting]`/`[shout]`/`[screams]`, `[breathy]` |
| Non-verbal | `[laughs]`/`[laughing]`/`[giggles]`, `[sighs]`/`[sigh]` |
| Non-verbal | `[gasp]`/`[inhale]`, `[clears throat]`/`[cough]`, `[snorts]` |

```csharp
using DemaConsulting.Speech.SynthesisSubsystem;

var spans = AudioTagParser.Parse("I can't believe it! [excited] That's amazing!");

foreach (var span in spans)
{
    Console.WriteLine(span.Kind == TaggedTextSpanKind.Tag
        ? $"tag: {span.Tag}"
        : $"text: {span.Text}");
}
```

Tag matching is case-insensitive and tolerant of extra internal whitespace (`[very   slow]`
resolves the same as `[very slow]`). Any bracket text this vocabulary does not recognize — a
typo, an unsupported phrase, or an unbalanced bracket — passes through unchanged as ordinary
narration rather than being dropped or rejected, so a reply is never worse than plain text.
