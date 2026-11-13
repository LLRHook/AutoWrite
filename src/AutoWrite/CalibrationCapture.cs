using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace AutoWrite
{
    internal static class CalibrationPassage
    {
        // Dense on purpose: the first 100 characters exercise all motor classes and two breaks.
        public const string Text =
            "In eDNA [12], aX=42.5%; (why?)\n(Smith, 2021): n=128; p<0.05.\nWe cite NASA and/or 'A'.\n" +
            "Table 2.1: aB[34] cD(56) eF=78; gH:90 iJ%12 kL/34.\n" +
            "Figure 3: mN[56] oP(78) qR=90; sT:12 uV%34 wX/56.\n" +
            "At 42.5%, e.g., the US team calls it \"robust\"; is it?\n" +
            "Their aA[12] bB(34) cC=56; dD:78 eE%90 fF/12.\n" +
            "Data (Smith et al., 2021) support H2: p < 0.05, n = 128.\n" +
            "A report's Table 2.1 tests 42.5% and/or 67.8% [12].\n" +
            "Here gG[34] hH(56) iI=78; jJ:90 kK%12 lL/34.\n" +
            "Figure 3 shows NASA's eDNA: \"A mixed result\"; why?\n" +
            "Next mM[56] nN(78) oO=90; pP:12 qQ%34 rR/56.\n" +
            "We compare aX=12 with bY=34, cZ=56 and dW=78 [90].\n" +
            "Can Table 4.2 separate cause/effect? Yes: p=0.01.\n" +
            "Also sS[78] tT(90) uU=12; vV:34 wW%56 xX/78.\n" +
            "A second review (Jones et al., 2022) tests H3 [24].\n" +
            "Quote \"aB:12\" then \"cD:34\"; keep EFG and HiJ.\n" +
            "Under Model 5, yY[90] zZ(12) aB=34; cD:56 eF%78.\n" +
            "R&D may compare 12+34=46, 8*9=72 and (42-7)/5=7.\n" +
            "The review asks: \"Is p<0.05 enough?\" No; cite [36].\n" +
            "Again gH[12] iJ(34) kL=56; mN:78 oP%90 qR/12.\n" +
            "A new Figure 6.1 uses eDNA, RNA and US data (2023).\n" +
            "Measure sT[34] uV(56) wX=78; yZ:90 aC%12 bD/34.\n" +
            "Use E=mc^2; a+b<c; x_y and {z} or [w] remain valid.\n" +
            "A final Table 7.2 cites [48]: 42.5%, n=256, p=0.02.\n" +
            "Repeat eF[56] gH(78) iJ=90; kL:12 mN%34 oP/56.\n" +
            "The author's NASA report checks and/or revises H4.\n" +
            "In closing qR[78] sT(90) uV=12; wX:34 yZ%56 aB/78.\n" +
            "A masters-level report quotes \"limits\" (Lee, 2024).\n" +
            "Figure 8: cD[90] eF(12) gH=34; iJ:56 kL%78 mN/90.\n" +
            "Do results agree? Use Table 9.3 and [60]; p < 0.05.\n";
    }

    internal sealed class CaptureKey
    {
        public double Milliseconds;
        public char Character;
        public CaptureKey(double milliseconds, char character) { Milliseconds = milliseconds; Character = character; }
    }

    internal sealed class CaptureSession
    {
        public readonly List<CaptureKey> Keys = new List<CaptureKey>();
        public readonly List<string> Raw = new List<string>();
        public int Seconds;
        public int IgnoredKeys;
        public bool Started;
        public bool Finished;
        public string TypedText = "";
        public CaptureSession(int seconds) { Seconds = seconds; }
        public bool Accept(char key, double milliseconds)
        {
            if (Finished) return false;
            if (milliseconds >= Seconds * 1000D && Started) { Finished = true; return false; }
            if (key == '\r') key = '\n';
            if (key != '\b' && key != '\n' && (key < ' ' || key > '~')) { Ignore("character " + (int)key, milliseconds); return false; }
            Started = true;
            Keys.Add(new CaptureKey(milliseconds, key));
            Raw.Add(milliseconds.ToString("F3", CultureInfo.InvariantCulture) + "\tcharacter\t" + (int)key);
            if (key == '\b') { if (TypedText.Length > 0) TypedText = TypedText.Substring(0, TypedText.Length - 1); }
            else TypedText += key;
            return true;
        }
        public void Ignore(string reason, double milliseconds)
        {
            IgnoredKeys++;
            Raw.Add(milliseconds.ToString("F3", CultureInfo.InvariantCulture) + "\tignored\t" + reason);
        }
        public string Save(bool completed)
        {
            var directory = Path.Combine(DataDirectory.PathRoot, "calibration"); Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, "capture-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".tsv");
            var lines = new List<string>();
            lines.Add("# version=1;seconds=" + Seconds + ";completed=" + completed + ";ignored_keys=" + IgnoredKeys);
            lines.Add("elapsed_ms\tkind\tcharacter_code_or_reason"); lines.AddRange(Raw);
            File.WriteAllLines(path, lines.ToArray(), new UTF8Encoding(false)); return path;
        }
    }

    internal class QuietForm : Form
    {
        internal static bool Diagnostic { get { return Environment.GetEnvironmentVariable("AUTOWRITE_DIAGNOSTIC_NOACTIVATE") == "1"; } }
        protected override bool ShowWithoutActivation { get { return Diagnostic; } }
        protected override CreateParams CreateParams
        {
            get { var p = base.CreateParams; if (Diagnostic) p.ExStyle |= 0x08000000; return p; }
        }
    }

    internal sealed class CaptureTextBox : TextBox
    {
        private char _suppressedCharacter;
        internal Action<char> Character;
        internal Action<string> Ignored;
        internal Action<int> Press;
        public CaptureTextBox()
        {
            Name = "CalibrationInput"; AccessibleName = "Typing input"; Multiline = true; ReadOnly = true; ShortcutsEnabled = false;
            AcceptsReturn = true; AcceptsTab = true; ScrollBars = ScrollBars.Vertical;
        }
        protected override bool IsInputKey(Keys keyData) { return true; }
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x100 || m.Msg == 0x104)
            {
                var key = (Keys)m.WParam.ToInt32(); int repeats = Math.Max(1, (int)(m.LParam.ToInt64() & 0xFFFF)); if (Press != null) for (int repeat = 0; repeat < repeats; repeat++) Press((int)key);
                if (key == Keys.Back && (ModifierKeys & Keys.Control) != 0) { _suppressedCharacter = (char)127; if (Ignored != null) Ignored("Ctrl+Backspace"); return; }
                if (key == Keys.Tab || key == Keys.Left || key == Keys.Right || key == Keys.Up || key == Keys.Down || key == Keys.Home || key == Keys.End || key == Keys.Delete || key == Keys.PageUp || key == Keys.PageDown || ((ModifierKeys & (Keys.Control | Keys.Alt)) != 0 && key != Keys.ControlKey && key != Keys.Menu))
                { _suppressedCharacter = key == Keys.Tab ? '\t' : ((ModifierKeys & Keys.Control) != 0 && key >= Keys.A && key <= Keys.Z) ? (char)((int)key & 31) : '\0'; if (Ignored != null) Ignored("key " + key); return; }
            }
            if (m.Msg == 0x102)
            {
                var value = (char)m.WParam.ToInt32();
                if (_suppressedCharacter != '\0' && _suppressedCharacter == value) { _suppressedCharacter = '\0'; return; }
                _suppressedCharacter = '\0';
                if ((ModifierKeys & (Keys.Control | Keys.Alt)) != 0 || value == '\t' || value == 127)
                { if (Ignored != null) Ignored("shortcut character " + (int)value); return; }
                int repeats = Math.Max(1, (int)(m.LParam.ToInt64() & 0xFFFF));
                if (Character != null) for (int repeat = 0; repeat < repeats; repeat++) Character(value); return;
            }
            if (m.Msg == 0x201 && !QuietForm.Diagnostic && FindForm() != null && FindForm().Visible)
            { Focus(); SelectionStart = TextLength; }
            if (m.Msg == 0x302 || m.Msg == 0x300 || m.Msg == 0x303 || m.Msg == 0x304 || m.Msg == 0x201 || m.Msg == 0x203)
            { if (Ignored != null) Ignored("edit message " + m.Msg); return; }
            base.WndProc(ref m);
        }
    }

    internal sealed class CalibrationDialog : QuietForm
    {
        private readonly ComboBox _length = new ComboBox();
        private readonly Label _clock = new Label();
        private readonly CaptureTextBox _input = new CaptureTextBox();
        private readonly Stopwatch _watch = new Stopwatch();
        private readonly Timer _timer = new Timer();
        private CaptureSession _session;
        internal CaptureSession CompletedSession;
        public CalibrationDialog()
        {
            Text = "Typing calibration"; Name = "CalibrationDialog"; Size = new Size(840, 660); MinimumSize = new Size(700, 560);
            StartPosition = FormStartPosition.CenterParent; Font = new Font("Segoe UI", 10);
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 5, ColumnCount = 1, Padding = new Padding(14) };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 55)); layout.RowStyles.Add(new RowStyle(SizeType.Percent, 45)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42)); Controls.Add(layout);
            var top = new FlowLayoutPanel { Dock = DockStyle.Fill }; _length.Name = "CalibrationLength"; _length.DropDownStyle = ComboBoxStyle.DropDownList;
            _length.Items.AddRange(new object[] { "30 seconds", "60 seconds" }); _length.SelectedIndex = 1; top.Controls.Add(_length);
            _clock.Text = "Clock starts with your first character"; _clock.AutoSize = true; top.Controls.Add(_clock); layout.Controls.Add(top);
            layout.Controls.Add(new Label { Text = "Copy the passage below. Corrections are Backspace only; paste, navigation and Tab are ignored.", Dock = DockStyle.Fill });
            layout.Controls.Add(new TextBox { Text = CalibrationPassage.Text.Replace("\n", "\r\n"), Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill, Font = new Font("Consolas", 12), Name = "CalibrationPassage", AccessibleName = "Calibration passage" });
            _input.Dock = DockStyle.Fill; _input.Font = new Font("Consolas", 12); layout.Controls.Add(_input);
            var cancel = new Button { Text = "Cancel", Name = "CalibrationCancel", Dock = DockStyle.Right }; cancel.Click += delegate { Close(); }; layout.Controls.Add(cancel); CancelButton = cancel;
            _input.Character = Accept;
            _input.Ignored = delegate(string reason) { EnsureSession(); _session.Ignore(reason, _watch.Elapsed.TotalMilliseconds); };
            _input.Press = delegate(int key) { EnsureSession(); _session.Raw.Add(_watch.Elapsed.TotalMilliseconds.ToString("F3", CultureInfo.InvariantCulture) + "\tkeydown\t" + key); };
            _timer.Interval = 20; _timer.Tick += delegate { if (_watch.IsRunning) { _clock.Text = Math.Max(0, _session.Seconds - _watch.Elapsed.TotalSeconds).ToString("F1") + " seconds remaining"; if (_watch.Elapsed.TotalSeconds >= _session.Seconds) Finish(); } }; _timer.Start();
        }
        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e); if (!QuietForm.Diagnostic) _input.Focus();
        }
        private void EnsureSession() { if (_session == null) _session = new CaptureSession(_length.SelectedIndex == 0 ? 30 : 60); if (!_watch.IsRunning) _session.Seconds = _length.SelectedIndex == 0 ? 30 : 60; }
        private void Accept(char key)
        {
            EnsureSession(); if (!_watch.IsRunning) { _watch.Start(); _length.Enabled = false; }
            if (_session.Accept(key, _watch.Elapsed.TotalMilliseconds)) { _input.Text = _session.TypedText.Replace("\n", "\r\n"); _input.SelectionStart = _input.TextLength; _input.ScrollToCaret(); }
            if (_session.Finished) Finish();
        }
        private void Finish() { _session.Finished = true; CompletedSession = _session; DialogResult = DialogResult.OK; Close(); }
        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _timer.Stop(); _timer.Dispose(); if (_session != null) _session.Save(CompletedSession != null); base.OnFormClosed(e);
        }
    }
}
