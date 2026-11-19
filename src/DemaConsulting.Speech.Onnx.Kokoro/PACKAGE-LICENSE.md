SPDX-License-Identifier: MIT AND Apache-2.0

# License of the DemaConsulting.Speech.Onnx.Kokoro Package

A NuGet package declares its license either as an SPDX expression or as a license file, never both.
This package ships its own code together with data embedded from another project, under its own
license, so it names this file as its license: the license that applies to the embedded data travels
with the package, and the verbatim third-party license text is in `THIRD-PARTY-NOTICES.md` at the
root of this package.

The SPDX expression on the first line lists every license that applies to something shipped in this
package: MIT for the code, and Apache-2.0 for the tokenizer data. The English text analysis and the
pronunciation data (derived from the CMU Pronouncing Dictionary) are not part of this package: they
come from the `DemaConsulting.Speech.Text.En` and `DemaConsulting.Speech.Phonemes.En` packages that this
package depends on, and those packages carry their own license files.

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

## Data Embedded in This Package

This package embeds one data file as an assembly resource. The MIT License above does not cover
it; it is covered by the license described here.

- **`kokoro-v1.0-tokenizer.json`**: the Kokoro v1.0 phoneme vocabulary (`tokenizer.json`),
  embedded verbatim from the `onnx-community/Kokoro-82M-v1.0-ONNX` export of Kokoro-82M by
  hexgrad, which is licensed under Apache-2.0. The Apache-2.0 license text is in
  `THIRD-PARTY-NOTICES.md`.

The Kokoro model weights and voice files are not part of this package. They are downloaded on
demand, and their license is declared by the model itself through the `LicenseName` and
`LicenseUrl` properties of `ISpeechModel`.

See `THIRD-PARTY-NOTICES.md` for the attribution, the full third-party license texts, and the
provenance of the embedded data.
