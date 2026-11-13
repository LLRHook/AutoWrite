# AutoWrite

AutoWrite is a small Windows desktop app that types pasted text into the active field when the global `F7` hotkey is pressed.

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

After that, open Start and search for `AutoWrite`. Windows generally requires pinning to Start to be done by the user, so pin it from the Start menu if you want it fixed there.

## Use

1. Open `dist\AutoWrite.exe`.
2. Paste text into AutoWrite.
3. Focus the destination text field in another window.
4. Press `F7` to start typing. Press `F7` again to stop.

The `Type in 3s` button can also start typing after the configured delay.

## Calibrate typing

Click **Calibrate typing**, choose 30 or 60 seconds (default 60), and copy the passage. The timer starts with the first accepted typing key and rejects characters at or after the deadline. The intentionally dense report-style passage covers capitals, digits, punctuation, shifted symbols and two paragraph breaks within its first 100 characters; it is longer than 900 characters so a 180 WPM typist has enough text for a minute. Capitals and symbols occur both after boundaries and inside words to separate their delays from pauses.

Corrections are **Backspace only**. Held Backspace repeats delete one character per repeat, including batched repeat messages. Enter inserts one line break. Ctrl+Backspace, arrow/Home/End navigation, mouse caret moves, paste/cut/undo and Tab are ignored and counted; clicks can focus the input but cannot reposition its caret. Modifiers and ignored actions do not start the timer. You can cancel or close without changing settings. Starting automatic typing is disabled while the test or its results are open.

After the deadline, results show achieved WPM, accuracy, and all 26 settings with their current value, proposed value and measured/kept explanation. Accuracy is one minus corrected error episodes plus remaining alignment edits, divided by aligned passage characters; a corrected transpose is one edit. **Apply** updates every main control and saves `settings.ini` immediately. A failed save restores the controls and keeps the results open. **Discard** changes neither settings nor controls.

Raw captures are stored beside telemetry in `%APPDATA%\AutoWrite\calibration` (or the overridden data directory), with sub-millisecond `Stopwatch` timestamps, physical key-down records, character/Backspace/Enter records, ignored-action records and a header containing duration, completion status and ignored-key count. Physical modifiers are retained in the raw log; analysis uses logical character latencies, including the Shift combination. Coalesced repeat messages share their observed timestamp while preserving the repeat count. They are local files; nothing uploads them.

### How settings are estimated

The analyser receives only timestamped keys and the known passage. It replays deletions, aligns the visible text to the passage with insert/delete/substitute/transpose edits, identifies repairs, and excludes repair keys from ordinary motor timing. Latency **into** a key belongs to that key. A joint nonlinear fit separates incoming character class, QWERTY pair rhythm and outgoing boundary pauses; rare long boundary residuals are treated separately. It uses the engine's log-normal spread, 8?23-key bursts, correction timing and detection-distance distribution. Proposed values are clamped to the existing control ranges.

| Setting | Method and minimum evidence |
| --- | --- |
| Target WPM | Five-character words over the whole timed window. At least 30 aligned consecutive gaps are required for a proposal. The nominal target is chosen by forward simulation of the recovered settings over the same passage/window, using independent fixed seeds, to reproduce measured achieved speed. |
| Button delay s | Kept: a start-button preference, unrelated to typing. |
| Uppercase / number / punctuation / shift-symbol delay | Joint incoming-class factors; at least 6 usable gaps per class. |
| Key-pair rhythm % | Joint same-hand, alternating-hand and repeated-key factors; at least 40 classified pairs. |
| Word pause ms | Extra outgoing whitespace pause; at least 12 gaps. |
| Sentence pause ms | Extra pause after `.`, `!`, `?`; at least 3 gaps. This includes periods in citations/decimals, matching the engine. |
| Paragraph pause ms | Extra pause after a line break on top of whitespace timing; at least 3 breaks. |
| Jitter % | Log motor-residual variance after removing serial burst variance, inverted using the engine's 0.65 scale; at least 60 ordinary residuals and 20 adjacent pairs. |
| Burst drift % | Lag-one log-residual covariance, adjusted for 8?23-key runs; at least 60 ordinary residuals and 20 adjacent pairs. This is a short-sample approximation. |
| Thinking pause % / Thinking max ms | Long boundary residual frequency and inverse uniform `[max/3,max]` duration model; at least 4 pauses and 30 boundaries. |
| Keep target WPM | **Kept at its current value**: a budget preference cannot be identified from a single recording. |
| Corrected mistakes | Enabled only after at least 3 observed corrected errors; otherwise kept. No errors never switches mistakes off. |
| Mistake rate % | Corrected episodes divided by weighted eligible keys, at least 3 errors; with at least 10 letter errors, use the letter base rate directly. Sparse class multipliers stay current. |
| Number / punctuation / shift mistakes x | Class error frequency relative to letter errors; need 10 errors in each compared class and 30 class opportunities. Otherwise kept with counts/reason. |
| Duplicate / transpose share % | Observed corrected episode kinds, at least 20 errors; usually kept in a minute. |
| Notice delay ms | Mean latency into the first Backspace, removing the pre-newline motor gap where applicable; at least 3 errors. |
| Repair key pause ms | Mean within-correction Backspace/retype gaps; at least 8 gaps and 3 errors. |
| Resume pause ms | After-retype gap minus final repair gap and expected motor/boundary delay; at least 3 complete repairs with a following key. |
| Detect after chars | Likelihood fit of the engine's fixed distance distribution, censored at word/paragraph boundaries; at least 8 errors and a 3:1 likelihood advantage. Otherwise kept. |

The 45?220 ms motor-delay clamp remains unchanged. Nominal Target WPM can therefore differ from measured speed. If the recovered settings cannot reproduce the measured speed, results mark Target WPM as **closest achievable** and show both the measured speed and AutoWrite's expected speed with the proposal. Ordinary letter gaps longer than about 220 ms cannot be matched by slowing the motor further. In the synthetic slow-letter example on this passage, the slowest forecast is roughly **46 WPM**: below that AutoWrite types faster than the person. The exact threshold depends on the other recovered settings. A synthetic 60-second log with 300 ms ordinary letter gaps measures 35.8 WPM, while its closest engine forecast is 46.2 WPM. Both clamp ends are tested by checking every accepted Target WPM. Existing settings files retain all original values; missing paragraph pause loads as zero. Difficult-key delay now precedes the difficult key; its magnitudes and the Keep target WPM budget are unchanged.

### Expected accuracy at 30 and 60 seconds

These are synthetic recovery measurements, not a claim about a person's biometric identity. Three distinct profiles, all with unclamped motor centers, each use 20 fixed ground-truth seeds per duration. Every setting with a D6 bound is actually measured in every 60-second gate run. Profiles vary WPM, all four class delays, pauses, jitter, pair rhythm, burst drift, thinking, mistake rate and correction timing. Two additional profiles deliberately hit the 220 and 45 ms limits and are reported outside the gate.

The speed figure below compares a single timed sample with the profile's **long-run achieved speed**, measured over the full passage and averaged across 20 seeds. A single minute can be a fast or a slow minute, so rerun the test if the measured speed looks wrong. A transcription test measures copying rather than composing; thinking pauses will usually read low. US QWERTY is assumed. One test produces one proposal; there is no history averaging or profile system.

| Profile | Nominal WPM | Jitter / pair / burst % | Word / sentence / paragraph ms | Upper / digit / punct. / shift delay % | Mistake % | Notice / repair / resume ms | Thinking % / max ms | Detect chars |
| --- | ---: | --- | --- | --- | ---: | --- | --- | ---: |
| P0 | 36 | 25 / 30 / 15 | 60 / 300 / 130 | 35 / 55 / 65 / 95 | 3 | 1050 / 150 / 300 | 2 / 1200 | 2 |
| P1 | 42 | 45 / 60 / 25 | 100 / 450 / 210 | 65 / 85 / 95 / 125 | 4 | 650 / 95 / 240 | 6 / 1700 | 4 |
| P2 | 52 | 70 / 95 / 40 | 40 / 180 / 80 | 95 / 115 / 125 / 155 | 5 | 350 / 55 / 100 | 8 / 2400 | 6 |

| Profile | Long-run achieved WPM | 30 s speed error, median / p90 % | 60 s speed error, median / p90 % |
| --- | ---: | --- | --- |
| P0 | 37.6 | 10.1 / 20.2 | 4.0 / 19.1 |
| P1 | 44.1 | 10.0 / 18.4 | 6.8 / 13.9 |
| P2 | 54.5 | 9.0 / 23.7 | 5.5 / 18.9 |

Replay error is relative to the original timed log. The expected-speed column uses a separate 20-seed ensemble; the two single-run columns show stochastic scatter, with the ground-truth control beside the recovered settings. All figures are percentages.

| Window / profile | Expected replay median / worst | Recovered single replay median / p90 | Ground-truth single replay floor median / p90 |
| --- | --- | --- | --- |
| 30 s / P0 | 1.5 / 6.9 | 11.1 / 24.1 | 12.2 / 27.6 |
| 30 s / P1 | 3.4 / 6.1 | 7.6 / 13.0 | 12.6 / 24.6 |
| 30 s / P2 | 2.2 / 6.4 | 8.9 / 13.2 | 10.5 / 29.1 |
| 60 s / P0 | 3.0 / 5.5 | 3.8 / 11.2 | 9.1 / 21.3 |
| 60 s / P1 | 2.3 / 3.7 | 4.5 / 8.6 | 6.5 / 19.0 |
| 60 s / P2 | 2.0 / 11.3 | 5.8 / 11.9 | 10.6 / 18.6 |

The following are the **worst p90 absolute errors across the three profiles** (each profile has 20 runs). These are deliberately more conservative than the median recovery gate. Sparse 30-second samples can keep current values and have large errors. `pp` means percentage points; p90 uses the nearest-rank order statistic.

| Setting | Unit | 30 s p90 error | 60 s p90 error |
| --- | --- | ---: | ---: |
| Word pause ms | ms | 60.0 | 17.0 |
| Sentence pause ms | ms | 182.0 | 67.0 |
| Paragraph pause ms | ms | 210.0 | 69.0 |
| Uppercase delay % | pp | 42.0 | 41.0 |
| Number delay % | pp | 53.0 | 37.0 |
| Punctuation delay % | pp | 63.0 | 54.0 |
| Shift-symbol delay % | pp | 100.0 | 50.0 |
| Jitter % | pp | 30.0 | 9.0 |
| Key-pair rhythm % | pp | 50.0 | 28.0 |
| Mistake rate % | pp | 2.0 | 2.0 |
| Notice delay ms | ms | 202.0 | 157.0 |
| Repair key pause ms | ms | 30.0 | 13.0 |
| Resume pause ms | ms | 353.0 | 191.0 |
| Button delay s | s | 0.0 | 0.0 |
| Burst drift % | pp | 32.0 | 31.0 |
| Thinking pause % | pp | 4.0 | 6.0 |
| Thinking max ms | ms | 1000.0 | 1000.0 |
| Keep target WPM | 0/1 mismatch | 0.0 | 0.0 |
| Number mistakes x | x | 0.0 | 0.0 |
| Punct. mistakes x | x | 0.0 | 0.0 |
| Shift mistakes x | x | 0.0 | 0.0 |
| Corrected mistakes | 0/1 mismatch | 0.0 | 0.0 |
| Duplicate share % | pp | 0.0 | 0.0 |
| Transpose share % | pp | 0.0 | 0.0 |
| Detect after chars | chars | 4.0 | 4.0 |

A zero error for a kept value does not establish that it was learned. The benchmark keeps the preference flags, class-risk multipliers and rare mistake-kind shares at their common current values when data is thin. Separate sufficient-evidence fixtures test risk, kind-share and detection estimation.

Outside the gate, nominal 5 WPM hits the 220 ms motor limit (long-run achieved 27.3 WPM); nominal 180 WPM hits 45 ms (achieved 64.8 WPM). These use P1's other parameters. Their full error distributions are printed separately.

The warned 30 s slow-clamp case remains outside the gate. Its thin sample cannot reproduce the measured speed with the recovered or kept timing parameters; the closest-achievable proposal is a limitation, not random scatter. The following independent single-run errors are published without a passing claim.

| Outside-gate window / clamp | Recovered replay median / p90 % | Control floor median / p90 % | Median ratio |
| --- | --- | --- | ---: |
| 30 s / slow | 8.8 / 28.6 | 13.0 / 22.8 | 0.67 |
| 30 s / fast | 3.1 / 11.8 | 11.2 / 19.7 | 0.28 |
| 60 s / slow | 3.5 / 10.9 | 8.3 / 10.9 | 0.42 |
| 60 s / fast | 4.0 / 9.9 | 5.1 / 12.9 | 0.79 |

The full suite prints the median recovered value and 90th-percentile single-run error for every setting at both durations, including kept values, plus actual engine clamp ranges and replay diagnostics. It writes `D6-distribution.tsv` and synthetic raw logs into the selected evidence directory. The gate checks the original per-setting median bounds, expected-speed replay within 5% (median over 20 samples of a disjoint 20-seed ensemble mean), and a recovered single-run replay median no greater than 1.25 times the ground-truth replay noise floor. Worst expected-replay runs are printed too. Single-run median/p90 scatter is published rather than labelled a passing accuracy bound.

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

AutoWrite runs as the current user. Windows blocks lower-privilege apps from sending input into elevated administrator windows, so run AutoWrite elevated only when the destination app is also elevated.

## Diagnostics

Tests and probes can override the data directory with the absolute-path environment variable `AUTOWRITE_DATA_DIR`. The override covers settings, telemetry and calibration captures. Normal use retains the location above. Do not point diagnostics at the real settings directory.

```powershell
.\test.ps1 -OutputDirectory "$env:TEMP\L1-autowrite-tests"
.\e2e.ps1 -OutputDirectory "$env:TEMP\L1-autowrite-evidence"
.\smoke.ps1 -OutputDirectory "$env:TEMP\L1-autowrite-probes"
```

Run `build.ps1` first for the last two commands. `test.ps1` compiles the app sources and the console assert harness with the same in-box 64-bit .NET Framework compiler, prints each result and totals, and returns nonzero on failure. It includes the real engine's seeded virtual-clock recovery benchmark. Without an output argument it creates an isolated temporary directory. `AUTOWRITE_TEST_SCOPE=estimators` explicitly skips the expensive D6 benchmark for focused debugging and reports that skip; leave it unset for the full gate.

`e2e.ps1` drives the built `dist\AutoWrite.exe` through UI Automation and window messages, never through global input. Its windows are shown without activation; it verifies the foreground stays unchanged, selects 30 seconds from the default 60, enters the actual passage, reads all proposals, exercises Discard, Apply and Cancel, and checks every main control and saved value. PNGs contain only the app's own windows, captured with `PrintWindow`. The test enables `AUTOWRITE_DIAGNOSTIC_NOACTIVATE=1`, which also suppresses the F7 registration; this environment switch is inert in normal use.

`smoke.ps1` runs the existing `--sendinput-probe` (one Shift press); exit `0` means Windows accepted `SendInput`. The separately explicit command below performs a live text check. It temporarily activates only a window the probe creates, types `AutoWrite: aB [12] (3.5%)?` and a second line `OK`, checks the exact text, then closes. It stops if its window loses the foreground. An internal watchdog and an external 15-second process timeout bound it. Do this only when the temporary focus change is acceptable:

```powershell
.\smoke.ps1 -OutputDirectory "$env:TEMP\L1-autowrite-live" -LiveTyping
.\smoke.ps1 -OutputDirectory "$env:TEMP\L1-autowrite-idle" -LiveTyping -RequireIdleSeconds 240
```

That invokes `--typing-smoke-probe`, an otherwise inert diagnostic switch. The optional idle precondition uses `GetLastInputInfo` in the current Windows session before creating a window. If idle time is below the requested seconds, it sends nothing and returns `3`, `skipped: user active`, with the measured idle time. It does not wait for the desktop to become idle. Without that option the check starts immediately, for an owner launching it from their terminal. If its own window cannot acquire foreground within 500 ms, it exits `2` without typing; foreground loss or timeout also returns `2`. The app switch accepts `--require-idle-seconds N`; malformed options return `4`. The isolated diagnostic directory contains `L1-live-probe.txt` with the outcome and elapsed time, and the script prints it. A mismatch reports expected length, received length and first differing index only; received text is never saved or printed. The calibration and virtual-engine test suites send no global input. Diagnostics close the processes they create and use isolated data directories.

Each run also writes telemetry to:

```text
%APPDATA%\AutoWrite\telemetry\autowrite-YYYYMMDD-HHMMSS-fff.tsv
```

The telemetry log records every key event, correction, pause, source index, intended character, sent character, elapsed timestamp, and gap since the previous emitted key. Use it to inspect actual WPM, correction count, pause length, and whether a target editor received the final text correctly.

## Timing Data Notes

Calibration learns speed, rhythm, boundary pauses and correction habits from one transcription test. The initial defaults remain a practical approximation based on public typing research:

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
