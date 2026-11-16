using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace AutoWrite.Tests
{
    internal static partial class Runner
    {
        private static void CleanTypistBenchmark()
        {
            var output = new StringBuilder("letter_ms\tseed\tkept_settings\trecorded_wpm\texpected_wpm\t" +
                "difference_percent\trhythm_error_percent\ttarget\n");
            foreach (int letter in new[] { 142, 70 })
            {
                for (int seed = 990; seed < 1000; seed++)
                {
                    var keys = TypistLog.Generate(seed, 60, 0, letter);
                    foreach (bool defaults in new[] { true, false })
                    {
                        var current = AppSettings.Defaults();
                        if (!defaults)
                        {
                            current.ThinkingPauseChancePercent = 0;
                            current.TypoChancePercent = 0;
                        }
                        var result = CalibrationAnalysis.Analyze(keys, 60, current);
                        var fields = new object[]
                        {
                            letter, seed, defaults ? "defaults" : "thinking_and_mistakes_off",
                            result.MeasuredWpm, result.PredictedWpm,
                            (result.PredictedWpm / result.MeasuredWpm - 1) * 100,
                            RelativeError(result.PredictedOrdinaryGapMilliseconds, result.OrdinaryGapMilliseconds),
                            result.Proposed.WordsPerMinute
                        };
                        for (int i = 0; i < fields.Length; i++)
                        {
                            if (i > 0)
                            {
                                output.Append('\t');
                            }
                            output.Append(Convert.ToString(fields[i], CultureInfo.InvariantCulture));
                        }
                        output.AppendLine();
                    }
                }
            }
            File.WriteAllText(Path.Combine(Output, "clean-typist.tsv"), output.ToString());
            Console.WriteLine("UNGATED CLEAN TYPIST\n" + output);
        }
    }
}
