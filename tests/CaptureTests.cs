using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace AutoWrite.Tests
{
    internal static partial class Runner
    {
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr window, int message, IntPtr wparam, IntPtr lparam);
        static partial void MoreTests()
        {
            Test("D2 dense first 100 characters and 900-character runway", delegate {
                var passage = CalibrationPassage.Text; var prefix = passage.Substring(0, 100);
                Assert(passage.Length > 900, "fast typist runs out");
                Assert(prefix.Split('\n').Length >= 3, "two paragraph breaks not reached");
                foreach (var c in new[] { '[', ']', '(', ')', 'S', '4', '%', '?', '"', '<', ':' })
                    Assert(prefix.IndexOf(c) >= 0, "missing class/example: " + c);
                foreach (var example in new[] { "(Smith et al., 2021)", "[12]", "42.5%", "p < 0.05", "n = 128", "Table 2.1", "Figure 3", "study's", "and/or", "long-term", "R&D", "NASA", "e.g.," })
                    Assert(passage.Contains(example), "missing natural report example: " + example);
                Assert(!System.Text.RegularExpressions.Regex.IsMatch(passage, "[a-z][A-Z]\\["), "invented pair token");
            });
            Test("capture repeats backspace, normalizes Enter, enforces deadline", delegate {
                var s = new CaptureSession(30);
                Assert(s.Accept('a', 0), "first key"); s.Accept('b', 10); s.Accept('\b', 20); s.Accept('\b', 30); s.Accept('\b', 40); s.Accept('c', 50); s.Accept('\r', 60);
                Assert(s.TypedText == "c\n" && s.Keys.Count == 7, "repeat backspace/Enter");
                Assert(!s.Accept('d', 30000) && s.Finished, "deadline boundary");
                Assert(!s.Accept('e', 30001), "finished capture accepted key");
            });
            Test("real input control rejects paste, Tab and caret moves", delegate {
                using (var box = new CaptureTextBox())
                {
                    var s = new CaptureSession(60); box.Character = delegate(char c) { s.Accept(c, s.Keys.Count); box.Text = s.TypedText; };
                    box.Ignored = delegate(string reason) { s.Ignore(reason, 0); };
                    var h = box.Handle;
                    SendMessage(h, 0x102, (IntPtr)'a', (IntPtr)1);
                    SendMessage(h, 0x102, (IntPtr)'b', (IntPtr)1);
                    SendMessage(h, 0x102, (IntPtr)'\b', (IntPtr)1);
                    SendMessage(h, 0x302, IntPtr.Zero, IntPtr.Zero);
                    SendMessage(h, 0x102, (IntPtr)'\t', (IntPtr)1);
                    SendMessage(h, 0x102, (IntPtr)127, (IntPtr)1);
                    SendMessage(h, 0x100, (IntPtr)Keys.Left, IntPtr.Zero);
                    SendMessage(h, 0x201, IntPtr.Zero, IntPtr.Zero);
                    Assert(s.TypedText == "a" && s.Keys.Count == 3 && s.IgnoredKeys == 5, "edit policy drift");
                    var path = s.Save(false); Assert(File.ReadAllText(path).Contains("ignored_keys=5"), "ignored count not saved");
                }
            });
            Test("held Backspace consumes every repeat; blocked Tab counts once", delegate {
                using (var box = new CaptureTextBox())
                {
                    var s = new CaptureSession(30); box.Character = delegate(char c) { s.Accept(c, 0); }; box.Ignored = delegate(string reason) { s.Ignore(reason, 0); };
                    SendMessage(box.Handle, 0x102, (IntPtr)'a', (IntPtr)3);
                    SendMessage(box.Handle, 0x102, (IntPtr)'\b', (IntPtr)3);
                    Assert(s.Keys.Count == 6 && s.TypedText == "", "held repeat batch lost keys");
                    SendMessage(box.Handle, 0x100, (IntPtr)Keys.Tab, (IntPtr)1); SendMessage(box.Handle, 0x102, (IntPtr)'\t', (IntPtr)1);
                    Assert(s.IgnoredKeys == 1 && s.Keys.Count == 6, "blocked Tab counted twice");
                }
            });
            AnalysisTests();
        }
        static partial void AnalysisTests();
    }
}
