using System;
using System.Collections.Generic;

namespace AutoWrite.Tests
{
    internal static partial class Runner
    {
        private static void AlignmentQualityTests()
        {
            Test("alignment quality accepts sloppy transcription and refuses wrong text at two speeds", delegate
            {
                foreach (int speed in new[] { 60, 125 })
                {
                    foreach (int mode in new[] { 0, 1, 2, 3, 4, 5 })
                    {
                        var keys = new List<CaptureKey>();
                        var random = new Random(872);
                        int count = Math.Min(CalibrationPassage.Text.Length - 400, speed * 5);
                        const string unrelated = "the birds flew over the mountain while we waited by the empty road ";
                        for (int i = 0; i < count; i++)
                        {
                            char c = CalibrationPassage.Text[i];
                            if (mode == 1 && i % 10 == 5 || mode == 5 && i % 2 == 1)
                            {
                                c = 'z';
                            }
                            if (mode == 2)
                            {
                                c = CalibrationPassage.Text[i + 400];
                            }
                            if (mode == 3)
                            {
                                c = unrelated[i % unrelated.Length];
                            }
                            if (mode == 4)
                            {
                                c = (char)('a' + random.Next(26));
                            }
                            keys.Add(new CaptureKey(i * 12000D / speed, c));
                        }
                        var current = AppSettings.Defaults();
                        var result = CalibrationAnalysis.Analyze(keys, 60, current);
                        bool accepted = mode <= 1;
                        int measured = result.Estimates.FindAll(delegate(CalibrationEstimate row) { return row.Measured; }).Count;
                        Console.WriteLine("QUALITY speed=" + speed + " mode=" + mode + " accuracy=" + result.Accuracy +
                            " measured=" + measured);
                        Assert(accepted ? measured > 0 : measured == 0, "alignment quality gate mode=" + mode + " speed=" + speed);
                        if (!accepted)
                        {
                            SameSettings(current, result.Proposed);
                            Assert(result.Warning.Contains("did not match the passage"), "missing mismatch explanation");
                        }
                        Assert(accepted ? result.Accuracy >= 70 : result.Accuracy < 70, "threshold fixture on wrong side");
                    }
                }
            });
        }
    }
}
