using System;
using System.Collections.Generic;
namespace AutoWrite.Tests
{
    internal static partial class Runner
    {
        // A separate additive/gamma model, cut at actual elapsed time after
        // inserting pauses. It never calls engine or analyser prediction helpers.
        private static List<CaptureKey> HumanLog(int placement, bool hesitation, bool errors)
        {
            string text = CalibrationPassage.Text;
            var random = new Random(915);
            var gaps = new double[text.Length];
            double regime = 1;
            for (int i = 1; i < text.Length; i++)
            {
                char c = text[i];
                if (text[i - 1] == ' ' && random.NextDouble() < .18)
                {
                    regime = random.NextDouble() < .5 ? .94 : 1.06;
                }
                double gamma = 0;
                for (int stage = 0; stage < 64; stage++)
                {
                    gamma -= Math.Log(Math.Max(1e-12, random.NextDouble())) / 64;
                }
                double cost = Char.IsUpper(c) ? 40 : Char.IsDigit(c) ? 60 :
                    "~!@#$%^&*()_+{}|:\"<>?".IndexOf(c) >= 0 ? 110 : Char.IsPunctuation(c) ? 80 : 0;
                gaps[i] = 100 * regime * gamma + cost;
            }
            for (int i = 0; i + 1 < text.Length; i++)
            {
                char c = text[i];
                if (".!?".IndexOf(c) >= 0)
                {
                    gaps[placement == 1 && i + 2 < text.Length && text[i + 1] == ' ' ? i + 2 : i + 1] += 410;
                }
                else if (Char.IsWhiteSpace(c))
                {
                    gaps[placement == 4 && c == ' ' ? i : i + 1] += 95;
                }
                if (c == '\n')
                {
                    gaps[placement == 2 ? i : i + 1] += placement == 3 ? 95 : 190;
                    if (placement == 3)
                    {
                        gaps[i] += 95;
                    }
                }
            }
            if (hesitation)
            {
                int changed = 0;
                for (int i = 10; i < text.Length && changed < 3; i++)
                {
                    if (Char.IsLower(text[i - 1]) && Char.IsLower(text[i]))
                    {
                        gaps[i] += 2000 + changed * 500;
                        changed++;
                        i += 20;
                    }
                }
                for (int i = 100; i < text.Length; i++)
                {
                    if ("~!@#$%^&*()_+{}|:\"<>?".IndexOf(text[i]) >= 0)
                    {
                        gaps[i] += 4000;
                        break;
                    }
                }
            }
            var keys = new List<CaptureKey>();
            double time = 0;
            int skip = text.IndexOf("found", StringComparison.Ordinal);
            for (int i = 0; i < text.Length; i++)
            {
                time += gaps[i];
                if (time >= 45000)
                {
                    break;
                }
                if (errors && i >= skip && i < skip + 6)
                {
                    continue;
                }
                // Uncorrected substitutions plus an omitted word are a separate error
                // process from the engine's planned and always-repaired mistakes.
                char c = errors && (i == 15 || i == 95) ? 'z' : text[i];
                keys.Add(new CaptureKey(time, c));
            }
            return keys;
        }

        private static AppSettings HumanCurrent()
        {
            var settings = AppSettings.Defaults();
            settings.EnableTypos = false;
            settings.JitterPercent = settings.BurstPercent = settings.ThinkingPauseChancePercent = 0;
            return settings;
        }

        private static void HumanLogTests()
        {
            Test("live probe configuration corrects mistakes through the virtual Backspace sink", delegate
            {
                var settings = LiveTypingProbe.Settings();
                Assert(settings.EnableTypos && settings.TypoChancePercent > 0, "probe mistakes disabled");
                var recording = RunEngine(LiveTypingProbe.ExpectedText, settings, LiveTypingProbe.Seed, Int64.MaxValue, false);
                int backspaces = 0;
                foreach (var key in recording.Keys)
                {
                    if (key.Value == '\b')
                    {
                        backspaces++;
                    }
                }
                Assert(backspaces > 0, "probe did not send Backspace");
                Assert(recording.FinalText() == LiveTypingProbe.ExpectedText, "probe final text differs");
                Console.WriteLine("PROBE VIRTUAL backspaces=" + backspaces + "; " + LiveTypingProbe.DifferenceSummary(LiveTypingProbe.
                ExpectedText, recording.FinalText()));
            });
            Test("human hesitations errors and skipped words do not move bounded estimates", delegate
            {
                var clean = CalibrationAnalysis.Analyze(HumanLog(0, false, false), 60, HumanCurrent());
                foreach (bool errors in new[]
                {
                    false, true
                })
                {
                    var log = HumanLog(0, true, errors);
                    Assert(log[log.Count - 1].Milliseconds < 45000, "generator exceeded the real window");
                    Assert(log.Count < HumanLog(0, false, errors).Count, "hesitations did not reduce characters typed");
                    var result = CalibrationAnalysis.Analyze(log, 60, HumanCurrent());
                    foreach (string setting in GateSettings)
                    {
                        double baseline = Convert.ToDouble(typeof(AppSettings).GetProperty(setting).GetValue(clean.Proposed, null));
                        double actual = Convert.ToDouble(typeof(AppSettings).GetProperty(setting).GetValue(result.Proposed, null));
                        Assert(Math.Abs(actual - baseline) <= Bound(setting, baseline), "hesitation " + setting + " errors=" + errors);
                    }
                    var replay = CalibrationReplay.Forecast(result.Proposed, 60);
                    Assert(RelativeError(replay.OrdinaryGapMilliseconds,
                        CalibrationMetrics.Measure(log).OrdinaryGapMilliseconds) <= 10, "hesitation changed key rhythm");
                    if (errors)
                    {
                        Assert(result.AlignedCharacters > log.Count && result.Accuracy < 100, "skipped word was not aligned");
                        Assert(result.Accuracy > 90, "lost alignment after skipped word");
                    }
                }
            });
            Test("human boundary units recover relocated word sentence and paragraph pauses", delegate
            {
                var clean = CalibrationAnalysis.Analyze(HumanLog(0, false, false), 60, HumanCurrent());
                for (int placement = 1; placement <= 4; placement++)
                {
                    var moved = CalibrationAnalysis.Analyze(HumanLog(placement, false, false), 60, HumanCurrent());
                    foreach (string setting in GateSettings)
                    {
                        double baseline = Convert.ToDouble(typeof(AppSettings).GetProperty(setting).GetValue(clean.Proposed, null));
                        double actual = Convert.ToDouble(typeof(AppSettings).GetProperty(setting).GetValue(moved.Proposed, null));
                        Assert(Math.Abs(actual - baseline) <= Bound(setting, baseline), "relocated " + placement + " " + setting);
                    }
                    Assert(Math.Abs(moved.Proposed.WordPauseMilliseconds - 95) <= 50, "word pause");
                    Assert(Math.Abs(moved.Proposed.SentencePauseMilliseconds - 410) <= 102.5, "sentence pause");
                    Assert(Math.Abs(moved.Proposed.ParagraphPauseMilliseconds - 190) <= 50, "paragraph pause");
                }
            });
        }
    }
}
