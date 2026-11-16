using System;
using System.Windows.Forms;

namespace AutoWrite.Tests
{
    internal static partial class Runner
    {
        private static void CapturePerformanceTests()
        {
            Test("key handler only records and echoes; timer refreshes passage once per change", delegate
            {
                using (var dialog = new CalibrationDialog())
                {
                    var input = (CaptureTextBox)dialog.Controls.Find("CalibrationInput", true)[0];
                    foreach (char c in CalibrationPassage.Text.Substring(0, 900))
                    {
                        input.Character(c);
                    }
                    Assert(dialog.ProgressUpdates == 0, "passage progress ran inside the key handler");
                    Assert(input.Text.Replace("\r\n", "\n") == CalibrationPassage.Text.Substring(0, 900), "incremental echo");
                    dialog.RefreshPendingProgress();
                    Assert(dialog.ProgressUpdates == 1, "dirty progress was not refreshed");
                    dialog.RefreshPendingProgress();
                    Assert(dialog.ProgressUpdates == 1, "unchanged text was re-aligned");
                    input.Character('\b');
                    input.Character('\r');
                    input.Character('\b');
                    Assert(input.Text.Replace("\r\n", "\n") == CalibrationPassage.Text.Substring(0, 899), "Backspace echo");
                }
            });
        }
    }
}
