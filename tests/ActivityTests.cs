using System;
using System.Collections.Generic;

namespace AutoWrite.Tests
{
    internal static partial class Runner
    {
        private static void ActivityTests()
        {
            Test("independent typist rhythm survives interruptions trailing time and early finish", delegate
            {
                foreach (int mode in new[] { 0, 1, 2, 3 })
                {
                    var keys = TypistLog.Generate(741, 60, mode, 142);
                    var measured = CalibrationMetrics.Measure(keys);
                    var result = CalibrationAnalysis.Analyze(keys, 60, AppSettings.Defaults());
                    var replay = CalibrationReplay.Forecast(result.Proposed, 60);
                    double error = RelativeError(replay.OrdinaryGapMilliseconds, measured.OrdinaryGapMilliseconds);
                    Console.WriteLine("ACTIVITY mode=" + mode + " keys=" + keys.Count + " recorded_gap_ms=" +
                        measured.OrdinaryGapMilliseconds + " replay_gap_ms=" + replay.OrdinaryGapMilliseconds +
                        " active_wpm=" + result.MeasuredWpm + " " + result.ActivityDescription);
                    Assert(error <= 10, "rhythm error " + error + " mode=" + mode);
                    Assert(!result.Warning.Contains("motor limit"), "false motor warning mode=" + mode);
                    Assert(result.MeasuredWpm == measured.Wpm, "active speed rule differs");
                    if (mode == 1)
                    {
                        Assert(measured.ExcludedPauses.Count == 1 && result.ActivityDescription.Contains("15."), "missing omission");
                    }
                }
            });
        }
    }
}
