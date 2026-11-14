using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Windows.Forms;

namespace AutoWrite.Tests
{
    internal static partial class Runner
    {
        private static CalibrationEstimate Estimate(CalibrationResult result, string setting)
        {
            return result.Estimates.Find(delegate(CalibrationEstimate row) { return row.Setting == setting; });
        }

        private static void EvidenceGateTests()
        {
            GoldenTests();
            Test("one to three long pauses keep both thinking settings", delegate
            {
                for (int count = 1; count <= 3; count++)
                {
                    var keys = TypistLog.Generate(872, 60, 0, 100);
                    double added = 0;
                    int pauses = 0;
                    for (int i = 1; i < keys.Count; i++)
                    {
                        if (keys[i - 1].Character == ' ' && pauses < count)
                        {
                            added += 2200;
                            pauses++;
                        }
                        keys[i].Milliseconds += added;
                    }
                    var current = AppSettings.Defaults();
                    var result = CalibrationAnalysis.Analyze(keys, 90, current);
                    foreach (string name in new[] { "ThinkingPauseChancePercent", "ThinkingPauseMilliseconds" })
                    {
                        var row = Estimate(result, name);
                        Assert(!row.Measured && Object.Equals(row.Current, row.Proposed), "thin thinking estimate " + count);
                    }
                }
            });
            Test("many total gaps do not replace class and paragraph sample minimums", delegate
            {
                var keys = new List<CaptureKey>();
                for (int i = 0; i < 60; i++)
                {
                    keys.Add(new CaptureKey(i * 100, CalibrationPassage.Text[i]));
                }
                var result = CalibrationAnalysis.Analyze(keys, 60, AppSettings.Defaults());
                Assert(result.AlignedCharacters == 60, "thin fixture did not align");
                foreach (string name in new[] { "UppercaseDelayPenaltyPercent", "ParagraphPauseMilliseconds" })
                {
                    var row = Estimate(result, name);
                    Assert(!row.Measured && Object.Equals(row.Current, row.Proposed), "thin samples measured: " + name);
                }
            });
            Test("capture preserves sub-millisecond timestamps in keys and raw events", delegate
            {
                var session = new CaptureSession(60);
                session.Accept('a', .1234);
                session.Accept('b', .9876);
                Assert(session.Keys[0].Milliseconds == .1234 && session.Keys[1].Milliseconds == .9876, "key time quantised");
                Assert(session.Raw[0].StartsWith("0.123\t", StringComparison.Ordinal) &&
                    session.Raw[1].StartsWith("0.988\t", StringComparison.Ordinal), "raw time quantised");
            });
            Test("notice estimate removes pre-Enter motor and sentence time", delegate
            {
                var keys = new List<CaptureKey>();
                double time = 0;
                for (int i = 0; i < 500; i++)
                {
                    char c = CalibrationPassage.Text[i];
                    bool correction = CalibrationPassage.Text[i + 1] == '\n';
                    keys.Add(new CaptureKey(time, correction ? '~' : c));
                    if (correction)
                    {
                        time += 460 + 100 + (".!?".IndexOf(c) >= 0 ? 410 : 0);
                        keys.Add(new CaptureKey(time, '\b'));
                        time += 80;
                        keys.Add(new CaptureKey(time, c));
                        time += 280;
                    }
                    else
                    {
                        time += 100 + (Char.IsWhiteSpace(c) ? 95 : 0) + (c == '\n' ? 190 : 0) +
                            (".!?".IndexOf(c) >= 0 ? 410 : 0);
                    }
                }
                var result = CalibrationAnalysis.Analyze(keys, 180, AppSettings.Defaults());
                Assert(Estimate(result, "CorrectionNoticeMilliseconds").Measured, "notice not measured");
                Assert(Math.Abs(result.Proposed.CorrectionNoticeMilliseconds - 460) <= 60,
                    "pre-Enter notice=" + result.Proposed.CorrectionNoticeMilliseconds);
            });
            Test("closing a started cancelled capture persists completed False", delegate
            {
                string quiet = Environment.GetEnvironmentVariable("AUTOWRITE_DIAGNOSTIC_NOACTIVATE");
                try
                {
                    Environment.SetEnvironmentVariable("AUTOWRITE_DIAGNOSTIC_NOACTIVATE", "1");
                    using (var dialog = new CalibrationDialog())
                    {
                        dialog.Show();
                        ((CaptureTextBox)dialog.Controls.Find("CalibrationInput", true)[0]).Character('S');
                        dialog.Close();
                    }
                    var files = Directory.GetFiles(Path.Combine(DataDirectory.PathRoot, "calibration"), "capture-*.tsv");
                    Array.Sort(files, StringComparer.Ordinal);
                    string header = File.ReadAllLines(files[files.Length - 1])[0];
                    Assert(header.Split(';')[2] == "completed=False", "cancelled capture marked complete: " + header);
                }
                finally
                {
                    Environment.SetEnvironmentVariable("AUTOWRITE_DIAGNOSTIC_NOACTIVATE", quiet);
                }
            });
        }
    }
}
