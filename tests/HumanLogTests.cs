using System;
using System.Collections.Generic;
namespace AutoWrite.Tests
{
    internal static partial class Runner
    {
        // Independent deterministic human-log generator: no TypingEngine or analysis prediction calls.
        private static List<CaptureKey> HumanLog(int placement, bool hesitation, bool errors)
        {
            string text = CalibrationPassage.Text;
            var gaps = new double[text.Length];
            const string left = "qwertasdfgzxcvb12345`~!@#$%";
            const string right = "yuiophjklnm67890^&*()_-+={[}]|\\:;\"'<,>.?/";
            const string shifted = "~!@#$%^&*()_+{}|:\"<>?";
            for (int i = 1; i < text.Length; i++)
            {
                char c = text[i], previous = text[i - 1];
                double factor = shifted.IndexOf(c) >= 0 ? 2.1 : Char.IsUpper(c) ? 1.4 : Char.IsDigit(c) ? 1.6 : Char.IsPunctuation(c) ||
                Char.IsSymbol(c) ? 1.8 : 1;
                char a = Char.ToLowerInvariant(previous), b = Char.ToLowerInvariant(c);
                int handA = left.IndexOf(a) >= 0 ? 0 : right.IndexOf(a) >= 0 ? 1 : -1;
                int handB = left.IndexOf(b) >= 0 ? 0 : right.IndexOf(b) >= 0 ? 1 : -1;
                double pair = a == b ? .45 : handA < 0 || handB < 0 ? 0 : handA == handB ? .22 : -.15;
                gaps[i] = 100 * factor * (1 + pair * .7);
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
            var extra = new double[text.Length];
            if (hesitation)
            {
                int changed = 0;
                for (int i = 10; i < text.Length && changed < 3; i++)
                {
                    if (Char.IsLower(text[i - 1]) && Char.IsLower(text[i]))
                    {
                        extra[i] += 2000 + changed * 500;
                        changed++;
                        i += 20;
                    }
                }
                for (int i = 100; i < text.Length; i++)
                {
                    if (shifted.IndexOf(text[i]) >= 0)
                    {
                        extra[i] += 4000;
                        break;
                    }
                }
            }
            var keys = new List<CaptureKey>();
            double time = 0;
            int skip = text.IndexOf("costs", StringComparison.Ordinal);
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
                char c = errors && (i == 15 || i == 95) ? 'z' : text[i];
                double delay = 0;
                for (int j = 0; j <= i; j++)
                {
                    delay += extra[j];
                }
                keys.Add(new CaptureKey(time + delay, c));
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
            Test("human hesitations errors and skipped words do not move bounded estimates", delegate
            {
                var clean = CalibrationAnalysis.Analyze(HumanLog(0, false, false), 60, HumanCurrent());
                foreach (bool errors in new[]
                {
                    false, true
                })
                {
                    var log = HumanLog(0, true, errors);
                    var result = CalibrationAnalysis.Analyze(log, 60, HumanCurrent());
                    foreach (string setting in GateSettings)
                    {
                        double baseline = Convert.ToDouble(typeof(AppSettings).GetProperty(setting).GetValue(clean.Proposed, null));
                        double actual = Convert.ToDouble(typeof(AppSettings).GetProperty(setting).GetValue(result.Proposed, null));
                        Assert(Math.Abs(actual - baseline) <= Bound(setting, baseline), "hesitation " + setting + " errors=" + errors);
                    }
                    Assert(Math.Abs(result.Proposed.WordsPerMinute - clean.Proposed.WordsPerMinute) <= clean.Proposed.WordsPerMinute * .05
                    , "hesitation changed replay speed");
                    if (errors)
                    {
                        Assert(result.AlignedCharacters > log.Count && result.Accuracy < 100, "skipped word was not aligned");
                        Assert(Math.Abs(result.AlignedCharacters - clean.AlignedCharacters) <= 1, "lost alignment after skipped word");
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
