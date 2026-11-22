SPDX-License-Identifier: MIT AND CC-BY-4.0

# License of the DemaConsulting.Speech.Onnx.Piper Package

A NuGet package declares its license either as an SPDX expression or as a license file, never both.
This package names this file as its license because the voice it installs is under a license other than
MIT: the license that applies to the voice travels with the package, and the attribution that license
requires is in `THIRD-PARTY-NOTICES.md` at the root of this package.

The SPDX expression on the first line lists every license that applies to the code this package ships
and to the voice this package downloads and installs on demand: MIT for the code, and CC-BY-4.0 for the
voice. The package embeds no data of its own. The English text analysis and the pronunciation data
(derived from the CMU Pronouncing Dictionary) are not part of this package: they come from the
`DemaConsulting.Speech.Text.En` and `DemaConsulting.Speech.Phonemes.En` packages that this package
depends on, and those packages carry their own license files.

## Code: MIT License

All source code of this package, and of the rest of the DemaConsulting.Speech repository, is
licensed under the MIT License:

MIT License

Copyright (c) 2026 DEMA Consulting

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.

## The Piper Voice: CC BY 4.0

The voice that the model of this package installs (`en_US-libritts_r-medium`, a Piper text-to-speech
voice trained on the LibriTTS-R corpus) is licensed under the Creative Commons Attribution 4.0
International license (CC BY 4.0, <https://creativecommons.org/licenses/by/4.0/>). The MIT License
above does not cover it. The voice files are not part of this package. They are downloaded on demand,
and the model declares their license through the `LicenseName` and `LicenseUrl` properties of
`ISpeechModel`.

CC BY 4.0 is an attribution license: if you redistribute the voice, or share material that you made
with it in a way that the license's attribution requirement covers, you must give the credit that is
reproduced in `THIRD-PARTY-NOTICES.md`.

**Lineage.** The voice's model card states that it was fine-tuned from the English `lessac` medium
voice, which was trained on the Blizzard Challenge 2013 Lessac data. That data is licensed for
non-commercial research use only. You must review the upstream terms and decide whether the voice
fits your use.

See `THIRD-PARTY-NOTICES.md` for the attribution, the lineage and the provenance of the voice.
