### TsvTable

**Purpose**: Be the one place where a data file is read. The unit turns the bytes of one tab-separated file into rows of
cells and, while doing so, enforces the canonical file format, so that every later check can trust that the text it
sees is exactly the text on disk and that offsets mean the same on every platform.

**Data Model**: `TsvTable` is an immutable result holding the display `Name` of the file, whether it `Exists`, the
`Header` cells and the list of trustworthy `Rows`. `TsvRow` is a read-only record of the one-based `LineNumber` and the
`Cells`; a comment line is never a row. `TsvLayout` is a read-only record that tells the reader what to expect: the
exact `ExpectedHeader` (or any), whether comment lines are allowed (`AllowComments`, only the manifest has them),
whether the first line is a header (`HasHeader`) and the check name that findings are attributed to (`Check`, `format`
by default). The format it enforces is: valid UTF-8 (strict decoding, no replacement character), no byte order mark, no
carriage return, a final line feed, no empty line, the same number of columns in every line, and every line in
Unicode normalization form C.

**Key Methods**:

- **Load(path, name, report, layout)**: a missing file is one finding (`required file is missing`) and an empty table;
  otherwise it reads all bytes, reports and skips a leading byte order mark, and decodes the rest strictly (an invalid
  sequence is one finding and no rows, because the text after it cannot be trusted). It then reports a carriage return
  (with its line), an empty file and a missing final line feed, splits the text on the line feed, and walks the lines:
  a line not in normalization form C (`string.IsNormalized`) and an empty line are findings; a line that begins with a
  number sign is skipped when comments are allowed and is otherwise an ordinary line; every other line is split on the
  tab character only, with `string.Split('\t')`, so no quoting rule applies and a sentence that starts with a quotation
  mark keeps it and keeps its offsets. The first line fixes the column count; a line with another count is a finding and
  is left out of the rows, so the checks that follow can index cells safely. When a header is expected and the first
  line differs, the finding shows both and no row is returned.
- **ColumnIndex(column)**: the zero-based index of a header cell, or -1.

Every finding names the file and, where it has one, the one-based line.

**Error Handling**: a defect in the content is a finding in the `CheckReport` and the reader continues so one run
shows every defect; it never throws for content. Input and output exceptions other than a missing file (for example an
access error) propagate to `CheckDataCommand`, which reports them as an input error (exit code 2).

**Dependencies**: `CheckReport` (to record findings); the .NET base class library only.

**Callers**: `TextEnDataSet`, `DataManifest`, and the unit tests.
