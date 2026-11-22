# ONNX Nemotron Speech-to-Text: Evaluation Record

This folder records the measurements behind the evaluated defaults of the `DemaConsulting.Speech.Onnx.NemotronStt`
engine and the accuracy gates of its real-model tests. The numbers were produced by the non-shipped evaluation tool
(`tools/DemaConsulting.Speech.SttEvaluation`, see its README) on the committed, license-tracked data in `data/stt-en/`
with the model `nemotron-3.5-asr-streaming-0.6b-onnx-int4`. Only this hand-written summary is committed: the tool writes
its reports and transcripts below the git-ignored `artifacts/` folder, and no output of any other engine is recorded
here.

## What Is Measured

The tool streams each recording through the model's backend the way a recognition session does (20 ms blocks, decoding
after each block, one flush at the end of the stream) and scores the final results against the reference transcripts of
`data/stt-en/MANIFEST.tsv`.

| Scenario | Audio |
| --- | --- |
| `librispeech` | each of the 24 LibriSpeech test-clean utterances (211 s, 560 words) streamed on its own |
| `poem` | the 50 s "Crossing the Bar" recitation (102 words) streamed as one recording with its natural pauses |
| `paused` | the first 4 utterances separated by 1.5 s of digital silence (4 finals expected) |
| `continuous` | utterances joined without gaps for 55 s (exercises the 20 s maximum utterance length) |
| `quiet` | every utterance attenuated by 20 dB |

- **WER**: corpus word error rate after normalization (lower case, punctuation removed except word-internal
  apostrophes).
- **Segments**: the number of non-empty final results.
- **Invented**: words after the last reference word the transcript aligned to (the signature of a tail hallucination).

The gates applied by `evaluate --check` and by the real-model tests (`SttEvaluationGates`) are a WER of at most 6% on
`librispeech` and at most 10% on `poem`.

## Before and After

Measured on Windows x64 in Release with the default options (blank penalty 1.0, endpoint rules 1.2 s, 2.4 s and 20 s).
"Before" is the engine at the start of this work: zero log-mel rows for padding, an input-clock quiet endpoint with a
model reset, and a blank penalty of 3.0.

| Scenario | Before: WER | Before: segments / invented | After: WER | After: segments / invented |
| --- | --- | --- | --- | --- |
| `librispeech` | 7.50% | 24 / 16 | 3.39% | 24 / 0 |
| `poem` | 7.84% | 1 / 5 | 2.94% | 8 / 0 |
| `paused` | 8.42% | 3 (4 expected) / 0 | 1.05% | 4 / 0 |
| `continuous` | 2.86% | 1 / 1 | 1.43% | 3 / 0 |
| `quiet` | 8.75% | 24 / 23 | 3.75% | 24 / 0 |

The invented words of the "before" column came from the all-zero log-mel rows that padded the last chunk: the model
hears them as loud sound and answered with invented words after the last spoken word (16 of them on the LibriSpeech
subset). Padding with
the features of real silence removed all of them. The single "before" segment of the poem shows that the input-clock
endpoint never split read speech, whose pauses the silence-run limiter shortens.

## Blank Penalty Sweep

The default blank penalty was chosen with `--engine-option BlankPenalty=<value>` after the padding, endpoint and
feature-extraction changes were complete. Selection rule: the lowest `librispeech` WER, subject to zero invented words
in every scenario and a `quiet` WER no more than 1 point worse than at 3.0.

| Blank penalty | `librispeech` | Segments | `poem` | `paused` | `continuous` (segments) | `quiet` |
| --- | --- | --- | --- | --- | --- | --- |
| 0 | 3.75% | 25 | 2.94% | 3.16% | 2.86% (4) | 3.75% |
| 0.5 | 3.39% | 25 | 2.94% | 2.11% | 2.14% (4) | 3.57% |
| **1.0** | **3.39%** | **24** | **2.94%** | **1.05%** | **1.43% (3)** | **3.75%** |
| 1.5 | 3.57% | 24 | 2.94% | 1.05% | 1.43% (3) | 4.11% |
| 2.0 | 3.75% | 24 | 2.94% | 1.05% | 1.43% (3) | 4.29% |
| 2.5 | 3.75% | 24 | 2.94% | 1.05% | 1.43% (4) | 5.00% |
| 3.0 | 4.82% | 25 | 2.94% | 1.05% | 2.14% (4) | 5.54% |

No value invented any words. 0.5 and 1.0 tie on the LibriSpeech subset (19 errors in 560 words). 1.0 is the default:
it finalizes exactly one result per LibriSpeech utterance (0.5 reports 25 finals for the 24 utterances), gives the
lowest WER on the paused and continuous scenarios, and differs from 0.5 on quiet speech by one word.

## Front-End Parity

The feature extractor was changed to the semantics of NeMo's front end (zero center padding, a symmetric Hann window and
a log guard of 2^-24). Measured with the engine fixes at a blank penalty of 3.0, the combined change cost one word on
the LibriSpeech subset (4.64% to 4.82%) and left the poem unchanged; the log guard alone moved quiet speech from 4.46%
to 5.54%. The change was kept because the guard is the value that NeMo and the model's own configuration
(`genai_config.json`, `log_eps`) use, and at the evaluated blank penalty quiet speech is at 3.75%.

## Limits of the Evidence

- The data set is small (24 read utterances and one recitation, all clean English); it guards against regressions and
  invented words, not against every accent, microphone or noise condition.
- The int4 kernels of ONNX Runtime differ slightly between processors and operating systems, so a word can change from
  one machine to another. The gates leave a margin for that, and the real-model tests run on Windows, Linux and macOS
  in CI.
- Wall-clock time and the real-time factor are not recorded here: the measuring machine was shared, and the same run
  varied several-fold between repetitions.
