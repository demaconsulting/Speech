# Speech-to-Text Evaluation Tool

A developer tool (never packed or shipped) that measures installed speech-to-text models on the committed evaluation
data in `data/stt-en/`: word error rate, segmentation, invented words, latency and speed. It gates the ONNX Nemotron
model (`--check`) and compares it with other models, such as the Sherpa Nemotron model, on identical audio
(`--compare`).

## Commands

Run from the repository root (Release is much faster than Debug):

```pwsh
# Evaluate the ONNX Nemotron model on every scenario
dotnet run --project tools/DemaConsulting.Speech.SttEvaluation -c Release -- evaluate

# Apply the accuracy gates (exit 1 when one fails)
dotnet run --project tools/DemaConsulting.Speech.SttEvaluation -c Release -- evaluate --check

# Compare the ONNX model with the Sherpa Nemotron model on the same audio
dotnet run --project tools/DemaConsulting.Speech.SttEvaluation -c Release -- evaluate --compare nemotron-3.5-asr-streaming-0.6b-onnx-int4 nemotron-speech-streaming-en-0.6b-560ms-int8-2026-04-25
```

Options:

- `--model <id>`: model to evaluate (repeatable; default `nemotron-3.5-asr-streaming-0.6b-onnx-int4`)
- `--compare <id> [<id>...]`: evaluate these models and write `compare.tsv`; the first is the baseline of the cross
  word error rate; with one id the default model is the baseline
- `--check`: apply the accuracy gates to the ONNX Nemotron model; adds the gated scenarios when `--scenario` omits them
- `--scenario <name>`: scenario to run (repeatable; default all, see below)
- `--engine-option <Name=Value>`: a declared parameter of the first model (for example `language=en-US`) or, for the
  ONNX Nemotron model, an internal engine option: `BlankPenalty` (non-negative number), `FlushTailChunks`
  (non-negative integer), `TrailingSilenceMs`, `TrailingSilenceAfterSpeechMs`, `MaxUtteranceMs` (endpoint rules in
  milliseconds, 0 disables a rule); an unknown name is an error
- `--trailing-silence <s>`: seconds of digital silence fed after each recording before the flush (default 0)
- `--data <dir>`: data folder (default `data/stt-en`)
- `--models-dir <dir>`: model store root (default: the per-user store, where `tools/ModelDownloader` installs models)
- `--output <dir>`: output folder; must be inside `artifacts/` (default `artifacts/stt-evaluation`)

Exit codes: 0 success, 1 a gate failed, 2 a usage, data or model error. A model that is not installed is reported with
the `tools/ModelDownloader` command that installs it.

## How Audio Is Fed

Each recording is decoded with the library's own WAV/FLAC reader (resampled with the library's resampler when its rate
differs from the model's) and streamed through the model's backend the way a recognition session does: 20 ms blocks,
each followed by decoding until the backend reports nothing new, every result passed through the model's text hook, and
one flush at the end of the stream. The backend is loaded once per model and reset before each recording.

## Scenarios

Every scenario reports WER, segments, invented words, latency and speed.

| Scenario | Audio | Notes |
| --- | --- | --- |
| `librispeech` | each of the 24 LibriSpeech utterances on its own | gated WER |
| `poem` | the 50 s "Crossing the Bar" recitation as one stream | gated WER, punctuated WER |
| `paused` | the first 4 utterances separated by 1.5 s of digital silence | 4 segments expected |
| `continuous` | utterances joined without gaps until at least 45 s | |
| `quiet` | every utterance attenuated by 20 dB | |

## Metrics

- **WER**: corpus word error rate (total substitutions, deletions and insertions over total reference words) after
  normalization: lower case, punctuation removed except apostrophes inside a word.
- **Punctuated WER**: verbatim comparison (casing and punctuation kept); only for references that are punctuated (the
  poem). Informational.
- **Segments**: the number of non-empty final results; the expected count is shown where the scenario defines it.
- **Invented words**: hypothesis words after the last reference word the hypothesis aligned to (tail insertions), the
  signature of a tail hallucination at flush.
- **Latency and speed** (reported only, never gated): the audio-clock time to the first result, the audio-clock lag from
  the end of a recording to its last final, the wall-clock processing time and the real-time factor (RTF).

## Gates

`--check` gates corpus WER only, for `nemotron-3.5-asr-streaming-0.6b-onnx-int4`: at most 6% on `librispeech` and at
most 10% on `poem`. The thresholds live in `SttEvaluationGates.cs`, which the real-model tests compile in as well, so
the tool and the tests apply the same gates.

## Output

Reports are written only below `artifacts/stt-evaluation/` (git-ignored): per model `summary.tsv`, `items.tsv` and
`transcripts.txt`, and with `--compare` a `compare.tsv`. No engine output - in particular no transcript of a third-party
model - is ever committed.

## Shared Code

`WordErrorRateCalculator.cs`, `SttDataSet.cs`, `SttTranscriber.cs` and `SttEvaluationGates.cs` are compiled (linked)
into `DemaConsulting.Speech.Onnx.NemotronStt.Tests` (and the calculator into `DemaConsulting.Speech.Sherpa.Tests`),
whose `Evaluation/` tests cover them. They use public library APIs only and compile for every target framework of the
tests.
