# Reference data

`reference/data` holds output of the Windows build. A port to another platform is
correct when it reproduces these files from the same inputs. Nothing here comes
from a person: every recording is simulated.

## Keeping it current

```powershell
.\reference.ps1
```

rewrites `reference/data` from the current Windows sources, using the same in-box
compiler as the build. `test.ps1` contains a test that fails when the committed
files no longer match the Windows build. When that happens the behaviour of the
engine, the analysis or a file format has changed: regenerate, commit the new
files with the change, and make every port match them again.

## Conventions

- Files are ASCII with LF line endings. Lines beginning with `#` in the `.tsv`
  index files are comments. Columns are separated by one tab.
- **Escaped text.** `\\` is a backslash, `\t` a tab, `\n` LF, `\r` CR, and
  `\uXXXX` one UTF-16 code unit. Every code unit outside printable ASCII is
  written as `\uXXXX`, so a character above U+FFFF appears as its two surrogate
  halves. The Windows build works on UTF-16 code units throughout: text length,
  positions, progress counts and per-key decisions are all per code unit.
- **Settings.** `Name=value;Name=value;...` with the 26 settings in stored order.
  Booleans are `True` and `False`.
- **Numbers.** Values written with full precision use the shortest form that
  .NET prints (`R`). Compare them with a small relative tolerance (1e-9 is
  ample); everything else, including all text shown to the user, is exact.
  Values given as 16 hex digits are the IEEE-754 bits of a double.
- The Windows build formats a few numbers with the user's regional settings.
  The reference was generated with `.` as the decimal separator.

## Files

| File | What it pins down |
| --- | --- |
| `random.tsv` | The random-number sequences of `System.Random` (.NET Framework 4) for ten seeds. The engine's plan for a run is a function of this sequence. |
| `format.tsv` | How the Windows build prints and rounds numbers: fixed decimals (`F1`, `F2`, `F3`), `Math.Round`, and integer casts, including the tie cases. |
| `passage.txt` | The calibration passage, escaped, on one line. |
| `settings-ranges.tsv` | Each setting's name, label, minimum, maximum and default. |
| `engine.tsv`, `engine-logs.tsv` | 235 runs of the typing engine on a virtual clock: settings, text and seed in, every key and its time out. Ids `g00` to `g26` are the golden cases of `tests/GoldenTests.cs`, in the same order. |
| `telemetry/t*.tsv` | The telemetry file of six runs. The wall-clock columns are replaced: `utc_iso` by `T`, `elapsed_ms` by `E`, and `gap_ms` by `G` where present. |
| `settings-load.tsv` | Settings files, including damaged and legacy ones, and the settings each one loads as. |
| `settings-load-bytes.tsv` | The same for files given as raw bytes: other encodings and invalid UTF-8. |
| `settings-save.tsv` | The file text written for given settings. |
| `capture-sessions.txt` | The recording rules of the calibration test: which keys count, when the test ends, and the saved recording. |
| `alignment.tsv` | How typed text is matched to the passage, which drives the position marker and the accuracy figure. |
| `analysis/index.tsv` | The list of analysis cases. |
| `analysis/<id>.capture.tsv` | A recording exactly as the app saves it. Its `# setting` lines are the settings in force when it was made, and its `character` rows are the keys. |
| `analysis/<id>.expected.tsv` | What the analysis makes of that recording: the figures, the summary, warning and activity lines of the results window, one `estimate` row per setting (name, label, current, proposed, measured, closest achievable, reason), and the proposed settings. |

## Engine log format

`engine-logs.tsv` has one row per case: the id, then one `time:code;` entry per
key the engine sent. `time` is milliseconds on the virtual clock and `code` is
the UTF-16 code unit sent, with `8` for Backspace and `10` for Enter. The
SHA-256 of that string (UTF-8) is the hash column of `engine.tsv`. The virtual
clock starts at zero and advances only by the pauses the engine asks for; keys
take no time.

## Behaviour of the Windows build that a port has to reproduce

These are properties of .NET that the reference depends on. They are listed
because another language's defaults differ.

- `Math.Round` rounds halves to the even neighbour. Casts to `int` truncate.
- `ToString("F1")` and similar round halves away from zero after reducing the
  value to 15 significant digits. `0.25` prints as `0.3` and `2.0005` to three
  decimals as `2.001`. See `format.tsv`.
- Character tests (`Char.IsWhiteSpace`, `IsControl`, `IsLetterOrDigit`,
  `IsDigit`, `IsUpper`, `IsPunctuation`, `IsSymbol`) apply to one UTF-16 code
  unit and follow Unicode general categories. `IsDigit` is decimal digits only.
  A surrogate half is none of these. White space is the space, line and
  paragraph separators plus U+0009 to U+000D, U+0085 and U+00A0; U+180E and
  U+200B are not white space.
- `Int32.TryParse` accepts an optional sign, ASCII digits, surrounding white
  space and trailing NUL characters, and rejects anything that overflows.
  `Boolean.TryParse` accepts `true` and `false` in any case with surrounding
  white space and NUL characters, and nothing else.
- A conversion from a floating-point value that does not fit an `int` gives
  the lowest `int`. The legacy `charactersPerSecond` key can reach this.
- Settings keys are compared without regard to case for ASCII letters only. A
  key containing U+212A, U+0131 or U+017F does not match.
- Settings files are read as UTF-8, or as UTF-16 when they begin with a UTF-16
  byte-order mark. A UTF-8 byte-order mark is skipped and invalid bytes become
  U+FFFD. Lines are split on CR LF, LF or CR only, and each line is trimmed of
  white space as defined above.
- Exact floating-point equality is used where the analysis looks for tied
  forecasts, so the order of arithmetic operations matters there.

## Where a port may differ

The Windows build classifies characters by Unicode 8.0. A character assigned
later, such as U+20BF, is unassigned to it: never a letter, digit, symbol or
mistake target. Its case mapping is also narrower than Unicode's: U+0130,
U+1E9E, U+212A, U+212B, U+2126 and U+01C5, among others, map to themselves.
Case mapping is used only to decide whether two neighbouring characters are the
same key.

A port that uses its platform's current Unicode data will differ for those
characters in rhythm class and mistake eligibility, never in the text typed.
That difference is accepted, and the reference texts avoid depending on it.
