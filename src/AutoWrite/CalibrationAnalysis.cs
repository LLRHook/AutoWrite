using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
namespace AutoWrite
{
    internal sealed class CalibrationEstimate
    {
        public string Setting;
        public object Current, Proposed;
        public bool Measured, ClosestAchievable;
        public string Reason;
    }
    internal sealed class CalibrationResult
    {
        public AppSettings Proposed;
        public readonly List<CalibrationEstimate> Estimates = new List<CalibrationEstimate>();
        public double MeasuredWpm, Accuracy;
        public double PredictedWpm, MinimumWpm, MaximumWpm;
        public string Warning = "";
        public int CorrectedMistakes, AlignedCharacters;
        public double BaseDelay;
        public double OrdinaryGapMilliseconds, PredictedOrdinaryGapMilliseconds;
        public string ActivityDescription = "";
    }
    internal sealed class AlignedGap
    {
        public char Previous, Current;
        public int Source, Event;
        public double Gap;
    }
    internal sealed class CorrectionEpisode
    {
        public int Start, End, Backspaces, FirstBackspace, LastRetype;
        public string Removed, Expected, Kind;
        public double Notice;
        public readonly List<double> Repair = new List<double>();
    }
    internal sealed class Alignment
    {
        public int End, Distance;
        public int[] Source;
    }
    internal static class FitParameter
    {
        internal const int Base = 0;
        internal const int Uppercase = 1;
        internal const int Number = 2;
        internal const int Punctuation = 3;
        internal const int ShiftSymbol = 4;
        internal const int Pair = 5;
        internal const int Word = 6;
        internal const int Sentence = 7;
        internal const int Paragraph = 8;
        internal const int Count = 9;
    }
    internal static class CalibrationAnalysis
    {
        // Genuine 10% uncorrected errors remain above 89%; wrong-start,
        // unrelated and random text are below 40% in both speed fixtures.
        internal const double MinimumAlignmentQuality = 70;
        internal static readonly string[] FitNames =
        {
            "UppercaseDelayPenaltyPercent", "NumberDelayPenaltyPercent", "PunctuationDelayPenaltyPercent",
            "ShiftSymbolDelayPenaltyPercent", "KeyPairRhythmPercent", "WordPauseMilliseconds", "SentencePauseMilliseconds",
            "ParagraphPauseMilliseconds"
        };
        private const string Left = "qwertasdfgzxcvb12345`~!@#$%";
        private const string Right = "yuiophjklnm67890^&*()_-+={[}]|\\:;\"'<,>.?/";
        internal static int Class(char c)
        {
            if ("~!@#$%^&*()_+{}|:\"<>?".IndexOf(c) >= 0)
            {
                return 4;
            }
            if (Char.IsUpper(c))
            {
                return 1;
            }
            if (Char.IsDigit(c))
            {
                return 2;
            }
            if (Char.IsPunctuation(c) || Char.IsSymbol(c))
            {
                return 3;
            }
            return 0;
        }

        internal static double Pair(char a, char b)
        {
            a = Char.ToLowerInvariant(a);
            b = Char.ToLowerInvariant(b);
            if (a == b)
            {
                return .45;
            }
            int x = Left.IndexOf(a) >= 0 ? 0 : Right.IndexOf(a) >= 0 ? 1 : -1;
            int y = Left.IndexOf(b) >= 0 ? 0 : Right.IndexOf(b) >= 0 ? 1 : -1;
            return x < 0 || y < 0 ? 0 : x == y ? .22 : -.15;
        }

        internal static bool Sentence(char c)
        {
            return c == '.' || c == '!' || c == '?';
        }

        internal static bool Boundary(char c)
        {
            return Char.IsWhiteSpace(c) || ",;:.!?".IndexOf(c) >= 0;
        }

        internal static Alignment AlignPrefix(string actual, string passage)
        {
            int n = actual.Length, m = Math.Min(passage.Length, n + 12);
            var d = new int[n + 1, m + 1];
            for (int i = 0; i <= n; i++)
            {
                d[i, 0] = i;
            }
            for (int j = 0; j <= m; j++)
            {
                d[0, j] = j;
            }
            for (int i = 1; i <= n; i++)
            {
                for (int j = 1; j <= m; j++)
                {
                    d[i, j] = Math.Min(d[i - 1, j] + 1, Math.Min(d[i, j - 1] + 1, d[i - 1, j - 1] + (actual[i - 1] == passage[j - 1] ? 0 :
                    1))
                    );
                    if (i > 1 && j > 1 && actual[i - 1] == passage[j - 2] && actual[i - 2] == passage[j - 1])
                    {
                        d[i, j] = Math.Min(d[i, j], d[i - 2, j - 2] + 1);
                    }
                }
            }
            int end = Math.Min(n, m);
            for (int j = Math.Max(0, n - 12); j <= m; j++)
            {
                if (d[n, j] < d[n, end] || (d[n, j] == d[n, end] && Math.Abs(j - n) < Math.Abs(end - n)))
                {
                    end = j;
                }
            }
            var map = new int[n];
            for (int k = 0; k < n; k++)
            {
                map[k] = -1;
            }
            int x = n, y = end;
            while (x > 0 || y > 0)
            {
                if (x > 1 && y > 1 && actual[x - 1] == passage[y - 2] && actual[x - 2] == passage[y - 1] && d[x, y] == d[x - 2, y - 2] + 1
                )
                {
                    map[x - 1] = y - 2;
                    map[x - 2] = y - 1;
                    x -= 2;
                    y -= 2;
                }
                else if (x > 0 && y > 0 && d[x, y] == d[x - 1, y - 1] + (actual[x - 1] == passage[y - 1] ? 0 : 1))
                {
                    map[x - 1] = y - 1;
                    x--;
                    y--;
                }
                else if (x > 0 && d[x, y] == d[x - 1, y] + 1)
                {
                    x--;
                }
                else y--;
            }
            return new Alignment
            {
                End = end, Distance = d[n, end], Source = map
            };
        }

        private static bool IsMissedShift(char expected, char sent)
        {
            if (Char.IsUpper(expected))
            {
                return Char.ToLowerInvariant(expected) == sent;
            }
            const string shifted = "~!@#$%^&*()_+{}|:\"<>?";
            const string plain = "`1234567890-=[]\\;'.,/";
            int index = shifted.IndexOf(expected);
            return index >= 0 && plain[index] == sent;
        }

        internal static List<AlignedGap> Gaps(IList<CaptureKey> keys, string passage, out List<CorrectionEpisode> corrections, out
        Alignment final)
        {
            corrections = new List<CorrectionEpisode>();
            var buffer = new StringBuilder();
            var live = new List<int>();
            var repaired = new HashSet<int>();
            for (int i = 0; i < keys.Count; i++)
            {
                if (keys[i].Character != '\b')
                {
                    buffer.Append(keys[i].Character);
                    live.Add(i);
                    continue;
                }
                int first = i;
                string before = buffer.ToString();
                var beforeAlignment = AlignPrefix(before, passage);
                int count = 0;
                while (i < keys.Count && keys[i].Character == '\b')
                {
                    if (buffer.Length > 0)
                    {
                        repaired.Add(live[live.Count - 1]);
                        live.RemoveAt(live.Count - 1);
                        buffer.Length--;
                        count++;
                    }
                    i++;
                }
                var afterAlignment = AlignPrefix(buffer.ToString(), passage);
                var episode = new CorrectionEpisode
                {
                    FirstBackspace = first, Start = afterAlignment.End, End = beforeAlignment.End, Backspaces = count, Notice = first > 0
                    ? keys[first].Milliseconds - keys[first - 1].Milliseconds : 0, Removed = before.Substring(before.Length - count),
                    LastRetype = -1
                };
                episode.Expected = passage.Substring(episode.Start, Math.Max(0, episode.End - episode.Start));
                episode.Kind = episode.Removed.Length > episode.Expected.Length ? "duplicate" : episode.Removed.Length == episode.Expected
                .Length && episode.Expected.Length >= 2 && episode.Removed[0] == episode.Expected[1] && episode.Removed[1] == episode.
                Expected[0] && episode.Removed.Substring(2) == episode.Expected.Substring(2) ? "transpose" : episode.Expected.Length > 0
                && episode.Removed.Length > 0 && IsMissedShift(episode.Expected[0], episode.Removed[0]) ? "missed-shift" : "nearby";
                for (int j = first + 1; j < i; j++)
                {
                    episode.Repair.Add(keys[j].Milliseconds - keys[j - 1].Milliseconds);
                }
                int retype = 0;
                while (i < keys.Count && retype < episode.Expected.Length && keys[i].Character == episode.Expected[retype])
                {
                    episode.Repair.Add(keys[i].Milliseconds - keys[i - 1].Milliseconds);
                    buffer.Append(keys[i].Character);
                    live.Add(i);
                    repaired.Add(i);
                    episode.LastRetype = i;
                    retype++;
                    i++;
                }
                if (count > 0 && retype == episode.Expected.Length && episode.Removed != episode.Expected)
                {
                    corrections.Add(episode);
                }
                i--;
            }
            final = AlignPrefix(buffer.ToString(), passage);
            var sourceByEvent = new Dictionary<int, int>();
            for (int i = 0; i < live.Count; i++)
            {
                if (final.Source[i] >= 0 && buffer[i] == passage[final.Source[i]])
                {
                    sourceByEvent[live[i]]
                    = final.Source[i];
                }
            }
            var gaps = new List<AlignedGap>();
            for (int i = 1; i < keys.Count; i++)
            {
                int a, b;
                if (!repaired.Contains(i) && !repaired.Contains(i - 1) && sourceByEvent.TryGetValue(i - 1, out a) &&
                sourceByEvent.TryGetValue(i, out b) && b == a + 1)
                {
                    gaps.Add(new AlignedGap
                    {
                        Previous = passage[a], Current = passage[b], Source = b, Event = i, Gap = keys[i].Milliseconds - keys[i -
                        1].Milliseconds
                    });
                }
            }
            return gaps;
        }

        internal static double Median(List<double> values)
        {
            if (values.Count == 0)
            {
                return 0;
            }
            values.Sort();
            int n = values.Count;
            return n % 2 == 1 ? values[n / 2] : (values[n / 2 - 1] + values[n / 2]) / 2;
        }

        internal static double Mean(List<double> values)
        {
            double sum = 0;
            foreach (double x in values)
            {
                sum += x;
            }
            return values.Count == 0 ? 0 : sum / values.Count;
        }

        internal static double Motor(AlignedGap row, double[] p)
        {
            int c = Class(row.Current);
            return p[FitParameter.Base] * (c == 0 ? 1 : 1 + p[c]) * (1 + Pair(row.Previous, row.Current) * p[FitParameter.Pair]);
        }

        internal static double Prediction(AlignedGap row, double[] p)
        {
            double pause = Sentence(row.Previous) ? p[FitParameter.Sentence] : Char.IsWhiteSpace(row.Previous) ? p[FitParameter.Word] : 0;
            if (row.Previous == '\n')
            {
                pause += p[FitParameter.Paragraph];
            }
            return Motor(row, p) + pause;
        }

        private static double[] Solve(double[,] a, double[] b)
        {
            int n = b.Length;
            for (int i = 0; i < n; i++)
            {
                int pivot = i;
                for (int j = i + 1; j < n; j++)
                {
                    if (Math.Abs(a[j, i]) > Math.Abs(a[pivot, i]))
                    {
                        pivot = j;
                    }
                }
                if (Math.Abs(a[pivot, i]) < 1e-10)
                {
                    continue;
                }
                for (int k = i; k < n; k++)
                {
                    double t = a[i, k];
                    a[i, k] = a[pivot, k];
                    a[pivot, k] = t;
                }
                double tb = b[i];
                b[i] = b[pivot];
                b[pivot] = tb;
                double div = a[i, i];
                for (int k = i; k < n; k++)
                {
                    a[i, k] /= div;
                }
                b[i] /= div;
                for (int j = 0; j < n; j++)
                {
                    if (j != i)
                    {
                        double f = a[j, i];
                        for (int k = i; k < n; k++)
                        {
                            a[j, k] -= f * a[i, k];
                        }
                        b[j] -= f * b[i];
                    }
                }
            }
            return b;
        }

        internal static List<List<AlignedGap>> BoundaryUnits(List<AlignedGap> rows)
        {
            var units = new List<List<AlignedGap>>();
            for (int i = 0; i < rows.Count; i++)
            {
                var unit = new List<AlignedGap>();
                unit.Add(rows[i]);
                while (Char.IsWhiteSpace(rows[i].Current) && i + 1 < rows.Count &&
                rows[i + 1].Event == rows[i].Event + 1 && rows[i + 1].Source == rows[i].Source + 1)
                {
                    i++;
                    unit.Add(rows[i]);
                }
                units.Add(unit);
            }
            return units;
        }

        private static double ParameterMaximum(int parameter)
        {
            switch (parameter)
            {
                case FitParameter.Base:
                return 400;
                case FitParameter.Uppercase:
                case FitParameter.Number:
                case FitParameter.Punctuation:
                return SettingRanges.MaximumClassDelay / 100D;
                case FitParameter.ShiftSymbol:
                return SettingRanges.MaximumShiftDelay / 100D;
                case FitParameter.Pair:
                return 1;
                case FitParameter.Word:
                return SettingRanges.MaximumShortPauseMilliseconds;
                default:
                return SettingRanges.MaximumLongPauseMilliseconds;
            }
        }

        internal static double[] Fit(List<AlignedGap> rows, AppSettings current)
        {
            var p = new double[FitParameter.Count];
            p[FitParameter.Base] = 120;
            p[FitParameter.Uppercase] = .2;
            p[FitParameter.Number] = .3;
            p[FitParameter.Punctuation] = .3;
            p[FitParameter.ShiftSymbol] = .5;
            p[FitParameter.Pair] = .5;
            p[FitParameter.Word] = current.WordPauseMilliseconds;
            p[FitParameter.Sentence] = current.SentencePauseMilliseconds;
            p[FitParameter.Paragraph] = current.ParagraphPauseMilliseconds;
            var units = BoundaryUnits(rows);
            for (int iteration = 0; iteration < 30; iteration++)
            {
                var a = new double[FitParameter.Count, FitParameter.Count];
                var b = new double[FitParameter.Count];
                foreach (var unit in units)
                {
                    double expected = 0, observed = 0, variance = 0;
                    var d = new double[FitParameter.Count];
                    foreach (var r in unit)
                    {
                        double motor = Motor(r, p), prediction = Prediction(r, p);
                        expected += prediction;
                        observed += r.Gap;
                        variance += motor * motor * .12 + Math.Pow(prediction - motor, 2) * .041 + 25;
                        int c = Class(r.Current);
                        double pair = Pair(r.Previous, r.Current);
                        d[FitParameter.Base] += motor / p[FitParameter.Base];
                        if (c > 0)
                        {
                            d[c] += p[FitParameter.Base] * (1 + pair * p[FitParameter.Pair]);
                        }
                        d[FitParameter.Pair] += p[FitParameter.Base] * (c == 0 ? 1 : 1 + p[c]) * pair;
                        if (Sentence(r.Previous))
                        {
                            d[FitParameter.Sentence]++;
                        }
                        else if (Char.IsWhiteSpace(r.Previous))
                        {
                            d[FitParameter.Word]++;
                        }
                        if (r.Previous == '\n')
                        {
                            d[FitParameter.Paragraph]++;
                        }
                    }
                    double residual = observed - expected;
                    double z = residual / Math.Sqrt(variance);
                    if (Math.Abs(z) > 6)
                    {
                        continue;
                    }
                    // Huber influence is bounded for every retained boundary or ordinary-key unit.
                    double influence = Math.Min(1, 3 / Math.Max(3, Math.Abs(z)));
                    double weight = influence / variance;
                    for (int j = 0; j < FitParameter.Count; j++)
                    {
                        b[j] += weight * d[j] * residual;
                        for (int k = 0; k < FitParameter.Count; k++)
                        {
                            a[j, k] += weight * d[j] * d[k];
                        }
                    }
                }
                for (int j = 0; j < FitParameter.Count; j++)
                {
                    a[j, j] += 1e-7;
                }
                var delta = Solve(a, b);
                for (int j = 0; j < FitParameter.Count; j++)
                {
                    p[j] = Math.Max(j == FitParameter.Base ? 20 : 0, Math.Min(ParameterMaximum(j), p[j] + .65 * delta[j]));
                }
            }
            return p;
        }

        private static AppSettings Copy(AppSettings source)
        {
            var result = new AppSettings();
            foreach (var p in typeof(AppSettings).GetProperties())
            {
                p.SetValue(result, p.GetValue(source, null), null);
            }
            return result;
        }

        public static CalibrationResult Analyze(IList<CaptureKey> keys, int seconds, AppSettings current)
        {
            var result = new CalibrationResult
            {
                Proposed = Copy(current)
            };
            foreach (var p in typeof(AppSettings).GetProperties())
            {
                result.Estimates.Add(new CalibrationEstimate
                {
                    Setting = p.Name, Current = p.GetValue(current, null), Proposed = p.GetValue(current, null), Reason = p.Name ==
                    "ButtonDelaySeconds" ? "Kept: start-button preference is unrelated to typing." :
                    "Kept: insufficient independent samples."
                });
            }
            List<CorrectionEpisode> corrections;
            Alignment alignment;
            var gaps = Gaps(keys, CalibrationPassage.Text, out corrections, out alignment);
            result.AlignedCharacters = alignment.End;
            result.CorrectedMistakes = corrections.Count;
            var metrics = CalibrationMetrics.Measure(keys);
            result.MeasuredWpm = metrics.Wpm;
            result.OrdinaryGapMilliseconds = metrics.OrdinaryGapMilliseconds;
            result.ActivityDescription = metrics.ActivityDescription();
            result.Accuracy = alignment.End == 0 ? 0 : Math.Max(0, 100D * (1 - (corrections.Count + alignment.Distance) / (double)
            alignment.End));
            double alignmentQuality = alignment.End == 0 ? 0 : 100D * (1 - alignment.Distance / (double)alignment.End);
            if (keys.Count >= 30 && alignmentQuality < MinimumAlignmentQuality)
            {
                result.Warning = "The typed text did not match the passage closely enough. All settings were kept; try again from the start.";
                foreach (var row in result.Estimates)
                {
                    row.Reason = "Kept: text matched less than 70% of the passage.";
                }
                return result;
            }
            if (gaps.Count < 30)
            {
                Keep(result, "WordsPerMinute", "Kept: " + gaps.Count + " consecutive aligned gaps; need 30 for a replay target.");
                return result;
            }
            var fit = Fit(gaps, current);
            result.BaseDelay = fit[FitParameter.Base];
            var counts = new int[FitParameter.Count];
            foreach (var r in gaps)
            {
                counts[Class(r.Current)]++;
                if (Pair(r.Previous, r.Current) != 0)
                {
                    counts[FitParameter.Pair]++;
                }
                if (r.Previous == ' ' || r.Previous == '\n')
                {
                    counts[FitParameter.Word]++;
                }
                if (Sentence(r.Previous))
                {
                    counts[FitParameter.Sentence]++;
                }
                if (r.Previous == '\n')
                {
                    counts[FitParameter.Paragraph]++;
                }
            }
            for (int i = 1; i < FitParameter.Count; i++)
            {
                int minimum = i <= 4 ? 6 : i == 5 ? 40 : i == 6 ? 12 : 3;
                if (counts[i] >= minimum)
                {
                    Set(result, FitNames[i - 1], (int)Math.Round(fit[i] * (i <= 5 ? 100 : 1)),
                    "Joint incoming-key / outgoing-pause fit; n=" + counts[i] + ".");
                }
                else Keep(result, FitNames[i - 1], "Kept: n=" + counts[i] + "; need " + minimum + " independent gaps.");
            }
            var logs = new List<double>();
            var byEvent = new Dictionary<int, double>();
            foreach (var r in gaps)
            {
                double motor = Motor(r, fit), residualMotor = r.Gap - (Prediction(r, fit) - motor);
                if (residualMotor > motor * .15 && residualMotor < motor * 3 && !Boundary(r.Previous) && !Char.IsWhiteSpace(r.Current))
                {
                    double l = Math.Log(residualMotor / motor);
                    logs.Add(l);
                    byEvent[r.Event] = l;
                }
            }
            if (logs.Count >= 60)
            {
                double mean = Mean(logs), variance = 0, covariance = 0;
                int pairs = 0;
                foreach (double l in logs)
                {
                    variance += (l - mean) * (l - mean);
                }
                variance /= logs.Count;
                foreach (var pair in byEvent)
                {
                    double previous;
                    if (byEvent.TryGetValue(pair.Key - 1, out previous))
                    {
                        covariance += (pair.Value - mean) * (previous - mean);
                        pairs++;
                    }
                }
                covariance = pairs > 0 ? Math.Max(0, covariance / pairs) : 0;
                if (pairs < 20)
                {
                    Keep(result, "BurstPercent", "Kept: " + pairs +
                    " adjacent ordinary residual pairs; need 20 to separate drift from jitter.");
                    Keep(result, "JitterPercent", "Kept: " + pairs +
                    " adjacent ordinary residual pairs; need 20 to separate jitter from drift.");
                }
                else
                {
                    double burstVariance = Math.Min(variance, covariance / .935);
                    double burst = Math.Min(1, Math.Sqrt(burstVariance * 3));
                    Set(result, "BurstPercent", (int)Math.Round(burst * 100), "Serial residual covariance over " + pairs +
                    " adjacent pairs (8–23-key bursts).");
                    Set(result, "JitterPercent", (int)Math.Round(Math.Sqrt(Math.Max(0, variance - burstVariance)) / .65 * 100),
                    "Log motor residual variance minus burst variance; n=" + logs.Count + ".");
                }
            }
            else
            {
                Keep(result, "JitterPercent", "Kept: " + logs.Count + " ordinary motor residuals; need 60.");
                Keep(result, "BurstPercent", "Kept: " + logs.Count + " ordinary motor residuals; need 60.");
            }
            var thinking = new List<double>();
            int boundaries = 0;
            foreach (var unit in BoundaryUnits(gaps))
            {
                bool boundary = false;
                double extra = 0, maximumMotor = 0;
                foreach (var r in unit)
                {
                    boundary |= Boundary(r.Previous);
                    extra += r.Gap - Prediction(r, fit);
                    maximumMotor = Math.Max(maximumMotor, Motor(r, fit));
                }
                if (!boundary)
                {
                    continue;
                }
                boundaries++;
                if (extra > Math.Max(350, maximumMotor * 2))
                {
                    thinking.Add(extra);
                }
            }
            if (thinking.Count >= 4 && boundaries >= 30)
            {
                Set(result, "ThinkingPauseChancePercent", (int)Math.Round(thinking.Count * 100D / boundaries),
                "Long boundary residuals; n=" + thinking.Count + ". Transcription underestimates composing pauses.");
                Set(result, "ThinkingPauseMilliseconds", (int)Math.Round(Mean(thinking) * 1.5), "Uniform [max/3,max] pause model; n=" +
                thinking.Count + ".");
            }
            else
            {
                Keep(result, "ThinkingPauseChancePercent", "Kept: " + thinking.Count + " long pauses / " + boundaries +
                " boundaries; need 4 / 30. Transcription reads composing pauses low.");
                Keep(result, "ThinkingPauseMilliseconds", "Kept: " + thinking.Count +
                " long pauses; need 4 across at least 30 boundaries.");
            }
            if (corrections.Count >= 3)
            {
                double weighted = 0;
                for (int i = 0; i < alignment.End; i++)
                {
                    char c = CalibrationPassage.Text[i];
                    int cl = Class(c);
                    if (Char.IsWhiteSpace(c))
                    {
                        continue;
                    }
                    weighted += cl == 2 ? result.Proposed.NumberTypoMultiplierPercent / 100D : cl == 3 ? result.Proposed.
                    PunctuationTypoMultiplierPercent / 100D : cl == 4 ? result.Proposed.ShiftSymbolTypoMultiplierPercent / 100D : 1;
                }
                Set(result, "TypoChancePercent", (int)Math.Round(corrections.Count * 100D / Math.Max(1, weighted)),
                "Corrected episodes / weighted eligible keys; episodes=" + corrections.Count + ".");
                Set(result, "EnableTypos", true, "Observed " + corrections.Count + " corrected episodes.");
                var notice = new List<double>();
                var repair = new List<double>();
                var resume = new List<double>();
                var detect = new List<double>();
                foreach (var c in corrections)
                {
                    // At a newline the engine notices after the preceding motor delay; remove that delay.
                    double n = c.Notice;
                    if (c.End < CalibrationPassage.Text.Length && CalibrationPassage.Text[c.End] == '\n' && c.End > 0)
                    {
                        n -= Prediction(new AlignedGap
                        {
                            Previous = CalibrationPassage.Text[c.End - 1], Current = '\n'
                        }, fit);
                    }
                    notice.Add(Math.Max(0, n));
                    repair.AddRange(c.Repair);
                    detect.Add(Math.Max(0, c.End - c.Start - 1));
                    if (c.LastRetype >= 0 && c.LastRetype + 1 < keys.Count && c.End > 0 && c.End < CalibrationPassage.Text.Length)
                    {
                        double next = keys[c.LastRetype + 1].Milliseconds - keys[c.LastRetype].Milliseconds;
                        if (CalibrationPassage.Text[c.End] != '\n')
                        {
                            next -= Prediction(new AlignedGap
                            {
                                Previous = CalibrationPassage.Text[c.End - 1], Current = CalibrationPassage.Text[c.End]
                            }, fit);
                        }
                        resume.Add(next);
                    }
                }
                double repairMean = Mean(repair);
                Set(result, "CorrectionNoticeMilliseconds", (int)Math.Round(Mean(notice)), "Mean latency into first Backspace; episodes="
                + notice.Count + ".");
                if (repair.Count >= 8)
                {
                    Set(result, "CorrectionPauseMilliseconds", (int)Math.Round(repairMean), "Within-correction key gaps; n=" + repair.
                    Count + ".");
                }
                if (resume.Count >= 3)
                {
                    Set(result, "CorrectionResumeMilliseconds", (int)Math.Round(Mean(resume) - repairMean),
                    "After-retype gap minus motor, boundary and final repair gap; n=" + resume.Count + ".");
                }
                if (corrections.Count >= 8)
                {
                    double bestScore = Double.NegativeInfinity, secondScore = Double.NegativeInfinity;
                    int bestMax = current.CorrectionDetectChars;
                    for (int max = 0; max <= 8; max++)
                    {
                        double score = 0;
                        foreach (var c in corrections)
                        {
                            double probability = 0;
                            int[] distances = max == 0 ? new[]
                            {
                                0
                            }
                            : new[]
                            {
                                Math.Min(max, 1), Math.Min(max, 2), Math.Min(max, 3), max
                            };
                            double[] probabilities = max == 0 ? new[]
                            {
                                1D
                            }
                            : new[]
                            {
                                .45, .30, .17, .08
                            };
                            for (int j = 0; j < distances.Length; j++)
                            {
                                int extra = c.Kind == "transpose" ? 1 : 0;
                                while (extra < distances[j] && c.Start + extra + 1 < CalibrationPassage.Text.Length && !((extra > 0 &&
                                Boundary(CalibrationPassage.Text[c.Start + extra])) || CalibrationPassage.Text[c.Start + extra + 1] ==
                                '\n'))
                                {
                                    extra++;
                                }
                                if (extra == c.End - c.Start - 1)
                                {
                                    probability += probabilities[j];
                                }
                            }
                            score += Math.Log(Math.Max(.000001, probability));
                        }
                        if (score > bestScore)
                        {
                            secondScore = bestScore;
                            bestScore = score;
                            bestMax = max;
                        }
                        else secondScore = Math.Max(secondScore, score);
                    }
                    if (bestScore - secondScore >= Math.Log(3))
                    {
                        Set(result, "CorrectionDetectChars", bestMax,
                        "Likelihood of fixed detection-distance distribution, censored at boundaries; n=" + corrections.Count + ".");
                    }
                    else Keep(result, "CorrectionDetectChars", (
                    ("Kept: boundary-censored detection distances do not distinguish maximum" + " distances (likelihood rat") +
                    "io below 3)." ))
                    ;
                }
                if (corrections.Count >= 20)
                {
                    int dup = 0, trans = 0;
                    foreach (var c in corrections)
                    {
                        if (c.Kind == "duplicate")
                        {
                            dup++;
                        }
                        if (c.Kind == "transpose")
                        {
                            trans++;
                        }
                    }
                    Set(result, "DuplicateChancePercent", (int)Math.Round(dup * 100D / corrections.Count), "Observed episode kinds; n=" +
                    corrections.Count + ".");
                    Set(result, "TransposeChancePercent", (int)Math.Round(trans * 100D / corrections.Count), "Observed episode kinds; n="
                    + corrections.Count + ".");
                }
            }
            else
            {
                Keep(result, "EnableTypos", ("Kept: fewer than 3 corrected episodes; no mistakes does not establish " +
                "a zero mistake rate."));
                Keep(result, "TypoChancePercent", "Kept: fewer than 3 corrected episodes; need 3.");
            }
            if (corrections.Count < 20)
            {
                Keep(result, "DuplicateChancePercent", "Kept: " + corrections.Count +
                " corrected episodes; need 20 to estimate kind shares.");
                Keep(result, "TransposeChancePercent", "Kept: " + corrections.Count +
                " corrected episodes; need 20 to estimate kind shares.");
            }
            // Multipliers need enough errors in EACH class, rather than arbitrary pseudo-counts.
            var classErrors = new int[5];
            var classEligible = new int[5];
            for (int i = 0; i < alignment.End; i++)
            {
                if (!Char.IsWhiteSpace(CalibrationPassage.Text[i]))
                {
                    classEligible[Class(
                    CalibrationPassage.Text[i])]++;
                }
            }
            foreach (var c in corrections)
            {
                if (c.Start < CalibrationPassage.Text.Length)
                {
                    classErrors[Class(CalibrationPassage.Text[c.Start
                    ])]++;
                }
            }
            int letterErrors = classErrors[0] + classErrors[1], letterEligible = classEligible[0] + classEligible[1];
            var multiplierNames = new[]
            {
                "NumberTypoMultiplierPercent", "PunctuationTypoMultiplierPercent", "ShiftSymbolTypoMultiplierPercent"
            };
            for (int cl = 2; cl <= 4; cl++)
            {
                if (letterErrors >= 10 && classErrors[cl] >= 10 && classEligible[cl] >= 30)
                {
                    Set(result, multiplierNames[cl - 2], (int)Math.Round(classErrors[cl] / (double)classEligible[cl] / (letterErrors / (
                    double
                    )letterEligible) * 100), "Class error frequency / letter error frequency; errors=" + classErrors[cl] + "/" +
                    letterErrors
                    + ".");
                }
                else Keep(result, multiplierNames[cl - 2], "Kept: class/letter errors=" + classErrors[cl] + "/" + letterErrors +
                "; need at least 10 each and 30 class opportunities.");
            }
            if (letterErrors >= 10)
            {
                Set(result, "TypoChancePercent", (int)Math.Round(letterErrors * 100D / letterEligible), "Letter-error base rate; n=" +
                letterErrors + ".");
            }
            Keep(result, "KeepTargetWpm", "Kept: timing-budget preference is not identifiable from a single typing recording.");
            CalibrationReplay.MatchSpeed(result, seconds);
            return result;
        }

        private static void Keep(CalibrationResult result, string name, string reason)
        {
            foreach (var row in result.Estimates)
            {
                if (row.Setting == name)
                {
                    row.Reason = reason;
                }
            }
        }

        internal static void Set(CalibrationResult result, string name, object value, string reason)
        {
            var p = typeof(AppSettings).GetProperty(name);
            if (value is int)
            {
                value = SettingRanges.Clamp(name, (int)value);
            }
            p.SetValue(result.Proposed, value, null);
            foreach (var row in result.Estimates)
            {
                if (row.Setting == name)
                {
                    row.Proposed = value;
                    row.Measured = true;
                    row.Reason = reason;
                }
            }
        }
    }
}
