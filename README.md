# AutoWrite

AutoWrite is a small Windows desktop app that types pasted text into the active field when the
global `F7` hotkey is pressed.

## Build

Run:

```powershell
.\build.ps1
```

The executable is written to:

```text
dist\AutoWrite.exe
```

The build uses the Windows .NET Framework C# compiler and does not install any packages.

The executable embeds the app icon from:

```text
assets\AutoWrite.ico
```

## Install in Start Menu

Run:

```powershell
.\install-start-menu.ps1
```

This creates a current-user Start Menu shortcut at:

```text
%APPDATA%\Microsoft\Windows\Start Menu\Programs\AutoWrite.lnk
```

After that, open Start and search for `AutoWrite`. Windows generally requires pinning to Start to be
done by the user, so pin it from the Start menu if you want it fixed there.

## Use

1. Open `dist\AutoWrite.exe`. 2. Paste text into AutoWrite. 3. Focus the destination text field in
another window. 4. Press `F7` to start typing. Press `F7` again to stop.

The `Type in 3s` button can also start typing after the configured delay.

## Calibrate typing

Click **Calibrate typing**, choose 30 or 60 seconds, and copy the report
passage from the beginning. The default is 60 seconds; the clock starts with
your first character. Use Backspace for corrections and Enter for paragraph
breaks. Paste, Tab and caret navigation are ignored. The passage follows and
marks your position. Printable AltGr characters are accepted.

Review your active typing speed, AutoWrite's expected speed, accuracy, and
each proposed setting. **Apply** updates the controls and saves immediately.
**Discard** or **Cancel** leaves settings unchanged. Sparse evidence keeps
the previous value or is labelled “few samples.” A poor passage match keeps
every setting and asks you to retry. Button delay and Keep target WPM remain
your preferences.

Calibration matches your ordinary letter-to-letter rhythm, rather than
slowing every key to fill the clock. Active speed runs from first to last
key; gaps of ten seconds or longer are omitted and listed in the results.
Shorter hesitations remain counted. AutoWrite may have a different overall
speed because it adds pauses and corrections at the proposed or kept values.

Copying is not composing. Thinking pauses are not measured unless the test
contains at least four long pauses; otherwise they stay at their previous
values. A single minute can be a fast or a slow minute, so rerun the test if
the measured speed looks wrong.

AutoWrite cannot make its ordinary motor gap longer than about 220 ms.
In the slow-letter example on this passage that means roughly **46 WPM**;
below that it types faster than the person. Other settings affect the limit.
When this limit prevents matching your rhythm, results propose the closest
achievable target and show both speeds. The fast motor limit is 45 ms.

Synthetic tests suggest a minute gives a closer rhythm match than half a
minute. The table shows typical (median) and 90th-percentile errors across
three timing models; these are simulation results, not a promise about a
person. Expected speed assumes thinking pauses already match the person;
keeping different thinking settings can increase its difference.

| Test | Rhythm error, median / 90th percentile | Expected speed error, median / 90th percentile |
| --- | --- | --- |
| 30 seconds | 3.7–6.2% / 5.5–9.7% | 5.5–12.6% / 15.8–22.2% |
| 60 seconds | 1.0–2.8% / 2.4–9.5% | 2.6–4.3% / 7.3–12.6% |

The match is closest on report-like text such as the passage. On plain prose
AutoWrite runs somewhat slower than you at the same finger speed; turning
**Keep target WPM** off narrows that difference.

[Accuracy measurements and estimation methods](docs/calibration-accuracy.md)
include test-to-test variation, individual setting errors and cross-text limits.

## Settings

- `Target WPM`: target typing speed. AutoWrite uses the common typing-test convention where one word is five characters, including spaces and punctuation. `80 WPM` is about `6.7 characters/sec`.
- `Button delay s`: delay used by the button start mode.
- `Jitter %`: log-normal random spread around the target character delay.
- `Key-pair rhythm %`: adds simple QWERTY pair timing. Alternating hands type a little faster; same-hand and repeated-key pairs type a little slower.
- `Burst drift %`: creates short faster/slower runs over several characters.
- `Word pause ms`: extra pause after spaces.
- `Sentence pause ms`: extra pause after `.`, `!`, and `?`.
- `Thinking pause %`: chance of adding a longer pause at word or punctuation boundaries.
- `Thinking max ms`: maximum length of those longer pauses.
- `Keep target WPM`: when enabled, symbol-heavy text still aims for the target WPM while individual difficult keys pause longer.
- `Paragraph pause ms`: extra pause after a line break, in addition to the existing whitespace pause. Default `0` preserves existing timing. CRLF is one paragraph break.
- `Uppercase delay %`: extra motor delay before uppercase letters.
- `Number delay %`: extra motor delay before digits.
- `Punctuation delay %`: extra motor delay before punctuation and symbols that do not require Shift.
- `Shift-symbol delay %`: extra motor delay before symbols such as `@`, `?`, `:`, `"` and `_`.
- `Corrected mistakes`: enables temporary mistakes that are corrected before continuing.
- `Mistake rate %`: base target mistake rate over eligible letters, digits, punctuation, and symbols. The default `1%` plans an exact per-run count instead of rolling independently for every key.
- `Number mistakes x`: multiplier applied to the base mistake rate for digits.
- `Punct. mistakes x`: multiplier applied to the base mistake rate for punctuation and non-shift symbols.
- `Shift mistakes x`: multiplier applied to the base mistake rate for shifted symbols. Shift-symbol mistakes often appear as missed-Shift corrections, such as `?` becoming `/` before repair.
- `Duplicate share %`: share of planned mistakes that should be duplicate-key mistakes. The tested default is `0` because nearby-key substitutions are more reliable across target editors.
- `Transpose share %`: share of planned mistakes that should be adjacent-character transpositions. The tested default is `0`.
- `Notice delay ms`: pause before correcting a visible mistake.
- `Detect after chars`: maximum number of additional characters to type before noticing a mistake. Word and punctuation boundaries can trigger earlier correction.
- `Repair key pause ms`: pause around corrective backspaces and retyped characters.
- `Resume pause ms`: pause after the correction is complete.

Settings are stored at:

```text
%APPDATA%\AutoWrite\settings.ini
```

AutoWrite runs as the current user. Windows blocks lower-privilege apps from sending input into
elevated administrator windows, so run AutoWrite elevated only when the destination app is also
elevated.

## Diagnostics

Tests and probes can override the data directory with the absolute-path environment variable
`AUTOWRITE_DATA_DIR`. The override covers settings, telemetry and calibration captures. Normal use
retains the location above. Do not point diagnostics at the real settings directory.

```powershell
.\test.ps1 -OutputDirectory "$env:TEMP\autowrite-tests"
.\e2e.ps1 -OutputDirectory "$env:TEMP\autowrite-evidence"
.\smoke.ps1 -OutputDirectory "$env:TEMP\autowrite-probes"
```

Run `build.ps1` first for the last two commands. `test.ps1` compiles the app sources and the console
assert harness with the same in-box 64-bit .NET Framework compiler, prints each result and totals,
and returns nonzero on failure. It includes the real engine's seeded virtual-clock recovery
benchmark. Without an output argument it creates an isolated temporary directory.
`AUTOWRITE_TEST_SCOPE=estimators` explicitly skips the expensive recovery benchmark for focused debugging
and reports that skip; leave it unset for the complete test suite.

`e2e.ps1` drives the built `dist\AutoWrite.exe` through UI Automation and window messages, never
through global input. Its windows are shown without activation; it verifies the foreground stays
unchanged, selects 30 seconds from the default 60, enters the actual passage, reads all proposals,
exercises Discard, Apply and Cancel, and checks every main control and saved value. PNGs contain
only the app's own windows, captured with `PrintWindow`. The test enables
`AUTOWRITE_DIAGNOSTIC_NOACTIVATE=1`, which also suppresses the F7 registration; this environment
switch is inert in normal use.

`smoke.ps1` runs the existing `--sendinput-probe` (one Shift press); exit `0` means Windows accepted
`SendInput`. The separately explicit command below performs a live text check. It temporarily
activates only a window the probe creates, types `AutoWrite: aB [12] (3.5%)?` and a second line
`OK`, checks the exact text, then closes. It stops if its window loses the foreground. An internal
watchdog and an external 15-second process timeout bound it. Do this only when the temporary focus
change is acceptable:

```powershell
.\smoke.ps1 -OutputDirectory "$env:TEMP\autowrite-live" -LiveTyping
.\smoke.ps1 -OutputDirectory "$env:TEMP\autowrite-idle" -LiveTyping -RequireIdleSeconds 240
```

The fixed-seed check deliberately makes and repairs mistakes and reports the number of Backspaces
sent. Received text is never printed.

That invokes `--typing-smoke-probe`, an otherwise inert diagnostic switch. The optional idle
precondition uses `GetLastInputInfo` in the current Windows session before creating a window. If
idle time is below the requested seconds, it sends nothing and returns `3`, `skipped: user active`,
with the measured idle time. It does not wait for the desktop to become idle. Without that option
the check starts immediately, for an owner launching it from their terminal. If its own window
cannot acquire foreground within 500 ms, it exits `2` without typing; foreground loss or timeout
also returns `2`. The app switch accepts `--require-idle-seconds N`; malformed options return `4`.
The isolated diagnostic directory contains `live-probe.txt` with the outcome and elapsed time, and
the script prints it. A mismatch reports expected length, received length and first differing index
only; received text is never saved or printed. The calibration and virtual-engine test suites send
no global input. Diagnostics close the processes they create and use isolated data directories.

Each run also writes telemetry to:

```text
%APPDATA%\AutoWrite\telemetry\autowrite-YYYYMMDD-HHMMSS-fff.tsv
```

The telemetry log records every key event, correction, pause, source index, intended character, sent
character, elapsed timestamp, and gap since the previous emitted key. Use it to inspect actual WPM,
correction count, pause length, and whether a target editor received the final text correctly.

## Timing Data Notes

Calibration learns speed, rhythm, boundary pauses and correction habits from one transcription test.
The initial defaults remain a practical approximation based on public typing research:

- Aalto University's 136M Keystrokes study reports data from 168,000 volunteers and highlights large individual variation, hand/finger pair effects, rollover behavior, and error differences between faster and slower typists.
- The CMU Keystroke Dynamics benchmark provides hold-time and flight-time fields for 51 subjects typing the same password 400 times.
- The PhysioNet Tappy dataset includes real-world keystroke logs with hold time, latency, flight time, and left/right hand direction fields.

Useful starting profile for fast human-style drafting:

```text
Target WPM: 80
Jitter %: 55
Key-pair rhythm %: 80
Burst drift %: 30
Word pause ms: 120
Sentence pause ms: 850
Paragraph pause ms: 0
Thinking pause %: 4
Thinking max ms: 2200
Keep target WPM: On
Uppercase delay %: 10
Number delay %: 25
Punctuation delay %: 30
Shift-symbol delay %: 55
Mistake rate %: 1
Number mistakes x: 1.75
Punct. mistakes x: 2.00
Shift mistakes x: 3.00
Duplicate share %: 0
Transpose share %: 0
Notice delay ms: 850
Detect after chars: 3
Repair key pause ms: 120
Resume pause ms: 260
```

Timing model notes:

- `Target WPM` is calculated as five-character words over full elapsed time, including pauses and corrections.
- Long thinking pauses are planned at run start and included in the base-delay math so that random long pauses do not pull the final speed far below the target.
- Difficult-key delay penalties are also included in the base-delay math when `Keep target WPM` is enabled, so numbers and symbols look slower locally without dragging the whole run far below target.
- Mistake planning uses weighted candidates, so digits, punctuation, and shifted symbols can receive a larger share of the planned corrections.
- Corrections are delayed: AutoWrite can type a few more characters, pause, backspace the affected run, retype it correctly, and then pause before resuming.
- The tested default uses nearby-key substitutions and missed-Shift substitutions because those paths are reliable in Notepad.

Public references:

- Aalto 136M Keystrokes: https://userinterfaces.aalto.fi/136Mkeystrokes/
- Aalto CHI 2018 paper PDF: https://userinterfaces.aalto.fi/136Mkeystrokes/resources/chi-18-analysis.pdf
- Cambridge summary of the same 136M-keystroke study: https://www.cam.ac.uk/research/news/what-makes-a-faster-typist
- Pause thresholds in spontaneous writing: https://www.jowr.org/jowr/article/view/682
- CMU Keystroke Dynamics benchmark: https://www.cs.cmu.edu/~keystroke/
- PhysioNet Tappy keystroke data: https://physionet.org/content/tappy/1.0.0/
