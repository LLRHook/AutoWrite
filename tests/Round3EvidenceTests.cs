using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Windows.Forms;

namespace AutoWrite.Tests
{
    internal static partial class Runner
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

        private static void Round3EvidenceTests()
        {
            Test("active speed omits the full interruption with an independently computed denominator", delegate
            {
                var keys = new List<CaptureKey>
                {
                    new CaptureKey(0, 'a'), new CaptureKey(100, 'b'),
                    new CaptureKey(15200, 'c'), new CaptureKey(15300, 'd')
                };
                var measured = CalibrationMetrics.Measure(keys);
                // Three characters after the initial key, over 100 + 100 ms.
                Assert(measured.ActiveMilliseconds == 200 && measured.Wpm == 180, "interruption remains in active time");
                Assert(measured.OrdinaryGapCount == 2 && measured.OrdinaryGapMilliseconds == 100, "interrupted rhythm gap retained");
            });
            Test("ordinary rhythm excludes wrong keys backspaces and their retyping", delegate
            {
                const string characters = "azyx\b\b\bbcdef";
                int[] times = { 0, 400, 800, 1200, 1300, 1400, 1500, 2000, 2500, 3000, 3100, 3200 };
                var keys = new List<CaptureKey>();
                for (int i = 0; i < characters.Length; i++)
                {
                    keys.Add(new CaptureKey(times[i], characters[i]));
                }
                var measured = CalibrationMetrics.Measure(keys);
                Assert(measured.OrdinaryGapCount == 1 && measured.OrdinaryGapMilliseconds == 100,
                    "correction gaps entered ordinary rhythm");
            });
            Test("two to five uppercase gaps cannot replace the six-sample eligibility minimum", delegate
            {
                var keys = new List<CaptureKey>();
                int uppercase = 0;
                for (int i = 0; i < CalibrationPassage.Text.Length; i++)
                {
                    char c = CalibrationPassage.Text[i];
                    if (i > 0 && Char.IsUpper(c))
                    {
                        uppercase++;
                    }
                    keys.Add(new CaptureKey(i * 100, c));
                    if (uppercase == 3)
                    {
                        break;
                    }
                }
                Assert(keys.Count >= 30 && uppercase == 3, "class sample fixture coverage");
                var row = Estimate(CalibrationAnalysis.Analyze(keys, 60, AppSettings.Defaults()), "UppercaseDelayPenaltyPercent");
                Assert(!row.Measured && Object.Equals(row.Current, row.Proposed), "three uppercase gaps were measured");
            });
            Test("sentence evidence labels a measured four-sample estimate", delegate
            {
                Assert(CalibrationEvidence.Describe("SentencePauseMilliseconds", 4, "sentence endings").Contains("few samples"),
                    "sentence sparse evidence label missing");
            });
            Test("message-loop timer refreshes dirty passage progress", delegate
            {
                string quiet = Environment.GetEnvironmentVariable("AUTOWRITE_DIAGNOSTIC_NOACTIVATE");
                IntPtr foreground = NativeMethods.GetForegroundWindow();
                try
                {
                    Environment.SetEnvironmentVariable("AUTOWRITE_DIAGNOSTIC_NOACTIVATE", "1");
                    using (var dialog = new CalibrationDialog())
                    using (var check = new Timer { Interval = 150 })
                    {
                        int updates = 0;
                        dialog.Shown += delegate
                        {
                            var input = (CaptureTextBox)dialog.Controls.Find("CalibrationInput", true)[0];
                            foreach (char c in CalibrationPassage.Text.Substring(0, 40))
                            {
                                input.Character(c);
                            }
                            Assert(dialog.ProgressUpdates == 0, "progress ran during key handling");
                            check.Start();
                        };
                        check.Tick += delegate
                        {
                            check.Stop();
                            updates = dialog.ProgressUpdates;
                            dialog.Close();
                        };
                        dialog.ShowDialog();
                        Assert(updates > 0, "timer never refreshed dirty passage");
                    }
                    Assert(NativeMethods.GetForegroundWindow() == foreground, "timer test changed foreground");
                }
                finally
                {
                    Environment.SetEnvironmentVariable("AUTOWRITE_DIAGNOSTIC_NOACTIVATE", quiet);
                }
            });
            Test("real modal calibration forwards failed-save warnings unlocks and applies", delegate
            {
                foreach (int failure in new[] { 0, 1 })
                {
                    foreach (bool completed in new[] { false, true })
                    {
                        ModalSaveFailure(failure, completed);
                    }
                }
            });
            Test("invalid capture path cannot bypass modal unlock or completed results", delegate
            {
                ModalSaveFailure(2, false);
                ModalSaveFailure(2, true);
            });
            Test("saved capture records current settings and reproduces all 26 proposals", delegate
            {
                var current = Profile(1);
                current.WordsPerMinute = 79;
                current.ThinkingPauseChancePercent = 7;
                current.ThinkingPauseMilliseconds = 1900;
                var constructor = typeof(CaptureSession).GetConstructor(new[] { typeof(int), typeof(AppSettings) });
                Assert(constructor != null, "capture cannot snapshot current settings");
                var session = (CaptureSession)constructor.Invoke(new object[] { 60, current });
                foreach (var key in HumanLog(0, false, false))
                {
                    session.Accept(key.Character, Math.Round(key.Milliseconds, 3));
                }
                var expected = CalibrationAnalysis.Analyze(session.Keys, 60, current);
                string path = session.Save(true);
                var restored = new AppSettings();
                var keys = new List<CaptureKey>();
                int settingsCount = 0;
                foreach (string line in File.ReadAllLines(path))
                {
                    string[] fields = line.Split('\t');
                    if (fields.Length == 3 && fields[0] == "# setting")
                    {
                        var property = typeof(AppSettings).GetProperty(fields[1]);
                        Assert(property != null, "unrecognised saved setting");
                        property.SetValue(restored, Convert.ChangeType(fields[2], property.PropertyType,
                            System.Globalization.CultureInfo.InvariantCulture), null);
                        settingsCount++;
                    }
                    if (fields.Length == 3 && fields[1] == "character")
                    {
                        keys.Add(new CaptureKey(Double.Parse(fields[0], System.Globalization.CultureInfo.InvariantCulture),
                            (char)Int32.Parse(fields[2], System.Globalization.CultureInfo.InvariantCulture)));
                    }
                }
                Assert(settingsCount == 26, "missing current settings in capture header: " + settingsCount);
                SameSettings(current, restored);
                SameSettings(expected.Proposed, CalibrationAnalysis.Analyze(keys, 60, restored).Proposed);
                // The snapshot must not change if a caller later edits its own settings.
                current.ButtonDelaySeconds++;
                string savedAgain = session.Save(false);
                Assert(File.ReadAllText(savedAgain).Contains("# setting\tButtonDelaySeconds\t" + restored.ButtonDelaySeconds),
                    "capture retained a mutable current-settings reference");
            });
        }

        private static void ModalSaveFailure(int failure, bool completed)
        {
            string previous = Environment.GetEnvironmentVariable("AUTOWRITE_DATA_DIR");
            string quiet = Environment.GetEnvironmentVariable("AUTOWRITE_DIAGNOSTIC_NOACTIVATE");
            string root = Path.Combine(Path.GetTempPath(), "autowrite-modal-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            string capturePath = Path.Combine(root, "calibration");
            DirectorySecurity originalAcl = null;
            var foreground = NativeMethods.GetForegroundWindow();
            int threadErrors = 0;
            System.Threading.ThreadExceptionEventHandler handler = delegate { threadErrors++; };
            Application.ThreadException += handler;
            try
            {
                if (failure == 0)
                {
                    File.WriteAllText(capturePath, "blocks the capture directory");
                }
                else if (failure == 1)
                {
                    Directory.CreateDirectory(capturePath);
                    originalAcl = Directory.GetAccessControl(capturePath);
                    var denied = Directory.GetAccessControl(capturePath);
                    denied.AddAccessRule(new FileSystemAccessRule(WindowsIdentity.GetCurrent().User,
                        FileSystemRights.Write, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                        PropagationFlags.None, AccessControlType.Deny));
                    Directory.SetAccessControl(capturePath, denied);
                }
                Environment.SetEnvironmentVariable("AUTOWRITE_DATA_DIR", root);
                Environment.SetEnvironmentVariable("AUTOWRITE_DIAGNOSTIC_NOACTIVATE", "1");
                bool sawCapture = false, sawResults = false, warning = false, applied = false, timedOut = false;
                Exception caught = null;
                using (var main = new MainForm())
                using (var drive = new Timer { Interval = 30 })
                {
                    main.Show();
                    var watch = Stopwatch.StartNew();
                    drive.Tick += delegate
                    {
                        try
                        {
                            var forms = new List<Form>();
                            foreach (Form form in Application.OpenForms)
                            {
                                forms.Add(form);
                            }
                            foreach (var form in forms)
                            {
                                var capture = form as CalibrationDialog;
                                if (capture != null && !sawCapture)
                                {
                                    sawCapture = true;
                                    ((CaptureTextBox)capture.Controls.Find("CalibrationInput", true)[0]).Character('S');
                                    if (failure == 2)
                                    {
                                        Environment.SetEnvironmentVariable("AUTOWRITE_DATA_DIR", root + "\\bad<path");
                                    }
                                    if (completed)
                                    {
                                        typeof(CalibrationDialog).GetMethod("Finish", PrivateInstance).Invoke(capture, null);
                                    }
                                    else
                                    {
                                        capture.Close();
                                    }
                                    return;
                                }
                                if (form is CalibrationResultsDialog && !sawResults)
                                {
                                    sawResults = true;
                                    warning = form.Controls.Find("CalibrationWarning", true)[0].Text.Contains("could not be saved");
                                    Environment.SetEnvironmentVariable("AUTOWRITE_DATA_DIR", root);
                                    ((Button)form.Controls.Find("CalibrationApply", true)[0]).PerformClick();
                                    applied = File.Exists(Path.Combine(root, "settings.ini"));
                                    return;
                                }
                            }
                            if (watch.ElapsedMilliseconds > 10000)
                            {
                                timedOut = true;
                                foreach (var form in forms)
                                {
                                    if (form != main)
                                    {
                                        form.Dispose();
                                    }
                                }
                                main.Close();
                            }
                        }
                        catch (Exception exception)
                        {
                            caught = exception;
                        }
                    };
                    drive.Start();
                    typeof(MainForm).GetMethod("OpenCalibration", PrivateInstance).Invoke(main, null);
                    drive.Stop();
                    Assert(sawCapture && sawResults == completed && !timedOut, "modal sequence lost failure=" + failure);
                    Assert(caught == null && threadErrors == 0, "unhandled save exception: " + caught);
                    Assert(!(bool)typeof(MainForm).GetField("_calibrationOpen", PrivateInstance).GetValue(main), "app stayed locked");
                    Assert(((Button)typeof(MainForm).GetField("_startButton", PrivateInstance).GetValue(main)).Enabled, "Start locked");
                    Assert(((Button)typeof(MainForm).GetField("_calibrateButton", PrivateInstance).GetValue(main)).Enabled, "Calibrate locked");
                    Assert(!completed || (warning && applied), "results lost save warning or Apply failed");
                    main.Close();
                }
                Assert(NativeMethods.GetForegroundWindow() == foreground, "modal test changed foreground");
            }
            finally
            {
                if (originalAcl != null)
                {
                    Directory.SetAccessControl(capturePath, originalAcl);
                }
                Application.ThreadException -= handler;
                Environment.SetEnvironmentVariable("AUTOWRITE_DATA_DIR", previous);
                Environment.SetEnvironmentVariable("AUTOWRITE_DIAGNOSTIC_NOACTIVATE", quiet);
            }
        }
    }
}
