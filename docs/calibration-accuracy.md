# Calibration accuracy and methods

These measurements use synthetic recordings, not a person's keystrokes.
Known timing models run through the real engine over the dialog's report
passage. Separate generators use gamma motor intervals, additive class
costs, changing burst regimes, boundary relocation and their own errors.
They cut at elapsed time after adding hesitations, so a pause reduces the
characters reached. They share no engine timing or analyser prediction code.

The three main models each have 20 recordings per duration. Each bounded
estimate must be measured in at least 18 minute-long recordings. Recovery
medians use measured rows; 90th-percentile errors include kept rows. A kept
proposal must exactly equal its current value. Every bounded current value
is the app default and differs from its known value. Thinking settings equal
the model's values for the expected-speed check, since composing pauses are
usually not measurable from copying; default-thinking results appear below.

The careful model's nominal speed moved from 36 to 40 WPM after the passage
changed to keep its motor delay strictly inside the unchanged clamp. Its
mistake rate remains 3%. Two additional limit cases are reported separately.

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

## Rhythm, active speed and repeatability

Speed uses five-character words from first to last key, excluding the first
character from the numerator. Entire gaps of at least 10 seconds are removed
and listed; shorter hesitations and corrections stay counted. Trailing time
after the last key never counts. The target matches ordinary-letter rhythm,
not nominal window speed. Both logs use exactly the same measurement code.

Expected replay averages 20 disjoint replay seeds per recording. Values
below are median / 90th-percentile absolute percentage errors across the 20
recordings. Both minute-long median match errors must be at most 5%.
The default-thinking column reports uncertainty that is deliberately kept.
Repeatability compares each recording with its model's mean over the same
window and passage, avoiding a change in text density.

| Length / model | Letter rhythm % | Expected active speed % | Default thinking: speed % | Test versus same-window mean % |
| --- | --- | --- | --- | --- |
| 30 s / Careful | 3.7 / 5.5 | 8.3 / 16.4 | 7.8 / 11.1 | 10.6 / 15.2 |
| 30 s / Mixed | 6.2 / 9.7 | 5.5 / 15.8 | 7.5 / 13.6 | 8.4 / 12.6 |
| 30 s / Brisk | 4.5 / 6.2 | 12.6 / 22.2 | 12.0 / 31.9 | 9.6 / 22.2 |
| 60 s / Careful | 1.0 / 2.4 | 2.6 / 7.3 | 1.8 / 5.3 | 2.9 / 10.5 |
| 60 s / Mixed | 1.5 / 2.9 | 3.0 / 7.9 | 2.8 / 9.7 | 3.4 / 5.8 |
| 60 s / Brisk | 2.8 / 9.5 | 4.3 / 12.6 | 3.3 / 12.6 | 5.0 / 20.0 |

A single minute can be fast or slow; rerun if its speed looks wrong.
The former comparison with a full-passage mean has been removed: it mostly
measured the symbol-dense opening against easier later paragraphs.

## Single-window replay scatter

These distributions pool 20 recordings and the same 20 replay seeds per
recording (400 errors per cell). Control A uses every known model value.
Control B uses known values for settings measured in that recording and the
actual kept values for settings it kept. The recovered / Control B median
ratio must be at most 1.25. Control A remains the absolute noise floor.
These single-window errors describe scatter, not an individual 5% guarantee.
Keeping uncertain thinking settings can widen minute-to-minute speed scatter;
the default-thinking expected-speed column shows its cost without speeding
ordinary keys to pay for those pauses.

| Length / model | Recovered median / p90 % | Control A median / p90 % | Control B median / p90 % | Ratio to A / B |
| --- | --- | --- | --- | --- |
| 30 s / Careful | 11.7 / 28.6 | 13.5 / 32.0 | 11.6 / 37.6 | 0.868 / 1.010 |
| 30 s / Mixed | 8.9 / 24.5 | 12.4 / 33.4 | 14.2 / 28.7 | 0.716 / 0.624 |
| 30 s / Brisk | 14.3 / 33.2 | 13.4 / 36.1 | 12.5 / 35.0 | 1.070 / 1.145 |
| 60 s / Careful | 5.5 / 17.0 | 6.4 / 18.5 | 6.1 / 18.1 | 0.848 / 0.896 |
| 60 s / Mixed | 6.1 / 15.7 | 7.2 / 18.7 | 6.7 / 18.2 | 0.844 / 0.899 |
| 60 s / Brisk | 7.4 / 19.7 | 8.0 / 22.5 | 8.6 / 22.8 | 0.919 / 0.852 |

## Individual setting recovery

Median recovery error / 90th-percentile single-recording error, in the units
shown. Bounds apply to each minute-long model median, not every recording.

### 30 seconds

| Setting | Careful | Mixed | Brisk | Bound |
| --- | --- | --- | --- | --- |
| Word pause ms | 5.5 / 34.0 | 7.0 / 49.0 | 24.0 / 65.0 | 50 / 50 / 50 |
| Sentence pause ms | 2.0 / 189.0 | 19.5 / 400.0 | 48.5 / 670.0 | 75 / 112.5 / 50 |
| Paragraph pause ms | 130.0 / 130.0 | 210.0 / 210.0 | 80.0 / 80.0 | 50 / 52.5 / 50 |
| Uppercase delay points | 1.0 / 25.0 | 2.0 / 55.0 | 3.0 / 85.0 | 20 / 20 / 20 |
| Digit delay points | 2.0 / 16.0 | 11.0 / 32.0 | 4.0 / 81.0 | 20 / 20 / 20 |
| Punctuation delay points | 4.5 / 35.0 | 4.0 / 65.0 | 24.0 / 95.0 | 20 / 20 / 20 |
| Shifted symbol delay points | 1.0 / 40.0 | 15.0 / 70.0 | 29.0 / 100.0 | 20 / 20 / 20 |
| Jitter points | 25.0 / 30.0 | 45.0 / 10.0 | 9.0 / 15.0 | 15 / 15 / 15 |
| Key-pair rhythm points | 4.0 / 50.0 | 6.0 / 20.0 | 9.0 / 35.0 | 25 / 25 / 25 |
| Mistake rate points | 0.0 / 2.0 | 1.0 / 3.0 | 0.0 / 3.0 | 1 / 1 / 1 |
| Notice ms | 95.0 / 200.0 | 27.0 / 200.0 | 17.0 / 148.0 | 315 / 195 / 105 |
| Repair ms | 1.0 / 30.0 | 2.0 / 25.0 | 1.0 / 4.0 | 60 / 60 / 60 |
| Resume ms | 9.0 / 123.0 | 39.0 / 190.0 | 42.0 / 237.0 | 90 / 72 / 60 |

### 60 seconds

| Setting | Careful | Mixed | Brisk | Bound |
| --- | --- | --- | --- | --- |
| Word pause ms | 1.0 / 18.0 | 1.5 / 39.0 | 4.0 / 28.0 | 50 / 50 / 50 |
| Sentence pause ms | 6.5 / 107.0 | 8.5 / 98.0 | 18.0 / 134.0 | 75 / 112.5 / 50 |
| Paragraph pause ms | 3.5 / 117.0 | 37.0 / 166.0 | 8.0 / 88.0 | 50 / 52.5 / 50 |
| Uppercase delay points | 3.0 / 11.0 | 8.5 / 40.0 | 9.5 / 69.0 | 20 / 20 / 20 |
| Digit delay points | 0.5 / 11.0 | 8.5 / 28.0 | 3.5 / 61.0 | 20 / 20 / 20 |
| Punctuation delay points | 1.0 / 14.0 | 2.0 / 25.0 | 9.0 / 75.0 | 20 / 20 / 20 |
| Shifted symbol delay points | 4.0 / 27.0 | 2.0 / 40.0 | 7.0 / 76.0 | 20 / 20 / 20 |
| Jitter points | 0.5 / 4.0 | 0.5 / 9.0 | 2.5 / 12.0 | 15 / 15 / 15 |
| Key-pair rhythm points | 0.5 / 19.0 | 2.5 / 34.0 | 6.0 / 35.0 | 25 / 25 / 25 |
| Mistake rate points | 0.0 / 2.0 | 0.0 / 2.0 | 0.0 / 2.0 | 1 / 1 / 1 |
| Notice ms | 19.5 / 122.0 | 20.0 / 96.0 | 18.5 / 75.0 | 315 / 195 / 105 |
| Repair ms | 1.0 / 10.0 | 0.0 / 5.0 | 0.5 / 3.0 | 60 / 60 / 60 |
| Resume ms | 25.5 / 92.0 | 8.0 / 170.0 | 51.5 / 208.0 | 90 / 72 / 60 |

### Kept proposals

Counts out of 20 recordings, ordered Careful / Mixed / Brisk. Kept values
are excluded from recovery medians and included in single-recording errors.

| Setting | 30 seconds | 60 seconds |
| --- | --- | --- |
| Word pause ms | 0 / 2 / 1 | 0 / 0 / 0 |
| Sentence pause ms | 1 / 4 / 4 | 0 / 0 / 0 |
| Paragraph pause ms | 20 / 20 / 20 | 0 / 0 / 0 |
| Uppercase delay points | 7 / 7 / 1 | 0 / 0 / 0 |
| Digit delay points | 0 / 0 / 1 | 0 / 0 / 0 |
| Punctuation delay points | 8 / 9 / 9 | 0 / 0 / 0 |
| Shifted symbol delay points | 2 / 3 / 2 | 0 / 0 / 1 |
| Jitter points | 20 / 20 / 18 | 0 / 0 / 0 |
| Key-pair rhythm points | 6 / 11 / 3 | 0 / 0 / 0 |
| Mistake rate points | 8 / 5 / 1 | 0 / 0 / 0 |
| Notice ms | 8 / 5 / 1 | 0 / 0 / 0 |
| Repair ms | 10 / 5 / 1 | 1 / 0 / 0 |
| Resume ms | 9 / 5 / 1 | 0 / 0 / 0 |

## Sparse-sample labels

The eligibility minima below are unchanged. “Few samples” is a display
warning, not a new gate. For bounded settings its level covers the observed
counts with errors beyond the recovery bound in these short recordings;
where none exceeded a bound, it marks the smallest observed count. This is
a retrospective warning, not proof that larger counts guarantee accuracy.
Some upper bins contain only a few recordings. Thinking, detection and kind
shares use advisory levels of 8, 12 and 30 observations respectively.

| Setting | Few below | Low-count error p90 | Other-count error p90 |
| --- | ---: | ---: | ---: |
| Word pause ms | 25 | 49 (n=56) | 30 (n=61) |
| Sentence pause ms | 12 | 144 (n=111) | no observations |
| Paragraph pause ms | 6 | 155 (n=60) | no observations |
| Uppercase delay points | 18 | 64 (n=104) | 11 (n=1) |
| Digit delay points | 22 | 56 (n=119) | no observations |
| Punctuation delay points | 18 | 57 (n=94) | no observations |
| Shifted symbol delay points | 14 | 58 (n=112) | no observations |
| Jitter points | 61 | 14 (n=1) | 9 (n=61) |
| Key-pair rhythm points | 134 | 34 (n=96) | 5 (n=4) |
| Mistake rate points | 17 | 2 (n=106) | no observations |
| Notice ms | 12 | 154 (n=99) | 79 (n=7) |
| Repair ms | 10 | 5 (n=4) | 6 (n=99) |
| Resume ms | 17 | 199 (n=105) | no observations |

## Estimation methods

At least 70% passage alignment is required before any setting is proposed.
Fixtures with about 10% uncorrected errors retain 90% alignment; wrong-start
and unrelated text have 19–22%, and random keys 4–7%, at 60 and 125 WPM.
Wrong text keeps every value and asks the person to retry.

Boundary gaps are fitted together: time before a space, after it, or on
either side of Enter contributes to one boundary total. Huber weighting and
rejection beyond six standardized residuals bound every gap's influence.
Ordinary residuals exclude boundary keys. US QWERTY is assumed for pair and
nearby-key effects. Copying understates composing pauses, so thin thinking
evidence stays at its previous value rather than being set to zero.

<!-- methods -->
| Setting | Method and minimum evidence |
| --- | --- |
| Target WPM | At least 30 aligned gaps. Forward replay matches the median gap into a lower-case letter following a lower-case letter inside a word, outside corrections. The same edit reconstruction measures both recordings and replays. |
| Button delay s | Kept: a start-button preference, unrelated to typing. |
| Uppercase / number / punctuation / shift-symbol delay | Joint boundary-unit incoming-class factors; at least 6 usable gaps per class. |
| Key-pair rhythm % | Joint same-hand, alternating-hand and repeated-key factors; at least 40 classified pairs. |
| Word pause ms | Extra boundary whitespace pause; at least 12 gaps. |
| Sentence pause ms | Extra pause after `.`, `!`, `?`; at least 3 gaps. This includes periods in citations/decimals, matching the engine. |
| Paragraph pause ms | Extra pause after a line break on top of whitespace timing; at least 3 breaks. |
| Jitter % | Log motor-residual variance after removing serial burst variance, inverted using the engine's 0.65 scale; at least 60 ordinary residuals and 20 adjacent pairs. |
| Burst drift % | Lag-one log-residual covariance, adjusted for 8–23-key runs; at least 60 ordinary residuals and 20 adjacent pairs. This is a short-sample approximation. |
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

The engine retains its 45–220 ms motor clamp. The nominal 5 and 180 WPM
models exercise its limits and are reported separately, outside the recovery
checks. A synthetic recording with 300 ms ordinary-letter gaps also proves
the real slow-typing limit. It produces the closest achievable warning.
The example reaches about 46 WPM at the slowest available motor setting;
below roughly that speed on this passage AutoWrite types faster than the
person. Other settings affect this threshold. Only a rhythm outside the
available motor range produces the motor-limit warning. Other differences
explain the pauses and corrections in the proposed or kept settings.

| Length / limit | Rhythm median / p90 % | Expected speed median / p90 % | Pooled replay median / p90 % |
| --- | --- | --- | --- |
| 30 s / Slow limit | 5.5 / 8.6 | 2.2 / 3.5 | 6.0 / 12.7 |
| 30 s / Fast limit | 7.4 / 14.4 | 2.1 / 6.5 | 9.2 / 24.8 |
| 60 s / Slow limit | 3.3 / 14.7 | 7.1 / 10.9 | 8.9 / 17.2 |
| 60 s / Fast limit | 2.5 / 8.1 | 3.3 / 9.1 | 8.3 / 17.7 |

## Cross-text limit

Target WPM is converted to a motor delay separately for every text, whereas
a person tends to keep finger speed steady. The closest match is therefore
on report-like text. Plain prose is somewhat slower in AutoWrite; turning
Keep target WPM off narrows this difference. This is a reported limitation,
not a changed engine model or an acceptance bound.

The benchmark first calibrates each minute-long recording at seed 1000,
then types report and plain prose over ten seeds (9000–9009). A reference
target on plain prose matches the report's mean motor delay. The percentage
compares AutoWrite with that same-motor reference, using active speed.

| Model | Keep target WPM | Report speed | Plain speed | Same-motor reference | Difference |
| --- | --- | ---: | ---: | ---: | ---: |
| Careful | True | 43.8 | 43.7 | 47.0 | -7.0% |
| Careful | False | 43.8 | 45.6 | 46.7 | -2.4% |
| Mixed | True | 43.1 | 43.1 | 47.4 | -9.1% |
| Mixed | False | 42.4 | 45.7 | 46.7 | -2.2% |
| Brisk | True | 51.0 | 50.5 | 55.9 | -9.7% |
| Brisk | False | 51.3 | 55.1 | 56.1 | -1.9% |

## Reproduce the measurements

Run `./test.ps1 -OutputDirectory <isolated-directory>` on Windows. The
in-box .NET Framework C# compiler builds the same sources as the app.
`D6-distribution.tsv` contains the setting errors, kept counts, match errors,
worst runs and pooled controls; `D6-estimates.tsv` contains each proposal.
`cross-text.tsv` is a reported benchmark with no numeric acceptance gate.
Raw synthetic logs accompany the tables. Recording seeds are 1000–1019;
expected and pooled replay seeds are 30000–30019. Target selection uses nine
separate seeds, 70001 + 101*i for i=0..8. The generators, seeds and all
benchmark inputs are committed in `tests/`.
