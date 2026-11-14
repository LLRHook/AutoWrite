using System;
using System.Text;
using System.Collections.Generic;
using System.Threading;
namespace AutoWrite
{
    internal static class CalibrationReplay
    {
        // Forward predictions use only the recovered settings. No ground-truth seed or options enter here.
        private sealed class ForecastRuntime : IEngineRuntime
        {
            private readonly int _seed;
            private readonly long _limit;
            private long _time;
            internal int Length;
            internal readonly List<CaptureKey> Keys = new List<CaptureKey>();
            internal ForecastRuntime(int seed, int seconds)
            {
                _seed = seed;
                _limit = seconds * 1000L;
            }
            public Random CreateRandom()
            {
                return new Random(_seed);
            }
            public IntPtr ForegroundWindow()
            {
                return IntPtr.Zero;
            }
            public bool IsKeyDown(int key)
            {
                return false;
            }
            public void ReleaseModifiers()
            {
            }
            public void SendCharacter(char value)
            {
                Keys.Add(new CaptureKey(_time, value));
                if (value == '\b')
                {
                    Length = Math.Max(0, Length - 1);
                }
                else Length++;
            }
            public void Sleep(int milliseconds, CancellationToken token)
            {
                _time += Math.Max(0, milliseconds);
                if (_time > _limit)
                {
                    throw new OperationCanceledException();
                }
            }
        }

        internal static TypingOptions Options(AppSettings settings)
        {
            var options = new TypingOptions();
            foreach (var p in typeof(AppSettings).GetProperties())
            {
                var target = typeof(TypingOptions).GetProperty(p.Name);
                if (target != null)
                {
                    target.SetValue(options, p.GetValue(settings, null), null);
                }
            }
            options.TelemetryPath = "-";
            return options;
        }

        internal static double Predict(AppSettings settings, int seconds)
        {
            return Forecast(settings, seconds).Wpm;
        }

        internal static CalibrationMetrics Forecast(AppSettings settings, int seconds)
        {
            var prediction = new CalibrationMetrics();
            for (int i = 0; i < 9; i++)
            {
                var runtime = new ForecastRuntime(70001 + i * 101, seconds);
                try
                {
                    new TypingEngine(runtime).TypeText(CalibrationPassage.Text, Options(settings), null, CancellationToken.None);
                }
                catch (OperationCanceledException)
                {
                }
                var metrics = CalibrationMetrics.Measure(runtime.Keys);
                prediction.Wpm += metrics.Wpm / 9;
                prediction.OrdinaryGapMilliseconds += metrics.OrdinaryGapMilliseconds / 9;
            }
            return prediction;
        }

        internal static void MatchSpeed(CalibrationResult result, int seconds)
        {
            var settings = result.Proposed;
            double measured = result.OrdinaryGapMilliseconds;
            settings.WordsPerMinute = 5;
            var slow = Forecast(settings, seconds);
            settings.WordsPerMinute = 180;
            var fast = Forecast(settings, seconds);
            result.MinimumWpm = slow.Wpm;
            result.MaximumWpm = fast.Wpm;
            int low = 5, high = 180, best = 5;
            double bestError = Math.Abs(slow.OrdinaryGapMilliseconds - measured);
            var bestPrediction = slow;
            while (low <= high)
            {
                int mid = (low + high) / 2;
                settings.WordsPerMinute = mid;
                var predicted = Forecast(settings, seconds);
                double error = Math.Abs(predicted.OrdinaryGapMilliseconds - measured);
                if (error < bestError)
                {
                    bestError = error;
                    best = mid;
                    bestPrediction = predicted;
                }
                if (predicted.OrdinaryGapMilliseconds > measured)
                {
                    low = mid + 1;
                }
                else
                {
                    high = mid - 1;
                }
            }
            if (Math.Abs(fast.OrdinaryGapMilliseconds - measured) < bestError)
            {
                best = 180;
                bestPrediction = fast;
            }
            CalibrationAnalysis.Set(result, "WordsPerMinute", best,
                "Ordinary letter gaps: " + measured.ToString("F1") + " ms; replay: " +
                bestPrediction.OrdinaryGapMilliseconds.ToString("F1") + " ms. Target chosen by forward replay.");
            result.PredictedWpm = bestPrediction.Wpm;
            result.PredictedOrdinaryGapMilliseconds = bestPrediction.OrdinaryGapMilliseconds;
            if (measured > slow.OrdinaryGapMilliseconds * 1.05 || measured < fast.OrdinaryGapMilliseconds * .95)
            {
                result.Warning = "The 45 to 220 ms motor limit prevents matching your ordinary letter rhythm. Measured " +
                    result.MeasuredWpm.ToString("F1") + " WPM; AutoWrite will type approximately " +
                    result.PredictedWpm.ToString("F1") + " WPM with the closest achievable Target WPM (" + best + ").";
                foreach (var row in result.Estimates)
                {
                    if (row.Setting == "WordsPerMinute")
                    {
                        row.ClosestAchievable = true;
                        row.Reason = "Closest achievable: " + row.Reason;
                    }
                }
            }
            else if (Math.Abs(result.PredictedWpm - result.MeasuredWpm) > result.MeasuredWpm * .05)
            {
                result.Warning = "AutoWrite matches your ordinary key rhythm; its expected speed differs because it adds " +
                    "pauses and corrections using the proposed settings, including values kept from before the test.";
            }
        }
    }
}
