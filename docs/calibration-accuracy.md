# Calibration accuracy and methods

These measurements use synthetic recordings, not a person's keystrokes.
The recordings come from the real typing engine, with known settings, over
the same report passage used in the dialog. Independent hand-built logs also
test relocated pauses, hesitations, uncorrected errors and skipped words.

Each of three timing models has 20 recordings at each test length. Two
additional models deliberately reach the engine's motor limits. Every
bounded timing estimate must be measured in at least 18 of 20 minute-long
recordings. Its median recovery error uses only measured estimates.
The 90th-percentile error includes all proposals, including kept values.
A kept proposal must equal the current setting exactly.

The analyser starts with the app defaults. Every bounded setting differs
from its known value in every model; keeping a default cannot count as
recovering the known value. The careful model's nominal speed changed from
36 to 40 WPM when the report passage changed, to keep its motor delay
strictly inside the unchanged clamp. Its mistake rate remains 3%.

## Timing models

| Model | Nominal WPM | Jitter / pair / burst % | Word / sentence / paragraph ms | Class delay % | Mistakes % | Notice / repair / resume ms |
| --- | ---: | --- | --- | --- | ---: | --- |
| Careful pace | 40 | 25 / 30 / 15 | 60 / 300 / 130 | 35 / 55 / 65 / 95 | 3 | 1050 / 150 / 300 |
| Mixed pace | 42 | 45 / 60 / 25 | 100 / 450 / 210 | 65 / 85 / 95 / 125 | 4 | 650 / 95 / 240 |
| Brisk pace | 52 | 70 / 95 / 40 | 40 / 180 / 80 | 95 / 115 / 125 / 155 | 5 | 350 / 55 / 100 |

Class delays are uppercase, number, punctuation and shifted symbol, in order.
Thinking probabilities/maxima are 2%/1200 ms, 6%/1700 ms and 8%/2400 ms.
Detection maxima are 2, 4 and 6 characters. Sparse mistake-kind shares and
class-risk multipliers retain common defaults; separate sufficient-error
recordings test those estimates.

## Speed and replay

Speed accuracy compares a short recording against mean achieved speed over
the full passage, averaged across 20 independent recordings. It does not
compare the measured speed against its own character count.

| Model | 30 s speed error median / p90 % | 60 s speed error median / p90 % |
| --- | --- | --- |
| Careful pace | 21.0 / 27.8 | 11.4 / 18.6 |
| Mixed pace | 25.6 / 32.9 | 15.5 / 20.1 |
| Brisk pace | 19.3 / 36.8 | 13.4 / 27.1 |

Replay uses separate random seeds. Expected replay is the mean of 20
simulations for each recovered proposal. Its per-model median error must
be at most 5%. A separate single simulation shows short-window variability;
Control A uses all known settings and remains the absolute floor. Control B
uses known values for settings measured in that recording and the analyser's
kept values for settings it kept. The recovered/Control B median ratio must
be at most 1.25; both ratios are published. This does not excuse keeping a
bounded setting: the independent 18-of-20 requirement still applies.

In the careful model diagnostic, keeping the previous thinking settings
raised the minute-long single-replay median error from 4.0% to 6.6%.
The careful minute-long replay ratio against Control B is 1.368, above the
1.25 acceptance limit; this check remains unresolved.

| Length / model | Expected replay median % | Single replay median / p90 % | Control A median / p90 % | Control B median / p90 % | Ratio A / B |
| --- | --- | --- | --- | --- | --- |
| 30 s / Careful pace | 3.6 | 9.1 / 20.0 | 10.1 / 23.5 | 12.6 / 27.6 | 0.899 / 0.720 |
| 30 s / Mixed pace | 2.7 | 11.4 / 21.8 | 7.7 / 29.5 | 11.4 / 20.0 | 1.478 / 0.995 |
| 30 s / Brisk pace | 1.3 | 6.3 / 22.6 | 10.3 / 39.2 | 12.7 / 35.2 | 0.617 / 0.499 |
| 60 s / Careful pace | 0.8 | 6.6 / 12.8 | 3.6 / 13.9 | 4.8 / 13.9 | 1.864 / 1.368 |
| 60 s / Mixed pace | 0.9 | 5.1 / 8.2 | 5.7 / 12.4 | 5.0 / 11.8 | 0.899 / 1.018 |
| 60 s / Brisk pace | 2.2 | 4.1 / 14.7 | 11.0 / 25.8 | 11.0 / 25.8 | 0.376 / 0.376 |

## Per-setting errors

Values below are the worst p90 absolute error across the three timing models.
Percentage settings use percentage points; pause settings use milliseconds.

| Setting | 30 s p90 | 60 s p90 |
| --- | ---: | ---: |
| Button delay s | 0.0 | 0.0 |
| Jitter % | 30.0 | 12.0 |
| Key-pair rhythm % | 50.0 | 35.0 |
| Burst drift % | 15.0 | 40.0 |
| Word pause ms | 65.0 | 39.0 |
| Sentence pause ms | 670.0 | 134.0 |
| Paragraph pause ms | 210.0 | 166.0 |
| Thinking pause % | 4.0 | 6.0 |
| Thinking max ms | 1000.0 | 1000.0 |
| Keep target WPM | 0.0 | 0.0 |
| Uppercase delay % | 85.0 | 69.0 |
| Number delay % | 81.0 | 61.0 |
| Punctuation delay % | 95.0 | 75.0 |
| Shift-symbol delay % | 100.0 | 76.0 |
| Number mistakes x | 0.0 | 0.0 |
| Punct. mistakes x | 0.0 | 0.0 |
| Shift mistakes x | 0.0 | 0.0 |
| Corrected mistakes | 0.0 | 0.0 |
| Mistake rate % | 3.0 | 2.0 |
| Duplicate share % | 0.0 | 0.0 |
| Transpose share % | 0.0 | 0.0 |
| Notice delay ms | 200.0 | 122.0 |
| Detect after chars | 3.0 | 4.0 |
| Repair key pause ms | 30.0 | 10.0 |
| Resume pause ms | 237.0 | 208.0 |

## Kept estimates

Counts below are kept proposals out of 20 recordings, by model.
Bounded estimates are shown separately so thin data remains visible.

| Setting / length | Careful pace | Mixed pace | Brisk pace |
| --- | ---: | ---: | ---: |
| Word pause ms / 30 s | 0 | 2 | 1 |
| Word pause ms / 60 s | 0 | 0 | 0 |
| Sentence pause ms / 30 s | 1 | 4 | 4 |
| Sentence pause ms / 60 s | 0 | 0 | 0 |
| Paragraph pause ms / 30 s | 20 | 20 | 20 |
| Paragraph pause ms / 60 s | 0 | 0 | 0 |
| Uppercase delay % / 30 s | 7 | 7 | 1 |
| Uppercase delay % / 60 s | 0 | 0 | 0 |
| Number delay % / 30 s | 0 | 0 | 1 |
| Number delay % / 60 s | 0 | 0 | 0 |
| Punctuation delay % / 30 s | 8 | 9 | 9 |
| Punctuation delay % / 60 s | 0 | 0 | 0 |
| Shift-symbol delay % / 30 s | 2 | 3 | 2 |
| Shift-symbol delay % / 60 s | 0 | 0 | 1 |
| Jitter % / 30 s | 20 | 20 | 18 |
| Jitter % / 60 s | 0 | 0 | 0 |
| Key-pair rhythm % / 30 s | 6 | 11 | 3 |
| Key-pair rhythm % / 60 s | 0 | 0 | 0 |
| Mistake rate % / 30 s | 8 | 5 | 1 |
| Mistake rate % / 60 s | 0 | 0 | 0 |
| Notice delay ms / 30 s | 8 | 5 | 1 |
| Notice delay ms / 60 s | 0 | 0 | 0 |
| Repair key pause ms / 30 s | 10 | 5 | 1 |
| Repair key pause ms / 60 s | 1 | 0 | 0 |
| Resume pause ms / 30 s | 9 | 5 | 1 |
| Resume pause ms / 60 s | 0 | 0 | 0 |

## Estimation methods

Boundary gaps are fitted together: time before a space, after it, or on
either side of Enter contributes to the same boundary total. Huber weighting
and rejection beyond six standardized residuals bound the influence of
hesitations at every position. Ordinary residuals exclude boundary keys.

<!-- methods -->
| Setting | Method and minimum evidence |
| --- | --- |
| Target WPM | Five-character words over the whole timed window. At least 30 aligned consecutive gaps are required for a proposal. The nominal target is chosen by forward simulation of the recovered settings over the same passage/window, using independent fixed seeds, to reproduce measured achieved speed. |
| Button delay s | Kept: a start-button preference, unrelated to typing. |
| Uppercase / number / punctuation / shift-symbol delay | Joint boundary-unit incoming-class factors; at least 6 usable gaps per class. |
| Key-pair rhythm % | Joint same-hand, alternating-hand and repeated-key factors; at least 40 classified pairs. |
| Word pause ms | Extra boundary whitespace pause; at least 12 gaps. |
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
<!-- /methods -->

## Motor limits

The engine retains its 45–220 ms motor clamp. Nominal speeds of 5 and
180 WPM exercise both limits and are reported separately rather than treated
as recoverable nominal speeds. A hand-built recording with 300 ms ordinary
letter gaps also tests the real slow-typing limit, which engine-generated
recordings alone cannot reveal. The proposal searches the accepted targets
and warns with measured and expected speed when a match is impossible.

| Length / limit | Single replay median / p90 % | Control median / p90 % |
| --- | --- | --- |
| 30 s / slow | 2.8 / 14.1 | 6.8 / 15.4 | 5.6 / 17.9 | 0.406 / 0.500 |
| 30 s / fast | 0.6 / 28.9 | 8.4 / 22.0 | 5.0 / 22.0 | 0.067 / 0.112 |
| 60 s / slow | 4.6 / 11.8 | 6.5 / 11.6 | 3.2 / 13.1 | 0.703 / 1.428 |
| 60 s / fast | 7.3 / 10.0 | 10.0 / 12.6 | 10.0 / 16.1 | 0.728 / 0.728 |

The warned short slow-limit recording is a documented limitation. Copying
also understates composing pauses, and US QWERTY is assumed. One test
produces one proposal; recordings are not averaged into personal profiles.

