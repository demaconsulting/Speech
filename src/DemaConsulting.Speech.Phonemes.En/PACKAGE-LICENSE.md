SPDX-License-Identifier: MIT AND LicenseRef-CMUdict

# License of the DemaConsulting.Speech.Phonemes.En Package

A NuGet package declares its license either as an SPDX expression or as a license file, never both. This package
names this file as its license so that the license texts and the attribution below travel with it. The expression on
the first line, `MIT AND LicenseRef-CMUdict`, is a hint for license scanners; this file is the license. It declares
the license of what this package itself contains: the code is licensed under the MIT License, and the embedded
pronunciation lexicon is derived from the CMU Pronouncing Dictionary, which is provided under the BSD-style license
of Carnegie Mellon University reproduced below (see "Data Embedded in This Package").

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

This package embeds one data file, `cmudict-en.bin.gz`: the words of the CMU Pronouncing Dictionary (CMUdict,
<https://github.com/cmusphinx/cmudict>) that consist of the letters a to z and apostrophes, with all of their
pronunciation variants, converted to a compact binary form (one byte per ARPAbet phone, words sorted and
front-coded, gzip-compressed). The pronunciations are those of CMUdict; the conversion changed only their
representation. CMUdict is licensed under the BSD-style license of Carnegie Mellon University, which requires that
the notice below be retained and reproduced with binary redistributions. `THIRD-PARTY-NOTICES.md` carries the same
notice together with the statement of the changes made.

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

The authors of CMUdict and Carnegie Mellon University do not endorse this project.
