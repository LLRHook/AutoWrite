using System;
using System.Collections.Generic;

namespace AutoWrite
{
    internal sealed class CalibrationMetrics
    {
        // Ten seconds is an interruption, rather than a composing pause. Remove the
        // whole gap, and report every omission; shorter hesitations remain in speed.
        internal const double InterruptionMilliseconds = 10000;
        internal double Wpm, OrdinaryGapMilliseconds, ActiveMilliseconds;
        internal int OrdinaryGapCount;
        internal readonly List<double> ExcludedPauses = new List<double>();

        internal static CalibrationMetrics Measure(IList<CaptureKey> keys)
        {
            var result = new CalibrationMetrics();
            if (keys.Count < 2)
            {
                return result;
            }
            int length = 0;
            foreach (var key in keys)
            {
                length = key.Character == '\b' ? Math.Max(0, length - 1) : length + 1;
            }
            result.ActiveMilliseconds = keys[keys.Count - 1].Milliseconds - keys[0].Milliseconds;
            for (int i = 1; i < keys.Count; i++)
            {
                double gap = keys[i].Milliseconds - keys[i - 1].Milliseconds;
                if (gap >= InterruptionMilliseconds)
                {
                    result.ExcludedPauses.Add(gap);
                    result.ActiveMilliseconds -= gap;
                }
            }
            result.Wpm = Math.Max(0, length - 1) * 12000D / Math.Max(1, result.ActiveMilliseconds);
            // Reconstruct edits without passage alignment: the same observable rule
            // applies to a person's recording and a forecast, including wrong text.
            var live = new List<int>();
            var correction = new bool[keys.Count];
            int retyping = 0;
            for (int i = 0; i < keys.Count; i++)
            {
                if (keys[i].Character == '\b')
                {
                    correction[i] = true;
                    retyping++;
                    if (live.Count > 0)
                    {
                        correction[live[live.Count - 1]] = true;
                        live.RemoveAt(live.Count - 1);
                    }
                }
                else
                {
                    live.Add(i);
                    if (retyping > 0)
                    {
                        correction[i] = true;
                        retyping--;
                    }
                }
            }
            var ordinary = new List<double>();
            for (int i = 1; i < keys.Count; i++)
            {
                char previous = keys[i - 1].Character, current = keys[i].Character;
                if (!correction[i - 1] && !correction[i] && previous >= 'a' && previous <= 'z' &&
                    current >= 'a' && current <= 'z')
                {
                    ordinary.Add(keys[i].Milliseconds - keys[i - 1].Milliseconds);
                }
            }
            result.OrdinaryGapMilliseconds = CalibrationAnalysis.Median(ordinary);
            result.OrdinaryGapCount = ordinary.Count;
            return result;
        }

        internal string ActivityDescription()
        {
            string description = "Active typing: first to last key; gaps under 10 s included.";
            if (ExcludedPauses.Count == 0)
            {
                return description + " No long pauses omitted.";
            }
            var omitted = new List<string>();
            foreach (double gap in ExcludedPauses)
            {
                omitted.Add((gap / 1000).ToString("F1") + " s");
            }
            return description + " Pauses not counted: " + String.Join(", ", omitted.ToArray()) + ".";
        }
    }
}
