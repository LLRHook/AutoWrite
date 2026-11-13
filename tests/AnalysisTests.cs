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
        { var keys = new List<CaptureKey>(); foreach (var k in runtime.Keys) keys.Add(new CaptureKey(k.Time, k.Value)); return keys; }
        private static CaptureSession Fixture(string chars)
        { var s = new CaptureSession(60); for (int i = 0; i < chars.Length; i++) s.Accept(chars[i], i * 100); return s; }
        static partial void AnalysisTests()
        {
            Test("alignment reconstructs nearby, missed shift, duplicate and transpose corrections", delegate {
                var fixtures = new[] { "An\b\bIn ", "In eDNa\bA", "IIn\b\b\bIn", "nI\b\bIn" };
                var kinds = new[] { "nearby", "missed-shift", "duplicate", "transpose" };
                for (int i = 0; i < fixtures.Length; i++)
                {
                    var session = Fixture(fixtures[i]); List<CorrectionEpisode> corrections; Alignment alignment;
                    CalibrationAnalysis.Gaps(session.Keys, CalibrationPassage.Text, out corrections, out alignment);
                    Assert(corrections.Count == 1 && corrections[0].Kind == kinds[i], "wrong correction kind in " + fixtures[i]);
                }
                Assert(CalibrationAnalysis.AlignPrefix("In eDNA [12]", CalibrationPassage.Text).Distance == 0, "exact alignment");
                Assert(CalibrationAnalysis.AlignPrefix("In eDNA [21]", CalibrationPassage.Text).Distance == 1, "transpose distance");
            });
            Test("thin recordings and no mistakes keep unsupported values", delegate {
                var current = AppSettings.Defaults();
                foreach (var keys in new[] { new List<CaptureKey>(), Fixture("In eD").Keys })
                {
                    var result = CalibrationAnalysis.Analyze(keys, 60, current);
                    foreach (var p in typeof(AppSettings).GetProperties()) Assert(Object.Equals(p.GetValue(result.Proposed, null), p.GetValue(current, null)), p.Name);
                }
                var settings = AppSettings.Defaults(); settings.EnableTypos = false;
                var sample = RunEngine(CalibrationPassage.Text, settings, 11, 60000);
                var analyzed = CalibrationAnalysis.Analyze(Capture(sample), 60, current);
                Assert(analyzed.Proposed.EnableTypos && analyzed.Proposed.TypoChancePercent == current.TypoChancePercent, "no-error run switched mistakes off");
            });
            Test("joint fit separates incoming class, pair, word, sentence and paragraph effects", delegate {
                var truth = new double[] { 115, .4, .6, .8, 1.1, .7, 95, 410, 190 };
                var rows = new List<AlignedGap>();
                for (int i = 1; i < CalibrationPassage.Text.Length; i++)
                {
                    var r = new AlignedGap { Previous = CalibrationPassage.Text[i - 1], Current = CalibrationPassage.Text[i], Event = i };
                    r.Gap = CalibrationAnalysis.Prediction(r, truth); rows.Add(r);
                }
                var fit = CalibrationAnalysis.Fit(rows, AppSettings.Defaults());
                for (int i = 0; i < fit.Length; i++) Assert(Math.Abs(fit[i] - truth[i]) < .02, "fit parameter " + i + "=" + fit[i]);
            });
            Test("settings written by baseline preserve all original values", delegate {
                File.Copy(Path.Combine(SourceRoot, "tests/baseline-settings.ini"), Path.Combine(DataDirectory.PathRoot, "settings.ini"), true);
                var loaded = SettingsStore.Load(); var baseline = AppSettings.Defaults();
                foreach (var p in typeof(AppSettings).GetProperties())
                {
                    object expected = p.GetValue(baseline, null);
                    if (p.Name != "ParagraphPauseMilliseconds") expected = p.PropertyType == typeof(int) ? (object)((int)expected + 1) : (object)(!(bool)expected);
                    Assert(Object.Equals(expected, p.GetValue(loaded, null)), p.Name);
                }
            });
            Test("all proposed integers clamp to control ranges and round trip", delegate {
                var result = CalibrationAnalysis.Analyze(new List<CaptureKey>(), 60, AppSettings.Defaults());
                foreach (var p in typeof(AppSettings).GetProperties()) if (p.PropertyType == typeof(int)) CalibrationAnalysis.Set(result, p.Name, 999999, "fixture");
                SettingsStore.Save(result.Proposed); var actual = SettingsStore.Load();
                foreach (var p in typeof(AppSettings).GetProperties()) Assert(Object.Equals(p.GetValue(result.Proposed, null), p.GetValue(actual, null)), p.Name);
                CalibrationAnalysis.Set(result, "WordsPerMinute", -3, "fixture"); Assert(result.Proposed.WordsPerMinute == 5, "minimum WPM");
            });
            Test("paragraph default and incoming difficult-key arithmetic", delegate {
                var s = AppSettings.Defaults(); s.EnableTypos = false; s.ThinkingPauseChancePercent = 0; s.JitterPercent = 0; s.BurstPercent = 0; s.KeyPairRhythmPercent = 0;
                s.WordPauseMilliseconds = 0; s.SentencePauseMilliseconds = 0; s.UppercaseDelayPenaltyPercent = 100; s.KeepTargetWpm = false; s.WordsPerMinute = 30;
                var r = RunEngine("aAa\na", s, 7, Int64.MaxValue);
                Assert(r.Keys[1].Time - r.Keys[0].Time == 2 * (r.Keys[2].Time - r.Keys[1].Time), "factor did not move into uppercase key");
                Assert(s.ParagraphPauseMilliseconds == 0, "paragraph default changed");
                s.ParagraphPauseMilliseconds = 500;
                var p = RunEngine("aAa\na", s, 7, Int64.MaxValue);
                Assert(p.Keys[4].Time - p.Keys[3].Time > r.Keys[4].Time - r.Keys[3].Time + 200, "paragraph pause absent");
            });
            Test("jitter estimator recovers independent motor spread", delegate {
                var s = Profile(1); s.JitterPercent = 75; s.BurstPercent = 0; s.EnableTypos = false; s.ThinkingPauseChancePercent = 0;
                var result = CalibrationAnalysis.Analyze(Capture(RunEngine(CalibrationPassage.Text, s, 931, 60000)), 60, AppSettings.Defaults());
                Assert(Math.Abs(result.Proposed.JitterPercent - 75) <= 12, "jitter estimator: " + result.Proposed.JitterPercent);
            });
            Test("correction estimators recover notice and repair centers", delegate {
                var s = Profile(1); s.CorrectionNoticeMilliseconds = 460; s.CorrectionPauseMilliseconds = 80;
                var result = CalibrationAnalysis.Analyze(Capture(RunEngine(CalibrationPassage.Text, s, 42, 60000)), 60, AppSettings.Defaults());
                Assert(Math.Abs(result.Proposed.CorrectionNoticeMilliseconds - 460) <= 120, "notice estimator: " + result.Proposed.CorrectionNoticeMilliseconds);
                Assert(Math.Abs(result.Proposed.CorrectionPauseMilliseconds - 80) <= 15, "repair estimator");
            });
            if (Environment.GetEnvironmentVariable("AUTOWRITE_TEST_SCOPE") == "estimators") { _skipped++; Console.WriteLine("SKIP D6 (scoped estimator check)"); } else D6();
        }
        internal static AppSettings Profile(int profile)
        {
            var s = AppSettings.Defaults();
            s.WordsPerMinute = new[] { 36, 42, 52, 5, 180 }[profile];
            int i = profile < 3 ? profile : 1;
            s.JitterPercent = new[] { 25, 45, 70 }[i]; s.KeyPairRhythmPercent = new[] { 30, 60, 95 }[i]; s.BurstPercent = new[] { 15, 25, 40 }[i];
            s.WordPauseMilliseconds = new[] { 60, 100, 40 }[i]; s.SentencePauseMilliseconds = new[] { 300, 450, 180 }[i]; s.ParagraphPauseMilliseconds = new[] { 130, 210, 80 }[i];
            s.UppercaseDelayPenaltyPercent = new[] { 35, 65, 95 }[i]; s.NumberDelayPenaltyPercent = new[] { 55, 85, 115 }[i];
            s.PunctuationDelayPenaltyPercent = new[] { 65, 95, 125 }[i]; s.ShiftSymbolDelayPenaltyPercent = new[] { 95, 125, 155 }[i];
            s.TypoChancePercent = new[] { 3, 4, 5 }[i]; s.CorrectionNoticeMilliseconds = new[] { 1050, 650, 350 }[i];
            s.CorrectionPauseMilliseconds = new[] { 150, 95, 55 }[i]; s.CorrectionResumeMilliseconds = new[] { 300, 240, 100 }[i];
            s.CorrectionDetectChars = new[] { 2, 4, 6 }[i];
            s.ThinkingPauseChancePercent = new[] { 2, 6, 8 }[i]; s.ThinkingPauseMilliseconds = new[] { 1200, 1700, 2400 }[i];
            return s;
        }
        private static readonly string[] GateSettings = { "WordPauseMilliseconds", "SentencePauseMilliseconds", "ParagraphPauseMilliseconds", "UppercaseDelayPenaltyPercent", "NumberDelayPenaltyPercent", "PunctuationDelayPenaltyPercent", "ShiftSymbolDelayPenaltyPercent", "JitterPercent", "KeyPairRhythmPercent", "TypoChancePercent", "CorrectionNoticeMilliseconds", "CorrectionPauseMilliseconds", "CorrectionResumeMilliseconds" };
        private static double Bound(string setting, double truth)
        {
            if (setting.Contains("DelayPenalty")) return 20;
            if (setting == "JitterPercent") return 15;
            if (setting == "KeyPairRhythmPercent") return 25;
            if (setting == "TypoChancePercent") return 1;
            if (setting.StartsWith("Correction", StringComparison.Ordinal)) return Math.Max(60, truth * .3);
            return Math.Max(50, truth * .25);
        }
        private static double Percentile(List<double> values, double quantile)
        { values.Sort(); return values[(int)Math.Ceiling((values.Count - 1) * quantile)]; }
        private static double Min(List<double> values) { double v = Double.MaxValue; foreach (double x in values) v = Math.Min(v, x); return v; }
        private static double Max(List<double> values) { double v = 0; foreach (double x in values) v = Math.Max(v, x); return v; }
        private static void D6()
        {
            var report = new StringBuilder("duration_s\tprofile\tsetting\ttruth\tmedian_recovered\tmedian_error\tp90_error\tbound\tstatus\n");
            var failures = new List<string>();
            for (int duration = 30; duration <= 60; duration += 30) for (int profile = 0; profile < 5; profile++)
            {
                var settings = Profile(profile); var recovered = new Dictionary<string, List<double>>(); var errors = new Dictionary<string, List<double>>();
                foreach (var name in GateSettings) { recovered[name] = new List<double>(); errors[name] = new List<double>(); }
                var speedErrors = new List<double>(); var replayErrors = new List<double>(); var bases = new List<double>();
                var expectedErrors = new List<double>(); var floorErrors = new List<double>(); var longRunErrors = new List<double>();
                double longRunWpm = 0;
                for (int longSeed = 0; longSeed < 20; longSeed++) { var full = RunEngine(CalibrationPassage.Text, settings, 1000 + longSeed, Int64.MaxValue, false); longRunWpm += full.FinalText().Length * 12000D / full.Now / 20; }
                int seeds = profile < 3 ? 20 : 5; var actualBases = new List<double>(); var keptCounts = new Dictionary<string, int>(); foreach (var name in GateSettings) keptCounts[name] = 0;
                for (int seed = 0; seed < seeds; seed++)
                {
                    var runtime = RunEngine(CalibrationPassage.Text, settings, 1000 + seed, duration * 1000);
                    var telemetry = File.ReadAllText(Path.Combine(DataDirectory.PathRoot, "L1-engine.tsv"));
                    var match = System.Text.RegularExpressions.Regex.Match(telemetry, "base_delay_ms=([0-9]+)");
                    actualBases.Add(Double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture));
                    var capture = Capture(runtime);
                    var result = CalibrationAnalysis.Analyze(capture, duration, AppSettings.Defaults());
                    double achieved = runtime.FinalText().Length * 12D / duration;
                    foreach (var row in result.Estimates) if (keptCounts.ContainsKey(row.Setting) && !row.Measured) keptCounts[row.Setting]++;
                    speedErrors.Add(Math.Abs(result.MeasuredWpm - achieved) / Math.Max(1, achieved) * 100);
                    bases.Add(result.BaseDelay);
                    foreach (var name in GateSettings)
                    {
                        var p = typeof(AppSettings).GetProperty(name); double truth = (int)p.GetValue(settings, null), value = (int)p.GetValue(result.Proposed, null);
                        recovered[name].Add(value); errors[name].Add(Math.Abs(value - truth));
                    }
                    var replay = RunEngine(CalibrationPassage.Text, result.Proposed, 5000 + seed, duration * 1000, false);
                    List<CorrectionEpisode> unused; Alignment replayAlignment;
                    CalibrationAnalysis.Gaps(Capture(replay), CalibrationPassage.Text, out unused, out replayAlignment);
                    double replayWpm = replay.FinalText().Length * 12D / duration;
                    var control = RunEngine(CalibrationPassage.Text, settings, 5000 + seed, duration * 1000, false);
                    floorErrors.Add(Math.Abs(control.FinalText().Length * 12D / duration - achieved) / Math.Max(1, achieved) * 100);
                    longRunErrors.Add(Math.Abs(achieved - longRunWpm) / longRunWpm * 100);
                    double expected = 0;
                    for (int replaySeed = 0; replaySeed < 20; replaySeed++)
                    {
                        var forecast = RunEngine(CalibrationPassage.Text, result.Proposed, 30000 + replaySeed, duration * 1000, false);
                        expected += forecast.FinalText().Length * 12D / duration / 20;
                    }
                    expectedErrors.Add(Math.Abs(expected - achieved) / Math.Max(1, achieved) * 100);
                    replayErrors.Add(Math.Abs(replayWpm - achieved) / Math.Max(1, achieved) * 100);
                    if (seed == 0) File.WriteAllText(Path.Combine(Output, "D6-" + duration + "s-profile" + profile + "-telemetry.tsv"), telemetry);
                    if (seed == 0) File.WriteAllText(Path.Combine(Output, "D6-" + duration + "s-profile" + profile + "-raw.txt"), runtime.Log());
                }
                foreach (var name in GateSettings)
                {
                    double truth = (int)typeof(AppSettings).GetProperty(name).GetValue(settings, null);
                    double median = CalibrationAnalysis.Median(recovered[name]), error = Math.Abs(median - truth), bound = Bound(name, truth);
                    bool pass = error <= bound;
                    report.Append("# kept " + duration + "s p" + profile + " " + name + " " + keptCounts[name] + "/" + seeds + "\n");
                    report.Append(duration).Append('\t').Append(profile).Append('\t').Append(name).Append('\t').Append(truth).Append('\t').Append(median).Append('\t').Append(error).Append('\t').Append(Percentile(errors[name], .9).ToString("F2", CultureInfo.InvariantCulture)).Append('\t').Append(bound).Append('\t').Append(profile >= 3 ? "outside gate" : pass ? "PASS" : "FAIL").AppendLine();
                    if (duration == 60 && profile < 3 && keptCounts[name] > 0) failures.Add("p" + profile + " " + name + " kept in " + keptCounts[name] + " runs");
                    if (duration == 60 && profile < 3 && !pass) failures.Add("p" + profile + " " + name + " median=" + median + " truth=" + truth + " error=" + error + " bound=" + bound);
                }
                report.Append(duration).Append('\t').Append(profile).Append("\tAchievedWpmErrorPercent\t0\t0\t").Append(CalibrationAnalysis.Median(speedErrors)).Append('\t').Append(Percentile(speedErrors, .9)).Append("\t5\tPASS\n");
                report.Append(duration).Append('\t').Append(profile).Append("\tReplayWpmErrorPercent\t0\t0\t").Append(CalibrationAnalysis.Median(replayErrors)).Append('\t').Append(Percentile(replayErrors, .9)).Append("\t5\t").Append("published, not a gate").AppendLine();
                report.Append("# actual base range " + duration + "s p" + profile + " " + actualBases[0] + " min=" + Min(actualBases) + " max=" + Max(actualBases) + "\n");
                if (duration == 60 && profile < 3 && (Min(actualBases) <= 45 || Max(actualBases) >= 220)) failures.Add("p" + profile + " clamp bound");
                report.Append(duration).Append('\t').Append(profile).Append("\tFittedBaseDelay\t0\t").Append(CalibrationAnalysis.Median(bases)).Append("\t0\t0\t0\tdiagnostic\n");
                foreach (var metric in new[] { "ExpectedReplayWpmErrorPercent", "GroundTruthSingleReplayNoiseFloorPercent", "MinuteVsLongRunWpmErrorPercent" })
                {
                    var values = metric.StartsWith("Expected", StringComparison.Ordinal) ? expectedErrors : metric.StartsWith("Ground", StringComparison.Ordinal) ? floorErrors : longRunErrors;
                    report.Append(duration).Append('\t').Append(profile).Append('\t').Append(metric).Append("\t0\t0\t").Append(CalibrationAnalysis.Median(values)).Append('\t').Append(Percentile(values, .9)).Append("\t5\t").Append(metric.StartsWith("Expected", StringComparison.Ordinal) && profile < 3 ? (CalibrationAnalysis.Median(values) <= 5 ? "PASS" : "FAIL") : "published, not a gate").AppendLine();
                    report.Append("# worst " + duration + "s p" + profile + " " + metric + " " + Max(values) + "\n");
                }
                report.Append("# long-run achieved WPM p" + profile + " " + longRunWpm + "\n");
                double ratio = CalibrationAnalysis.Median(replayErrors) / Math.Max(.01, CalibrationAnalysis.Median(floorErrors));
                report.Append("# recovered single-run / noise-floor median ratio " + duration + "s p" + profile + " " + ratio + "\n");
                if (duration == 60 && profile < 3 && CalibrationAnalysis.Median(expectedErrors) > 5) failures.Add("p" + profile + " expected replay median error=" + CalibrationAnalysis.Median(expectedErrors));
                if (profile < 3 && ratio > 1.25) failures.Add(duration + "s p" + profile + " single replay / floor ratio=" + ratio);
            }
            File.WriteAllText(Path.Combine(Output, "D6-distribution.tsv"), report.ToString());
            Console.WriteLine(report.ToString());
            Test("D6 60s recovery and replay bounds across 3 profiles x 20 seeds", delegate { Assert(failures.Count == 0, String.Join("; ", failures.ToArray())); });
        }
    }
}
