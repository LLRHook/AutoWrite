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
| `engine.tsv`, `engine-logs.tsv` | 223 runs of the typing engine on a virtual clock: settings, text and seed in, every key and its time out. Ids `g00` to `g26` are the golden cases of `tests/GoldenTests.cs`, in the same order. |
| `telemetry/t*.tsv` | The telemetry file of six runs. The wall-clock columns are replaced: `utc_iso` by `T`, `elapsed_ms` by `E`, and `gap_ms` by `G` where present. |
| `settings-load.tsv` | Settings files, including damaged and legacy ones, and the settings each one loads as. |
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
  `IsDigit`, `IsUpper`, `IsPunctuation`, `IsSymbol`) and `ToLowerInvariant` /
  `ToUpperInvariant` apply to one UTF-16 code unit and follow Unicode general
  categories. `IsDigit` is decimal digits only. A surrogate half is none of
  these.
- `Int32.TryParse` accepts an optional sign, ASCII digits and surrounding
  white space, and rejects anything that overflows. `Boolean.TryParse` accepts
  `true` and `false` in any case with surrounding white space, and nothing else.
- Settings files are read as UTF-8 with a byte-order mark skipped, split on CR
  LF, LF or CR, and each line is trimmed of Unicode white space.
- Exact floating-point equality is used where the analysis looks for tied
  forecasts, so the order of arithmetic operations matters there.
