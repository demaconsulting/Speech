# Third-Party Notices for the DemaConsulting.Speech.Text.En Package

## Scope of This File: a Dependency Notice

This file is a notice about a **dependency**, not about content of this package. The package
`DemaConsulting.Speech.Text.En` embeds no third-party data in this release: its assembly contains only compiled code
under the MIT License (see `PACKAGE-LICENSE.md`). It does require the separate NuGet package
`DemaConsulting.PartOfSpeech`, a package of DEMA Consulting that is restored next to this one. That package
carries its own license files and embeds data adapted from the third-party works below. This file reproduces the
attribution and license notices of those works, as their licenses require of that package, for your convenience and so
that anyone who installs this package can see what its dependency contains. The licenses below apply to
`DemaConsulting.PartOfSpeech`, not to this package. Later increments of this package may embed data of their own; this
file will then be revisited.

The MIT License in the repository `LICENSE` file, and in `PACKAGE-LICENSE.md`, covers only the code of this
repository. It does not cover the data described here.

The notices below are reproduced from the `THIRD-PARTY-NOTICES.md` of `DemaConsulting.PartOfSpeech`. Paths such as
`data/masc/` refer to the repository of that package, <https://github.com/demaconsulting/PartOfSpeech>, not to this
repository. The complete notices of that package, including courtesy notices for sources that do not require
attribution, are shipped in that package.

## Known Limitation of the Dependency: Text Retention

`DemaConsulting.PartOfSpeech` 0.1.0-beta.4 keeps the characters of the tokens it tags in a per-thread working buffer
that it never clears, so words analyzed on a thread can remain recoverable from the process memory after the request
ends. This package keeps no reference to the analyzed text or to its results after a call returns, but it cannot clear
the dependency's private buffer, so `EnglishTextAnalyzer` currently does not meet the core `ITextAnalyzer` contract
that an analyzer must not retain the text. The issue is tracked upstream at
<https://github.com/demaconsulting/PartOfSpeech/issues/29>. For that reason this package is not yet published to
NuGet: it is gated until the dependency is fixed and the pin is bumped. A host that builds it from source and has a
strict requirement that no analyzed text outlive a request must account for this.

## Data Embedded in This Package

None in this release. This package embeds no data of its own; its assembly contains only compiled code. This section
will describe any data that a later release embeds.

## The Manually Annotated Sub-Corpus (MASC)

The lexicon and the trained weights that `DemaConsulting.PartOfSpeech` embeds are adapted in part from MASC.

- **Title:** The Manually Annotated Sub-Corpus (MASC), Penn Treebank syntax annotations of the written texts (release of
  2026-09-25)
- **Creators:** the MASC project: Nancy Ide (Vassar College, principal investigator), Collin Baker (International
  Computer Science Institute), Christiane Fellbaum (Princeton University) and Rebecca Passonneau (Columbia University);
  the texts are drawn from the Open American National Corpus (OANC)
- **Source:** <https://anc.org/data/masc/>; the archive used is
  <https://github.com/nancyide/anc-website/releases/download/datasets-2026-09-25/penn-treebank.tgz>
- **License:** Creative Commons Attribution 3.0 United States (CC BY 3.0 US),
  <https://creativecommons.org/licenses/by/3.0/us/>
- **Citation:** Nancy Ide, Collin Baker, Christiane Fellbaum, Charles Fillmore and Rebecca Passonneau, "MASC: the
  Manually Annotated Sub-Corpus of American English", Proceedings of the Sixth International Conference on Language
  Resources and Evaluation (LREC 2008), <https://aclanthology.org/L08-1105/>
- **Changes:** the work was adapted by DEMA Consulting. The Penn Treebank part-of-speech tags and tree positions of every
  token were converted to Universal POS (UPOS) tags; the empty-category traces were removed, quotation marks and dashes
  were normalized, the sentences were reduced to those of 3 to 70 tokens, and the spoken genre and a few documents and
  sentences that overlap evaluation data were left out. The converted data is not the original MASC data. The trained
  weights of the tagger (`weights.bin`) were learned in part from the converted data: they are numbers that a training
  program computed from the tags and the text of the converted sentences, and they contain no MASC text. The weights are
  provided under the terms of this license. The authors of
  MASC do not endorse this project or the converted data. The full statement is `data/masc/ATTRIBUTION.md` in the
  repository of `DemaConsulting.PartOfSpeech`.

## Open English WordNet

The spelling-to-UPOS lexicon that `DemaConsulting.PartOfSpeech` embeds is derived from the word forms of Open English
WordNet.

- **Title:** Open English WordNet 2025 Edition
- **Creators:** the Open English WordNet team and contributors (John P. McCrae and others)
- **Source:** <https://en-word.net> and <https://github.com/globalwordnet/english-wordnet>
- **License:** Creative Commons Attribution 4.0 International (CC BY 4.0), <https://creativecommons.org/licenses/by/4.0/>
- **Changes:** only the spellings of the entries and their inflected forms were used. They were combined with other
  sources and reduced to a table that maps a lower-case spelling to the UPOS tags it may carry. The lexicon is not the
  original Open English WordNet data, and the glosses, examples and relations are not part of it. The analyses table,
  which the same embedded resource carries, was derived from the same word forms (Open English WordNet and its
  rule-generated inflections, the Moby Part-of-Speech reverse forms, the additions of the WordSense project under the
  MIT License and the closed-class list under CC0 1.0) and adds no other source. For most spellings it records the
  lower-case base word (lemma), the UPOS tag and a subset of the morphological features of each form (Degree, Number,
  Person, Tense and VerbForm, written as `Name=Value` pairs joined by `|`); all other columns of the word forms were
  left out, so the table is not the original data either. Open English WordNet and its contributors do not endorse this
  project.

### Princeton WordNet

Open English WordNet is derived from Princeton WordNet under the WordNet License and developed further under CC BY 4.0.
Its `LICENSE.md` says: "You may share and adapt this resource providing attribution is given to both Princeton WordNet
and the Open English Wordnet team." The notice that Open English WordNet distributes as `WNDB_License.txt` is
reproduced here, as the license requires (Princeton University, <https://wordnet.princeton.edu>):

```text
This software and database is being provided to you, the LICENSEE, by
the Open English Wordnet team under the Creative Commons Attribution 4.0
International License (CC-BY 4.0).

Open English Wordnet 2023 Copyright 2023 by the Open English Wordnet team.

Permission to use, copy, modify and distribute this software and
database and its documentation for any purpose and without fee or
royalty is hereby granted, provided that you agree to comply with
the following copyright notice and statements, including the disclaimer,
and that the same appear on ALL copies of the software, database and
documentation, including modifications that you make for internal
use or for distribution.

WordNet 3.1 Copyright 2011 by Princeton University.  All rights reserved.

THIS SOFTWARE AND DATABASE IS PROVIDED "AS IS" AND PRINCETON
UNIVERSITY MAKES NO REPRESENTATIONS OR WARRANTIES, EXPRESS OR
IMPLIED.  BY WAY OF EXAMPLE, BUT NOT LIMITATION, PRINCETON
UNIVERSITY MAKES NO REPRESENTATIONS OR WARRANTIES OF MERCHANT-
ABILITY OR FITNESS FOR ANY PARTICULAR PURPOSE OR THAT THE USE
OF THE LICENSED SOFTWARE, DATABASE OR DOCUMENTATION WILL NOT
INFRINGE ANY THIRD PARTY PATENTS, COPYRIGHTS, TRADEMARKS OR
OTHER RIGHTS.

The name of Princeton University or Princeton may not be used in
advertising or publicity pertaining to distribution of the software
and/or database.  Title to copyright in this software, database and
any associated documentation shall at all times remain with
Princeton University and LICENSEE agrees to preserve same.
```

## Courtesy Notices

The lexicon also uses the part-of-speech data of the Moby Part-of-Speech List (Moby Part-of-Speech II) by Grady Ward,
<https://www.gutenberg.org/ebooks/3203>, which the author dedicated to the public domain, and corpora whose
annotations and selection are dedicated to the public domain under CC0 1.0. These sources do not require attribution;
the full courtesy notices are in the `THIRD-PARTY-NOTICES.md` of `DemaConsulting.PartOfSpeech`.
