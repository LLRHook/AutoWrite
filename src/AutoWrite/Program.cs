using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

[assembly: System.Reflection.AssemblyTitle("AutoWrite")]
[assembly: System.Reflection.AssemblyDescription("Hotkey-triggered human-paced text writer")]
[assembly: System.Reflection.AssemblyCompany("AutoWrite")]
[assembly: System.Reflection.AssemblyProduct("AutoWrite")]
[assembly: System.Reflection.AssemblyVersion("1.0.0.0")]
[assembly: System.Reflection.AssemblyFileVersion("1.0.0.0")]

namespace AutoWrite
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            if (args != null && args.Length > 0 && String.Equals(args[0], "--sendinput-probe", StringComparison.OrdinalIgnoreCase))
            {
                return SelfTest.RunSendInputProbe();
            }

            Application.Run(new MainForm());
            return 0;
        }
    }

    internal sealed class MainForm : Form
    {
        private const int HotkeyId = 0xA701;
        private const int HotkeyVk = (int)Keys.F7;

        private readonly TextBox _textBox;
        private readonly NumericUpDown _wpmBox;
        private readonly NumericUpDown _jitterBox;
        private readonly NumericUpDown _keyPairRhythmBox;
        private readonly NumericUpDown _burstBox;
        private readonly NumericUpDown _wordPauseBox;
        private readonly NumericUpDown _sentencePauseBox;
        private readonly NumericUpDown _thinkingChanceBox;
        private readonly NumericUpDown _thinkingPauseBox;
        private readonly CheckBox _keepTargetWpmCheckBox;
        private readonly NumericUpDown _uppercasePenaltyBox;
        private readonly NumericUpDown _numberPenaltyBox;
        private readonly NumericUpDown _punctuationPenaltyBox;
        private readonly NumericUpDown _shiftSymbolPenaltyBox;
        private readonly NumericUpDown _numberTypoMultiplierBox;
        private readonly NumericUpDown _punctuationTypoMultiplierBox;
        private readonly NumericUpDown _shiftSymbolTypoMultiplierBox;
        private readonly CheckBox _typoCheckBox;
        private readonly NumericUpDown _typoChanceBox;
        private readonly NumericUpDown _duplicateChanceBox;
        private readonly NumericUpDown _transposeChanceBox;
        private readonly NumericUpDown _noticeDelayBox;
        private readonly NumericUpDown _detectCharsBox;
        private readonly NumericUpDown _correctionPauseBox;
        private readonly NumericUpDown _resumePauseBox;
        private readonly NumericUpDown _buttonDelayBox;
        private readonly Button _startButton;
        private readonly Button _stopButton;
        private readonly Label _statusLabel;
        private readonly Label _hotkeyLabel;
        private readonly ProgressBar _progressBar;
        private readonly TypingEngine _typingEngine;

        private CancellationTokenSource _typingCancellation;
        private bool _hotkeyRegistered;

        public MainForm()
        {
            Text = "AutoWrite";
            TrySetApplicationIcon();
            MinimumSize = new Size(900, 560);
            Size = new Size(1040, 680);
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 9F);
            BackColor = Color.FromArgb(248, 249, 251);

            _typingEngine = new TypingEngine();

            var settings = SettingsStore.Load();

            var root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.ColumnCount = 2;
            root.RowCount = 3;
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 360F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 54F));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
            Controls.Add(root);

            var header = new Panel();
            header.Dock = DockStyle.Fill;
            header.Padding = new Padding(16, 8, 16, 8);
            header.BackColor = Color.White;
            root.Controls.Add(header, 0, 0);
            root.SetColumnSpan(header, 2);

            var title = new Label();
            title.Text = "AutoWrite";
            title.AutoSize = true;
            title.Font = new Font("Segoe UI Semibold", 15F, FontStyle.Bold);
            title.Location = new Point(16, 12);
            header.Controls.Add(title);

            _hotkeyLabel = new Label();
            _hotkeyLabel.Text = "F7 pending";
            _hotkeyLabel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _hotkeyLabel.TextAlign = ContentAlignment.MiddleRight;
            _hotkeyLabel.Size = new Size(220, 24);
            _hotkeyLabel.Location = new Point(header.Width - _hotkeyLabel.Width - 16, 15);
            header.Controls.Add(_hotkeyLabel);
            header.Resize += delegate
            {
                _hotkeyLabel.Location = new Point(header.Width - _hotkeyLabel.Width - 16, 15);
            };

            _textBox = new TextBox();
            _textBox.Dock = DockStyle.Fill;
            _textBox.Multiline = true;
            _textBox.ScrollBars = ScrollBars.Vertical;
            _textBox.AcceptsReturn = true;
            _textBox.AcceptsTab = true;
            _textBox.WordWrap = true;
            _textBox.BorderStyle = BorderStyle.FixedSingle;
            _textBox.Font = new Font("Consolas", 10.5F);
            _textBox.Margin = new Padding(16, 14, 10, 12);
            root.Controls.Add(_textBox, 0, 1);

            var sidePanel = new Panel();
            sidePanel.Dock = DockStyle.Fill;
            sidePanel.Padding = new Padding(8, 14, 16, 12);
            sidePanel.BackColor = Color.FromArgb(248, 249, 251);
            sidePanel.AutoScroll = true;
            root.Controls.Add(sidePanel, 1, 1);

            var sideLayout = new TableLayoutPanel();
            sideLayout.Dock = DockStyle.Top;
            sideLayout.AutoSize = true;
            sideLayout.ColumnCount = 1;
            sideLayout.RowCount = 2;
            sideLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            sideLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            sideLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            sidePanel.Controls.Add(sideLayout);

            var settingsPanel = new TableLayoutPanel();
            settingsPanel.Dock = DockStyle.Top;
            settingsPanel.AutoSize = true;
            settingsPanel.ColumnCount = 2;
            settingsPanel.RowCount = 0;
            settingsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58F));
            settingsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42F));
            settingsPanel.Padding = new Padding(0);
            sideLayout.Controls.Add(settingsPanel, 0, 0);

            AddSectionLabel(settingsPanel, "Typing");

            _wpmBox = CreateNumberBox(5, 180, settings.WordsPerMinute);
            AddLabeledControl(settingsPanel, "Target WPM", _wpmBox);

            _buttonDelayBox = CreateNumberBox(0, 10, settings.ButtonDelaySeconds);
            AddLabeledControl(settingsPanel, "Button delay s", _buttonDelayBox);

            AddSectionLabel(settingsPanel, "Rhythm");

            _jitterBox = CreateNumberBox(0, 100, settings.JitterPercent);
            AddLabeledControl(settingsPanel, "Jitter %", _jitterBox);

            _keyPairRhythmBox = CreateNumberBox(0, 100, settings.KeyPairRhythmPercent);
            AddLabeledControl(settingsPanel, "Key-pair rhythm %", _keyPairRhythmBox);

            _burstBox = CreateNumberBox(0, 100, settings.BurstPercent);
            AddLabeledControl(settingsPanel, "Burst drift %", _burstBox);

            _wordPauseBox = CreateNumberBox(0, 1000, settings.WordPauseMilliseconds);
            AddLabeledControl(settingsPanel, "Word pause ms", _wordPauseBox);

            _sentencePauseBox = CreateNumberBox(0, 3000, settings.SentencePauseMilliseconds);
            AddLabeledControl(settingsPanel, "Sentence pause ms", _sentencePauseBox);

            _thinkingChanceBox = CreateNumberBox(0, 25, settings.ThinkingPauseChancePercent);
            AddLabeledControl(settingsPanel, "Thinking pause %", _thinkingChanceBox);

            _thinkingPauseBox = CreateNumberBox(0, 5000, settings.ThinkingPauseMilliseconds);
            AddLabeledControl(settingsPanel, "Thinking max ms", _thinkingPauseBox);

            AddSectionLabel(settingsPanel, "Difficult keys");

            _keepTargetWpmCheckBox = new CheckBox();
            _keepTargetWpmCheckBox.Text = "Keep target WPM";
            _keepTargetWpmCheckBox.Checked = settings.KeepTargetWpm;
            _keepTargetWpmCheckBox.AutoSize = true;
            _keepTargetWpmCheckBox.Margin = new Padding(0, 4, 0, 4);
            var keepWpmRow = settingsPanel.RowCount++;
            settingsPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            settingsPanel.Controls.Add(_keepTargetWpmCheckBox, 0, keepWpmRow);
            settingsPanel.SetColumnSpan(_keepTargetWpmCheckBox, 2);

            _uppercasePenaltyBox = CreateNumberBox(0, 200, settings.UppercaseDelayPenaltyPercent);
            AddLabeledControl(settingsPanel, "Uppercase delay %", _uppercasePenaltyBox);

            _numberPenaltyBox = CreateNumberBox(0, 200, settings.NumberDelayPenaltyPercent);
            AddLabeledControl(settingsPanel, "Number delay %", _numberPenaltyBox);

            _punctuationPenaltyBox = CreateNumberBox(0, 200, settings.PunctuationDelayPenaltyPercent);
            AddLabeledControl(settingsPanel, "Punctuation delay %", _punctuationPenaltyBox);

            _shiftSymbolPenaltyBox = CreateNumberBox(0, 300, settings.ShiftSymbolDelayPenaltyPercent);
            AddLabeledControl(settingsPanel, "Shift-symbol delay %", _shiftSymbolPenaltyBox);

            _numberTypoMultiplierBox = CreateMultiplierBox(0, 500, settings.NumberTypoMultiplierPercent);
            AddLabeledControl(settingsPanel, "Number mistakes x", _numberTypoMultiplierBox);

            _punctuationTypoMultiplierBox = CreateMultiplierBox(0, 500, settings.PunctuationTypoMultiplierPercent);
            AddLabeledControl(settingsPanel, "Punct. mistakes x", _punctuationTypoMultiplierBox);

            _shiftSymbolTypoMultiplierBox = CreateMultiplierBox(0, 500, settings.ShiftSymbolTypoMultiplierPercent);
            AddLabeledControl(settingsPanel, "Shift mistakes x", _shiftSymbolTypoMultiplierBox);

            AddSectionLabel(settingsPanel, "Correction");

            _typoCheckBox = new CheckBox();
            _typoCheckBox.Text = "Corrected mistakes";
            _typoCheckBox.Checked = settings.EnableTypos;
            _typoCheckBox.AutoSize = true;
            _typoCheckBox.Margin = new Padding(0, 4, 0, 4);
            var typoRow = settingsPanel.RowCount++;
            settingsPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            settingsPanel.Controls.Add(_typoCheckBox, 0, typoRow);
            settingsPanel.SetColumnSpan(_typoCheckBox, 2);

            _typoChanceBox = CreateNumberBox(0, 10, settings.TypoChancePercent);
            AddLabeledControl(settingsPanel, "Mistake rate %", _typoChanceBox);

            _duplicateChanceBox = CreateNumberBox(0, 100, settings.DuplicateChancePercent);
            AddLabeledControl(settingsPanel, "Duplicate share %", _duplicateChanceBox);

            _transposeChanceBox = CreateNumberBox(0, 100, settings.TransposeChancePercent);
            AddLabeledControl(settingsPanel, "Transpose share %", _transposeChanceBox);

            _noticeDelayBox = CreateNumberBox(0, 3000, settings.CorrectionNoticeMilliseconds);
            AddLabeledControl(settingsPanel, "Notice delay ms", _noticeDelayBox);

            _detectCharsBox = CreateNumberBox(0, 8, settings.CorrectionDetectChars);
            AddLabeledControl(settingsPanel, "Detect after chars", _detectCharsBox);

            _correctionPauseBox = CreateNumberBox(0, 1000, settings.CorrectionPauseMilliseconds);
            AddLabeledControl(settingsPanel, "Repair key pause ms", _correctionPauseBox);

            _resumePauseBox = CreateNumberBox(0, 2000, settings.CorrectionResumeMilliseconds);
            AddLabeledControl(settingsPanel, "Resume pause ms", _resumePauseBox);

            var buttonPanel = new FlowLayoutPanel();
            buttonPanel.Dock = DockStyle.Top;
            buttonPanel.FlowDirection = FlowDirection.LeftToRight;
            buttonPanel.WrapContents = false;
            buttonPanel.AutoSize = true;
            buttonPanel.Padding = new Padding(0, 18, 0, 0);
            sideLayout.Controls.Add(buttonPanel, 0, 1);

            _startButton = new Button();
            _startButton.Text = "Type in 3s";
            _startButton.Width = 116;
            _startButton.Height = 32;
            _startButton.Click += delegate { StartTyping(true); };
            buttonPanel.Controls.Add(_startButton);

            _stopButton = new Button();
            _stopButton.Text = "Stop";
            _stopButton.Width = 92;
            _stopButton.Height = 32;
            _stopButton.Enabled = false;
            _stopButton.Click += delegate { StopTyping(); };
            buttonPanel.Controls.Add(_stopButton);

            var footer = new Panel();
            footer.Dock = DockStyle.Fill;
            footer.Padding = new Padding(16, 4, 16, 6);
            footer.BackColor = Color.White;
            root.Controls.Add(footer, 0, 2);
            root.SetColumnSpan(footer, 2);

            _statusLabel = new Label();
            _statusLabel.Dock = DockStyle.Left;
            _statusLabel.AutoSize = false;
            _statusLabel.Width = 470;
            _statusLabel.TextAlign = ContentAlignment.MiddleLeft;
            footer.Controls.Add(_statusLabel);

            _progressBar = new ProgressBar();
            _progressBar.Dock = DockStyle.Right;
            _progressBar.Width = 260;
            _progressBar.Minimum = 0;
            _progressBar.Maximum = 100;
            footer.Controls.Add(_progressBar);

            ApplyButtonDelayLabel();
            _buttonDelayBox.ValueChanged += delegate { ApplyButtonDelayLabel(); };
            _typoCheckBox.CheckedChanged += delegate { UpdateTypoControlState(); };
            UpdateTypoControlState();
            SetStatus("Ready", false);
        }

        private void TrySetApplicationIcon()
        {
            try
            {
                Icon associatedIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
                if (associatedIcon != null)
                {
                    Icon = associatedIcon;
                }
            }
            catch
            {
                // Icon loading is cosmetic; the app should still run if Windows cannot read it.
            }
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            RegisterHotkey();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            SaveSettings();
            StopTyping();
            if (_hotkeyRegistered)
            {
                NativeMethods.UnregisterHotKey(Handle, HotkeyId);
                _hotkeyRegistered = false;
            }

            base.OnFormClosing(e);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == NativeMethods.WmHotkey && m.WParam.ToInt32() == HotkeyId)
            {
                if (_typingCancellation == null)
                {
                    StartTyping(false);
                }
                else
                {
                    StopTyping();
                }

                return;
            }

            base.WndProc(ref m);
        }

        private void RegisterHotkey()
        {
            if (_hotkeyRegistered)
            {
                return;
            }

            if (NativeMethods.RegisterHotKey(Handle, HotkeyId, 0, HotkeyVk))
            {
                _hotkeyRegistered = true;
                _hotkeyLabel.Text = "F7 armed";
                SetStatus("Ready", false);
                return;
            }

            var error = new Win32Exception(Marshal.GetLastWin32Error());
            _hotkeyLabel.Text = "F7 unavailable";
            SetStatus("F7 unavailable: " + error.Message, true);
        }

        private void StartTyping(bool useButtonDelay)
        {
            if (_typingCancellation != null)
            {
                StopTyping();
                return;
            }

            var text = _textBox.Text;
            if (String.IsNullOrEmpty(text))
            {
                SetStatus("No text", true);
                return;
            }

            var options = ReadOptions();
            options.StartDelayMilliseconds = useButtonDelay ? (int)_buttonDelayBox.Value * 1000 : 0;
            options.TriggerVirtualKey = useButtonDelay ? 0 : HotkeyVk;
            options.TelemetryPath = TelemetryLogger.CreatePath();
            var telemetryPath = options.TelemetryPath;

            _typingCancellation = new CancellationTokenSource();
            var token = _typingCancellation.Token;
            SetRunningState(true);
            SetStatus(options.StartDelayMilliseconds > 0 ? "Starting" : "Typing", false);
            UpdateProgress(0, text.Length);

            Action<int, int> progress = delegate(int completed, int total)
            {
                if (IsDisposed || !IsHandleCreated)
                {
                    return;
                }

                try
                {
                    BeginInvoke((Action)delegate
                    {
                        UpdateProgress(completed, total);
                    });
                }
                catch (InvalidOperationException)
                {
                }
            };

            Task.Factory.StartNew(delegate
            {
                _typingEngine.TypeText(text, options, progress, token);
            }, token, TaskCreationOptions.LongRunning, TaskScheduler.Default).ContinueWith(delegate(Task task)
            {
                var cancellation = _typingCancellation;
                _typingCancellation = null;
                if (cancellation != null)
                {
                    cancellation.Dispose();
                }

                SetRunningState(false);

                if (task.IsCanceled || token.IsCancellationRequested)
                {
                    SetStatus("Stopped", false);
                    return;
                }

                if (task.IsFaulted)
                {
                    var ex = task.Exception == null ? null : task.Exception.GetBaseException();
                    SetStatus(ex == null ? "Typing failed" : ex.Message, true);
                    return;
                }

                UpdateProgress(text.Length, text.Length);
                SetStatus("Done. Log: " + Path.GetFileName(telemetryPath), false);
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        private void StopTyping()
        {
            if (_typingCancellation != null && !_typingCancellation.IsCancellationRequested)
            {
                _typingCancellation.Cancel();
                SetStatus("Stopping", false);
            }
        }

        private TypingOptions ReadOptions()
        {
            return new TypingOptions
            {
                WordsPerMinute = (int)_wpmBox.Value,
                JitterPercent = (int)_jitterBox.Value,
                KeyPairRhythmPercent = (int)_keyPairRhythmBox.Value,
                BurstPercent = (int)_burstBox.Value,
                WordPauseMilliseconds = (int)_wordPauseBox.Value,
                SentencePauseMilliseconds = (int)_sentencePauseBox.Value,
                ThinkingPauseChancePercent = (int)_thinkingChanceBox.Value,
                ThinkingPauseMilliseconds = (int)_thinkingPauseBox.Value,
                KeepTargetWpm = _keepTargetWpmCheckBox.Checked,
                UppercaseDelayPenaltyPercent = (int)_uppercasePenaltyBox.Value,
                NumberDelayPenaltyPercent = (int)_numberPenaltyBox.Value,
                PunctuationDelayPenaltyPercent = (int)_punctuationPenaltyBox.Value,
                ShiftSymbolDelayPenaltyPercent = (int)_shiftSymbolPenaltyBox.Value,
                NumberTypoMultiplierPercent = ReadMultiplierPercent(_numberTypoMultiplierBox),
                PunctuationTypoMultiplierPercent = ReadMultiplierPercent(_punctuationTypoMultiplierBox),
                ShiftSymbolTypoMultiplierPercent = ReadMultiplierPercent(_shiftSymbolTypoMultiplierBox),
                EnableTypos = _typoCheckBox.Checked,
                TypoChancePercent = (int)_typoChanceBox.Value,
                DuplicateChancePercent = (int)_duplicateChanceBox.Value,
                TransposeChancePercent = (int)_transposeChanceBox.Value,
                CorrectionNoticeMilliseconds = (int)_noticeDelayBox.Value,
                CorrectionDetectChars = (int)_detectCharsBox.Value,
                CorrectionPauseMilliseconds = (int)_correctionPauseBox.Value,
                CorrectionResumeMilliseconds = (int)_resumePauseBox.Value
            };
        }

        private void SaveSettings()
        {
            SettingsStore.Save(new AppSettings
            {
                WordsPerMinute = (int)_wpmBox.Value,
                JitterPercent = (int)_jitterBox.Value,
                KeyPairRhythmPercent = (int)_keyPairRhythmBox.Value,
                BurstPercent = (int)_burstBox.Value,
                WordPauseMilliseconds = (int)_wordPauseBox.Value,
                SentencePauseMilliseconds = (int)_sentencePauseBox.Value,
                ThinkingPauseChancePercent = (int)_thinkingChanceBox.Value,
                ThinkingPauseMilliseconds = (int)_thinkingPauseBox.Value,
                KeepTargetWpm = _keepTargetWpmCheckBox.Checked,
                UppercaseDelayPenaltyPercent = (int)_uppercasePenaltyBox.Value,
                NumberDelayPenaltyPercent = (int)_numberPenaltyBox.Value,
                PunctuationDelayPenaltyPercent = (int)_punctuationPenaltyBox.Value,
                ShiftSymbolDelayPenaltyPercent = (int)_shiftSymbolPenaltyBox.Value,
                NumberTypoMultiplierPercent = ReadMultiplierPercent(_numberTypoMultiplierBox),
                PunctuationTypoMultiplierPercent = ReadMultiplierPercent(_punctuationTypoMultiplierBox),
                ShiftSymbolTypoMultiplierPercent = ReadMultiplierPercent(_shiftSymbolTypoMultiplierBox),
                EnableTypos = _typoCheckBox.Checked,
                TypoChancePercent = (int)_typoChanceBox.Value,
                DuplicateChancePercent = (int)_duplicateChanceBox.Value,
                TransposeChancePercent = (int)_transposeChanceBox.Value,
                CorrectionNoticeMilliseconds = (int)_noticeDelayBox.Value,
                CorrectionDetectChars = (int)_detectCharsBox.Value,
                CorrectionPauseMilliseconds = (int)_correctionPauseBox.Value,
                CorrectionResumeMilliseconds = (int)_resumePauseBox.Value,
                ButtonDelaySeconds = (int)_buttonDelayBox.Value
            });
        }

        private void SetRunningState(bool running)
        {
            _textBox.Enabled = !running;
            _wpmBox.Enabled = !running;
            _jitterBox.Enabled = !running;
            _keyPairRhythmBox.Enabled = !running;
            _burstBox.Enabled = !running;
            _wordPauseBox.Enabled = !running;
            _sentencePauseBox.Enabled = !running;
            _thinkingChanceBox.Enabled = !running;
            _thinkingPauseBox.Enabled = !running;
            _keepTargetWpmCheckBox.Enabled = !running;
            _uppercasePenaltyBox.Enabled = !running;
            _numberPenaltyBox.Enabled = !running;
            _punctuationPenaltyBox.Enabled = !running;
            _shiftSymbolPenaltyBox.Enabled = !running;
            _numberTypoMultiplierBox.Enabled = !running && _typoCheckBox.Checked;
            _punctuationTypoMultiplierBox.Enabled = !running && _typoCheckBox.Checked;
            _shiftSymbolTypoMultiplierBox.Enabled = !running && _typoCheckBox.Checked;
            _typoCheckBox.Enabled = !running;
            _typoChanceBox.Enabled = !running && _typoCheckBox.Checked;
            _duplicateChanceBox.Enabled = !running && _typoCheckBox.Checked;
            _transposeChanceBox.Enabled = !running && _typoCheckBox.Checked;
            _noticeDelayBox.Enabled = !running && _typoCheckBox.Checked;
            _detectCharsBox.Enabled = !running && _typoCheckBox.Checked;
            _correctionPauseBox.Enabled = !running && _typoCheckBox.Checked;
            _resumePauseBox.Enabled = !running && _typoCheckBox.Checked;
            _buttonDelayBox.Enabled = !running;
            _startButton.Enabled = !running;
            _stopButton.Enabled = running;
        }

        private void SetStatus(string message, bool isError)
        {
            _statusLabel.Text = message;
            _statusLabel.ForeColor = isError ? Color.FromArgb(175, 35, 35) : Color.FromArgb(45, 55, 72);
        }

        private void UpdateProgress(int completed, int total)
        {
            if (total <= 0)
            {
                _progressBar.Value = 0;
                return;
            }

            var value = Math.Max(0, Math.Min(100, (int)Math.Round((double)completed * 100D / (double)total)));
            _progressBar.Value = value;
            if (_typingCancellation != null)
            {
                SetStatus("Typing " + completed + " / " + total, false);
            }
        }

        private void UpdateTypoControlState()
        {
            if (_typingCancellation != null)
            {
                return;
            }

            _typoChanceBox.Enabled = _typoCheckBox.Checked;
            _duplicateChanceBox.Enabled = _typoCheckBox.Checked;
            _transposeChanceBox.Enabled = _typoCheckBox.Checked;
            _numberTypoMultiplierBox.Enabled = _typoCheckBox.Checked;
            _punctuationTypoMultiplierBox.Enabled = _typoCheckBox.Checked;
            _shiftSymbolTypoMultiplierBox.Enabled = _typoCheckBox.Checked;
            _noticeDelayBox.Enabled = _typoCheckBox.Checked;
            _detectCharsBox.Enabled = _typoCheckBox.Checked;
            _correctionPauseBox.Enabled = _typoCheckBox.Checked;
            _resumePauseBox.Enabled = _typoCheckBox.Checked;
        }

        private void ApplyButtonDelayLabel()
        {
            _startButton.Text = "Type in " + (int)_buttonDelayBox.Value + "s";
        }

        private static NumericUpDown CreateNumberBox(int minimum, int maximum, int value)
        {
            var box = new NumericUpDown();
            box.Minimum = minimum;
            box.Maximum = maximum;
            box.Value = Math.Max(minimum, Math.Min(maximum, value));
            box.Dock = DockStyle.Fill;
            box.Margin = new Padding(4, 4, 0, 4);
            box.TextAlign = HorizontalAlignment.Right;
            return box;
        }

        private static NumericUpDown CreateMultiplierBox(int minimumPercent, int maximumPercent, int valuePercent)
        {
            var box = new NumericUpDown();
            box.Minimum = minimumPercent / 100M;
            box.Maximum = maximumPercent / 100M;
            box.DecimalPlaces = 2;
            box.Increment = 0.25M;
            var value = valuePercent / 100M;
            if (value < box.Minimum)
            {
                value = box.Minimum;
            }
            else if (value > box.Maximum)
            {
                value = box.Maximum;
            }

            box.Value = value;
            box.Dock = DockStyle.Fill;
            box.Margin = new Padding(4, 4, 0, 4);
            box.TextAlign = HorizontalAlignment.Right;
            return box;
        }

        private static int ReadMultiplierPercent(NumericUpDown box)
        {
            return Decimal.ToInt32(Decimal.Round(box.Value * 100M, 0));
        }

        private static void AddSectionLabel(TableLayoutPanel panel, string text)
        {
            var label = new Label();
            label.Text = text;
            label.AutoSize = true;
            label.Font = new Font("Segoe UI Semibold", 9.25F, FontStyle.Bold);
            label.Margin = new Padding(0, 12, 0, 4);
            var row = panel.RowCount++;
            panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            panel.Controls.Add(label, 0, row);
            panel.SetColumnSpan(label, 2);
        }

        private static void AddLabeledControl(TableLayoutPanel panel, string labelText, Control control)
        {
            var label = new Label();
            label.Text = labelText;
            label.Dock = DockStyle.Fill;
            label.TextAlign = ContentAlignment.MiddleLeft;
            label.Margin = new Padding(0, 4, 0, 4);
            var row = panel.RowCount++;
            panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            panel.Controls.Add(label, 0, row);
            panel.Controls.Add(control, 1, row);
        }
    }

    internal sealed class TypingOptions
    {
        public int WordsPerMinute { get; set; }
        public int JitterPercent { get; set; }
        public int KeyPairRhythmPercent { get; set; }
        public int BurstPercent { get; set; }
        public int WordPauseMilliseconds { get; set; }
        public int SentencePauseMilliseconds { get; set; }
        public int ThinkingPauseChancePercent { get; set; }
        public int ThinkingPauseMilliseconds { get; set; }
        public bool KeepTargetWpm { get; set; }
        public int UppercaseDelayPenaltyPercent { get; set; }
        public int NumberDelayPenaltyPercent { get; set; }
        public int PunctuationDelayPenaltyPercent { get; set; }
        public int ShiftSymbolDelayPenaltyPercent { get; set; }
        public int NumberTypoMultiplierPercent { get; set; }
        public int PunctuationTypoMultiplierPercent { get; set; }
        public int ShiftSymbolTypoMultiplierPercent { get; set; }
        public bool EnableTypos { get; set; }
        public int TypoChancePercent { get; set; }
        public int DuplicateChancePercent { get; set; }
        public int TransposeChancePercent { get; set; }
        public int CorrectionNoticeMilliseconds { get; set; }
        public int CorrectionDetectChars { get; set; }
        public int CorrectionPauseMilliseconds { get; set; }
        public int CorrectionResumeMilliseconds { get; set; }
        public int StartDelayMilliseconds { get; set; }
        public int TriggerVirtualKey { get; set; }
        public string TelemetryPath { get; set; }
    }

    internal sealed class TypingEngine
    {
        private readonly Dictionary<char, char[]> _nearbyKeys;
        private readonly Dictionary<char, int> _keyboardHands;

        public TypingEngine()
        {
            _nearbyKeys = new Dictionary<char, char[]>();
            _keyboardHands = new Dictionary<char, int>();
            AddNearby('a', "qwsz");
            AddNearby('b', "vghn");
            AddNearby('c', "xdfv");
            AddNearby('d', "serfcx");
            AddNearby('e', "wsdr");
            AddNearby('f', "drtgvc");
            AddNearby('g', "ftyhbv");
            AddNearby('h', "gyujnb");
            AddNearby('i', "ujko");
            AddNearby('j', "huikmn");
            AddNearby('k', "jiolm");
            AddNearby('l', "kop");
            AddNearby('m', "njk");
            AddNearby('n', "bhjm");
            AddNearby('o', "iklp");
            AddNearby('p', "ol");
            AddNearby('q', "wa");
            AddNearby('r', "edft");
            AddNearby('s', "awedxz");
            AddNearby('t', "rfgy");
            AddNearby('u', "yhji");
            AddNearby('v', "cfgb");
            AddNearby('w', "qase");
            AddNearby('x', "zsdc");
            AddNearby('y', "tghu");
            AddNearby('z', "asx");
            AddNearby('1', "2q");
            AddNearby('2', "13qw");
            AddNearby('3', "24we");
            AddNearby('4', "35er");
            AddNearby('5', "46rt");
            AddNearby('6', "57ty");
            AddNearby('7', "68yu");
            AddNearby('8', "79ui");
            AddNearby('9', "80io");
            AddNearby('0', "9op");
            AddNearby('-', "0=[]");
            AddNearby('=', "-[]");
            AddNearby('[', "p=]");
            AddNearby(']', "[\\");
            AddNearby('\\', "]");
            AddNearby(';', "lp'");
            AddNearby('\'', ";");
            AddNearby(',', "m.");
            AddNearby('.', ",/");
            AddNearby('/', ".");
            AddNearby('`', "1");

            AddHand("qwertasdfgzxcvb12345", 0);
            AddHand("yuiophjklnm67890", 1);
            AddHand("`~!@#$%", 0);
            AddHand("^&*()_-+={[}]|\\:;\"'<,>.?/", 1);
        }

        public void TypeText(string text, TypingOptions options, Action<int, int> progress, CancellationToken token)
        {
            using (var telemetry = TelemetryLogger.Start(options.TelemetryPath, text, options))
            {
                if (options.StartDelayMilliseconds > 0)
                {
                    Pause(options.StartDelayMilliseconds, token, telemetry, "start_delay", -1, "button delay");
                }

                if (options.TriggerVirtualKey > 0)
                {
                    WaitForVirtualKeyRelease(options.TriggerVirtualKey, token);
                }

                NativeMethods.ReleaseModifierKeys();

                var targetWindow = NativeMethods.GetForegroundWindow();
                var random = new Random();
                var mistakePlan = CreateMistakePlan(text, options, random);
                var rhythmPlan = CreateRhythmPlan(text, options, random);
                var baseDelay = CalculateBaseDelay(text, options, mistakePlan.Count, rhythmPlan);
                telemetry.LogInfo("calibration", "base_delay_ms=" + baseDelay +
                    ";planned_mistakes=" + mistakePlan.Count +
                    ";planned_thinking_pauses=" + rhythmPlan.ThinkingPauses.Count +
                    ";target_wpm=" + options.WordsPerMinute);

                var state = new RhythmState();
                state.ThinkingPauses = rhythmPlan.ThinkingPauses;
                var completed = 0;
                var total = text.Length;
                ActiveMistake activeMistake = null;

                for (var index = 0; index < text.Length; index++)
                {
                    token.ThrowIfCancellationRequested();

                    var current = text[index];

                    if (current == '\r' || current == '\n')
                    {
                        if (activeMistake != null)
                        {
                            CorrectActiveMistake(text, activeMistake, targetWindow, options, random, telemetry, token);
                            activeMistake = null;
                        }

                        SendEnter(targetWindow, token, telemetry, index, current, "enter", "newline");
                        if (current == '\r' && index + 1 < text.Length && text[index + 1] == '\n')
                        {
                            index++;
                            completed++;
                        }

                        completed++;
                        Report(progress, completed, total);
                        Pause(NextDelay(text, index, baseDelay, options, random, state), token, telemetry, "motor_delay", index, "after newline");
                        continue;
                    }

                    Mistake plannedMistake;
                    if (activeMistake == null && mistakePlan.TryGetValue(index, out plannedMistake))
                    {
                        if (plannedMistake.Kind == MistakeKind.Transpose && index + 1 < text.Length)
                        {
                            var next = text[index + 1];
                            SendUnicodeChar(next, targetWindow, token, telemetry, index, current, "mistake_transpose", "sent next char first");
                            Pause(NextRepairDelay(options, random), token, telemetry, "mistake_key_gap", index, "transpose second key");
                            SendUnicodeChar(current, targetWindow, token, telemetry, index + 1, next, "mistake_transpose", "sent previous char second");
                            activeMistake = new ActiveMistake(plannedMistake, index, index + 1, 2, 0);
                            index++;
                            completed += 2;
                        }
                        else if (plannedMistake.Kind == MistakeKind.Duplicate)
                        {
                            SendUnicodeChar(current, targetWindow, token, telemetry, index, current, "final_key", "before duplicate");
                            Pause(NextRepairDelay(options, random), token, telemetry, "mistake_key_gap", index, "duplicate extra key");
                            SendUnicodeChar(current, targetWindow, token, telemetry, index, current, "mistake_duplicate", "extra duplicate");
                            activeMistake = new ActiveMistake(plannedMistake, index, index, 1, 1);
                            completed++;
                        }
                        else
                        {
                            var action = plannedMistake.Kind == MistakeKind.MissedShift ? "mistake_shift" : "mistake_nearby";
                            var note = plannedMistake.Kind == MistakeKind.MissedShift ? "missed shift" : "nearby substitution";
                            SendUnicodeChar(plannedMistake.SentChar, targetWindow, token, telemetry, index, current, action, note);
                            activeMistake = new ActiveMistake(plannedMistake, index, index, 1, 0);
                            completed++;
                        }
                    }
                    else
                    {
                        SendUnicodeChar(current, targetWindow, token, telemetry, index, current, "final_key", "normal");
                        if (activeMistake != null)
                        {
                            activeMistake.CorrectThroughIndex = index;
                            activeMistake.TypedFinalCharacters++;
                        }

                        completed++;
                    }

                    Report(progress, completed, total);

                    if (activeMistake != null && ShouldCorrectNow(text, index, activeMistake))
                    {
                        CorrectActiveMistake(text, activeMistake, targetWindow, options, random, telemetry, token);
                        activeMistake = null;
                    }

                    Pause(NextDelay(text, index, baseDelay, options, random, state), token, telemetry, "motor_delay", index, "after key");
                }

                if (activeMistake != null)
                {
                    CorrectActiveMistake(text, activeMistake, targetWindow, options, random, telemetry, token);
                }

                telemetry.LogInfo("run_done", "completed=" + completed + ";total=" + total);
            }
        }

        private void AddNearby(char key, string nearby)
        {
            _nearbyKeys[key] = nearby.ToCharArray();
        }

        private void AddHand(string keys, int hand)
        {
            foreach (var key in keys)
            {
                _keyboardHands[key] = hand;
            }
        }

        private Dictionary<int, Mistake> CreateMistakePlan(string text, TypingOptions options, Random random)
        {
            var plan = new Dictionary<int, Mistake>();
            if (!options.EnableTypos || options.TypoChancePercent <= 0)
            {
                return plan;
            }

            var candidates = new List<MistakeCandidate>();
            var totalWeight = 0D;
            for (var index = 0; index < text.Length; index++)
            {
                if (IsTypableMistakeTarget(text[index]))
                {
                    var weight = MistakeWeightFor(text[index], options);
                    if (weight > 0D)
                    {
                        candidates.Add(new MistakeCandidate(index, weight));
                        totalWeight += weight;
                    }
                }
            }

            var targetMistakes = (int)Math.Round(totalWeight * (Math.Max(0, options.TypoChancePercent) / 100D));
            targetMistakes = Math.Max(options.TypoChancePercent > 0 && candidates.Count > 0 ? 1 : 0, targetMistakes);
            targetMistakes = Math.Min(targetMistakes, candidates.Count);

            var blocked = new HashSet<int>();
            while (plan.Count < targetMistakes)
            {
                var candidate = PickWeightedCandidate(candidates, blocked, random);
                if (candidate == null)
                {
                    break;
                }

                var index = candidate.Index;
                if (blocked.Contains(index))
                {
                    continue;
                }

                var mistake = CreateMistake(text, index, options, random);
                if (mistake == null)
                {
                    blocked.Add(index);
                    continue;
                }

                plan[index] = mistake;
                blocked.Add(index - 1);
                blocked.Add(index);
                blocked.Add(index + 1);
                if (mistake.Kind == MistakeKind.Transpose)
                {
                    blocked.Add(index + 2);
                }
            }

            return plan;
        }

        private static MistakeCandidate PickWeightedCandidate(IList<MistakeCandidate> candidates, HashSet<int> blocked, Random random)
        {
            var totalWeight = 0D;
            foreach (var candidate in candidates)
            {
                if (!blocked.Contains(candidate.Index))
                {
                    totalWeight += Math.Max(0D, candidate.Weight);
                }
            }

            if (totalWeight <= 0D)
            {
                return null;
            }

            var roll = random.NextDouble() * totalWeight;
            foreach (var candidate in candidates)
            {
                if (blocked.Contains(candidate.Index))
                {
                    continue;
                }

                roll -= Math.Max(0D, candidate.Weight);
                if (roll <= 0D)
                {
                    return candidate;
                }
            }

            return null;
        }

        private Mistake CreateMistake(string text, int index, TypingOptions options, Random random)
        {
            var duplicateWeight = Clamp(options.DuplicateChancePercent, 0, 100);
            var transposeWeight = Clamp(options.TransposeChancePercent, 0, 100);
            if (duplicateWeight + transposeWeight > 100)
            {
                var scale = 100D / (duplicateWeight + transposeWeight);
                duplicateWeight = (int)Math.Round(duplicateWeight * scale);
                transposeWeight = Math.Max(0, 100 - duplicateWeight);
            }

            var nearbyWeight = Math.Max(0, 100 - duplicateWeight - transposeWeight);
            var totalWeight = Math.Max(1, nearbyWeight + duplicateWeight + transposeWeight);
            var roll = random.Next(totalWeight);

            if (roll < transposeWeight && CanTranspose(text, index))
            {
                return new Mistake(MistakeKind.Transpose, '\0', NextDetectionDistance(options, random));
            }

            if (roll < transposeWeight + duplicateWeight)
            {
                return new Mistake(MistakeKind.Duplicate, text[index], NextDetectionDistance(options, random));
            }

            char typo;
            if (TryPickMissedShift(text[index], out typo))
            {
                return new Mistake(MistakeKind.MissedShift, typo, NextDetectionDistance(options, random));
            }

            if (TryPickNearby(text[index], random, out typo))
            {
                return new Mistake(MistakeKind.Nearby, typo, NextDetectionDistance(options, random));
            }

            return new Mistake(MistakeKind.Duplicate, text[index], NextDetectionDistance(options, random));
        }

        private bool TryPickNearby(char intended, Random random, out char typo)
        {
            typo = '\0';
            var lower = Char.ToLowerInvariant(intended);
            char[] choices;
            if (!_nearbyKeys.TryGetValue(lower, out choices) || choices.Length == 0)
            {
                return false;
            }

            typo = choices[random.Next(choices.Length)];
            if (Char.IsUpper(intended))
            {
                typo = Char.ToUpperInvariant(typo);
            }

            return true;
        }

        private static bool TryPickMissedShift(char intended, out char typo)
        {
            switch (intended)
            {
                case '~':
                    typo = '`';
                    return true;
                case '!':
                    typo = '1';
                    return true;
                case '@':
                    typo = '2';
                    return true;
                case '#':
                    typo = '3';
                    return true;
                case '$':
                    typo = '4';
                    return true;
                case '%':
                    typo = '5';
                    return true;
                case '^':
                    typo = '6';
                    return true;
                case '&':
                    typo = '7';
                    return true;
                case '*':
                    typo = '8';
                    return true;
                case '(':
                    typo = '9';
                    return true;
                case ')':
                    typo = '0';
                    return true;
                case '_':
                    typo = '-';
                    return true;
                case '+':
                    typo = '=';
                    return true;
                case '{':
                    typo = '[';
                    return true;
                case '}':
                    typo = ']';
                    return true;
                case '|':
                    typo = '\\';
                    return true;
                case ':':
                    typo = ';';
                    return true;
                case '"':
                    typo = '\'';
                    return true;
                case '<':
                    typo = ',';
                    return true;
                case '>':
                    typo = '.';
                    return true;
                case '?':
                    typo = '/';
                    return true;
                default:
                    typo = '\0';
                    return false;
            }
        }

        private static bool CanTranspose(string text, int index)
        {
            if (index + 1 >= text.Length)
            {
                return false;
            }

            var current = text[index];
            var next = text[index + 1];
            if (!IsTypableMistakeTarget(current) || !IsTypableMistakeTarget(next))
            {
                return false;
            }

            return Char.ToLowerInvariant(current) != Char.ToLowerInvariant(next);
        }

        private static int NextDetectionDistance(TypingOptions options, Random random)
        {
            var max = Math.Max(0, options.CorrectionDetectChars);
            if (max <= 0)
            {
                return 0;
            }

            var roll = random.NextDouble();
            if (roll < 0.45D)
            {
                return Math.Min(max, 1);
            }

            if (roll < 0.75D)
            {
                return Math.Min(max, 2);
            }

            if (roll < 0.92D)
            {
                return Math.Min(max, 3);
            }

            return max;
        }

        private static bool ShouldCorrectNow(string text, int index, ActiveMistake activeMistake)
        {
            if (activeMistake.TypedFinalCharacters - 1 >= activeMistake.Mistake.DetectAfterCharacters)
            {
                return true;
            }

            return activeMistake.TypedFinalCharacters > 1 && IsWordBoundary(text[index]);
        }

        private void CorrectActiveMistake(
            string text,
            ActiveMistake activeMistake,
            IntPtr targetWindow,
            TypingOptions options,
            Random random,
            TelemetryLogger telemetry,
            CancellationToken token)
        {
            var throughIndex = Math.Max(activeMistake.StartIndex, Math.Min(activeMistake.CorrectThroughIndex, text.Length - 1));
            var noticeDelay = NextVariableDelay(options.CorrectionNoticeMilliseconds, 0.35D, random);
            Pause(noticeDelay, token, telemetry, "notice_error", throughIndex,
                "kind=" + activeMistake.Mistake.Kind + ";start=" + activeMistake.StartIndex + ";through=" + throughIndex);

            var finalCharacters = throughIndex - activeMistake.StartIndex + 1;
            var backspaces = finalCharacters + activeMistake.ExtraCharacters;
            for (var offset = 0; offset < backspaces; offset++)
            {
                SendBackspace(targetWindow, token, telemetry, throughIndex - offset, '\b', "repair_backspace", "correction");
                Pause(NextRepairDelay(options, random), token, telemetry, "repair_delay", throughIndex, "after backspace");
            }

            for (var index = activeMistake.StartIndex; index <= throughIndex; index++)
            {
                var current = text[index];
                if (current == '\r' || current == '\n')
                {
                    SendEnter(targetWindow, token, telemetry, index, current, "repair_retype", "newline");
                }
                else
                {
                    SendUnicodeChar(current, targetWindow, token, telemetry, index, current, "repair_retype", "correction");
                }

                Pause(NextRepairDelay(options, random), token, telemetry, "repair_delay", index, "after retype");
            }

            Pause(NextVariableDelay(options.CorrectionResumeMilliseconds, 0.4D, random), token, telemetry, "resume_after_fix", throughIndex, "correction complete");
            telemetry.LogInfo("correction_done", "kind=" + activeMistake.Mistake.Kind +
                ";start=" + activeMistake.StartIndex +
                ";through=" + throughIndex +
                ";backspaces=" + backspaces);
        }

        private static RhythmPlan CreateRhythmPlan(string text, TypingOptions options, Random random)
        {
            var plan = new RhythmPlan();
            if (options.ThinkingPauseChancePercent <= 0 || options.ThinkingPauseMilliseconds <= 0)
            {
                return plan;
            }

            var candidates = new List<int>();
            for (var index = 0; index < text.Length; index++)
            {
                if (IsWordBoundary(text[index]))
                {
                    candidates.Add(index);
                }
            }

            var targetPauses = (int)Math.Round(candidates.Count * (options.ThinkingPauseChancePercent / 100D));
            if (targetPauses <= 0 && candidates.Count > 0 && options.ThinkingPauseChancePercent > 0)
            {
                targetPauses = 1;
            }

            Shuffle(candidates, random);
            foreach (var index in candidates)
            {
                if (plan.ThinkingPauses.Count >= targetPauses)
                {
                    break;
                }

                var minimum = Math.Max(1, options.ThinkingPauseMilliseconds / 3);
                var delay = random.Next(minimum, options.ThinkingPauseMilliseconds + 1);
                plan.ThinkingPauses[index] = delay;
            }

            return plan;
        }

        private static int CalculateBaseDelay(string text, TypingOptions options, int mistakeCount, RhythmPlan rhythmPlan)
        {
            var targetMilliseconds = text.Length * 60000D / (Math.Max(1, options.WordsPerMinute) * 5D);
            var naturalPauseBudget = EstimateNaturalPauseBudget(text, options, rhythmPlan);
            var correctionBudget = EstimateCorrectionBudget(options, mistakeCount);
            var availableForMotor = targetMilliseconds - naturalPauseBudget - correctionBudget;
            if (availableForMotor < text.Length * 45D)
            {
                availableForMotor = text.Length * 45D;
            }

            var motorUnits = options.KeepTargetWpm ? EstimateMotorUnits(text, options) : Math.Max(1D, text.Length);
            var baseDelay = (int)Math.Round(availableForMotor / Math.Max(1D, motorUnits)) - 5;
            return Clamp(baseDelay, 45, 220);
        }

        private static double EstimateMotorUnits(string text, TypingOptions options)
        {
            var units = 0D;
            for (var index = 0; index < text.Length; index++)
            {
                units += CharacterDelayFactor(text[index], options);
            }

            return Math.Max(1D, units);
        }

        private static double EstimateNaturalPauseBudget(string text, TypingOptions options, RhythmPlan rhythmPlan)
        {
            var budget = 0D;
            for (var index = 0; index < text.Length; index++)
            {
                var current = text[index];
                if (IsSentencePunctuation(current))
                {
                    budget += options.SentencePauseMilliseconds;
                }
                else if (Char.IsWhiteSpace(current))
                {
                    budget += options.WordPauseMilliseconds;
                }

                int thinkingDelay;
                if (rhythmPlan != null && rhythmPlan.ThinkingPauses.TryGetValue(index, out thinkingDelay))
                {
                    budget += thinkingDelay;
                }
            }

            return budget;
        }

        private static double EstimateCorrectionBudget(TypingOptions options, int mistakeCount)
        {
            if (mistakeCount <= 0 || !options.EnableTypos)
            {
                return 0D;
            }

            var averageDetectDistance = Math.Min(Math.Max(0, options.CorrectionDetectChars), 2.2D);
            var averageBackspacesAndRetypes = 2D * (averageDetectDistance + 1D) + 0.25D;
            return mistakeCount *
                (options.CorrectionNoticeMilliseconds +
                 options.CorrectionResumeMilliseconds +
                 (averageBackspacesAndRetypes * Math.Max(1, options.CorrectionPauseMilliseconds)));
        }

        private static int NextVariableDelay(int centerMilliseconds, double spread, Random random)
        {
            if (centerMilliseconds <= 0)
            {
                return 0;
            }

            var factor = 1D + ((random.NextDouble() * 2D - 1D) * spread);
            factor = Math.Max(0.2D, Math.Min(2.2D, factor));
            return Math.Max(1, (int)Math.Round(centerMilliseconds * factor));
        }

        private static int NextRepairDelay(TypingOptions options, Random random)
        {
            return NextVariableDelay(Math.Max(1, options.CorrectionPauseMilliseconds), 0.35D, random);
        }

        private static void Shuffle<T>(IList<T> values, Random random)
        {
            for (var index = values.Count - 1; index > 0; index--)
            {
                var other = random.Next(index + 1);
                var temp = values[index];
                values[index] = values[other];
                values[other] = temp;
            }
        }

        private int NextDelay(string text, int index, int baseDelay, TypingOptions options, Random random, RhythmState state)
        {
            var delay = (double)baseDelay;
            var current = index >= 0 && index < text.Length ? text[index] : '\0';
            var next = index + 1 < text.Length ? text[index + 1] : '\0';

            delay *= KeyPairFactor(current, next, options.KeyPairRhythmPercent);
            delay *= CharacterDelayFactor(current, options);

            if (state.BurstRemaining <= 0)
            {
                state.BurstRemaining = random.Next(8, 24);
                var burstRange = Math.Max(0, options.BurstPercent) / 100D;
                state.BurstFactor = 1D + ((random.NextDouble() * 2D - 1D) * burstRange);
                state.BurstFactor = Math.Max(0.25D, Math.Min(2.5D, state.BurstFactor));
            }

            state.BurstRemaining--;
            delay *= state.BurstFactor;

            if (options.JitterPercent > 0)
            {
                var sigma = Math.Min(1.0D, options.JitterPercent / 100D * 0.65D);
                var normal = NextGaussian(random);
                delay *= Math.Exp((normal * sigma) - (sigma * sigma / 2D));
            }

            if (IsSentencePunctuation(current))
            {
                delay += options.SentencePauseMilliseconds * (0.65D + random.NextDouble() * 0.7D);
            }
            else if (Char.IsWhiteSpace(current))
            {
                delay += options.WordPauseMilliseconds * (0.65D + random.NextDouble() * 0.7D);
            }

            int thinkingDelay;
            if (state.ThinkingPauses != null && state.ThinkingPauses.TryGetValue(index, out thinkingDelay))
            {
                delay += thinkingDelay;
            }

            return Math.Max(1, (int)Math.Round(delay));
        }

        private double KeyPairFactor(char current, char next, int rhythmPercent)
        {
            if (rhythmPercent <= 0 || current == '\0' || next == '\0')
            {
                return 1D;
            }

            var amount = Math.Max(0, Math.Min(100, rhythmPercent)) / 100D;
            if (Char.ToLowerInvariant(current) == Char.ToLowerInvariant(next))
            {
                return 1D + (0.45D * amount);
            }

            int currentHand;
            int nextHand;
            if (!_keyboardHands.TryGetValue(Char.ToLowerInvariant(current), out currentHand) ||
                !_keyboardHands.TryGetValue(Char.ToLowerInvariant(next), out nextHand))
            {
                return 1D;
            }

            return currentHand == nextHand ? 1D + (0.22D * amount) : 1D - (0.15D * amount);
        }

        private static bool IsTypableMistakeTarget(char value)
        {
            return !Char.IsWhiteSpace(value) &&
                   !Char.IsControl(value) &&
                   (Char.IsLetterOrDigit(value) || IsPunctuationOrSymbol(value));
        }

        private static double CharacterDelayFactor(char value, TypingOptions options)
        {
            if (value == '\0' || Char.IsControl(value))
            {
                return 1D;
            }

            if (IsShiftSymbol(value))
            {
                return 1D + (Math.Max(0, options.ShiftSymbolDelayPenaltyPercent) / 100D);
            }

            if (Char.IsUpper(value))
            {
                return 1D + (Math.Max(0, options.UppercaseDelayPenaltyPercent) / 100D);
            }

            if (Char.IsDigit(value))
            {
                return 1D + (Math.Max(0, options.NumberDelayPenaltyPercent) / 100D);
            }

            if (IsPunctuationOrSymbol(value))
            {
                return 1D + (Math.Max(0, options.PunctuationDelayPenaltyPercent) / 100D);
            }

            return 1D;
        }

        private static double MistakeWeightFor(char value, TypingOptions options)
        {
            if (!IsTypableMistakeTarget(value))
            {
                return 0D;
            }

            if (IsShiftSymbol(value))
            {
                return Math.Max(0, options.ShiftSymbolTypoMultiplierPercent) / 100D;
            }

            if (Char.IsDigit(value))
            {
                return Math.Max(0, options.NumberTypoMultiplierPercent) / 100D;
            }

            if (IsPunctuationOrSymbol(value))
            {
                return Math.Max(0, options.PunctuationTypoMultiplierPercent) / 100D;
            }

            return 1D;
        }

        private static bool IsPunctuationOrSymbol(char value)
        {
            return Char.IsPunctuation(value) || Char.IsSymbol(value);
        }

        private static bool IsShiftSymbol(char value)
        {
            switch (value)
            {
                case '~':
                case '!':
                case '@':
                case '#':
                case '$':
                case '%':
                case '^':
                case '&':
                case '*':
                case '(':
                case ')':
                case '_':
                case '+':
                case '{':
                case '}':
                case '|':
                case ':':
                case '"':
                case '<':
                case '>':
                case '?':
                    return true;
                default:
                    return false;
            }
        }

        private static int Clamp(int value, int minimum, int maximum)
        {
            return Math.Max(minimum, Math.Min(maximum, value));
        }

        private static bool IsSentencePunctuation(char value)
        {
            return value == '.' || value == '!' || value == '?';
        }

        private static bool IsWordBoundary(char value)
        {
            return Char.IsWhiteSpace(value) || value == ',' || value == ';' || value == ':' ||
                   value == '.' || value == '!' || value == '?';
        }

        private static double NextGaussian(Random random)
        {
            var u1 = 1D - random.NextDouble();
            var u2 = 1D - random.NextDouble();
            return Math.Sqrt(-2D * Math.Log(u1)) * Math.Sin(2D * Math.PI * u2);
        }

        private static void Sleep(int milliseconds, CancellationToken token)
        {
            if (milliseconds <= 0)
            {
                token.ThrowIfCancellationRequested();
                return;
            }

            if (token.WaitHandle.WaitOne(milliseconds))
            {
                token.ThrowIfCancellationRequested();
            }
        }

        private static void Pause(int milliseconds, CancellationToken token, TelemetryLogger telemetry, string eventName, int sourceIndex, string note)
        {
            if (milliseconds > 0 && telemetry != null)
            {
                telemetry.LogPause(eventName, sourceIndex, milliseconds, note);
            }

            Sleep(milliseconds, token);
        }

        private static void SendUnicodeChar(
            char value,
            IntPtr targetWindow,
            CancellationToken token,
            TelemetryLogger telemetry,
            int sourceIndex,
            char intended,
            string action,
            string note)
        {
            WaitForTargetWindow(targetWindow, token);
            NativeMethods.SendUnicodeChar(value);
            if (telemetry != null)
            {
                telemetry.LogKey(action, sourceIndex, intended, value, note);
            }
        }

        private static void SendBackspace(
            IntPtr targetWindow,
            CancellationToken token,
            TelemetryLogger telemetry,
            int sourceIndex,
            char intended,
            string action,
            string note)
        {
            WaitForTargetWindow(targetWindow, token);
            NativeMethods.SendBackspace();
            if (telemetry != null)
            {
                telemetry.LogKey(action, sourceIndex, intended, '\b', note);
            }
        }

        private static void SendEnter(
            IntPtr targetWindow,
            CancellationToken token,
            TelemetryLogger telemetry,
            int sourceIndex,
            char intended,
            string action,
            string note)
        {
            WaitForTargetWindow(targetWindow, token);
            NativeMethods.SendEnter();
            if (telemetry != null)
            {
                telemetry.LogKey(action, sourceIndex, intended, '\n', note);
            }
        }

        private static void WaitForTargetWindow(IntPtr targetWindow, CancellationToken token)
        {
            if (targetWindow == IntPtr.Zero)
            {
                return;
            }

            while (NativeMethods.GetForegroundWindow() != targetWindow)
            {
                Sleep(50, token);
            }
        }

        private static void WaitForVirtualKeyRelease(int virtualKey, CancellationToken token)
        {
            var deadline = Environment.TickCount + 1000;
            while (NativeMethods.IsKeyDown(virtualKey) && Environment.TickCount < deadline)
            {
                Sleep(10, token);
            }
        }

        private static void Report(Action<int, int> progress, int completed, int total)
        {
            if (progress != null)
            {
                progress(completed, total);
            }
        }

        private sealed class RhythmState
        {
            public int BurstRemaining { get; set; }
            public double BurstFactor { get; set; }
            public Dictionary<int, int> ThinkingPauses { get; set; }
        }

        private sealed class RhythmPlan
        {
            public RhythmPlan()
            {
                ThinkingPauses = new Dictionary<int, int>();
            }

            public Dictionary<int, int> ThinkingPauses { get; private set; }
        }

        private enum MistakeKind
        {
            Nearby,
            MissedShift,
            Duplicate,
            Transpose
        }

        private sealed class MistakeCandidate
        {
            public MistakeCandidate(int index, double weight)
            {
                Index = index;
                Weight = weight;
            }

            public int Index { get; private set; }
            public double Weight { get; private set; }
        }

        private sealed class Mistake
        {
            public Mistake(MistakeKind kind, char sentChar, int detectAfterCharacters)
            {
                Kind = kind;
                SentChar = sentChar;
                DetectAfterCharacters = detectAfterCharacters;
            }

            public MistakeKind Kind { get; private set; }
            public char SentChar { get; private set; }
            public int DetectAfterCharacters { get; private set; }
        }

        private sealed class ActiveMistake
        {
            public ActiveMistake(Mistake mistake, int startIndex, int correctThroughIndex, int typedFinalCharacters, int extraCharacters)
            {
                Mistake = mistake;
                StartIndex = startIndex;
                CorrectThroughIndex = correctThroughIndex;
                TypedFinalCharacters = typedFinalCharacters;
                ExtraCharacters = extraCharacters;
            }

            public Mistake Mistake { get; private set; }
            public int StartIndex { get; private set; }
            public int CorrectThroughIndex { get; set; }
            public int TypedFinalCharacters { get; set; }
            public int ExtraCharacters { get; private set; }
        }
    }

    internal sealed class TelemetryLogger : IDisposable
    {
        private readonly StreamWriter _writer;
        private readonly Stopwatch _stopwatch;
        private readonly object _syncRoot;
        private long _lastKeyElapsedMilliseconds;
        private int _eventId;

        private TelemetryLogger(StreamWriter writer)
        {
            _writer = writer;
            _stopwatch = Stopwatch.StartNew();
            _syncRoot = new object();
            _lastKeyElapsedMilliseconds = -1;
        }

        public static string CreatePath()
        {
            var directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "AutoWrite",
                "telemetry");
            Directory.CreateDirectory(directory);
            return Path.Combine(directory, "autowrite-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture) + ".tsv");
        }

        public static TelemetryLogger Start(string path, string text, TypingOptions options)
        {
            if (String.IsNullOrEmpty(path))
            {
                path = CreatePath();
            }

            var writer = new StreamWriter(path, false, new UTF8Encoding(false));
            var logger = new TelemetryLogger(writer);
            logger.WriteHeader();
            logger.LogInfo("run_start",
                "length=" + text.Length +
                ";wpm=" + options.WordsPerMinute +
                ";keep_target_wpm=" + options.KeepTargetWpm +
                ";mistake_rate_percent=" + options.TypoChancePercent +
                ";number_delay_penalty_percent=" + options.NumberDelayPenaltyPercent +
                ";punctuation_delay_penalty_percent=" + options.PunctuationDelayPenaltyPercent +
                ";shift_symbol_delay_penalty_percent=" + options.ShiftSymbolDelayPenaltyPercent +
                ";number_typo_multiplier_percent=" + options.NumberTypoMultiplierPercent +
                ";punctuation_typo_multiplier_percent=" + options.PunctuationTypoMultiplierPercent +
                ";shift_symbol_typo_multiplier_percent=" + options.ShiftSymbolTypoMultiplierPercent +
                ";notice_ms=" + options.CorrectionNoticeMilliseconds +
                ";detect_chars=" + options.CorrectionDetectChars +
                ";repair_ms=" + options.CorrectionPauseMilliseconds +
                ";resume_ms=" + options.CorrectionResumeMilliseconds);
            return logger;
        }

        public void LogInfo(string eventName, string note)
        {
            WriteEvent(eventName, -1, '\0', '\0', null, note);
        }

        public void LogPause(string eventName, int sourceIndex, int delayMilliseconds, string note)
        {
            WriteEvent(eventName, sourceIndex, '\0', '\0', delayMilliseconds, note);
        }

        public void LogKey(string eventName, int sourceIndex, char intended, char sent, string note)
        {
            WriteEvent(eventName, sourceIndex, intended, sent, null, note);
            _lastKeyElapsedMilliseconds = _stopwatch.ElapsedMilliseconds;
        }

        private void WriteHeader()
        {
            _writer.WriteLine("event_id\tutc_iso\telapsed_ms\tgap_ms\tevent\tsource_index\tintended_code\tintended_text\tsent_code\tsent_text\tdelay_ms\tnote");
        }

        private void WriteEvent(string eventName, int sourceIndex, char intended, char sent, int? delayMilliseconds, string note)
        {
            lock (_syncRoot)
            {
                var elapsed = _stopwatch.ElapsedMilliseconds;
                string gap = "";
                if (sent != '\0' && _lastKeyElapsedMilliseconds >= 0)
                {
                    gap = (elapsed - _lastKeyElapsedMilliseconds).ToString(CultureInfo.InvariantCulture);
                }

                _eventId++;
                _writer.Write(_eventId.ToString(CultureInfo.InvariantCulture));
                _writer.Write('\t');
                _writer.Write(DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture));
                _writer.Write('\t');
                _writer.Write(elapsed.ToString(CultureInfo.InvariantCulture));
                _writer.Write('\t');
                _writer.Write(gap);
                _writer.Write('\t');
                _writer.Write(Escape(eventName));
                _writer.Write('\t');
                _writer.Write(sourceIndex.ToString(CultureInfo.InvariantCulture));
                _writer.Write('\t');
                _writer.Write(CharCode(intended));
                _writer.Write('\t');
                _writer.Write(Escape(CharText(intended)));
                _writer.Write('\t');
                _writer.Write(CharCode(sent));
                _writer.Write('\t');
                _writer.Write(Escape(CharText(sent)));
                _writer.Write('\t');
                _writer.Write(delayMilliseconds.HasValue ? delayMilliseconds.Value.ToString(CultureInfo.InvariantCulture) : "");
                _writer.Write('\t');
                _writer.WriteLine(Escape(note));
                _writer.Flush();
            }
        }

        private static string CharCode(char value)
        {
            return value == '\0' ? "" : "U+" + ((int)value).ToString("X4", CultureInfo.InvariantCulture);
        }

        private static string CharText(char value)
        {
            if (value == '\0')
            {
                return "";
            }

            if (value == '\n')
            {
                return "<enter>";
            }

            if (value == '\r')
            {
                return "<cr>";
            }

            if (value == '\b')
            {
                return "<backspace>";
            }

            if (value == '\t')
            {
                return "<tab>";
            }

            if (value == ' ')
            {
                return "<space>";
            }

            return value.ToString();
        }

        private static string Escape(string value)
        {
            if (String.IsNullOrEmpty(value))
            {
                return "";
            }

            return value.Replace("\\", "\\\\").Replace("\t", "\\t").Replace("\r", "\\r").Replace("\n", "\\n");
        }

        public void Dispose()
        {
            _writer.Dispose();
        }
    }

    internal sealed class AppSettings
    {
        public int WordsPerMinute { get; set; }
        public int JitterPercent { get; set; }
        public int KeyPairRhythmPercent { get; set; }
        public int BurstPercent { get; set; }
        public int WordPauseMilliseconds { get; set; }
        public int SentencePauseMilliseconds { get; set; }
        public int ThinkingPauseChancePercent { get; set; }
        public int ThinkingPauseMilliseconds { get; set; }
        public bool KeepTargetWpm { get; set; }
        public int UppercaseDelayPenaltyPercent { get; set; }
        public int NumberDelayPenaltyPercent { get; set; }
        public int PunctuationDelayPenaltyPercent { get; set; }
        public int ShiftSymbolDelayPenaltyPercent { get; set; }
        public int NumberTypoMultiplierPercent { get; set; }
        public int PunctuationTypoMultiplierPercent { get; set; }
        public int ShiftSymbolTypoMultiplierPercent { get; set; }
        public bool EnableTypos { get; set; }
        public int TypoChancePercent { get; set; }
        public int DuplicateChancePercent { get; set; }
        public int TransposeChancePercent { get; set; }
        public int CorrectionNoticeMilliseconds { get; set; }
        public int CorrectionDetectChars { get; set; }
        public int CorrectionPauseMilliseconds { get; set; }
        public int CorrectionResumeMilliseconds { get; set; }
        public int ButtonDelaySeconds { get; set; }

        public static AppSettings Defaults()
        {
            return new AppSettings
            {
                WordsPerMinute = 80,
                JitterPercent = 55,
                KeyPairRhythmPercent = 80,
                BurstPercent = 30,
                WordPauseMilliseconds = 120,
                SentencePauseMilliseconds = 850,
                ThinkingPauseChancePercent = 4,
                ThinkingPauseMilliseconds = 2200,
                KeepTargetWpm = true,
                UppercaseDelayPenaltyPercent = 10,
                NumberDelayPenaltyPercent = 25,
                PunctuationDelayPenaltyPercent = 30,
                ShiftSymbolDelayPenaltyPercent = 55,
                NumberTypoMultiplierPercent = 175,
                PunctuationTypoMultiplierPercent = 200,
                ShiftSymbolTypoMultiplierPercent = 300,
                EnableTypos = true,
                TypoChancePercent = 1,
                DuplicateChancePercent = 0,
                TransposeChancePercent = 0,
                CorrectionNoticeMilliseconds = 850,
                CorrectionDetectChars = 3,
                CorrectionPauseMilliseconds = 120,
                CorrectionResumeMilliseconds = 260,
                ButtonDelaySeconds = 3
            };
        }
    }

    internal static class SettingsStore
    {
        public static AppSettings Load()
        {
            var settings = AppSettings.Defaults();
            var path = SettingsPath;
            if (!File.Exists(path))
            {
                return settings;
            }

            foreach (var rawLine in File.ReadAllLines(path))
            {
                var line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith("#"))
                {
                    continue;
                }

                var separator = line.IndexOf('=');
                if (separator <= 0)
                {
                    continue;
                }

                var key = line.Substring(0, separator).Trim();
                var value = line.Substring(separator + 1).Trim();

                if (String.Equals(key, "charactersPerSecond", StringComparison.OrdinalIgnoreCase))
                {
                    var charactersPerSecond = ReadInt(value, 0);
                    if (charactersPerSecond > 0)
                    {
                        settings.WordsPerMinute = charactersPerSecond == 15
                            ? settings.WordsPerMinute
                            : Clamp((int)Math.Round(charactersPerSecond * 60D / 5D), 5, 180);
                    }
                }
                else if (String.Equals(key, "wordsPerMinute", StringComparison.OrdinalIgnoreCase))
                {
                    settings.WordsPerMinute = ReadInt(value, settings.WordsPerMinute);
                }
                else if (String.Equals(key, "varianceMilliseconds", StringComparison.OrdinalIgnoreCase))
                {
                    settings.JitterPercent = ReadInt(value, settings.JitterPercent);
                }
                else if (String.Equals(key, "jitterPercent", StringComparison.OrdinalIgnoreCase))
                {
                    settings.JitterPercent = ReadInt(value, settings.JitterPercent);
                }
                else if (String.Equals(key, "keyPairRhythmPercent", StringComparison.OrdinalIgnoreCase))
                {
                    settings.KeyPairRhythmPercent = ReadInt(value, settings.KeyPairRhythmPercent);
                }
                else if (String.Equals(key, "burstPercent", StringComparison.OrdinalIgnoreCase))
                {
                    settings.BurstPercent = ReadInt(value, settings.BurstPercent);
                }
                else if (String.Equals(key, "wordPauseMilliseconds", StringComparison.OrdinalIgnoreCase))
                {
                    settings.WordPauseMilliseconds = ReadInt(value, settings.WordPauseMilliseconds);
                }
                else if (String.Equals(key, "sentencePauseMilliseconds", StringComparison.OrdinalIgnoreCase))
                {
                    settings.SentencePauseMilliseconds = ReadInt(value, settings.SentencePauseMilliseconds);
                }
                else if (String.Equals(key, "thinkingPauseChancePercent", StringComparison.OrdinalIgnoreCase))
                {
                    settings.ThinkingPauseChancePercent = ReadInt(value, settings.ThinkingPauseChancePercent);
                }
                else if (String.Equals(key, "thinkingPauseMilliseconds", StringComparison.OrdinalIgnoreCase))
                {
                    settings.ThinkingPauseMilliseconds = ReadInt(value, settings.ThinkingPauseMilliseconds);
                }
                else if (String.Equals(key, "keepTargetWpm", StringComparison.OrdinalIgnoreCase))
                {
                    settings.KeepTargetWpm = ReadBool(value, settings.KeepTargetWpm);
                }
                else if (String.Equals(key, "uppercaseDelayPenaltyPercent", StringComparison.OrdinalIgnoreCase))
                {
                    settings.UppercaseDelayPenaltyPercent = ReadInt(value, settings.UppercaseDelayPenaltyPercent);
                }
                else if (String.Equals(key, "numberDelayPenaltyPercent", StringComparison.OrdinalIgnoreCase))
                {
                    settings.NumberDelayPenaltyPercent = ReadInt(value, settings.NumberDelayPenaltyPercent);
                }
                else if (String.Equals(key, "punctuationDelayPenaltyPercent", StringComparison.OrdinalIgnoreCase))
                {
                    settings.PunctuationDelayPenaltyPercent = ReadInt(value, settings.PunctuationDelayPenaltyPercent);
                }
                else if (String.Equals(key, "shiftSymbolDelayPenaltyPercent", StringComparison.OrdinalIgnoreCase))
                {
                    settings.ShiftSymbolDelayPenaltyPercent = ReadInt(value, settings.ShiftSymbolDelayPenaltyPercent);
                }
                else if (String.Equals(key, "numberTypoMultiplierPercent", StringComparison.OrdinalIgnoreCase))
                {
                    settings.NumberTypoMultiplierPercent = ReadInt(value, settings.NumberTypoMultiplierPercent);
                }
                else if (String.Equals(key, "punctuationTypoMultiplierPercent", StringComparison.OrdinalIgnoreCase))
                {
                    settings.PunctuationTypoMultiplierPercent = ReadInt(value, settings.PunctuationTypoMultiplierPercent);
                }
                else if (String.Equals(key, "shiftSymbolTypoMultiplierPercent", StringComparison.OrdinalIgnoreCase))
                {
                    settings.ShiftSymbolTypoMultiplierPercent = ReadInt(value, settings.ShiftSymbolTypoMultiplierPercent);
                }
                else if (String.Equals(key, "enableTypos", StringComparison.OrdinalIgnoreCase))
                {
                    settings.EnableTypos = ReadBool(value, settings.EnableTypos);
                }
                else if (String.Equals(key, "typoChancePercent", StringComparison.OrdinalIgnoreCase))
                {
                    settings.TypoChancePercent = ReadInt(value, settings.TypoChancePercent);
                }
                else if (String.Equals(key, "duplicateChancePercent", StringComparison.OrdinalIgnoreCase))
                {
                    settings.DuplicateChancePercent = ReadInt(value, settings.DuplicateChancePercent);
                }
                else if (String.Equals(key, "transposeChancePercent", StringComparison.OrdinalIgnoreCase))
                {
                    settings.TransposeChancePercent = ReadInt(value, settings.TransposeChancePercent);
                }
                else if (String.Equals(key, "correctionNoticeMilliseconds", StringComparison.OrdinalIgnoreCase))
                {
                    settings.CorrectionNoticeMilliseconds = ReadInt(value, settings.CorrectionNoticeMilliseconds);
                }
                else if (String.Equals(key, "correctionDetectChars", StringComparison.OrdinalIgnoreCase))
                {
                    settings.CorrectionDetectChars = ReadInt(value, settings.CorrectionDetectChars);
                }
                else if (String.Equals(key, "correctionPauseMilliseconds", StringComparison.OrdinalIgnoreCase))
                {
                    settings.CorrectionPauseMilliseconds = ReadInt(value, settings.CorrectionPauseMilliseconds);
                }
                else if (String.Equals(key, "correctionResumeMilliseconds", StringComparison.OrdinalIgnoreCase))
                {
                    settings.CorrectionResumeMilliseconds = ReadInt(value, settings.CorrectionResumeMilliseconds);
                }
                else if (String.Equals(key, "buttonDelaySeconds", StringComparison.OrdinalIgnoreCase))
                {
                    settings.ButtonDelaySeconds = ReadInt(value, settings.ButtonDelaySeconds);
                }
            }

            settings.WordsPerMinute = Clamp(settings.WordsPerMinute, 5, 180);
            settings.JitterPercent = Clamp(settings.JitterPercent, 0, 100);
            settings.KeyPairRhythmPercent = Clamp(settings.KeyPairRhythmPercent, 0, 100);
            settings.BurstPercent = Clamp(settings.BurstPercent, 0, 100);
            settings.WordPauseMilliseconds = Clamp(settings.WordPauseMilliseconds, 0, 1000);
            settings.SentencePauseMilliseconds = Clamp(settings.SentencePauseMilliseconds, 0, 3000);
            settings.ThinkingPauseChancePercent = Clamp(settings.ThinkingPauseChancePercent, 0, 25);
            settings.ThinkingPauseMilliseconds = Clamp(settings.ThinkingPauseMilliseconds, 0, 5000);
            settings.UppercaseDelayPenaltyPercent = Clamp(settings.UppercaseDelayPenaltyPercent, 0, 200);
            settings.NumberDelayPenaltyPercent = Clamp(settings.NumberDelayPenaltyPercent, 0, 200);
            settings.PunctuationDelayPenaltyPercent = Clamp(settings.PunctuationDelayPenaltyPercent, 0, 200);
            settings.ShiftSymbolDelayPenaltyPercent = Clamp(settings.ShiftSymbolDelayPenaltyPercent, 0, 300);
            settings.NumberTypoMultiplierPercent = Clamp(settings.NumberTypoMultiplierPercent, 0, 500);
            settings.PunctuationTypoMultiplierPercent = Clamp(settings.PunctuationTypoMultiplierPercent, 0, 500);
            settings.ShiftSymbolTypoMultiplierPercent = Clamp(settings.ShiftSymbolTypoMultiplierPercent, 0, 500);
            settings.TypoChancePercent = Clamp(settings.TypoChancePercent, 0, 10);
            settings.DuplicateChancePercent = Clamp(settings.DuplicateChancePercent, 0, 100);
            settings.TransposeChancePercent = Clamp(settings.TransposeChancePercent, 0, 100);
            settings.CorrectionNoticeMilliseconds = Clamp(settings.CorrectionNoticeMilliseconds, 0, 3000);
            settings.CorrectionDetectChars = Clamp(settings.CorrectionDetectChars, 0, 8);
            settings.CorrectionPauseMilliseconds = Clamp(settings.CorrectionPauseMilliseconds, 0, 1000);
            settings.CorrectionResumeMilliseconds = Clamp(settings.CorrectionResumeMilliseconds, 0, 2000);
            settings.ButtonDelaySeconds = Clamp(settings.ButtonDelaySeconds, 0, 10);
            return settings;
        }

        public static void Save(AppSettings settings)
        {
            try
            {
                var directory = Path.GetDirectoryName(SettingsPath);
                if (!String.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var lines = new[]
                {
                    "wordsPerMinute=" + settings.WordsPerMinute,
                    "jitterPercent=" + settings.JitterPercent,
                    "keyPairRhythmPercent=" + settings.KeyPairRhythmPercent,
                    "burstPercent=" + settings.BurstPercent,
                    "wordPauseMilliseconds=" + settings.WordPauseMilliseconds,
                    "sentencePauseMilliseconds=" + settings.SentencePauseMilliseconds,
                    "thinkingPauseChancePercent=" + settings.ThinkingPauseChancePercent,
                    "thinkingPauseMilliseconds=" + settings.ThinkingPauseMilliseconds,
                    "keepTargetWpm=" + settings.KeepTargetWpm,
                    "uppercaseDelayPenaltyPercent=" + settings.UppercaseDelayPenaltyPercent,
                    "numberDelayPenaltyPercent=" + settings.NumberDelayPenaltyPercent,
                    "punctuationDelayPenaltyPercent=" + settings.PunctuationDelayPenaltyPercent,
                    "shiftSymbolDelayPenaltyPercent=" + settings.ShiftSymbolDelayPenaltyPercent,
                    "numberTypoMultiplierPercent=" + settings.NumberTypoMultiplierPercent,
                    "punctuationTypoMultiplierPercent=" + settings.PunctuationTypoMultiplierPercent,
                    "shiftSymbolTypoMultiplierPercent=" + settings.ShiftSymbolTypoMultiplierPercent,
                    "enableTypos=" + settings.EnableTypos,
                    "typoChancePercent=" + settings.TypoChancePercent,
                    "duplicateChancePercent=" + settings.DuplicateChancePercent,
                    "transposeChancePercent=" + settings.TransposeChancePercent,
                    "correctionNoticeMilliseconds=" + settings.CorrectionNoticeMilliseconds,
                    "correctionDetectChars=" + settings.CorrectionDetectChars,
                    "correctionPauseMilliseconds=" + settings.CorrectionPauseMilliseconds,
                    "correctionResumeMilliseconds=" + settings.CorrectionResumeMilliseconds,
                    "buttonDelaySeconds=" + settings.ButtonDelaySeconds
                };
                File.WriteAllLines(SettingsPath, lines);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        private static string SettingsPath
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "AutoWrite",
                    "settings.ini");
            }
        }

        private static int ReadInt(string value, int fallback)
        {
            int parsed;
            return Int32.TryParse(value, out parsed) ? parsed : fallback;
        }

        private static bool ReadBool(string value, bool fallback)
        {
            bool parsed;
            return Boolean.TryParse(value, out parsed) ? parsed : fallback;
        }

        private static int Clamp(int value, int minimum, int maximum)
        {
            return Math.Max(minimum, Math.Min(maximum, value));
        }
    }

    internal static class SelfTest
    {
        public static int RunSendInputProbe()
        {
            try
            {
                NativeMethods.SendProbeKey();
                return 0;
            }
            catch
            {
                return 2;
            }
        }
    }

    internal static class NativeMethods
    {
        public const int WmHotkey = 0x0312;

        private const uint InputKeyboard = 1;
        private const uint KeyeventfKeyup = 0x0002;
        private const uint KeyeventfUnicode = 0x0004;
        private const ushort VkBack = 0x08;
        private const ushort VkShift = 0x10;
        private const ushort VkControl = 0x11;
        private const ushort VkMenu = 0x12;
        private const ushort VkReturn = 0x0D;
        private const ushort VkLeftWin = 0x5B;
        private const ushort VkRightWin = 0x5C;

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, int vk);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        public static bool IsKeyDown(int virtualKey)
        {
            return (GetAsyncKeyState(virtualKey) & unchecked((short)0x8000)) != 0;
        }

        public static void SendUnicodeChar(char value)
        {
            var inputs = new INPUT[2];
            inputs[0].type = InputKeyboard;
            inputs[0].U.ki.wScan = (ushort)value;
            inputs[0].U.ki.dwFlags = KeyeventfUnicode;
            inputs[1].type = InputKeyboard;
            inputs[1].U.ki.wScan = (ushort)value;
            inputs[1].U.ki.dwFlags = KeyeventfUnicode | KeyeventfKeyup;
            Send(inputs);
        }

        public static void SendBackspace()
        {
            SendVirtualKey(VkBack);
        }

        public static void SendEnter()
        {
            SendVirtualKey(VkReturn);
        }

        public static void SendProbeKey()
        {
            SendVirtualKey(VkShift);
        }

        public static void ReleaseModifierKeys()
        {
            SendVirtualKeyUp(VkShift);
            SendVirtualKeyUp(VkControl);
            SendVirtualKeyUp(VkMenu);
            SendVirtualKeyUp(VkLeftWin);
            SendVirtualKeyUp(VkRightWin);
        }

        private static void SendVirtualKey(ushort virtualKey)
        {
            var inputs = new INPUT[2];
            inputs[0].type = InputKeyboard;
            inputs[0].U.ki.wVk = virtualKey;
            inputs[1].type = InputKeyboard;
            inputs[1].U.ki.wVk = virtualKey;
            inputs[1].U.ki.dwFlags = KeyeventfKeyup;
            Send(inputs);
        }

        private static void SendVirtualKeyUp(ushort virtualKey)
        {
            var inputs = new INPUT[1];
            inputs[0].type = InputKeyboard;
            inputs[0].U.ki.wVk = virtualKey;
            inputs[0].U.ki.dwFlags = KeyeventfKeyup;
            Send(inputs);
        }

        private static void Send(INPUT[] inputs)
        {
            var sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));
            if (sent != (uint)inputs.Length)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT
        {
            public uint type;
            public InputUnion U;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct InputUnion
        {
            [FieldOffset(0)]
            public MOUSEINPUT mi;

            [FieldOffset(0)]
            public KEYBDINPUT ki;

            [FieldOffset(0)]
            public HARDWAREINPUT hi;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct HARDWAREINPUT
        {
            public uint uMsg;
            public ushort wParamL;
            public ushort wParamH;
        }
    }
}
