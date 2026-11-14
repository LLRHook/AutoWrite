using System;
using System.Collections.Generic;

namespace AutoWrite
{
    internal static class CalibrationEvidence
    {
        // Display warnings only. Estimator eligibility and numeric bounds do not change.
        // Levels cover observed sample counts with errors beyond the recovery bounds
        // in the 30/60 s benchmark; the accuracy document publishes both sample bins.
        private static readonly Dictionary<string, int> FewBelow = new Dictionary<string, int>
        {
            { "WordPauseMilliseconds", 25 }, { "SentencePauseMilliseconds", 12 },
            { "ParagraphPauseMilliseconds", 6 }, { "UppercaseDelayPenaltyPercent", 18 },
            { "NumberDelayPenaltyPercent", 22 }, { "PunctuationDelayPenaltyPercent", 18 },
            { "ShiftSymbolDelayPenaltyPercent", 14 }, { "JitterPercent", 61 },
            { "KeyPairRhythmPercent", 134 }, { "BurstPercent", 39 }, { "TypoChancePercent", 17 },
            { "CorrectionNoticeMilliseconds", 12 }, { "CorrectionPauseMilliseconds", 10 },
            { "CorrectionResumeMilliseconds", 17 }, { "ThinkingPauseChancePercent", 8 },
            { "ThinkingPauseMilliseconds", 8 }, { "CorrectionDetectChars", 12 },
            { "DuplicateChancePercent", 30 }, { "TransposeChancePercent", 30 }
        };

        internal static string Describe(string setting, int count, string noun)
        {
            int level;
            string text = "Measured from " + count + " " + noun;
            if (FewBelow.TryGetValue(setting, out level) && count < level)
            {
                return text + " (few samples; rerun for a steadier value).";
            }
            return text + ".";
        }
    }
}
