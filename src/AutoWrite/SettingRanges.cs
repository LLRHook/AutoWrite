using System;
namespace AutoWrite
{
    internal static class SettingRanges
    {
        internal const int MinimumValue = 0, MinimumWpm = 5, MaximumWpm = 180, MaximumPercent = 100;
        internal const int MaximumMistakeRate = 10;
        internal const int MaximumDetectionChars = 8;
        internal const int MaximumButtonDelaySeconds = 10;
        internal const int MaximumThinkingChance = 25;
        internal const int MaximumClassDelay = 200;
        internal const int MaximumShiftDelay = 300;
        internal const int MaximumMistakeMultiplier = 500;
        internal const int MaximumShortPauseMilliseconds = 1000;
        internal const int MaximumResumePauseMilliseconds = 2000;
        internal const int MaximumLongPauseMilliseconds = 3000;
        internal const int MaximumThinkingPauseMilliseconds = 5000;
        internal static int Minimum(string name)
        {
            return name == "WordsPerMinute" ? MinimumWpm : MinimumValue;
        }

        internal static int Maximum(string name)
        {
            switch (name)
            {
                case "WordsPerMinute":
                return MaximumWpm;
                case "ButtonDelaySeconds":
                return MaximumButtonDelaySeconds;
                case "CorrectionDetectChars":
                return MaximumDetectionChars;
                case "TypoChancePercent":
                return MaximumMistakeRate;
                case "ThinkingPauseChancePercent":
                return MaximumThinkingChance;
                case "ThinkingPauseMilliseconds":
                return MaximumThinkingPauseMilliseconds;
                case "CorrectionResumeMilliseconds":
                return MaximumResumePauseMilliseconds;
                case "NumberTypoMultiplierPercent":
                return MaximumMistakeMultiplier;
                case "PunctuationTypoMultiplierPercent":
                return MaximumMistakeMultiplier;
                case "ShiftSymbolTypoMultiplierPercent":
                return MaximumMistakeMultiplier;
                case "ShiftSymbolDelayPenaltyPercent":
                return MaximumShiftDelay;
                case "UppercaseDelayPenaltyPercent":
                return MaximumClassDelay;
                case "NumberDelayPenaltyPercent":
                return MaximumClassDelay;
                case "PunctuationDelayPenaltyPercent":
                return MaximumClassDelay;
                case "SentencePauseMilliseconds":
                return MaximumLongPauseMilliseconds;
                case "ParagraphPauseMilliseconds":
                return MaximumLongPauseMilliseconds;
                case "CorrectionNoticeMilliseconds":
                return MaximumLongPauseMilliseconds;
                case "WordPauseMilliseconds":
                return MaximumShortPauseMilliseconds;
                case "CorrectionPauseMilliseconds":
                return MaximumShortPauseMilliseconds;
                default:
                return MaximumPercent;
            }
        }

        internal static int Clamp(string name, int value)
        {
            return Math.Max(Minimum(name), Math.Min(Maximum(name), value));
        }
    }
}
