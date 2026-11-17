# Third-Party Notices for the DemaConsulting.Speech.Text.En Package

## Scope of This File: Embedded Model Data and a Dependency Notice

This file has two parts. The first is a notice about **content of this package**: the trained heteronym model that the
assembly embeds was trained on sentences adapted from the Manually Annotated Sub-Corpus (MASC), and the attribution
that the license of MASC requires is given below. The second is a notice about a **dependency**: this package requires
the separate NuGet package `DemaConsulting.PartOfSpeech`, a package of DEMA Consulting that is restored next to this
one. That package carries its own license files and embeds data adapted from the third-party works below. This file
reproduces the attribution and license notices of those works, as their licenses require of that package, for your
convenience and so that anyone who installs this package can see what its dependency contains. The licenses of the
works that only the dependency embeds (Open English WordNet and Princeton WordNet) apply to
`DemaConsulting.PartOfSpeech`, not to this package.

The assembly of this package contains compiled code, two small lexicon tables (abbreviations and initialisms) and the
two tables of the reading inventory (readings and forms) that DEMA Consulting wrote from scratch, the lexicon tables
under the MIT License (see `PACKAGE-LICENSE.md`) and the inventory tables under CC0-1.0, and the trained heteronym
model (`heteronyms.bin`) described next.

The MIT License in the repository `LICENSE` file, and in `PACKAGE-LICENSE.md`, covers only the code of this
repository. It does not cover the data described here.

The notices below are reproduced from the `THIRD-PARTY-NOTICES.md` of `DemaConsulting.PartOfSpeech`, with the
statement of the changes extended for the data of this package. Paths such as `data/masc/` refer to the repository of
that package, <https://github.com/demaconsulting/PartOfSpeech>, not to this repository. The complete notices of that
package, including courtesy notices for sources that do not require attribution, are shipped in that package.

## Data Embedded in This Package

The package embeds one trained model, `heteronyms.bin`: the weights with which the analyzer decides the reading of a
heteronym such as *record* from its sentence. It was trained on labeled sentences from three license classes, listed
here for the 19,865 training rows (18,935 distinct sentences; a further 3,235 development rows were used only to choose
the settings of the training):

| License class of the sentence | Training rows | Source |
| --- | ---: | --- |
| CC BY 3.0 US | 2,328 | adapted from MASC (below) |
| Public domain | 3,860 | works of the United States federal government and Project Gutenberg texts |
| CC0-1.0 | 13,677 | sentences written by language models for this purpose, and CC0 sentences of the Tatoeba project |

The weights are numbers that a training program computed from these sentences and their labels; the model contains no
sentence text. Because the weights were trained in part on data adapted from MASC, they are provided under the terms
of the MASC license (CC BY 3.0 US), with the attribution below. The labels, that is the reading of each word, were
chosen by rules from part-of-speech tags or by language models and audited blind by a second model; DEMA Consulting
dedicated all content that it generated or labeled to the public domain under CC0 1.0 (the Creative Commons CC0 1.0
Universal public-domain dedication). No human reviewed the model-chosen readings. The statement of the data, with the
license of every sentence, is `data/text-en/README.md` and `data/text-en/ATTRIBUTION.md` in the repository of this
package. The tables of the reading inventory (`readings.tsv` and `forms.tsv`) and the two lexicon tables were written
by DEMA Consulting, the inventory under CC0-1.0 and the lexicon tables under the MIT License; see `PACKAGE-LICENSE.md`.

## The Manually Annotated Sub-Corpus (MASC)

The heteronym model that this package embeds, and the lexicon and the trained weights that `DemaConsulting.PartOfSpeech`
embeds, are adapted in part from MASC.

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
- **Changes:** the work was adapted by DEMA Consulting. The Penn Treebank part-of-speech tags and tree positions of
  every token were converted to Universal POS (UPOS) tags; the empty-category traces were removed, quotation marks and
  dashes were normalized, the sentences were reduced to those of 3 to 70 tokens, and the spoken genre and a few
  documents and sentences that overlap evaluation data were left out. The converted data is not the original MASC data.
  The trained weights of the tagger (`weights.bin`) were learned in part from the converted data: they are numbers that
  a training program computed from the tags and the text of the converted sentences, and they contain no MASC text. The
  weights are provided under the terms of this license. The authors of MASC do not endorse this project or the converted
  data. The full statement is `data/masc/ATTRIBUTION.md` in the repository of `DemaConsulting.PartOfSpeech`.
- **Changes for the heteronym model of this package:** DEMA Consulting adapted those converted sentences further. From
  them, the sentences that contain a word of the reading inventory were selected, and each such word was labeled with
  its reading (by rule from the part-of-speech tag, or by a language model whose choices were audited blind by a second
  model) and given character offsets; sentences that overlap an evaluation set were left out; and the documents were
  assigned to the training, development and holdout splits. The sentences are kept as their tokens joined by single
  spaces. The heteronym weights (`heteronyms.bin`) were learned in part from these adapted sentences: they are numbers
  that a training program computed from the labels and from features of the sentences, and they contain no MASC text.
  The weights are provided under the terms of this license. The authors of MASC do not endorse this project or the
  labeled data. The full statement is `data/text-en/ATTRIBUTION.md` in the repository of this package.

## Open English WordNet

The spelling-to-UPOS lexicon that `DemaConsulting.PartOfSpeech` embeds is derived from the word forms of Open English
WordNet.

- **Title:** Open English WordNet 2025 Edition
- **Creators:** the Open English WordNet team and contributors (John P. McCrae and others)
- **Source:** <https://en-word.net> and <https://github.com/globalwordnet/english-wordnet>
- **License:** Creative Commons Attribution 4.0 International (CC BY 4.0),
  <https://creativecommons.org/licenses/by/4.0/>
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
