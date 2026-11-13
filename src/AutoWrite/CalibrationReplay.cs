using System;
using System.Text;
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
            internal ForecastRuntime(int seed, int seconds) { _seed = seed; _limit = seconds * 1000L; }
            public Random CreateRandom() { return new Random(_seed); }
            public IntPtr ForegroundWindow() { return IntPtr.Zero; }
            public bool IsKeyDown(int key) { return false; }
            public void ReleaseModifiers() { }
            public void SendCharacter(char value) { if (value == '\b') Length = Math.Max(0, Length - 1); else Length++; }
            public void Sleep(int milliseconds, CancellationToken token) { _time += Math.Max(0, milliseconds); if (_time > _limit) throw new OperationCanceledException(); }
        }
        internal static TypingOptions Options(AppSettings settings)
        {
            var options = new TypingOptions();
            foreach (var p in typeof(AppSettings).GetProperties())
            {
                var target = typeof(TypingOptions).GetProperty(p.Name);
                if (target != null) target.SetValue(options, p.GetValue(settings, null), null);
            }
            options.TelemetryPath = "-"; return options;
        }
        internal static double Predict(AppSettings settings, int seconds)
        {
            double characters = 0;
            for (int i = 0; i < 9; i++)
            {
                var runtime = new ForecastRuntime(70001 + i * 101, seconds);
                try { new TypingEngine(runtime).TypeText(CalibrationPassage.Text, Options(settings), null, CancellationToken.None); }
                catch (OperationCanceledException) { }
                characters += runtime.Length;
            }
            return characters / 9 * 12 / seconds;
        }
        internal static void MatchSpeed(CalibrationResult result, int seconds)
        {
            var settings = result.Proposed; int original = settings.WordsPerMinute;
            settings.WordsPerMinute = 5; double slow = Predict(settings, seconds);
            settings.WordsPerMinute = 180; double fast = Predict(settings, seconds);
            result.MinimumWpm = slow; result.MaximumWpm = fast;
            double measured = result.MeasuredWpm;
            int low = 5, high = 180, best = original; double bestError = Double.MaxValue, bestPrediction = 0;
            while (low <= high)
            {
                int mid = (low + high) / 2; settings.WordsPerMinute = mid;
                double predicted = Predict(settings, seconds), error = Math.Abs(predicted - measured);
                if (error < bestError) { bestError = error; best = mid; bestPrediction = predicted; }
                if (predicted < measured) low = mid + 1; else high = mid - 1;
            }
            if (Math.Abs(slow - measured) < bestError) { best = 5; bestPrediction = slow; bestError = Math.Abs(slow - measured); }
            if (Math.Abs(fast - measured) < bestError) { best = 180; bestPrediction = fast; bestError = Math.Abs(fast - measured); }
            CalibrationAnalysis.Set(result, "WordsPerMinute", best, "Measured " + measured.ToString("F1") + " achieved WPM. Target chosen by forward replay of the recovered settings; predicted " + bestPrediction.ToString("F1") + " WPM (absolute forecast error " + bestError.ToString("F1") + ").");
            result.PredictedWpm = bestPrediction;
            if (measured < slow * .95 || measured > fast * 1.05)
            {
                result.Warning = "Measured " + measured.ToString("F1") + " WPM cannot be reproduced with these settings and the engine's 45–220 ms motor limit. AutoWrite will type at approximately " + bestPrediction.ToString("F1") + " WPM with the proposed closest achievable Target WPM (" + best + ").";
                foreach (var row in result.Estimates) if (row.Setting == "WordsPerMinute")
                { row.ClosestAchievable = true; row.Reason = "Closest achievable: measured " + measured.ToString("F1") + " WPM; predicted " + bestPrediction.ToString("F1") + " WPM. Target chosen by forward replay; see warning."; }
            }
        }
    }
}
