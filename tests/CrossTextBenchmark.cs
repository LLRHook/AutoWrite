using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace AutoWrite.Tests
{
    internal static partial class Runner
    {
        private static AppSettings CopySettings(AppSettings source)
        {
            var result = new AppSettings();
            foreach (var property in typeof(AppSettings).GetProperties())
            {
                property.SetValue(result, property.GetValue(source, null), null);
            }
            return result;
        }

        private static double[] CrossTextRun(string text, AppSettings settings)
        {
            double speed = 0, motor = 0;
            for (int seed = 0; seed < 10; seed++)
            {
                var runtime = RunEngine(text, settings, 9000 + seed, Int64.MaxValue);
                speed += CalibrationMetrics.Measure(Capture(runtime)).Wpm / 10;
                string telemetry = File.ReadAllText(Path.Combine(DataDirectory.PathRoot, "engine.tsv"));
                motor += Double.Parse(Regex.Match(telemetry, "base_delay_ms=([0-9]+)").Groups[1].Value,
                    CultureInfo.InvariantCulture) / 10;
            }
            return new[] { speed, motor };
        }

        private static void CrossTextBenchmark()
        {
            const string paragraph = "the morning was quiet as we walked along the road beside the river. " +
                "we stopped to watch the water and then went on toward the old bridge. " +
                "after a while the sun came out and the hills were clear against the sky.\n";
            string plain = paragraph + paragraph + paragraph + paragraph;
            var output = new StringBuilder("profile\tkeep_target\treport_wpm\treport_motor_ms\tplain_wpm\t" +
                "same_motor_plain_wpm\tdifference_percent\n");
            for (int profile = 0; profile < 3; profile++)
            {
                var truth = Profile(profile);
                var current = AppSettings.Defaults();
                current.ThinkingPauseChancePercent = truth.ThinkingPauseChancePercent;
                current.ThinkingPauseMilliseconds = truth.ThinkingPauseMilliseconds;
                var recording = Capture(RunEngine(CalibrationPassage.Text, truth, 1000, 60000, false));
                foreach (bool keep in new[] { true, false })
                {
                    current.KeepTargetWpm = keep;
                    var calibrated = CalibrationAnalysis.Analyze(recording, 60, current).Proposed;
                    var report = CrossTextRun(CalibrationPassage.Text, calibrated);
                    var actual = CrossTextRun(plain, calibrated);
                    double bestError = Double.MaxValue, referenceWpm = 0;
                    int low = 5, high = 180;
                    while (low <= high)
                    {
                        int target = (low + high) / 2;
                        var reference = CopySettings(calibrated);
                        reference.WordsPerMinute = target;
                        var probe = CrossTextRun(plain, reference);
                        double error = Math.Abs(probe[1] - report[1]);
                        if (error < bestError)
                        {
                            bestError = error;
                            referenceWpm = probe[0];
                        }
                        if (probe[1] > report[1])
                        {
                            low = target + 1;
                        }
                        else
                        {
                            high = target - 1;
                        }
                    }
                    output.Append(profile).Append('\t').Append(keep).Append('\t').Append(report[0]);
                    output.Append('\t').Append(report[1]).Append('\t').Append(actual[0]);
                    output.Append('\t').Append(referenceWpm).Append('\t');
                    output.AppendLine(((actual[0] - referenceWpm) / referenceWpm * 100).ToString("F3", CultureInfo.InvariantCulture));
                }
            }
            File.WriteAllText(Path.Combine(Output, "cross-text.tsv"), output.ToString());
            Console.WriteLine("CROSS-TEXT (reported, no accuracy gate)\n" + output);
        }
    }
}
