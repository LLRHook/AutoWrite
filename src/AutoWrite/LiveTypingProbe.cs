using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
namespace AutoWrite
{
    internal static class LiveTypingProbe
    {
        internal const int Seed = 751;
        internal const string ExpectedText = "AutoWrite: aB [12] (3.5%)?\nOK";
        internal static AppSettings Settings()
        {
            var settings = AppSettings.Defaults();
            settings.WordsPerMinute = 180;
            settings.JitterPercent = 0;
            settings.BurstPercent = 0;
            settings.KeyPairRhythmPercent = 0;
            settings.WordPauseMilliseconds = 0;
            settings.SentencePauseMilliseconds = 0;
            settings.ParagraphPauseMilliseconds = 0;
            settings.ThinkingPauseChancePercent = 0;
            settings.EnableTypos = true;
            settings.TypoChancePercent = 10;
            settings.CorrectionDetectChars = 0;
            settings.CorrectionNoticeMilliseconds = 50;
            settings.CorrectionPauseMilliseconds = 20;
            settings.CorrectionResumeMilliseconds = 20;
            settings.UppercaseDelayPenaltyPercent = 0;
            settings.NumberDelayPenaltyPercent = 0;
            settings.PunctuationDelayPenaltyPercent = 0;
            settings.ShiftSymbolDelayPenaltyPercent = 0;
            return settings;
        }
        [StructLayout(LayoutKind.Sequential)]
        private struct LastInput
        {
            internal uint Size, Time;
        }
        [DllImport("user32.dll")]
        private static extern bool GetLastInputInfo(ref LastInput info);
        internal static double IdleSeconds(uint now, uint last)
        {
            uint elapsed = unchecked(now - last);
            return elapsed > Int32.MaxValue ? 0 : elapsed / 1000D;
        }

        internal static string DifferenceSummary(string expected, string received)
        {
            int i = 0;
            while (i < expected.Length && i < received.Length && expected[i] == received[i])
            {
                i++;
            }
            int difference = i == expected.Length && i == received.Length ? -1 : i;
            return "expected_length=" + expected.Length + "; received_length=" + received.Length + "; first_difference=" + difference;
        }

        private static void Outcome(string value)
        {
            Directory.CreateDirectory(DataDirectory.PathRoot);
            File.WriteAllText(Path.Combine(DataDirectory.PathRoot, "live-probe.txt"), value);
        }
        // This explicit diagnostic is the ONLY automated check that activates a window.
        // Every input call is guarded; it never waits for or reclaims lost foreground.
        private sealed class GuardedRuntime : IEngineRuntime
        {
            private readonly WindowsEngineRuntime _windows = new WindowsEngineRuntime();
            private readonly IntPtr _window;
            private readonly Stopwatch _watch;
            internal int Backspaces;
            internal GuardedRuntime(IntPtr window, Stopwatch watch)
            {
                _window = window;
                _watch = watch;
            }
            private void Guard()
            {
                if (_watch.ElapsedMilliseconds > 11000 || NativeMethods.GetForegroundWindow() != _window)
                {
                    throw new OperationCanceledException("Probe window lost foreground or timed out.");
                }
            }
            public Random CreateRandom()
            {
                return new Random(Seed);
            }
            public IntPtr ForegroundWindow()
            {
                Guard();
                return _window;
            }
            public bool IsKeyDown(int key)
            {
                Guard();
                return _windows.IsKeyDown(key);
            }
            public void ReleaseModifiers()
            {
                Guard();
                _windows.ReleaseModifiers();
            }
            public void SendCharacter(char value)
            {
                Guard();
                _windows.SendCharacter(value);
                if (value == '\b')
                {
                    Backspaces++;
                }
            }
            public void Sleep(int milliseconds, CancellationToken token)
            {
                Guard();
                _windows.Sleep(milliseconds, token);
                Guard();
            }
        }

        internal static int Run(int requiredIdleSeconds)
        {
            const string expected = ExpectedText;
            string idle = "idle_seconds=not_requested";
            if (requiredIdleSeconds > 0)
            {
                var info = new LastInput
                {
                    Size = (uint)Marshal.SizeOf(typeof(LastInput))
                };
                if (!GetLastInputInfo(ref info))
                {
                    Outcome("exit=2; idle_seconds=unknown; GetLastInputInfo failed; no window or input created");
                    return 2;
                }
                double seconds = IdleSeconds(unchecked((uint)Environment.TickCount), info.Time);
                idle = "idle_seconds=" + seconds.ToString("F3", CultureInfo.InvariantCulture) + "; required_idle_seconds=" +
                requiredIdleSeconds;
                if (seconds < requiredIdleSeconds)
                {
                    Outcome("exit=3; skipped: user active; " + idle + "; no window or input created");
                    return 3;
                }
            }
            int result = 1;
            string reason = "Text did not match";
            long elapsed = 0;
            string difference = DifferenceSummary(expected, "");
            GuardedRuntime runtime = null;
            using (var form = new Form
            {
                Text = "AutoWrite owned SendInput smoke check", Size = new Size(540, 200), StartPosition = FormStartPosition.CenterScreen
            })
            {
                using (var input = new TextBox
                {
                    Multiline = true, Dock = DockStyle.Fill
                })
                {
                    using (var cancel = new CancellationTokenSource())
                    {
                        using (var timer = new System.Windows.Forms.Timer
                        {
                            Interval = 20
                        })
                        {
                            form.Controls.Add(input);
                            var watch = new Stopwatch();
                            Task worker = null;
                            form.Shown += delegate
                            {
                                watch.Start();
                                form.Activate();
                                input.Focus();
                                timer.Start();
                            };
                            timer.Tick += delegate
                            {
                                if (worker == null)
                                {
                                    if (watch.ElapsedMilliseconds > 500)
                                    {
                                        result = 2;
                                        reason = "Own window did not acquire foreground; no input sent";
                                        form.Close();
                                        return;
                                    }
                                    if (NativeMethods.GetForegroundWindow() != form.Handle)
                                    {
                                        return;
                                    }
                                    input.Focus();
                                    var settings = Settings();
                                    var options = CalibrationReplay.Options(settings);
                                    runtime = new GuardedRuntime(form.Handle, watch);
                                    worker = Task.Factory.StartNew(delegate
                                    {
                                        new TypingEngine(runtime).TypeText(expected, options, null, cancel.Token);
                                    });
                                    return;
                                }
                                if (watch.ElapsedMilliseconds > 11500 || NativeMethods.GetForegroundWindow() != form.Handle)
                                {
                                    result = 2;
                                    reason = "Own window lost foreground or exceeded timeout; input stopped";
                                    cancel.Cancel();
                                    if (worker.IsCompleted)
                                    {
                                        form.Close();
                                    }
                                    return;
                                }
                                if (worker != null && worker.IsCompleted)
                                {
                                    result = !worker.IsFaulted && !worker.IsCanceled && input.Text.Replace("\r\n", "\n") == expected ? 0 :
                                    1;
                                    reason = (result == 0 ? "Exact expected text received through real SendInput; " :
                                    "Worker failed or text did not match; ") + DifferenceSummary(expected, input.Text.Replace("\r\n", "\n"
                                    ));
                                    form.Close();
                                }
                            };
                            form.FormClosing += delegate
                            {
                                cancel.Cancel();
                            };
                            Application.Run(form);
                            difference = DifferenceSummary(expected, input.Text.Replace("\r\n", "\n"));
                            timer.Stop();
                            elapsed = watch.ElapsedMilliseconds;
                            if (worker != null)
                            {
                                try
                                {
                                    if (!worker.Wait(1000))
                                    {
                                        result = 2;
                                    }
                                }
                                catch (AggregateException)
                                {
                                    result = 2;
                                }
                            }
                        }
                    }
                }
            }
            Outcome("exit=" + result + "; elapsed_ms=" + elapsed + "; " + idle + "; " + reason + "; backspaces_sent=" + (runtime == null ?
            0 : runtime.Backspaces) + "; " + difference);
            return result;
        }
    }
}
