using System;
using System.IO;
using System.Windows.Forms;

namespace AutoWrite.Tests
{
    internal static partial class Runner
    {
        private static void SameSettings(AppSettings expected, AppSettings actual)
        { foreach (var p in typeof(AppSettings).GetProperties()) Assert(Object.Equals(p.GetValue(expected, null), p.GetValue(actual, null)), p.Name); }
        private static int ProposalCount(Control control)
        {
            int count = control.AccessibleName != null && control.AccessibleName.StartsWith("Proposed ", StringComparison.Ordinal) ? 1 : 0;
            foreach (Control child in control.Controls) count += ProposalCount(child); return count;
        }
        static partial void ResultsTests()
        {
            Test("results expose every current/proposed setting; discard has no writes", delegate {
                SettingsStore.Save(AppSettings.Defaults()); var path = Path.Combine(DataDirectory.PathRoot, "settings.ini"); var before = File.ReadAllBytes(path);
                var result = CalibrationAnalysis.Analyze(new System.Collections.Generic.List<CaptureKey>(), 60, AppSettings.Defaults()); bool applied = false;
                using (var dialog = new CalibrationResultsDialog(result, delegate(AppSettings s) { applied = true; return true; }))
                    Assert(ProposalCount(dialog) == typeof(AppSettings).GetProperties().Length, "missing result row");
                Assert(!applied && Convert.ToBase64String(before) == Convert.ToBase64String(File.ReadAllBytes(path)), "discard wrote settings");
            });
            Test("apply changes all main controls and saves immediately", delegate {
                var current = AppSettings.Defaults(); SettingsStore.Save(current);
                using (var form = new MainForm())
                {
                    SameSettings(current, form.CurrentSettings()); var proposed = AppSettings.Defaults();
                    foreach (var p in typeof(AppSettings).GetProperties()) p.SetValue(proposed, p.PropertyType == typeof(bool) ? (object)(!(bool)p.GetValue(proposed, null)) : (object)((int)p.GetValue(proposed, null) + 1), null);
                    Assert(form.ApplyCalibration(proposed), "apply save failed"); SameSettings(proposed, form.CurrentSettings()); SameSettings(proposed, SettingsStore.Load());
                    Assert(form.ApplyCalibration(proposed), "second apply failed"); SameSettings(proposed, SettingsStore.Load());
                }
            });
            Test("failed save restores main controls and leaves existing settings intact", delegate {
                var original = AppSettings.Defaults(); SettingsStore.Save(original);
                using (var form = new MainForm())
                {
                    var previous = Environment.GetEnvironmentVariable("AUTOWRITE_DATA_DIR");
                    var blocked = Path.Combine(Output, "L1-data-path-is-a-file"); File.WriteAllText(blocked, "owned negative fixture");
                    try
                    {
                        Environment.SetEnvironmentVariable("AUTOWRITE_DATA_DIR", blocked);
                        var proposed = AppSettings.Defaults(); proposed.WordsPerMinute = 47;
                        Assert(!form.ApplyCalibration(proposed), "unwritable path accepted"); SameSettings(original, form.CurrentSettings());
                    }
                    finally { Environment.SetEnvironmentVariable("AUTOWRITE_DATA_DIR", previous); }
                    SameSettings(original, SettingsStore.Load());
                }
            });
        }
    }
}
