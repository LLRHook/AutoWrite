using System;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace AutoWrite.Tests
{
    internal static partial class Runner
    {
        private static void CaptureFailureTests()
        {
            Test("failed capture save always closes and completed results can still apply", delegate
            {
                string previous = Environment.GetEnvironmentVariable("AUTOWRITE_DATA_DIR");
                string quiet = Environment.GetEnvironmentVariable("AUTOWRITE_DIAGNOSTIC_NOACTIVATE");
                int threadErrors = 0;
                System.Threading.ThreadExceptionEventHandler handler = delegate { threadErrors++; };
                Application.ThreadException += handler;
                try
                {
                    Environment.SetEnvironmentVariable("AUTOWRITE_DIAGNOSTIC_NOACTIVATE", "1");
                    foreach (bool completed in new[] { false, true })
                    {
                        string root = Path.Combine(Output, "capture-failure-" + completed);
                        Directory.CreateDirectory(root);
                        File.WriteAllText(Path.Combine(root, "calibration"), "blocked directory");
                        Environment.SetEnvironmentVariable("AUTOWRITE_DATA_DIR", root);
                        bool closed = false, applied = false;
                        using (var dialog = new CalibrationDialog())
                        {
                            dialog.Show();
                            var input = (CaptureTextBox)dialog.Controls.Find("CalibrationInput", true)[0];
                            input.Character('S');
                            dialog.FormClosed += delegate { closed = true; };
                            if (completed)
                            {
                                typeof(CalibrationDialog).GetMethod("Finish", BindingFlags.Instance | BindingFlags.NonPublic)
                                    .Invoke(dialog, null);
                            }
                            else
                            {
                                dialog.Close();
                            }
                            Assert(closed && !dialog.Visible && threadErrors == 0, "save failure prevented close");
                            Assert(dialog.SaveWarning.Contains("could not be saved"), "save failure not explained");
                            if (completed)
                            {
                                Assert(dialog.CompletedSession != null, "completed recording lost");
                                var result = CalibrationAnalysis.Analyze(dialog.CompletedSession.Keys, 60, AppSettings.Defaults());
                                result.Warning = dialog.SaveWarning;
                                using (var results = new CalibrationResultsDialog(result, delegate(AppSettings proposed)
                                {
                                    applied = SettingsStore.Save(proposed);
                                    return applied;
                                }))
                                {
                                    results.Show();
                                    ((Button)results.Controls.Find("CalibrationApply", true)[0]).PerformClick();
                                }
                                Assert(applied, "save failure prevented applying settings");
                            }
                        }
                    }
                }
                finally
                {
                    Application.ThreadException -= handler;
                    Environment.SetEnvironmentVariable("AUTOWRITE_DATA_DIR", previous);
                    Environment.SetEnvironmentVariable("AUTOWRITE_DIAGNOSTIC_NOACTIVATE", quiet);
                }
            });
        }
    }
}
