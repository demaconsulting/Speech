SPDX-License-Identifier: MIT AND CC-BY-3.0-US

# License of the DemaConsulting.Speech.Text.En Package

A NuGet package declares its license either as an SPDX expression or as a license file, never both. This package
names this file as its license so that the license texts, the attribution and the dependency notice below travel with
it. The expression on the first line, `MIT AND CC-BY-3.0-US`, is a hint for license scanners; this file is the
license. It declares the license of what this package itself contains: the code and the lexicon tables are licensed
under the MIT License, the reading inventory is dedicated to the public domain under CC0-1.0, and the embedded
heteronym model is provided under the terms of the Creative Commons Attribution 3.0 United States License (CC BY 3.0
US) because it was trained on sentences adapted from a work under that license (see "Data Embedded in This Package").

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

This package embeds the following data.

- **Lexicon tables (MIT).** Two small tables of its own: a list of written-English abbreviations with their spoken
  forms and a list of initialisms. DEMA Consulting wrote both from scratch for this package; no third-party list was
  copied. They are released under the MIT License above, like the code.
- **Reading inventory (CC0-1.0).** Two small tables (`readings.tsv` and `forms.tsv`) that list the pronunciation
  readings of English heteronyms such as *record* and the written forms that can carry them. DEMA Consulting wrote
  these tables and released them under CC0-1.0 (the Creative Commons CC0 1.0 Universal public-domain dedication).
- **Heteronym model (CC BY 3.0 US terms).** The file `heteronyms.bin` holds the trained weights with which the analyzer
  decides which reading of a heteronym a word has in its sentence. The weights are numbers that a training program
  computed from labeled sentences; the model contains no sentence text. The 19,865 training rows (18,935 distinct
  sentences), of which the model is the result, come from three license classes: 2,328 rows adapted from the Manually
  Annotated Sub-Corpus (MASC), licensed under CC BY 3.0 US (the sentences, their part-of-speech tags and their
  selection come from MASC); 3,860 public-domain rows (works of the United States federal government and Project
  Gutenberg texts); and 13,677 rows under CC0-1.0 (sentences written by language models for this purpose, and CC0
  sentences of the Tatoeba project). The labels (the reading of each word) were chosen by rules from part-of-speech
  tags or by language models, and DEMA Consulting dedicated all content that it generated or labeled to the public
  domain under CC0 1.0. A development set of 3,235 rows of the same classes was used only to choose the settings of
  the training. Because
  the weights were trained in part on data adapted from MASC, they are provided under the terms of its license: you may
  share and adapt them, including commercially, provided you give the attribution in `THIRD-PARTY-NOTICES.md` and
  indicate the changes made. The authors of MASC do not endorse this project or the model.

`THIRD-PARTY-NOTICES.md` carries the attribution of MASC and the statement of the changes made. The data and the
program that trained the model are in the repository of this package (`data/text-en/` and
`tools/DemaConsulting.Speech.Text.En.Trainer/`); the model can be reproduced from them.

## Dependency: DemaConsulting.PartOfSpeech

This package requires the separate NuGet package `DemaConsulting.PartOfSpeech`, which tags the words of a text with
their part of speech. That package is not part of this package and is not bundled in it: NuGet restores it next to this
one, and it carries its own license files. Its code is licensed under the MIT License; it embeds a lexicon and trained
weights that come from third parties and are licensed under other licenses (CC BY 4.0, CC BY 3.0 US), which apply to
that package and not to this one. `THIRD-PARTY-NOTICES.md` at the root of this package describes that dependency and
reproduces its attribution notices for your convenience. Follow the license files of `DemaConsulting.PartOfSpeech`
for any copy of that package that you redistribute.
