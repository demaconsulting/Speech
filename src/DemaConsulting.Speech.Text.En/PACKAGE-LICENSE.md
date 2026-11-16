SPDX-License-Identifier: MIT

# License of the DemaConsulting.Speech.Text.En Package

A NuGet package declares its license either as an SPDX expression or as a license file, never both. This package
names this file as its license so that the license text and the dependency notice below travel with it. The
expression on the first line, `MIT`, is a hint for license scanners; this file is the license. It declares only the
license of what this package itself contains.

## Code: MIT License

All source code of this package, and of the rest of the DemaConsulting.Speech repository, is licensed under the MIT
License:

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

This package embeds two small lexicon tables of its own: a list of written-English abbreviations with their spoken
forms and a list of initialisms. DEMA Consulting wrote both from scratch for this package; no third-party list was
copied. They are released under the MIT License above, like the code, and the package embeds no third-party data. If
a later release embeds data from a third party (for example trained weights), this file and `THIRD-PARTY-NOTICES.md`
will describe it and state its license.

## Dependency: DemaConsulting.PartOfSpeech

This package requires the separate NuGet package `DemaConsulting.PartOfSpeech`, which tags the words of a text with
their part of speech. That package is not part of this package and is not bundled in it: NuGet restores it next to this
one, and it carries its own license files. Its code is licensed under the MIT License; it embeds a lexicon and trained
weights that come from third parties and are licensed under other licenses (CC BY 4.0, CC BY 3.0 US), which apply to
that package and not to this one. `THIRD-PARTY-NOTICES.md` at the root of this package describes that dependency and
reproduces its attribution notices for your convenience. Follow the license files of `DemaConsulting.PartOfSpeech`
for any copy of that package that you redistribute.
