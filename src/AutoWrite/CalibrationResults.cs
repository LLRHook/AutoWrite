using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
namespace AutoWrite
{
    internal static class SettingNames
    {
        internal static readonly Dictionary<string, string> Labels = new Dictionary<string, string>
        {
            {
                "WordsPerMinute", "Target WPM"
            },
            {
                "ButtonDelaySeconds", "Button delay s"
            },
            {
                "JitterPercent", "Jitter %"
            },
            {
                "KeyPairRhythmPercent", "Key-pair rhythm %"
            },
            {
                "BurstPercent", "Burst drift %"
            },
            {
                "WordPauseMilliseconds", "Word pause ms"
            },
            {
                "SentencePauseMilliseconds", "Sentence pause ms"
            },
            {
                "ParagraphPauseMilliseconds", "Paragraph pause ms"
            },
            {
                "ThinkingPauseChancePercent", "Thinking pause %"
            },
            {
                "ThinkingPauseMilliseconds", "Thinking max ms"
            },
            {
                "KeepTargetWpm", "Keep target WPM"
            },
            {
                "UppercaseDelayPenaltyPercent", "Uppercase delay %"
            },
            {
                "NumberDelayPenaltyPercent", "Number delay %"
            },
            {
                "PunctuationDelayPenaltyPercent", "Punctuation delay %"
            },
            {
                "ShiftSymbolDelayPenaltyPercent", "Shift-symbol delay %"
            },
            {
                "NumberTypoMultiplierPercent", "Number mistakes x"
            },
            {
                "PunctuationTypoMultiplierPercent", "Punct. mistakes x"
            },
            {
                "ShiftSymbolTypoMultiplierPercent", "Shift mistakes x"
            },
            {
                "EnableTypos", "Corrected mistakes"
            },
            {
                "TypoChancePercent", "Mistake rate %"
            },
            {
                "DuplicateChancePercent", "Duplicate share %"
            },
            {
                "TransposeChancePercent", "Transpose share %"
            },
            {
                "CorrectionNoticeMilliseconds", "Notice delay ms"
            },
            {
                "CorrectionDetectChars", "Detect after chars"
            },
            {
                "CorrectionPauseMilliseconds", "Repair key pause ms"
            },
            {
                "CorrectionResumeMilliseconds", "Resume pause ms"
            }
        };
        internal static string Format(string name, object value)
        {
            if (value is bool)
            {
                return (bool)value ? "On" : "Off";
            }
            if (name.Contains("Multiplier"))
            {
                return ((int)value / 100D).ToString("F2", CultureInfo.InvariantCulture) + "x";
            }
            return Convert.ToString(value, CultureInfo.InvariantCulture);
        }
    }
    internal sealed class CalibrationResultsDialog : QuietForm
    {
        internal CalibrationResultsDialog(CalibrationResult result, Func<AppSettings, bool> apply)
        {
            Text = "Calibration results";
            Name = "CalibrationResults";
            Size = new Size(1040, 780);
            MinimumSize = new Size(860, 580);
            StartPosition = FormStartPosition.CenterParent;
            Font = new Font("Segoe UI", 9F);
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(14)
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, String.IsNullOrEmpty(result.Warning) ? 0 : 58));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
            Controls.Add(root);
            var summary = new Label
            {
                Dock = DockStyle.Fill, Name = "CalibrationSummary", AccessibleName = "Calibration summary", Text = "Measured speed: " +
                result.MeasuredWpm.ToString("F1") + " WPM     Accuracy: " + result.Accuracy.ToString("F1") + "%     Corrected episodes: "
                + result.CorrectedMistakes
            };
            root.Controls.Add(summary);
            var warning = new Label
            {
                Dock = DockStyle.Fill, Text = result.Warning, ForeColor = Color.DarkRed, Name = "CalibrationWarning", AccessibleName =
                "Calibration warning"
            };
            root.Controls.Add(warning);
            var scroll = new Panel
            {
                Dock = DockStyle.Fill, AutoScroll = true
            };
            root.Controls.Add(scroll);
            var table = new TableLayoutPanel
            {
                Dock = DockStyle.Top, AutoSize = true, ColumnCount = 4, RowCount = 1, Padding = new Padding(0, 0, 18, 0)
            };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 88));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 88));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            scroll.Controls.Add(table);
            foreach (var heading in new[]
            {
                "Setting", "Current", "Proposed", "Evidence / reason"
            })
            {
                table.Controls.Add(new Label
                {
                    Text = heading, AutoSize = true, Font = new Font(Font, FontStyle.Bold), Margin = new Padding(3, 4, 3, 10)
                });
            }
            foreach (var estimate in result.Estimates)
            {
                int row = table.RowCount++;
                table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                string label = SettingNames.Labels[estimate.Setting];
                table.Controls.Add(new Label
                {
                    Text = label, Dock = DockStyle.Fill, AutoSize = true, Margin = new Padding(3, 8, 3, 8)
                }, 0, row);
                table.Controls.Add(ValueBox("Current " + label, SettingNames.Format(estimate.Setting, estimate.Current)), 1, row);
                table.Controls.Add(ValueBox("Proposed " + label, SettingNames.Format(estimate.Setting, estimate.Proposed)), 2, row);
                string evidence = estimate.Measured && !estimate.ClosestAchievable ? "Measured: " + estimate.Reason : estimate.Reason;
                table.Controls.Add(new Label
                {
                    Text = evidence, AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(8, 8, 3, 8), MaximumSize = new Size(550,
                    0)
                }, 3, row);
            }
            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(0, 12, 0, 0)
            };
            root.Controls.Add(buttons);
            var discard = new Button
            {
                Text = "Discard", AccessibleName = "Discard calibration", Width = 100
            };
            discard.Click += delegate
            {
                Close();
            };
            buttons.Controls.Add(discard);
            CancelButton = discard;
            var accept = new Button
            {
                Text = "Apply", AccessibleName = "Apply calibration", Width = 100
            };
            accept.Click += delegate
            {
                if (apply(result.Proposed))
                {
                    DialogResult = DialogResult.OK;
                    Close();
                }
                else
                {
                    warning.Text = ("The settings could not be saved. Controls were restored; check the dat" +
                    "a directory's write permissions.");
                    root.RowStyles[1].Height = 58;
                }
            };
            buttons.Controls.Add(accept);
        }

        private static TextBox ValueBox(string name, string value)
        {
            return new TextBox
            {
                Text = value, ReadOnly = true, AccessibleName = name, Name = name.Replace(" ", ""), Dock = DockStyle.Top, Margin = new
                Padding(3, 6, 3, 6)
            };
        }
    }
    internal sealed partial class MainForm
    {
        private bool _calibrationOpen;
        private readonly Dictionary<string, Control> _calibrationControls = new Dictionary<string, Control>();
        private void InitializeCalibrationControls()
        {
            Map("WordsPerMinute", _wpmBox);
            Map("ButtonDelaySeconds", _buttonDelayBox);
            Map("JitterPercent", _jitterBox);
            Map("KeyPairRhythmPercent", _keyPairRhythmBox);
            Map("BurstPercent", _burstBox);
            Map("WordPauseMilliseconds", _wordPauseBox);
            Map("SentencePauseMilliseconds", _sentencePauseBox);
            Map("ParagraphPauseMilliseconds", _paragraphPauseBox);
            Map("ThinkingPauseChancePercent", _thinkingChanceBox);
            Map("ThinkingPauseMilliseconds", _thinkingPauseBox);
            Map("KeepTargetWpm", _keepTargetWpmCheckBox);
            Map("UppercaseDelayPenaltyPercent", _uppercasePenaltyBox);
            Map("NumberDelayPenaltyPercent", _numberPenaltyBox);
            Map("PunctuationDelayPenaltyPercent", _punctuationPenaltyBox);
            Map("ShiftSymbolDelayPenaltyPercent", _shiftSymbolPenaltyBox);
            Map("NumberTypoMultiplierPercent", _numberTypoMultiplierBox);
            Map("PunctuationTypoMultiplierPercent", _punctuationTypoMultiplierBox);
            Map("ShiftSymbolTypoMultiplierPercent", _shiftSymbolTypoMultiplierBox);
            Map("EnableTypos", _typoCheckBox);
            Map("TypoChancePercent", _typoChanceBox);
            Map("DuplicateChancePercent", _duplicateChanceBox);
            Map("TransposeChancePercent", _transposeChanceBox);
            Map("CorrectionNoticeMilliseconds", _noticeDelayBox);
            Map("CorrectionDetectChars", _detectCharsBox);
            Map("CorrectionPauseMilliseconds", _correctionPauseBox);
            Map("CorrectionResumeMilliseconds", _resumePauseBox);
        }

        private void Map(string name, Control control)
        {
            _calibrationControls.Add(name, control);
            control.Name = name;
            control.AccessibleName = SettingNames.Labels[name];
        }

        internal AppSettings CurrentSettings()
        {
            var settings = new AppSettings();
            foreach (var pair in _calibrationControls)
            {
                object value = pair.Value is CheckBox ? (object)((CheckBox)pair.Value).Checked : (object)(int)Math.Round(((NumericUpDown)
                pair.Value).Value * (pair.Key.Contains("Multiplier") ? 100 : 1));
                typeof(AppSettings).GetProperty(pair.Key).SetValue(settings, value, null);
            }
            return settings;
        }

        private void SetCalibrationControls(AppSettings settings)
        {
            foreach (var pair in _calibrationControls)
            {
                object value = typeof(AppSettings).GetProperty(pair.Key).GetValue(settings, null);
                if (pair.Value is CheckBox)
                {
                    ((CheckBox)pair.Value).Checked = (bool)value;
                }
                else ((NumericUpDown)pair.Value).Value = (int)value / (pair.Key.Contains("Multiplier") ? 100M : 1M);
            }
        }

        internal bool ApplyCalibration(AppSettings settings)
        {
            var previous = CurrentSettings();
            SetCalibrationControls(settings);
            if (SettingsStore.Save(CurrentSettings()))
            {
                return true;
            }
            SetCalibrationControls(previous);
            return false;
        }

        private void ReleaseCalibration()
        {
            _calibrationOpen = false;
            _calibrateButton.Enabled = _typingCancellation == null;
            _startButton.Enabled = _typingCancellation == null;
        }

        private void OpenCalibration()
        {
            if (_typingCancellation != null || _calibrationOpen)
            {
                return;
            }
            _calibrationOpen = true;
            _startButton.Enabled = false;
            var current = CurrentSettings();
            var dialog = new CalibrationDialog();
            _calibrateButton.Enabled = false;
            dialog.FormClosed += delegate
            {
                if (!IsDisposed && !Disposing)
                {
                    if (dialog.CompletedSession == null)
                    {
                        ReleaseCalibration();
                    }
                    if (dialog.CompletedSession != null)
                    {
                        var result = CalibrationAnalysis.Analyze(dialog.CompletedSession.Keys, dialog.CompletedSession.Seconds, current);
                        var results = new CalibrationResultsDialog(result, ApplyCalibration);
                        results.FormClosed += delegate
                        {
                            ReleaseCalibration();
                            results.Dispose();
                        };
                        if (QuietForm.Diagnostic)
                        {
                            results.Show(this);
                        }
                        else results.ShowDialog(this);
                    }
                }
                dialog.Dispose();
            };
            if (QuietForm.Diagnostic)
            {
                dialog.Show(this);
            }
            else dialog.ShowDialog(this);
        }
    }
}
