using System;
using System.Collections.Generic;

namespace AutoWrite
{
    internal static class CalibrationEvidence
    {
        // Display warnings only. Estimator eligibility and numeric bounds do not change.
        // A measured row is sparse below twice its estimator's minimum sample count.
        private static readonly Dictionary<string, int> FewBelow = new Dictionary<string, int>
        {
            { "WordPauseMilliseconds", 24 }, { "SentencePauseMilliseconds", 6 },
            { "ParagraphPauseMilliseconds", 6 }, { "UppercaseDelayPenaltyPercent", 12 },
            { "NumberDelayPenaltyPercent", 12 }, { "PunctuationDelayPenaltyPercent", 12 },
            { "ShiftSymbolDelayPenaltyPercent", 12 }, { "JitterPercent", 120 },
            { "KeyPairRhythmPercent", 80 }, { "BurstPercent", 40 }, { "TypoChancePercent", 6 },
            { "CorrectionNoticeMilliseconds", 6 }, { "CorrectionPauseMilliseconds", 16 },
            { "CorrectionResumeMilliseconds", 6 }, { "ThinkingPauseChancePercent", 8 },
            { "ThinkingPauseMilliseconds", 8 }, { "CorrectionDetectChars", 16 },
            { "DuplicateChancePercent", 40 }, { "TransposeChancePercent", 40 }, { "EnableTypos", 6 },
            { "NumberTypoMultiplierPercent", 20 }, { "PunctuationTypoMultiplierPercent", 20 },
            { "ShiftSymbolTypoMultiplierPercent", 20 }
        };

        internal static string Describe(string setting, int count, string noun)
        {
            int level;
            string text = "Measured from " + count + " " + noun;
            if (FewBelow.TryGetValue(setting, out level) && count < level)
            {
                return text + " (few samples).";
            }
            return text + ".";
        }
    }
}
