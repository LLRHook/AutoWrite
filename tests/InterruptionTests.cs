using System;
using System.Collections.Generic;

namespace AutoWrite.Tests
{
    internal static partial class Runner
    {
        private static List<CaptureKey> ComposingLog(bool interrupted, bool midWord)
        {
            // Paired recordings use the same finite transcription, rather than letting
            // different amounts of text confound the effect of omitting one observation.
            // The underlying gamma/additive typist is independent of the engine.
            var source = HumanLog(0, false, false);
            var keys = new List<CaptureKey>();
            double added = 0;
            int composing = 0;
            bool away = false;
            for (int i = 0; i < Math.Min(230, source.Count); i++)
            {
                bool boundary = i > 0 && source[i - 1].Character == ' ';
                if (boundary && composing < 3 && i > 20 + composing * 35)
                {
                    added += 1600;
                    composing++;
                }
                bool ordinary = i > 0 && Char.IsLower(source[i - 1].Character) && Char.IsLower(source[i].Character);
                if (interrupted && !away && i > 160 && (midWord ? ordinary : boundary))
                {
                    added += midWord ? 12000 : 15000;
                    away = true;
                }
                keys.Add(new CaptureKey(source[i].Milliseconds + added, source[i].Character));
            }
            Assert(composing == 3 && (!interrupted || away), "pause fixture coverage");
            Assert(keys[keys.Count - 1].Milliseconds < 60000, "pause fixture exceeded the actual window");
            return keys;
        }

        private static void InterruptionTests()
        {
            Test("slow 211 and 313 ms typists get the reachable edge of a tied replay", delegate
            {
                foreach (int motor in new[] { 211, 313 })
                {
                    var keys = new List<CaptureKey>();
                    double time = 0;
                    for (int i = 0; i < CalibrationPassage.Text.Length && time < 60000; i++)
                    {
                        char c = CalibrationPassage.Text[i];
                        keys.Add(new CaptureKey(time, c));
                        time += motor + (c == ' ' ? 100 : 0) + (".!?".IndexOf(c) >= 0 ? 450 : 0) + (c == '\n' ? 210 : 0);
                    }
                    var current = HumanCurrent();
                    current.KeyPairRhythmPercent = 0;
                    var result = CalibrationAnalysis.Analyze(keys, 60, current);
                    int selected = result.Proposed.WordsPerMinute;
                    double gap = result.PredictedOrdinaryGapMilliseconds;
                    result.Proposed.WordsPerMinute = 5;
                    if (motor == 313)
                    {
                        Assert(CalibrationReplay.Forecast(result.Proposed, 60).OrdinaryGapMilliseconds == gap, "slow fixture missed plateau");
                    }
                    int edge = selected;
                    for (int target = 6; target <= 180; target++)
                    {
                        result.Proposed.WordsPerMinute = target;
                        if (CalibrationReplay.Forecast(result.Proposed, 60).OrdinaryGapMilliseconds == gap)
                        {
                            edge = Math.Max(edge, target);
                        }
                    }
                    Assert(selected == edge && edge > 5, "slow tie motor=" + motor + " selected=" + selected + " edge=" + edge);
                    Assert(Estimate(result, "WordsPerMinute").ClosestAchievable == (motor == 313), "incorrect slow-limit label");
                    Console.WriteLine("CLAMP TIE motor=" + motor + " selected=" + selected + " edge=" + edge);
                }
            });
            Test("interrupted notice repair and resume do not become correction evidence", delegate
            {
                foreach (int phase in new[] { -1, 0, 1, 2 })
                {
                    var keys = new List<CaptureKey>();
                    double time = 0;
                    int episodes = 0;
                    for (int i = 0; i < 150; i++)
                    {
                        char c = CalibrationPassage.Text[i];
                        bool mistake = (i == 25 || i == 95 || i == 135);
                        keys.Add(new CaptureKey(time, mistake ? 'z' : c));
                        if (mistake)
                        {
                            time += 600 + (episodes == 0 && phase == 0 ? 15000 : 0);
                            keys.Add(new CaptureKey(time, '\b'));
                            time += 100 + (episodes == 0 && phase == 1 ? 15000 : 0);
                            keys.Add(new CaptureKey(time, c));
                            time += 250 + (episodes == 0 && phase == 2 ? 15000 : 0);
                            episodes++;
                        }
                        time += 100;
                    }
                    var result = CalibrationAnalysis.Analyze(keys, 60, AppSettings.Defaults());
                    Assert(result.CorrectedMistakes == 3, "observed correction count lost");
                    foreach (string name in new[] { "CorrectionNoticeMilliseconds", "TypoChancePercent" })
                    {
                        var row = Estimate(result, name);
                        Assert(row.Measured == (phase == -1), "interrupted correction evidence phase=" + phase + " " + name);
                        if (phase >= 0)
                        {
                            Assert(Object.Equals(row.Proposed, row.Current), "interrupted correction changed a kept value");
                        }
                    }
                }
            });
            Test("interruptions are omitted from thinking fit and ordinary rhythm", delegate
            {
                var clean = CalibrationAnalysis.Analyze(ComposingLog(false, false), 60, AppSettings.Defaults());
                foreach (bool midWord in new[] { false, true })
                {
                    var result = CalibrationAnalysis.Analyze(ComposingLog(true, midWord), 60, AppSettings.Defaults());
                    foreach (string name in new[] { "ThinkingPauseChancePercent", "ThinkingPauseMilliseconds" })
                    {
                        Assert(!Estimate(clean, name).Measured, "clean fixture measured thin thinking");
                        Assert(!Estimate(result, name).Measured, "interruption counted as composing: " + name);
                    }
                    // Removing one noisy observation can move a fitted value. Allow at
                    // most one target step and 5 ms / 3 percentage points elsewhere:
                    // rounding plus the influence of one gamma sample, far below D6 bounds.
                    foreach (string name in GateSettings)
                    {
                        double before = Convert.ToDouble(Estimate(clean, name).Proposed);
                        double after = Convert.ToDouble(Estimate(result, name).Proposed);
                        double tolerance = name.EndsWith("Milliseconds", StringComparison.Ordinal) ? 5 : 3;
                        Assert(Math.Abs(after - before) <= tolerance, "interrupted estimate " + name + ": " + before + " -> " + after);
                    }
                    Assert(Math.Abs(clean.Proposed.WordsPerMinute - result.Proposed.WordsPerMinute) <= 1, "interrupted target");
                    Console.WriteLine("INTERRUPTION mid_word=" + midWord + " target=" + result.Proposed.WordsPerMinute +
                        " thinking=" + result.Proposed.ThinkingPauseChancePercent + "/" + result.Proposed.ThinkingPauseMilliseconds);
                }
            });
        }
    }
}
