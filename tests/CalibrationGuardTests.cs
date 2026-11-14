using System;
using System.Reflection;
using System.Windows.Forms;

namespace AutoWrite.Tests
{
    internal static partial class Runner
    {
        private static void CalibrationGuardTests()
        {
            Test("Start and F7 leave the app untouched while calibration is open", delegate
            {
                using (var form = new MainForm())
                {
                    const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                    typeof(MainForm).GetField("_calibrationOpen", flags).SetValue(form, true);
                    // Empty text makes even the deliberately broken guard safe: it can
                    // change status, but cannot start a real engine or send global input.
                    ((TextBox)typeof(MainForm).GetField("_textBox", flags).GetValue(form)).Text = "";
                    var status = (Label)typeof(MainForm).GetField("_statusLabel", flags).GetValue(form);
                    status.Text = "guard sentinel";
                    typeof(MainForm).GetMethod("StartTyping", flags).Invoke(form, new object[] { true });
                    Assert(status.Text == "guard sentinel", "Start ran while calibrating");
                    var message = Message.Create(form.Handle, NativeMethods.WmHotkey, (IntPtr)0xA701, IntPtr.Zero);
                    object[] arguments = { message };
                    typeof(MainForm).GetMethod("WndProc", flags).Invoke(form, arguments);
                    Assert(status.Text == "guard sentinel", "F7 ran while calibrating");
                    Assert(typeof(MainForm).GetField("_typingCancellation", flags).GetValue(form) == null, "typing started");
                }
            });
        }
    }
}
