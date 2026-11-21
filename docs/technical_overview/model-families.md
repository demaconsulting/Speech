# Model Families and the English Front End

The library is engine-neutral: a model package registers its models into a `SpeechModelCatalog`, and the core library
never names a concrete model. Only some models speak through the English front end. The core library offers the seam,
and each model package decides how to use it. A synthesis model opts into text analysis by returning an analyzer from
`ISynthesisModel.TextAnalyzer` (the default is none, and the text is then not analyzed). The annotations reach a
backend only if that backend implements the optional `IAnnotatedSynthesisBackend`, and the backend decides how to
interpret them; the core library does not require any particular use. The Piper and Kokoro packages return an
`EnglishTextAnalyzer`, and their backends implement `IAnnotatedSynthesisBackend` and phonemize each chunk with
`DemaConsulting.Speech.Phonemes.En`. That combination is what this document calls the DEMA English front end.

## Direct ONNX Voices on the DEMA English Front End

Two text-to-speech models run their voice graph directly on Microsoft's ONNX Runtime and use the whole English front
end. They are the models that the listening-improvement loop improves.

| | Piper | Kokoro |
| --- | --- | --- |
| Package | `DemaConsulting.Speech.Onnx.Piper` | `DemaConsulting.Speech.Onnx.Kokoro` |
| Model id | `piper-onnx-libritts-r-en-us-medium` | `kokoro-onnx-v1_0-en` |
| Registration | `.AddPiperModels()` | `.AddKokoroModels()` |
| Voices | 904 speakers (`speaker=0` to `903`) | 29 English voices (`voice=af_heart`) |
| Sample rate | 22,050 Hz | 24,000 Hz |
| Phoneme symbols | espeak-style IPA (Piper renderer) | Kokoro vocabulary (Kokoro renderer) |
| Voice license | CC BY 4.0 (LibriTTS-R, Piper) | Apache-2.0 |

Both packages share the text analysis and the pronunciation stage. They differ in three ways:

- **Renderer**: Piper takes espeak-style IPA, and Kokoro takes the symbols of its own vocabulary.
- **Framing**: each backend frames the phonemes for its voice graph in its own way.
- **Chunking**: the Piper model has its own capability profile, which joins the clause pieces that the core chunker
  splits at commas, semicolons and colons, so it speaks a whole sentence as one utterance; the voice swallows the first
  word of a very short utterance. Kokoro keeps the library's default chunking, so each clause is its own utterance.

The two packages, and the two front-end packages they depend on, are published only in pre-release versions.

## Other Direct ONNX Models

Two further models run directly on ONNX Runtime but do not use the English front end, so the loop does not apply to
them:

- **Chatterbox Turbo** (`DemaConsulting.Speech.Onnx.ChatterboxTurbo`) is a voice-cloning text-to-speech model. It reads
  text with its own byte-level BPE tokenizer rather than phonemes, and speaks in the voice of a reference clip.
- **Nemotron 3.5 streaming recognition** (`DemaConsulting.Speech.Onnx.NemotronStt`) is a speech-to-text model.
  Recognition has no text front end, so nothing in this document concerns it.

The Sherpa-hosted models of `DemaConsulting.Speech.Sherpa` are legacy, to be removed after a bridge release that ships
both them and the direct ONNX models; they use neither Text.En nor Phonemes.En, so the direct ONNX models on the DEMA
English front end are the path forward.
