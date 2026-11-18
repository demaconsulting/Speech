# Third-Party Notices for the DemaConsulting.Speech.Phonemes.En Package

This package embeds data that comes from the project listed below. This file reproduces its copyright notice and
license text, as its license requires, and states what was changed. The MIT License in the repository `LICENSE` file,
and in `PACKAGE-LICENSE.md`, covers only the code of this repository. It does not cover the data described here.

## CMU Pronouncing Dictionary (CMUdict)

- **Source**: <https://github.com/cmusphinx/cmudict> (`cmudict.dict`, SHA-256
  `81917843c7f44ce2b094ac63873c2c7a4cf802040792c455ba3ca406891c3d22`)
- **Copyright**: Copyright (C) 1993-2015 Carnegie Mellon University. All rights reserved.
- **License**: a BSD-style license (the BSD-2-Clause conditions plus an acknowledgment of funding), reproduced in
  full below.
- **Used in**: the embedded lexicon `cmudict-en.bin.gz`, and the reading table `reading-pronunciations.tsv`, whose
  rows for the heteronym readings of `DemaConsulting.Speech.Text.En` are either pronunciation variants copied from
  CMUdict (`cmu#N`), derived from a CMUdict entry by a named sound rule (`rule:<name>`) or written by DEMA Consulting
  from CMUdict and phonetic knowledge alone (`authored`).
- **Changes**: the dictionary was filtered to the entries whose word consists of the letters a to z and apostrophes
  (133,973 of its 135,166 entries), grouped by word with the numbered variants of a word (`word(2)`) stored as
  ordered pronunciation variants of that word, two repeated identical variants were dropped, and the result was
  converted to a compact binary form (one byte per ARPAbet phone, words sorted and front-coded, gzip-compressed).
  The pronunciations themselves are unchanged. No other dictionary is used.

The license text, as published by the CMUdict project:

```text
Copyright (C) 1993-2015 Carnegie Mellon University. All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions
are met:

1. Redistributions of source code must retain the above copyright
   notice, this list of conditions and the following disclaimer.
   The contents of this file are deemed to be source code.

2. Redistributions in binary form must reproduce the above copyright
   notice, this list of conditions and the following disclaimer in
   the documentation and/or other materials provided with the
   distribution.

This work was supported in part by funding from the Defense Advanced
Research Projects Agency, the Office of Naval Research and the National
Science Foundation of the United States of America, and by member
companies of the Carnegie Mellon Sphinx Speech Consortium. We acknowledge
the contributions of many volunteers to the expansion and improvement of
this dictionary.

THIS SOFTWARE IS PROVIDED BY CARNEGIE MELLON UNIVERSITY ``AS IS'' AND
ANY EXPRESSED OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO,
THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR
PURPOSE ARE DISCLAIMED.  IN NO EVENT SHALL CARNEGIE MELLON UNIVERSITY
NOR ITS EMPLOYEES BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT
LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
```
