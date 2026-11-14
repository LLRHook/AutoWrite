using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace AutoWrite.Tests
{
    internal static partial class Runner
    {
        [DllImport("user32.dll")]
        private static extern bool GetKeyboardState(byte[] state);
        [DllImport("user32.dll")]
        private static extern bool SetKeyboardState(byte[] state);

        private static void AltGrTests()
        {
            Test("AltGr printable characters are text and Ctrl shortcuts count once", delegate
            {
                var original = new byte[256];
                Assert(GetKeyboardState(original), "cannot read thread key state");
                try
                {
                    using (var box = new CaptureTextBox())
                    {
                        var session = new CaptureSession(60);
                        box.Character = delegate(char c) { session.Accept(c, session.Keys.Count); };
                        box.Ignored = delegate(string reason) { session.Ignore(reason, 0); };
                        var state = new byte[256];
                        state[0x11] = state[0xA2] = state[0x12] = state[0xA5] = 0x80;
                        Assert(SetKeyboardState(state), "cannot set thread key state");
                        int[] codes = { 0x38, 0x51, 0x39 };
                        string text = "[@]";
                        for (int i = 0; i < text.Length; i++)
                        {
                            SendMessage(box.Handle, 0x104, (IntPtr)codes[i], (IntPtr)1);
                            SendMessage(box.Handle, 0x102, (IntPtr)text[i], (IntPtr)1);
                        }
                        Assert(session.TypedText == text && session.IgnoredKeys == 0, "AltGr text lost or counted ignored");
                        state[0x12] = state[0xA5] = 0;
                        SetKeyboardState(state);
                        SendMessage(box.Handle, 0x100, (IntPtr)Keys.Back, (IntPtr)1);
                        SendMessage(box.Handle, 0x102, (IntPtr)127, (IntPtr)1);
                        SendMessage(box.Handle, 0x100, (IntPtr)Keys.V, (IntPtr)1);
                        SendMessage(box.Handle, 0x102, (IntPtr)22, (IntPtr)1);
                        Assert(session.TypedText == text && session.IgnoredKeys == 2, "Ctrl shortcut counted twice");
                    }
                }
                finally
                {
                    Assert(SetKeyboardState(original), "cannot restore thread key state");
                }
            });
        }
    }
}
