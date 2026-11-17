# MASC Attribution

License: CC-BY-3.0-US

Rows of `train.tsv`, `dev.tsv` and `holdout-natural.tsv` whose `license` column is `CC-BY-3.0-US` (source `harvest-masc`
and `harvest-masc-llm`) carry a sentence adapted from the following work, which is licensed under the Creative Commons
Attribution 3.0 United States License.

- **Title:** The Manually Annotated Sub-Corpus (MASC), Penn Treebank syntax annotations of the written texts
  (release of 2026-09-25)
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

## Changes Made

The work was adapted by DEMA Consulting. The text of the MASC attribution above (title, creators, source, license,
citation) is the attribution that the PartOfSpeech project publishes for the same corpus (`data/masc/ATTRIBUTION.md` and
`THIRD-PARTY-NOTICES.md` there), unchanged. The changes:

- the Penn Treebank part-of-speech tags and tree positions of every token were converted to Universal POS tags, the
  empty-category traces were removed, the quotation marks and dashes were normalized, the sentences were reduced to
  those of 3 to 70 tokens, the spoken genre, five documents and 104 short sentences that overlap evaluation data were
  left out, and the sentences are kept as their tokens joined by single spaces (all in the PartOfSpeech project,
  `data/masc/ATTRIBUTION.md`);
- from those sentences, the ones that contain a word of the reading inventory were selected, and each such word was
  labeled with its reading (by rule from the part-of-speech tag, or by a language model whose choices were audited blind
  by a second model) and given character offsets;
- sentences that overlap an evaluation set were left out, and the documents were assigned to the training, development
  and holdout splits.

The texts themselves are MASC's. The authors of MASC do not endorse this project or the labeled data.
