using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
namespace AutoWrite.Tests
{
    internal static partial class Runner
    {
        private static List<CaptureKey> Capture(VirtualRuntime runtime)
        {
            var keys = new List<CaptureKey>();
            foreach (var k in runtime.Keys)
            {
                keys.Add(new CaptureKey(k.Time, k.Value));
            }
            return keys;
        }

        private static CaptureSession Fixture(string chars)
        {
            var s = new CaptureSession(60);
            for (int i = 0; i < chars.Length; i++)
            {
                s.Accept(chars[i], i * 100);
            }
            return s;
        }

        static partial void AnalysisTests()
        {
            ActivityTests();
            HumanLogTests();
            Test("live probe idle boundary and mismatch summaries never expose received text", delegate
            {
                Assert(LiveTypingProbe.IdleSeconds(300000, 60001) < 240 && LiveTypingProbe.IdleSeconds(300000, 60000) == 240,
                "idle threshold boundary");
                Assert(LiveTypingProbe.IdleSeconds(100, UInt32.MaxValue - 899) == 1, "idle tick wrap");
                Assert(LiveTypingProbe.IdleSeconds(100, 200) == 0, "future input timestamp must fail closed");
                string summary = LiveTypingProbe.DifferenceSummary("abc", "owner private text");
                Assert(summary == "expected_length=3; received_length=18; first_difference=0" && !summary.Contains("owner"),
                "received text leaked");
                Assert(LiveTypingProbe.DifferenceSummary("abc", "ab").EndsWith("first_difference=2"), "prefix mismatch index");
                Assert(LiveTypingProbe.DifferenceSummary("abc", "abc").EndsWith("first_difference=-1"), "equal text index");
            });
            Test("alignment reconstructs nearby, missed shift, duplicate and transpose corrections", delegate
            {
                const string passage = "In eDNA [12], aX=42.5%; (why?)";
                var fixtures = new[]
                {
                    "An\b\bIn ", "In eDNa\bA", "IIn\b\b\bIn", "nI\b\bIn", "nI e\b\b\b\bIn e"
                };
                var kinds = new[]
                {
                    "nearby", "missed-shift", "duplicate", "transpose", "transpose"
                };
                for (int i = 0; i < fixtures.Length; i++)
                {
                    var session = Fixture(fixtures[i]);
                    List<CorrectionEpisode> corrections;
                    Alignment alignment;
                    CalibrationAnalysis.Gaps(session.Keys, passage, out corrections, out alignment);
                    Assert(corrections.Count == 1 && corrections[0].Kind == kinds[i], "wrong correction kind in " + fixtures[i]);
                }
                Assert(CalibrationAnalysis.AlignPrefix("In eDNA [12]", passage).Distance == 0, "exact alignment");
                Assert(CalibrationAnalysis.AlignPrefix("In eDNA [21]", passage).Distance == 1, "transpose distance");
            });
            Test("thin recordings and no mistakes keep unsupported values", delegate
            {
                var current = AppSettings.Defaults();
                foreach (var keys in new[]
                {
                    new List<CaptureKey>(), Fixture("In eD").Keys
                })
                {
                    var result = CalibrationAnalysis.Analyze(keys, 60, current);
                    foreach (var p in typeof(AppSettings).GetProperties())
                    {
                        Assert(Object.Equals(p.GetValue(result.Proposed, null), p. GetValue(current, null)), p.Name);
                    }
                }
                var settings = AppSettings.Defaults();
                settings.EnableTypos = false;
                var sample = RunEngine(CalibrationPassage.Text, settings, 11, 60000);
                var analyzed = CalibrationAnalysis.Analyze(Capture(sample), 60, current);
                Assert(analyzed.Proposed.EnableTypos && analyzed.Proposed.TypoChancePercent == current.TypoChancePercent,
                "no-error run switched mistakes off");
            });
            Test("joint fit separates incoming class, pair, word, sentence and paragraph effects", delegate
            {
                var truth = new double[]
                {
                    115, .4, .6, .8, 1.1, .7, 95, 410, 190
                };
                var rows = new List<AlignedGap>();
                for (int i = 1; i < CalibrationPassage.Text.Length; i++)
                {
                    var r = new AlignedGap
                    {
                        Previous = CalibrationPassage.Text[i - 1], Current = CalibrationPassage.Text[i], Event = i, Source = i
                    };
                    r.Gap = CalibrationAnalysis.Prediction(r, truth);
                    rows.Add(r);
                }
                var fit = CalibrationAnalysis.Fit(rows, AppSettings.Defaults());
                for (int i = 0; i < fit.Length; i++)
                {
                    Assert(Math.Abs(fit[i] - truth[i]) < .02, "fit parameter " + i + "=" + fit[i]);
                }
            });
            Test("settings written by baseline preserve all original values", delegate
            {
                File.Copy(Path.Combine(SourceRoot, "tests/baseline-settings.ini"), Path.Combine(DataDirectory.PathRoot, "settings.ini"),
                true);
                var loaded = SettingsStore.Load();
                var baseline = AppSettings.Defaults();
                foreach (var p in typeof(AppSettings).GetProperties())
                {
                    object expected = p.GetValue(baseline, null);
                    if (p.Name != "ParagraphPauseMilliseconds")
                    {
                        expected = p.PropertyType == typeof(int) ? (object)((int)expected + 1) : (
                        object)(!(bool)expected);
                    }
                    Assert(Object.Equals(expected, p.GetValue(loaded, null)), p.Name);
                }
            });
            Test("all proposed integers clamp to control ranges and round trip", delegate
            {
                var result = CalibrationAnalysis.Analyze(new List<CaptureKey>(), 60, AppSettings.Defaults());
                foreach (var p in typeof(AppSettings).GetProperties())
                {
                    if (p.PropertyType == typeof(int))
                    {
                        CalibrationAnalysis.Set(result, p.Name, 999999, "fixture");
                    }
                }
                SettingsStore.Save(result.Proposed);
                var actual = SettingsStore.Load();
                foreach (var p in typeof(AppSettings).GetProperties())
                {
                    Assert(Object.Equals(p.GetValue(result.Proposed, null), p.GetValue(
                    actual, null)), p.Name);
                }
                CalibrationAnalysis.Set(result, "WordsPerMinute", -3, "fixture");
                Assert(result.Proposed.WordsPerMinute == 5, "minimum WPM");
            });
            Test("paragraph default and incoming difficult-key arithmetic", delegate
            {
                var s = AppSettings.Defaults();
                s.EnableTypos = false;
                s.ThinkingPauseChancePercent = 0;
                s.JitterPercent = 0;
                s
                .BurstPercent = 0;
                s.KeyPairRhythmPercent = 0;
                s.WordPauseMilliseconds = 0;
                s.SentencePauseMilliseconds = 0;
                s.UppercaseDelayPenaltyPercent = 100;
                s.KeepTargetWpm = false;
                s.WordsPerMinute = 30;
                var r = RunEngine("aAa\na", s, 7, Int64.MaxValue);
                Assert(r.Keys[1].Time - r.Keys[0].Time == 2 * (r.Keys[2].Time - r.Keys[1].Time), "factor did not move into uppercase key")
                ;
                Assert(s.ParagraphPauseMilliseconds == 0, "paragraph default changed");
                s.ParagraphPauseMilliseconds = 500;
                var p = RunEngine("aAa\na", s, 7, Int64.MaxValue);
                Assert(p.Keys[4].Time - p.Keys[3].Time > r.Keys[4].Time - r.Keys[3].Time + 200, "paragraph pause absent");
                Assert(RunEngine("aAa\r\na", s, 7, Int64.MaxValue).Log() == p.Log(), "CRLF added paragraph pause twice");
                Assert(File.ReadAllText(Path.Combine(DataDirectory.PathRoot, "engine.tsv")).Contains("paragraph_pause_ms=500"),
                "paragraph setting missing from telemetry");
                var budget = typeof(TypingEngine).GetMethod("EstimateNaturalPauseBudget", System.Reflection.BindingFlags.Static | System
                .Reflection.BindingFlags.NonPublic);
                Assert((double)budget.Invoke(null, new object[]
                {
                    "a\nb", Options(s), null
                }) == 500, "paragraph missing from WPM budget");
                Assert((double)budget.Invoke(null, new object[]
                {
                    "a\r\nb", Options(s), null
                }) == 500, "CRLF paragraph budget counted twice");
            });
            Test("jitter estimator recovers independent motor spread", delegate
            {
                var s = Profile(1);
                s.JitterPercent = 75;
                s.BurstPercent = 0;
                s.EnableTypos = false;
                s.ThinkingPauseChancePercent = 0;
                var result = CalibrationAnalysis.Analyze(Capture(RunEngine(CalibrationPassage.Text, s, 931, 60000)), 60, AppSettings
                .Defaults());
                Assert(Math.Abs(result.Proposed.JitterPercent - 75) <= 12, "jitter estimator: " + result.Proposed.JitterPercent);
            });
            Test("correction estimators recover notice and repair centers", delegate
            {
                var s = Profile(1);
                s.CorrectionNoticeMilliseconds = 460;
                s.CorrectionPauseMilliseconds = 80;
                var result = CalibrationAnalysis.Analyze(Capture(RunEngine(CalibrationPassage.Text, s, 42, 60000)), 60, AppSettings
                .Defaults());
                Assert(Math.Abs(result.Proposed.CorrectionNoticeMilliseconds - 460) <= 120, "notice estimator: " + result.Proposed
                .CorrectionNoticeMilliseconds);
                Assert(Math.Abs(result.Proposed.CorrectionPauseMilliseconds - 80) <= 15, "repair estimator");
            });
            Test("synthetic slow and fast motor logs propose closest achievable speeds", delegate
            {
                foreach (int motor in new[]
                {
                    300, 30
                })
                {
                    var current = AppSettings.Defaults();
                    current.KeepTargetWpm = false;
                    current.JitterPercent = current.BurstPercent = current.KeyPairRhythmPercent = 0;
                    current.ThinkingPauseChancePercent = 0;
                    current.EnableTypos = false;
                    var keys = new List<CaptureKey>();
                    double time = 0;
                    for (int i = 0; i < CalibrationPassage.Text.Length && time < 60000; i++)
                    {
                        keys.Add(new CaptureKey(time, CalibrationPassage.Text[i]));
                        char c = CalibrationPassage.Text[i];
                        time += motor + (c == ' ' ? 100 : 0) + (".!?".IndexOf(c) >= 0 ? 450 : 0) + (c == '\n' ? 210 : 0);
                    }
                    var result = CalibrationAnalysis.Analyze(keys, 60, current);
                    Assert(!result.Proposed.KeepTargetWpm, "budget preference changed");
                    var row = result.Estimates.Find(delegate(CalibrationEstimate e)
                    {
                        return e.Setting == "WordsPerMinute";
                    });
                    Assert(row.Measured && row.ClosestAchievable && row.Reason.Contains("Closest achievable"), "closest row missing for "
                    + motor);
                    Assert(result.Warning.Contains(result.MeasuredWpm.ToString("F1")) && result.Warning.Contains(result.PredictedWpm
                    .ToString("F1")), "warning must show both speeds");
                    Assert(Math.Abs(CalibrationReplay.Predict(result.Proposed, 60) - result.PredictedWpm) < .0001,
                    "proposed speed differs from warning");
                    double endpoint = motor == 300 ? result.MinimumWpm : result.MaximumWpm;
                    Assert(Math.Abs(endpoint - result.PredictedWpm) < .0001, "proposal is not nearest endpoint");
                    int proposedTarget = result.Proposed.WordsPerMinute;
                    double bestError = Math.Abs(result.PredictedOrdinaryGapMilliseconds - result.OrdinaryGapMilliseconds);
                    for (int target = 5; target <= 180; target++)
                    {
                        result.Proposed.WordsPerMinute = target;
                        Assert(Math.Abs(CalibrationReplay.Forecast(result.Proposed, 60).OrdinaryGapMilliseconds -
                            result.OrdinaryGapMilliseconds) + .0001 >= bestError,
                        "closer accepted target exists: " + target);
                    }
                    result.Proposed.WordsPerMinute = proposedTarget;
                    var baseMethod = typeof(TypingEngine).GetMethod("CalculateBaseDelay", System.Reflection.BindingFlags.Static | System
                    .Reflection.BindingFlags.NonPublic);
                    int center = (int)baseMethod.Invoke(null, new object[]
                    {
                        CalibrationPassage.Text, Options(result.Proposed), 0, null
                    });
                    Assert(center == (motor == 300 ? 220 : 45), "motor clamp endpoint: " + center);
                    Console.WriteLine("SYNTHETIC motor_ms=" + motor + " measured_wpm=" + result.MeasuredWpm.ToString("F3") +
                    " nearest_wpm=" + result.PredictedWpm.ToString("F3") + " target=" + result.Proposed.WordsPerMinute);
                }
            });
            Test("burst and thinking estimators recover their distinct processes", delegate
            {
                var s = Profile(1);
                s.EnableTypos = false;
                s.JitterPercent = 25;
                s.BurstPercent = 50;
                s.ThinkingPauseChancePercent = 25;
                s
                .ThinkingPauseMilliseconds = 3000;
                var full = RunEngine(CalibrationPassage.Text, s, 919, Int64.MaxValue);
                var result = CalibrationAnalysis.Analyze(Capture(full), (int)Math.Ceiling(full.Now / 1000D), AppSettings.Defaults());
                Assert(Math.Abs(result.Proposed.BurstPercent - 50) <= 20, "burst estimate: " + result.Proposed.BurstPercent);
                Assert(Math.Abs(result.Proposed.ThinkingPauseChancePercent - 25) <= 6, "thinking chance: " + result.Proposed
                .ThinkingPauseChancePercent);
                Assert(Math.Abs(result.Proposed.ThinkingPauseMilliseconds - 3000) <= 700, "thinking maximum: " + result.Proposed
                .ThinkingPauseMilliseconds);
            });
            Test("class mistake risk and kind shares require sufficient independent errors", delegate
            {
                var keys = new List<CaptureKey>();
                var opportunities = new int[4];
                var errors = new int[4];
                double time = 0;
                const string shifted = "~!@#$%^&*()_+{}|:\"<>?";
                foreach (char c in CalibrationPassage.Text)
                {
                    int cl = Char.IsLetter(c) ? 0 : Char.IsDigit(c) ? 1 : shifted.IndexOf(c) >= 0 ? 3 : Char.IsPunctuation(c) || Char
                    .IsSymbol(c) ? 2 : -1;
                    if (cl >= 0)
                    {
                        opportunities[cl]++;
                    }
                    if (cl >= 0 && errors[cl] < (cl == 0 ? 40 : 10))
                    {
                        keys.Add(new CaptureKey(time, '~'));
                        time += 400;
                        keys.Add(new CaptureKey(time, '\b'));
                        time += 80;
                        errors[cl]++;
                    }
                    keys.Add(new CaptureKey(time, c));
                    time += 100;
                }
                var result = CalibrationAnalysis.Analyze(keys, (int)Math.Ceiling(time / 1000D), AppSettings.Defaults());
                var names = new[]
                {
                    "NumberTypoMultiplierPercent", "PunctuationTypoMultiplierPercent", "ShiftSymbolTypoMultiplierPercent"
                };
                for (int i = 1; i < 4; i++)
                {
                    int expected = Math.Min(500, (int)Math.Round(errors[i] / (double)opportunities[i] / (errors[0] / (double)opportunities
                    [0]) * 100));
                    int actual = (int)typeof(AppSettings).GetProperty(names[i - 1]).GetValue(result.Proposed, null);
                    Assert(Math.Abs(expected - actual) <= 2, names[i - 1] + ": expected " + expected + " actual " + actual);
                }
                Assert(result.Proposed.DuplicateChancePercent == 0 && result.Proposed.TransposeChancePercent == 0,
                "known nearby-only kind shares");
            });
            Test("detection estimator inverts fixed distribution with boundary censoring", delegate
            {
                var s = Profile(1);
                s.TypoChancePercent = 10;
                s.CorrectionDetectChars = 2;
                s.DuplicateChancePercent = 0;
                s
                .TransposeChancePercent = 0;
                var full = RunEngine(CalibrationPassage.Text, s, 777, Int64.MaxValue);
                var result = CalibrationAnalysis.Analyze(Capture(full), (int)Math.Ceiling(full.Now / 1000D), AppSettings.Defaults());
                Assert(result.Proposed.CorrectionDetectChars == 2, "detection maximum: " + result.Proposed.CorrectionDetectChars);
            });
            ResultsTests();
            if (Environment.GetEnvironmentVariable("AUTOWRITE_TEST_SCOPE") == "estimators")
            {
                _skipped++;
                Console.WriteLine("SKIP D6 (scoped estimator check)");
            }
            else D6();
        }

        static partial void ResultsTests();
        internal static AppSettings Profile(int profile)
        {
            var s = AppSettings.Defaults();
            s.WordsPerMinute = new[]
            {
                40, 42, 52, 5, 180
            }
            [profile];
            int i = profile < 3 ? profile : 1;
            s.JitterPercent = new[]
            {
                25, 45, 70
            }
            [i];
            s.KeyPairRhythmPercent = new[]
            {
                30, 60, 95
            }
            [i];
            s.BurstPercent = new[]
            {
                15, 25, 40
            }
            [i];
            s.WordPauseMilliseconds = new[]
            {
                60, 100, 40
            }
            [i];
            s.SentencePauseMilliseconds = new[]
            {
                300, 450, 180
            }
            [i];
            s.ParagraphPauseMilliseconds = new[]
            {
                130, 210, 80
            }
            [i];
            s.UppercaseDelayPenaltyPercent = new[]
            {
                35, 65, 95
            }
            [i];
            s.NumberDelayPenaltyPercent = new[]
            {
                55, 85, 115
            }
            [i];
            s.PunctuationDelayPenaltyPercent = new[]
            {
                65, 95, 125
            }
            [i];
            s.ShiftSymbolDelayPenaltyPercent = new[]
            {
                95, 125, 155
            }
            [i];
            s.TypoChancePercent = new[]
            {
                3, 4, 5
            }
            [i];
            s.CorrectionNoticeMilliseconds = new[]
            {
                1050, 650, 350
            }
            [i];
            s.CorrectionPauseMilliseconds = new[]
            {
                150, 95, 55
            }
            [i];
            s.CorrectionResumeMilliseconds = new[]
            {
                300, 240, 100
            }
            [i];
            s.CorrectionDetectChars = new[]
            {
                2, 4, 6
            }
            [i];
            s.ThinkingPauseChancePercent = new[]
            {
                2, 6, 8
            }
            [i];
            s.ThinkingPauseMilliseconds = new[]
            {
                1200, 1700, 2400
            }
            [i];
            return s;
        }

        private static readonly string[] GateSettings =
        {
            "WordPauseMilliseconds", "SentencePauseMilliseconds", "ParagraphPauseMilliseconds", "UppercaseDelayPenaltyPercent",
            "NumberDelayPenaltyPercent", "PunctuationDelayPenaltyPercent", "ShiftSymbolDelayPenaltyPercent", "JitterPercent",
            "KeyPairRhythmPercent", "TypoChancePercent", "CorrectionNoticeMilliseconds", "CorrectionPauseMilliseconds",
            "CorrectionResumeMilliseconds"
        };
        private static double Bound(string setting, double truth)
        {
            if (setting.Contains("DelayPenalty"))
            {
                return 20;
            }
            if (setting == "JitterPercent")
            {
                return 15;
            }
            if (setting == "KeyPairRhythmPercent")
            {
                return 25;
            }
            if (setting == "TypoChancePercent")
            {
                return 1;
            }
            if (setting.StartsWith("Correction", StringComparison.Ordinal))
            {
                return Math.Max(60, truth * .3);
            }
            return Math.Max(50, truth * .25);
        }

        private static double Percentile(List<double> values, double quantile)
        {
            values.Sort();
            return values[Math.Max(0, (int)Math.Ceiling(values.Count * quantile) - 1)];
        }

        private static double Min(List<double> values)
        {
            double v = Double.MaxValue;
            foreach (double x in values)
            {
                v = Math.Min(v, x);
            }
            return v;
        }

        private static double Max(List<double> values)
        {
            double v = 0;
            foreach (double x in values)
            {
                v = Math.Max(v, x);
            }
            return v;
        }

        private static double RelativeError(double actual, double expected)
        {
            return Math.Abs(actual - expected) / Math.Max(1, expected) * 100;
        }

        private static void Metric(StringBuilder report, int duration, int profile, string name, List<double> values)
        {
            report.Append(duration).Append('\t').Append(profile).Append('\t').Append(name).Append("\t0\t0\t");
            report.Append(CalibrationAnalysis.Median(values)).Append('\t').Append(Percentile(values, .9));
            report.Append("\tNaN\treported\n");
            report.Append("# worst ").Append(duration).Append("s p").Append(profile).Append(' ').Append(name);
            report.Append(' ').Append(Max(values)).AppendLine();
        }

        private static void D6()
        {
            var report = new StringBuilder("duration_s\tprofile\tsetting\ttruth\tmedian_recovered\tmedian_error\tp90_error\tbound\tstatus\n");
            var runs = new StringBuilder("duration_s\tprofile\tseed\tsetting\ttruth\trecovered\tmeasured\treason\n");
            var failures = new List<string>();
            var reportSettings = new List<string>(GateSettings);
            foreach (var property in typeof(AppSettings).GetProperties())
            {
                if (property.Name != "WordsPerMinute" && !reportSettings.Contains(property.Name))
                {
                    reportSettings.Add(property.Name);
                }
            }
            report.AppendLine("# recording seeds 1000..1019; pooled replay ensemble 30000..30019; forecast 70001+101*i, i=0..8");
            report.AppendLine("# Current bounded settings are defaults; current thinking settings equal the profile for speed gates.");
            for (int duration = 30; duration <= 60; duration += 30)
            {
                for (int profile = 0; profile < 5; profile++)
                {
                    var settings = Profile(profile);
                    var recovered = new Dictionary<string, List<double>>();
                    var errors = new Dictionary<string, List<double>>();
                    var kept = new Dictionary<string, int>();
                    foreach (string name in reportSettings)
                    {
                        recovered[name] = new List<double>();
                        errors[name] = new List<double>();
                        kept[name] = 0;
                    }
                    var replayErrors = new List<double>();
                    var controlAErrors = new List<double>();
                    var controlBErrors = new List<double>();
                    var expectedErrors = new List<double>();
                    var rhythmErrors = new List<double>();
                    var defaultThinkingErrors = new List<double>();
                    var actualBases = new List<double>();
                    var recordingSpeeds = new List<double>();
                    var controls = new List<CalibrationMetrics>();
                    for (int replaySeed = 0; replaySeed < 20; replaySeed++)
                    {
                        controls.Add(CalibrationMetrics.Measure(Capture(RunEngine(CalibrationPassage.Text,
                            settings, 30000 + replaySeed, duration * 1000, false))));
                    }
                    int seeds = profile < 3 ? 20 : 5;
                    for (int seed = 0; seed < seeds; seed++)
                    {
                        var runtime = RunEngine(CalibrationPassage.Text, settings, 1000 + seed, duration * 1000);
                        string telemetry = File.ReadAllText(Path.Combine(DataDirectory.PathRoot, "engine.tsv"));
                        var match = System.Text.RegularExpressions.Regex.Match(telemetry, "base_delay_ms=([0-9]+)");
                        actualBases.Add(Double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture));
                        var capture = Capture(runtime);
                        var measured = CalibrationMetrics.Measure(capture);
                        recordingSpeeds.Add(measured.Wpm);
                        var current = AppSettings.Defaults();
                        current.ThinkingPauseChancePercent = settings.ThinkingPauseChancePercent;
                        current.ThinkingPauseMilliseconds = settings.ThinkingPauseMilliseconds;
                        var result = CalibrationAnalysis.Analyze(capture, duration, current);
                        var defaultsResult = CalibrationAnalysis.Analyze(capture, duration, AppSettings.Defaults());
                        var controlBSettings = new AppSettings();
                        foreach (var row in result.Estimates)
                        {
                            var property = typeof(AppSettings).GetProperty(row.Setting);
                            property.SetValue(controlBSettings, row.Measured ? property.GetValue(settings, null) : row.Proposed, null);
                            runs.Append(duration).Append('\t').Append(profile).Append('\t').Append(1000 + seed);
                            runs.Append('\t').Append(row.Setting).Append('\t').Append(property.GetValue(settings, null));
                            runs.Append('\t').Append(row.Proposed).Append('\t').Append(row.Measured).Append('\t');
                            runs.AppendLine(row.Reason.Replace("\t", " ").Replace("\r", " ").Replace("\n", " "));
                            if (!row.Measured && !Object.Equals(row.Proposed, row.Current))
                            {
                                failures.Add("kept setting changed: " + row.Setting);
                            }
                            if (!reportSettings.Contains(row.Setting))
                            {
                                continue;
                            }
                            bool bounded = Array.IndexOf(GateSettings, row.Setting) >= 0;
                            double truth = Convert.ToDouble(property.GetValue(settings, null));
                            double value = Convert.ToDouble(row.Proposed);
                            if (bounded && profile < 3 && Object.Equals(property.GetValue(settings, null), row.Current))
                            {
                                failures.Add("current equals ground truth: " + row.Setting);
                            }
                            if (row.Measured || !bounded)
                            {
                                recovered[row.Setting].Add(value);
                            }
                            if (!row.Measured)
                            {
                                kept[row.Setting]++;
                            }
                            errors[row.Setting].Add(Math.Abs(value - truth));
                        }
                        double expected = 0, ordinary = 0, defaultExpected = 0;
                        for (int replaySeed = 0; replaySeed < 20; replaySeed++)
                        {
                            var forecast = CalibrationMetrics.Measure(Capture(RunEngine(CalibrationPassage.Text,
                                result.Proposed, 30000 + replaySeed, duration * 1000, false)));
                            var controlB = CalibrationMetrics.Measure(Capture(RunEngine(CalibrationPassage.Text,
                                controlBSettings, 30000 + replaySeed, duration * 1000, false)));
                            var defaultForecast = CalibrationMetrics.Measure(Capture(RunEngine(CalibrationPassage.Text,
                                defaultsResult.Proposed, 30000 + replaySeed, duration * 1000, false)));
                            expected += forecast.Wpm / 20;
                            ordinary += forecast.OrdinaryGapMilliseconds / 20;
                            defaultExpected += defaultForecast.Wpm / 20;
                            replayErrors.Add(RelativeError(forecast.Wpm, measured.Wpm));
                            controlAErrors.Add(RelativeError(controls[replaySeed].Wpm, measured.Wpm));
                            controlBErrors.Add(RelativeError(controlB.Wpm, measured.Wpm));
                        }
                        expectedErrors.Add(RelativeError(expected, measured.Wpm));
                        rhythmErrors.Add(RelativeError(ordinary, measured.OrdinaryGapMilliseconds));
                        defaultThinkingErrors.Add(RelativeError(defaultExpected, measured.Wpm));
                        File.WriteAllText(Path.Combine(Output, "D6-" + duration + "s-p" + profile + "-s" + seed + ".txt"), runtime.Log());
                    }
                    foreach (string name in reportSettings)
                    {
                        double truth = Convert.ToDouble(typeof(AppSettings).GetProperty(name).GetValue(settings, null));
                        bool bounded = Array.IndexOf(GateSettings, name) >= 0;
                        double median = CalibrationAnalysis.Median(recovered[name]);
                        double error = Math.Abs(median - truth);
                        double bound = bounded ? Bound(name, truth) : Double.NaN;
                        bool gated = duration == 60 && profile < 3 && bounded;
                        report.AppendLine("# kept " + duration + "s p" + profile + " " + name + " " + kept[name] + "/" + seeds);
                        report.Append(duration).Append('\t').Append(profile).Append('\t').Append(name).Append('\t').Append(truth);
                        report.Append('\t').Append(median).Append('\t').Append(error).Append('\t');
                        report.Append(Percentile(errors[name], .9)).Append('\t').Append(bound).Append('\t');
                        report.AppendLine(gated ? (error <= bound && kept[name] <= 2 ? "PASS" : "FAIL") : "reported");
                        if (gated && (error > bound || kept[name] > 2))
                        {
                            failures.Add("p" + profile + " " + name + " error=" + error + " bound=" + bound + " kept=" + kept[name]);
                        }
                    }
                    Metric(report, duration, profile, "ExpectedReplayWpmErrorPercent", expectedErrors);
                    Metric(report, duration, profile, "ExpectedReplayOrdinaryGapErrorPercent", rhythmErrors);
                    Metric(report, duration, profile, "DefaultThinkingExpectedWpmErrorPercent", defaultThinkingErrors);
                    Metric(report, duration, profile, "PooledReplayWpmErrorPercent", replayErrors);
                    Metric(report, duration, profile, "PooledControlAErrorPercent", controlAErrors);
                    Metric(report, duration, profile, "PooledControlBErrorPercent", controlBErrors);
                    double meanSpeed = CalibrationAnalysis.Mean(recordingSpeeds);
                    var variation = new List<double>();
                    foreach (double speed in recordingSpeeds)
                    {
                        variation.Add(RelativeError(speed, meanSpeed));
                    }
                    Metric(report, duration, profile, "TestVsSameWindowMeanWpmErrorPercent", variation);
                    double ratio = CalibrationAnalysis.Median(replayErrors) / Math.Max(.01, CalibrationAnalysis.Median(controlBErrors));
                    report.AppendLine("# pooled recovered / Control B ratio " + duration + "s p" + profile + " " + ratio);
                    report.AppendLine("# actual base range " + duration + "s p" + profile + " " + Min(actualBases) + ".." + Max(actualBases));
                    if (profile < 3 && ratio > 1.25)
                    {
                        failures.Add(duration + "s p" + profile + " pooled replay / Control B ratio=" + ratio);
                    }
                    if (duration == 60 && profile < 3)
                    {
                        if (CalibrationAnalysis.Median(expectedErrors) > 5 || CalibrationAnalysis.Median(rhythmErrors) > 5)
                        {
                            failures.Add("p" + profile + " expected active speed error=" + CalibrationAnalysis.Median(expectedErrors) +
                                "; ordinary gap error=" + CalibrationAnalysis.Median(rhythmErrors));
                        }
                        if (Min(actualBases) <= 45 || Max(actualBases) >= 220)
                        {
                            failures.Add("p" + profile + " clamp bound");
                        }
                    }
                }
            }
            File.WriteAllText(Path.Combine(Output, "D6-distribution.tsv"), report.ToString());
            File.WriteAllText(Path.Combine(Output, "D6-estimates.tsv"), runs.ToString());
            Console.WriteLine(report.ToString());
            Test("D6 60s recovery rhythm active-speed and pooled replay bounds", delegate
            {
                Assert(failures.Count == 0, String.Join("; ", failures.ToArray()));
            });
        }
    }
}
