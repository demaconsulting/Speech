### PhonemeStream

**Purpose**: Provide the layout that both renderers share and the splitter that cuts a long text into parts of a bounded
size, so that the two voices cannot disagree on spacing or on where a text is cut.

**Data Model**: stateless static helpers. Constants: the smallest part limit a caller may ask for (16 symbols), the
reserve kept free in every part (10 symbols) and the clause marks after which a text is preferably cut
(`, ; : . ! ? — …`). The cost of an item is its number of Unicode scalar values plus one for the space that separates
it. `WordShape` holds the helpers over a span of phones that the sound rules of both renderers share: the ordinal of the
vowel with the primary stress, the vowel count, the symbol and the vowel test at an index, the flap test (a `t` between
a vowel or `r` and an unstressed vowel) and the test for an inflection ending.

**Key Methods**:

- **Join(pieces, items, openingMarks, standaloneMarks, attached)**: concatenates the rendered pieces of the items: an
  item with an empty piece adds nothing; a word is separated from the previous output by one space unless it follows an
  opening mark or is attached to the word before it (a weak-form group); a closing mark attaches to the word before it;
  an opening mark gets a space before it and none after; a standalone mark (a dash) stands between spaces
- **StartsWithVowel(items, index)**, **NextWordText(items, index)**: what the next word is, which some weak forms depend
  on
- **Split(items, pieces, maxSymbols, render)**: with the budget `maxSymbols` minus the reserve, takes as many items as
  fit; when that is not all of the rest, the part ends after the last clause mark in its second half if there is one,
  otherwise after the last word that fits; a single item longer than the budget is one part; the run of items is
  rendered by the renderer's own function, so the part is exactly what `Render` gives for that stretch, and a part that
  renders to nothing is omitted. The reserve exists because rendering a run can differ from the sum of the pieces by a
  few symbols at its edges (a linking `ɹ`, a different weak form of `the`)

**Error Handling**: the helpers do not validate their arguments (the renderers do, before calling them); an empty
sequence gives no part.

**Dependencies**: the result types of the PhonemizerSubsystem.

**Callers**: `EspeakIpaRenderer` and `MisakiRenderer`.
