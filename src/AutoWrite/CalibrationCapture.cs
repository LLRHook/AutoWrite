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
        // Short report paragraphs provide natural variety without invented typing tokens.
        public const string Text = "Smith et al. (2021) found a 42.5% rise [12].\n" + "Is it \"robust\"? Yes: p < 0.05.\n" +
        "NASA and the US call the study's n = 128 cases \"reliable\".\n" + "R&D needs long-term evidence (Smith et al., 2021).\n" +
        "Table 2.1 lists costs and/or gains. Figure 3 shows them.\n" + (
        ("Figure 3 compares the UK and US samples (Lee et al., 2022). The report" + " calls the trend \"stable") +
        "\"; e.g., both groups improved by 12.8%.\n" ) + (
        ("The methods cover three stages: recruitment, review, and analysis. Par" + "ticipants read the same in") +
        "structions, and the team recorded each response [24].\n" ) + (
        ("The R&D review found a 36.2% reduction in waste (Jones et al., 2023). " + "Could this reflect a chang") +
        "e in demand? The authors tested that explanation before accepting the result.\n" ) + (
        ("Table 4.2 reports the mean, range, and sample size: n = 256, p < 0.01." + " Its notes distinguish sho") +
        "rt-term variation from a sustained change; the difference matters for planning.\n" ) + (
        ("A second NASA study assessed water quality [36]. It found that 18.4% o" + "f samples exceeded the thr") +
        "eshold, while the control group remained within the expected range.\n" ) + (
        ("The analysis follows the same sequence in each region: collect evidenc" + "e, compare estimates, and ") + (
        "report uncertainty. A missing value is marked as unavailable rather th" + "an treated as zero (Brown ") + "et al., 2020).\n" ) +
        (
        ("Figure 5 shows the distribution before and after the policy change. Th" + "e central estimate rose fr") + (
        "om 21.6% to 34.9%, but the intervals overlap. The report therefore des" + "cribes the evidence as \"p") +
        "romising, with limits\".\n"
        ) + ( ("The review considers cost and/or reliability when comparing options. L" + "ong-term maintenance may o") + (
        "utweigh an initial saving; the discussion separates those effects and " + "states the assumptions [48") + "].\n" ) + (
        ("The final model includes age, location, and prior experience. Table 6." + "1 gives the adjusted estim") +
        "ates (Smith et al., 2024), with n = 512 and p < 0.05 for the main comparison.\n" ) + (
        ("These findings support further evaluation rather than an immediate cha" + "nge in policy. What additi") + (
        "onal evidence would resolve the uncertainty? A larger sample and an in" + "dependent replication woul") +
        "d strengthen the case.\n" )
        + ( ("The conclusion returns to the research question and weighs the evidenc" + "e fairly. Future R&D shoul") + (
        "d test the mechanism in other settings, publish the full methods, and " + "explain any departures fro") + "m the plan [60].\n" );
    }
    internal sealed class CaptureKey
    {
        public double Milliseconds;
        public char Character;
        public CaptureKey(double milliseconds, char character)
        {
            Milliseconds = milliseconds;
            Character = character;
        }
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
        public CaptureSession(int seconds)
        {
            Seconds = seconds;
        }

        public bool Accept(char key, double milliseconds)
        {
            if (Finished)
            {
                return false;
            }
            if (milliseconds >= Seconds * 1000D && Started)
            {
                Finished = true;
                return false;
            }
            if (key == '\r')
            {
                key = '\n';
            }
            if (key != '\b' && key != '\n' && (key < ' ' || key > '~'))
            {
                Ignore("character " + (int)key, milliseconds);
                return false;
            }
            Started = true;
            Keys.Add(new CaptureKey(milliseconds, key));
            Raw.Add(milliseconds.ToString("F3", CultureInfo.InvariantCulture) + "\tcharacter\t" + (int)key);
            if (key == '\b')
            {
                if (TypedText.Length > 0)
                {
                    TypedText = TypedText.Substring(0, TypedText.Length - 1);
                }
            }
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
            var directory = Path.Combine(DataDirectory.PathRoot, "calibration");
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, "capture-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + "-" + Guid.NewGuid().
            ToString("N").Substring(0, 8) + ".tsv");
            var lines = new List<string>();
            lines.Add("# version=1;seconds=" + Seconds + ";completed=" + completed + ";ignored_keys=" + IgnoredKeys);
            lines.Add("elapsed_ms\tkind\tcharacter_code_or_reason");
            lines.AddRange(Raw);
            File.WriteAllLines(path, lines.ToArray(), new UTF8Encoding(false));
            return path;
        }
    }
    internal class QuietForm : Form
    {
        internal static bool Diagnostic
        {
            get
            {
                return Environment.GetEnvironmentVariable("AUTOWRITE_DIAGNOSTIC_NOACTIVATE") == "1";
            }
        }

        protected override bool ShowWithoutActivation
        {
            get
            {
                return Diagnostic;
            }
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var p = base.CreateParams;
                if (Diagnostic)
                {
                    p.ExStyle |= 0x08000000;
                }
                return p;
            }
        }
    }
    internal sealed class CaptureTextBox : TextBox
    {
        private bool _suppressCharacter;
        internal Action<char> Character;
        internal Action<string> Ignored;
        internal Action<int> Press;
        public CaptureTextBox()
        {
            Name = "CalibrationInput";
            AccessibleName = "Typing input";
            Multiline = true;
            ReadOnly = false;
            ShortcutsEnabled = false;
            BackColor = SystemColors.Window;
            ForeColor = SystemColors.WindowText;
            AcceptsReturn = true;
            AcceptsTab = true;
            ScrollBars = ScrollBars.Vertical;
        }

        protected override bool IsInputKey(Keys keyData)
        {
            return true;
        }

        private static bool ShortcutModifier
        {
            get
            {
                Keys modifiers = ModifierKeys & (Keys.Control | Keys.Alt);
                // AltGr is Ctrl+Alt on Windows; its printable WM_CHAR is ordinary text.
                return modifiers == Keys.Control || modifiers == Keys.Alt;
            }
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x100 || m.Msg == 0x104)
            {
                var key = (Keys)m.WParam.ToInt32();
                _suppressCharacter = false;
                int repeats = Math.Max(1, (int)(m.LParam.ToInt64() & 0xFFFF));
                if (Press != null)
                {
                    for (int repeat = 0; repeat < repeats; repeat++)
                    {
                        Press((int)key);
                    }
                }
                if (key == Keys.Back && (ModifierKeys & Keys.Control) != 0)
                {
                    _suppressCharacter = true;
                    if (Ignored != null)
                    {
                        Ignored("Ctrl+Backspace");
                    }
                    return;
                }
                if (key == Keys.Tab || key == Keys.Left || key == Keys.Right || key == Keys.Up || key == Keys.Down || key == Keys.Home ||
                key == Keys.End || key == Keys.Delete || key == Keys.PageUp || key == Keys.PageDown ||
                (ShortcutModifier && key != Keys.ControlKey && key != Keys.Menu))
                {
                    _suppressCharacter = true;
                    if (Ignored != null)
                    {
                        Ignored("key " + key);
                    }
                    return;
                }
            }
            if (m.Msg == 0x102)
            {
                var value = (char)m.WParam.ToInt32();
                if (_suppressCharacter)
                {
                    _suppressCharacter = false;
                    return;
                }
                if (ShortcutModifier || value == '\t' || value == 127)
                {
                    if (Ignored != null)
                    {
                        Ignored("shortcut character " + (int)value);
                    }
                    return;
                }
                int repeats = Math.Max(1, (int)(m.LParam.ToInt64() & 0xFFFF));
                if (Character != null)
                {
                    for (int repeat = 0; repeat < repeats; repeat++)
                    {
                        Character(value);
                    }
                }
                return;
            }
            if (m.Msg == 0x201 && !QuietForm.Diagnostic && FindForm() != null && FindForm().Visible)
            {
                Focus();
                SelectionStart = TextLength;
            }
            if (m.Msg == 0x302 || m.Msg == 0x300 || m.Msg == 0x303 || m.Msg == 0x304 || m.Msg == 0x201 || m.Msg == 0x203)
            {
                if (Ignored != null)
                {
                    Ignored("edit message " + m.Msg);
                }
                return;
            }
            base.WndProc(ref m);
        }
    }
    internal sealed class CalibrationDialog : QuietForm
    {
        private readonly ComboBox _length = new ComboBox();
        private readonly Label _clock = new Label();
        private readonly RichTextBox _passage = new RichTextBox();
        private readonly CaptureTextBox _input = new CaptureTextBox();
        private readonly Stopwatch _watch = new Stopwatch();
        private readonly Timer _timer = new Timer();
        private CaptureSession _session;
        internal CaptureSession CompletedSession;
        internal string SaveWarning = "";
        public CalibrationDialog()
        {
            Text = "Typing calibration";
            Name = "CalibrationDialog";
            Size = new Size(840, 660);
            MinimumSize = new Size(700, 560);
            StartPosition = FormStartPosition.CenterParent;
            Font = new Font("Segoe UI", 10);
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, RowCount = 5, ColumnCount = 1, Padding = new Padding(14)
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 55));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 45));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            Controls.Add(layout);
            var top = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill
            };
            _length.Name = "CalibrationLength";
            _length.DropDownStyle = ComboBoxStyle.DropDownList;
            _length.Items.AddRange(new object[]
            {
                "30 seconds", "60 seconds"
            });
            _length.SelectedIndex = 1;
            top.Controls.Add(_length);
            _clock.Text = "Clock starts with your first character";
            _clock.AutoSize = true;
            top.Controls.Add(_clock);
            layout.Controls.Add(top);
            layout.Controls.Add(new Label
            {
                Text = ("Copy the passage below. Corrections are Backspace only; paste, navigat" + "ion and Tab are ignored."), Dock =
                DockStyle.Fill
            });
            _passage.Text = CalibrationPassage.Text;
            _passage.ReadOnly = true;
            _passage.BackColor = SystemColors.Window;
            _passage.ScrollBars = RichTextBoxScrollBars.Vertical;
            _passage.Dock = DockStyle.Fill;
            _passage.Font = new Font("Consolas", 12);
            _passage.Name = "CalibrationPassage";
            _passage.AccessibleName = "Calibration passage";
            layout.Controls.Add(_passage);
            _input.Dock = DockStyle.Fill;
            _input.Font = new Font("Consolas", 12);
            layout.Controls.Add(_input);
            var cancel = new Button
            {
                Text = "Cancel", Name = "CalibrationCancel", Dock = DockStyle.Right
            };
            cancel.Click += delegate
            {
                Close();
            };
            layout.Controls.Add(cancel);
            CancelButton = cancel;
            _input.Character = Accept;
            _input.Ignored = delegate(string reason)
            {
                EnsureSession();
                _session.Ignore(reason, _watch.Elapsed.TotalMilliseconds);
            };
            _input.Press = delegate(int key)
            {
                EnsureSession();
                _session.Raw.Add(_watch.Elapsed.TotalMilliseconds.ToString("F3", CultureInfo.InvariantCulture) + "\tkeydown\t" + key);
            };
            _timer.Interval = 20;
            _timer.Tick += delegate
            {
                if (_watch.IsRunning)
                {
                    _clock.Text = Math.Max(0, _session.Seconds - _watch.Elapsed.TotalSeconds).ToString("F1") + " seconds remaining";
                    if (_watch.Elapsed.TotalSeconds >= _session.Seconds)
                    {
                        Finish();
                    }
                }
            };
            _timer.Start();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            if (!QuietForm.Diagnostic)
            {
                _input.Focus();
            }
        }

        private void EnsureSession()
        {
            if (_session == null)
            {
                _session = new CaptureSession(_length.SelectedIndex == 0 ? 30 : 60);
            }
            if (!_watch.IsRunning)
            {
                _session.Seconds = _length.SelectedIndex == 0 ? 30 : 60;
            }
        }

        private void Accept(char key)
        {
            EnsureSession();
            if (!_watch.IsRunning)
            {
                _watch.Start();
                _length.Enabled = false;
            }
            if (_session.Accept(key, _watch.Elapsed.TotalMilliseconds))
            {
                _input.Text = _session.TypedText.Replace("\n", "\r\n");
                _input.SelectionStart = _input.TextLength;
                _input.ScrollToCaret();
                UpdateProgress(_session.TypedText);
            }
            if (_session.Finished)
            {
                Finish();
            }
        }

        internal void UpdateProgress(string typed)
        {
            int reached = Math.Min(_passage.TextLength, CalibrationAnalysis.AlignPrefix(typed, CalibrationPassage.Text).End);
            _passage.Select(0, _passage.TextLength);
            _passage.SelectionColor = SystemColors.WindowText;
            _passage.SelectionBackColor = SystemColors.Window;
            _passage.Select(0, reached);
            _passage.SelectionColor = Color.DimGray;
            _passage.Select(reached, reached < _passage.TextLength ? 1 : 0);
            _passage.SelectionBackColor = Color.LightGoldenrodYellow;
            _passage.ScrollToCaret();
        }

        private void Finish()
        {
            _session.Finished = true;
            CompletedSession = _session;
            DialogResult = DialogResult.OK;
            Close();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _timer.Stop();
            _timer.Dispose();
            if (_session != null)
            {
                try
                {
                    _session.Save(CompletedSession != null);
                }
                catch (IOException)
                {
                    SaveWarning = "The recording could not be saved. You can still review and apply these settings.";
                }
                catch (UnauthorizedAccessException)
                {
                    SaveWarning = "The recording could not be saved. You can still review and apply these settings.";
                }
            }
            base.OnFormClosed(e);
        }
    }
}
