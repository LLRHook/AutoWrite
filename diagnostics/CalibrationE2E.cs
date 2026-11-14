using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Automation;
using Accessibility;
internal static class CalibrationE2E
{
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern bool PostMessage(IntPtr h, int m, IntPtr w, IntPtr l);
    [DllImport("user32.dll")] private static extern bool IsWindowEnabled(IntPtr h);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern short VkKeyScan(char c);
    [DllImport("user32.dll", CharSet = CharSet.Auto)] private static extern IntPtr SendMessage(IntPtr h, int m, IntPtr w, IntPtr l);
    [DllImport("user32.dll", CharSet = CharSet.Auto)] private static extern IntPtr SendMessage(IntPtr h, int m, IntPtr w, StringBuilder l)
    ;
    [DllImport("user32.dll")] private static extern bool PrintWindow(IntPtr h, IntPtr dc, uint flags);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr h, out Rect rect);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowLong(IntPtr window, int index);
    [DllImport("user32.dll")] private static extern IntPtr GetParent(IntPtr window);
    [DllImport("user32.dll")] private static extern bool EnumChildWindows(IntPtr parent, EnumWindow callback, IntPtr parameter);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr h, StringBuilder text, int length);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindow callback, IntPtr parameter);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr h, StringBuilder text, int length)
    ;
    [DllImport("oleacc.dll")] private static extern int AccessibleObjectFromWindow(IntPtr window, uint objectId, ref Guid iid, [MarshalAs(
    UnmanagedType.Interface)] out object accessible);
    private delegate bool EnumWindow(IntPtr h, IntPtr parameter);
    private struct Rect
    {
        public int Left, Top, Right, Bottom;
    }
    private static Process _process;
    private static IntPtr _foreground;
    private static string _output;
    private static int _assertions;
    private static readonly Dictionary<string, string> Names = new Dictionary<string, string>
    {
        {
            "wordsPerMinute", "Target WPM"
        },
        {
            "jitterPercent", "Jitter %"
        },
        {
            "keyPairRhythmPercent", "Key-pair rhythm %"
        },
        {
            "burstPercent", "Burst drift %"
        },
        {
            "wordPauseMilliseconds", "Word pause ms"
        },
        {
            "sentencePauseMilliseconds", "Sentence pause ms"
        },
        {
            "paragraphPauseMilliseconds", "Paragraph pause ms"
        },
        {
            "thinkingPauseChancePercent", "Thinking pause %"
        },
        {
            "thinkingPauseMilliseconds", "Thinking max ms"
        },
        {
            "keepTargetWpm", "Keep target WPM"
        },
        {
            "uppercaseDelayPenaltyPercent", "Uppercase delay %"
        },
        {
            "numberDelayPenaltyPercent", "Number delay %"
        },
        {
            "punctuationDelayPenaltyPercent", "Punctuation delay %"
        },
        {
            "shiftSymbolDelayPenaltyPercent", "Shift-symbol delay %"
        },
        {
            "numberTypoMultiplierPercent", "Number mistakes x"
        },
        {
            "punctuationTypoMultiplierPercent", "Punct. mistakes x"
        },
        {
            "shiftSymbolTypoMultiplierPercent", "Shift mistakes x"
        },
        {
            "enableTypos", "Corrected mistakes"
        },
        {
            "typoChancePercent", "Mistake rate %"
        },
        {
            "duplicateChancePercent", "Duplicate share %"
        },
        {
            "transposeChancePercent", "Transpose share %"
        },
        {
            "correctionNoticeMilliseconds", "Notice delay ms"
        },
        {
            "correctionDetectChars", "Detect after chars"
        },
        {
            "correctionPauseMilliseconds", "Repair key pause ms"
        },
        {
            "correctionResumeMilliseconds", "Resume pause ms"
        },
        {
            "buttonDelaySeconds", "Button delay s"
        }
    };
    private static void Assert(bool value, string message)
    {
        if (!value)
        {
            throw new Exception(message);
        }
        _assertions++;
        Console.WriteLine("PASS " + message);
    }
    private static void ForegroundGuard()
    {
        if (GetForegroundWindow() != _foreground)
        {
            throw new Exception("Foreground changed; stopped non-activating automation.");
        }
    }
    private static AutomationElement Find(AutomationElement root, string name)
    {
        ForegroundGuard();
        var parent = (IntPtr)root.Current.NativeWindowHandle;
        IntPtr named = IntPtr.Zero;
        EnumChildWindows(parent, delegate(IntPtr h, IntPtr unused)
        {
            uint pid;
            GetWindowThreadProcessId(h, out pid);
            if (pid != _process.Id)
            {
                return true;
            }
            var type = new StringBuilder(256);
            GetClassName(h, type, type.Capacity);
            if (type.ToString().IndexOf("STATIC", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
            object accessible;
            var iid = new Guid("618736e0-3c3d-11cf-810c-00aa00389b71");
            if (AccessibleObjectFromWindow(h, 0xFFFFFFFC, ref iid, out accessible) == 0)
            {
                var a = accessible as IAccessible;
                if (a != null && a.get_accName(0) == name)
                {
                    named = h;
                    return false;
                }
            }
            return true;
        }, IntPtr.Zero);
        if (named != IntPtr.Zero)
        {
            return AutomationElement.FromHandle(named);
        }
        var elements = root.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.NameProperty, name));
        if (elements.Count == 0 && (name.StartsWith("Proposed ", StringComparison.Ordinal) || name.StartsWith("Current ", StringComparison
        .Ordinal)))
        {
            EnumChildWindows(parent, delegate(IntPtr h, IntPtr unused)
            {
                if ((GetWindowLong(h, -16) & 0x00200000) != 0)
                {
                    SendMessage(h, 0x115, (IntPtr)7, IntPtr.Zero);
                    SendMessage(h, 0x20A, (IntPtr)(-1200 << 16), IntPtr.Zero);
                }
                return true;
            }, IntPtr.Zero);
            ForegroundGuard();
            elements = root.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.NameProperty, name));
        }
        if (elements.Count == 0)
        {
            var dump = new StringBuilder("Missing " + name + "\r\n");
            EnumChildWindows(parent, delegate(IntPtr h, IntPtr unused)
            {
                var type = new StringBuilder(256);
                GetClassName(h, type, type.Capacity);
                var text = new StringBuilder(256);
                GetWindowText(h, text, text.Capacity);
                object accessible;
                var iid = new Guid("618736e0-3c3d-11cf-810c-00aa00389b71");
                string accessibleName = "";
                if (AccessibleObjectFromWindow(h, 0xFFFFFFFC, ref iid, out accessible) == 0 && accessible is IAccessible)
                {
                    accessibleName = ((IAccessible)accessible).get_accName(0);
                }
                dump.AppendLine(h + " " + type + " name=" + accessibleName + " text=" + text);
                return true;
            }, IntPtr.Zero);
            File.WriteAllText(Path.Combine(_output, "missing-control.txt"), dump.ToString());
            throw new Exception("UI Automation element missing: " + name);
        }
        foreach (AutomationElement e in elements)
        {
            if (e.Current.ControlType != ControlType.Text)
            {
                return e;
            }
        }
        return elements[0];
    }
    private static IntPtr Window(string title)
    {
        IntPtr found = IntPtr.Zero;
        EnumWindows(delegate(IntPtr h, IntPtr unused)
        {
            uint pid;
            GetWindowThreadProcessId(h, out pid);
            if (pid == _process.Id)
            {
                var text = new StringBuilder(512);
                GetWindowText(h, text, text.Capacity);
                if (text.ToString() == title)
                {
                    found = h;
                    return false;
                }
            }
            return true;
        }, IntPtr.Zero);
        if (found == IntPtr.Zero)
        {
            throw new Exception("Owned window missing: " + title);
        }
        return found;
    }
    private static IntPtr WaitForOwnedWindow(string title)
    {
        using (var ready = new ManualResetEvent(false))
        {
            AutomationEventHandler opened = delegate(object sender, AutomationEventArgs e)
            {
                try
                {
                    var window = sender as AutomationElement;
                    if (window != null && window.Current.ProcessId == _process.Id && window.Current.Name == title)
                    {
                        ready.Set();
                    }
                }
                catch (ElementNotAvailableException)
                {
                }
            };
            Automation.AddAutomationEventHandler(WindowPattern.WindowOpenedEvent, AutomationElement.RootElement, TreeScope.Children,
            opened);
            try
            {
                try
                {
                    return Window(title);
                }
                catch (Exception)
                {
                }
                if (!ready.WaitOne(10000))
                {
                    // Non-activating modal forms do not consistently raise UIA's
                    // WindowOpened event. Re-enumerate our process at the deadline.
                    try
                    {
                        return Window(title);
                    }
                    catch (Exception)
                    {
                        throw new Exception("Timed out waiting for owned window: " + title);
                    }
                }
                return Window(title);
            }
            finally
            {
                Automation.RemoveAutomationEventHandler(WindowPattern.WindowOpenedEvent, AutomationElement.RootElement, opened);
            }
        }
    }
    private static string Read(AutomationElement element)
    {
        if (element.Current.ControlType == ControlType.Spinner)
        {
            IntPtr native = (IntPtr)element.Current.NativeWindowHandle;
            foreach (var parent in new[]
            {
                native, GetParent(native)
            })
            {
                uint pid;
                if (parent == IntPtr.Zero || GetWindowThreadProcessId(parent, out pid) == 0 || pid != _process.Id)
                {
                    continue;
                }
                var edits = new List<IntPtr>();
                EnumChildWindows(parent, delegate(IntPtr h, IntPtr unused)
                {
                    var name = new StringBuilder(256);
                    GetClassName(h, name, name.Capacity);
                    if (name.ToString().IndexOf("EDIT", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        edits.Add(h);
                    }
                    return true;
                }, IntPtr.Zero);
                if (edits.Count == 1)
                {
                    var editText = new StringBuilder(128);
                    SendMessage(edits[0], 0xD, (IntPtr)editText.Capacity, editText);
                    return editText.ToString();
                }
            }
            // The .NET 4 MSAA spinner proxy exposes the arrow buttons as a zero-valued range.
            // Read its real sibling edit window, located using the named UIA element's row.
            var bounds = element.Current.BoundingRectangle;
            IntPtr editHandle = IntPtr.Zero;
            EnumChildWindows(Window("AutoWrite"), delegate(IntPtr h, IntPtr unused)
            {
                var type = new StringBuilder(256);
                GetClassName(h, type, type.Capacity);
                Rect r;
                GetWindowRect(h, out r);
                if (type.ToString().IndexOf("EDIT", StringComparison.OrdinalIgnoreCase) >= 0 && Math.Abs((r.Top + r.Bottom) / 2D - (bounds
                .Top + bounds.Bottom) / 2) < 6 && r.Left >= bounds.Left - 220 && r.Right <= bounds.Right + 3)
                {
                    editHandle = h;
                }
                return true;
            }, IntPtr.Zero);
            if (editHandle != IntPtr.Zero)
            {
                var value = new StringBuilder(128);
                SendMessage(editHandle, 0xD, (IntPtr)value.Capacity, value);
                return value.ToString();
            }
            throw new Exception("No owned edit window for numeric setting: " + element.Current.Name);
        }
        object pattern;
        if (element.TryGetCurrentPattern(ValuePattern.Pattern, out pattern))
        {
            return ((ValuePattern)pattern).Current.Value;
        }
        if (element.TryGetCurrentPattern(TogglePattern.Pattern, out pattern))
        {
            return ((TogglePattern)pattern).Current.ToggleState == ToggleState.On ? "On" : "Off";
        }
        object accessible;
        var iid = new Guid("618736e0-3c3d-11cf-810c-00aa00389b71");
        if (AccessibleObjectFromWindow((IntPtr)element.Current.NativeWindowHandle, 0xFFFFFFFC, ref iid, out accessible) == 0)
        {
            var a = accessible as IAccessible;
            if (a != null)
            {
                if (element.Current.ControlType == ControlType.CheckBox)
                {
                    return ((int)a.get_accState(0) & 0x10) != 0 ? "On" : "Off";
                }
                string value = a.get_accValue(0);
                if (!String.IsNullOrEmpty(value))
                {
                    return value;
                }
            }
        }
        if (element.TryGetCurrentPattern(RangeValuePattern.Pattern, out pattern))
        {
            return ((RangeValuePattern)pattern).Current.Value. ToString(CultureInfo.InvariantCulture);
        }
        var text = new StringBuilder(12000);
        SendMessage((IntPtr)element.Current.NativeWindowHandle, 0xD, (IntPtr)text.Capacity, text);
        return text.ToString();
    }
    private static string MainValue(AutomationElement main, string key, string label)
    {
        return Read(Find(main, label));
    }
    private static void Click(AutomationElement root, string name)
    {
        ForegroundGuard();
        var button = Find(root, name);
        var handle = (IntPtr)button.Current.NativeWindowHandle;
        if (handle == IntPtr.Zero)
        {
            throw new Exception("No button window for " + name);
        }
        if (name == "Calibrate typing")
        {
            Assert(PostMessage(handle, 0xF5, IntPtr.Zero, IntPtr.Zero), "posted modal calibration click");
        }
        else
        {
            SendMessage(handle, 0xF5, IntPtr.Zero, IntPtr.Zero);
        }
        ForegroundGuard();
        Console.WriteLine("BUTTON " + name + " via BM_CLICK");
    }
    private static void Image(IntPtr window, string name)
    {
        Rect rectangle;
        if (!GetWindowRect(window, out rectangle))
        {
            throw new Exception("GetWindowRect failed");
        }
        using (var bitmap = new Bitmap(rectangle.Right - rectangle.Left, rectangle.Bottom - rectangle.Top))
        {
            using (var graphics = Graphics.FromImage(bitmap))
            {
                IntPtr dc = graphics.GetHdc();
                bool ok;
                try
                {
                    ok = PrintWindow(window, dc, 0);
                }
                finally
                {
                    graphics.ReleaseHdc(dc);
                }
                if (!ok)
                {
                    throw new Exception("PrintWindow failed");
                }
                bitmap.Save(Path.Combine(_output, name), ImageFormat.Png);
            }
        }
        ForegroundGuard();
        Console.WriteLine("IMAGE " + name + " (owned window only)");
    }
    private static Dictionary<string, string> Ini(string path)
    {
        var result = new Dictionary<string, string>();
        foreach (var line in File.ReadAllLines(path))
        {
            int p = line.IndexOf('=');
            if (p > 0)
            {
                result.Add(line.Substring(0, p), line.Substring(p + 1));
            }
        }
        return result;
    }
    private static string Normalize(string key, string value)
    {
        if (value == "On" || value == "True" || value == "Checked")
        {
            return "True";
        }
        if (value == "Off" || value == "False" || value == "Unchecked")
        {
            return "False";
        }
        if (key.Contains("Multiplier"))
        {
            return Math.Round(Double.Parse(value.TrimEnd('x'), CultureInfo.InvariantCulture) * 100).ToString(
            CultureInfo.InvariantCulture);
        }
        return Double.Parse(value, CultureInfo.InvariantCulture).ToString("0", CultureInfo.InvariantCulture);
    }
    private static AutomationElement PerformCapture(AutomationElement main, int cycle)
    {
        Click(main, "Calibrate typing");
        var captureHandle = WaitForOwnedWindow("Typing calibration");
        var capture = AutomationElement.FromHandle(captureHandle);
        ForegroundGuard();
        Assert(!IsWindowEnabled((IntPtr)main.Current.NativeWindowHandle), "capture uses the owner's modal path");
        Assert(!Find(main, "Calibrate typing").Current.IsEnabled, "second calibration disabled during test");
        Assert(!Find(main, "Type in 4s").Current.IsEnabled, "automatic typing disabled during test");
        Image(captureHandle, "calibration-dialog-" + cycle + ".png");
        var combos = capture.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType
        .ComboBox));
        Assert(combos.Count == 1, "duration selector present");
        Assert(Read(combos[0]).Contains("60"), "default duration is 60 seconds");
        SendMessage((IntPtr)combos[0].Current.NativeWindowHandle, 0x14E, IntPtr.Zero, IntPtr.Zero);
        Assert(Read(combos[0]).Contains("30"), "selected duration is 30 seconds");
        var passage = Read(Find(capture, "Calibration passage")).Replace("\r\n", "\n");
        var input = Find(capture, "Typing input");
        var inputHandle = (IntPtr)input.Current.NativeWindowHandle;
        Assert(passage.Length > 900 && inputHandle != IntPtr.Zero, "real passage and real input found through UI Automation");
        var elapsed = Stopwatch.StartNew();
        var complete = new ManualResetEvent(false);
        Exception deliveryFailure = null;
        int index = 0;
        double next = 0;
        using (var timer = new System.Threading.Timer(delegate
        {
            try
            {
                ForegroundGuard();
                if (elapsed.ElapsedMilliseconds >= 33000)
                {
                    complete.Set();
                    return;
                }
                if (index >= 180 || elapsed.ElapsedMilliseconds < next || elapsed.ElapsedMilliseconds > 24000)
                {
                    return;
                }
                char c = passage[index];
                int key = c == '\n' ? 0x0D : VkKeyScan(c) & 0xFF;
                Assert(key != 0xFF, "character has a virtual-key mapping");
                SendMessage(inputHandle, 0x100, (IntPtr)key, (IntPtr)1);
                SendMessage(inputHandle, 0x102, (IntPtr)(c == '\n' ? '\r' : c), (IntPtr)1);
                SendMessage(inputHandle, 0x101, (IntPtr)key, (IntPtr)1);
                index++;
                if (index == 12)
                {
                    SendMessage(inputHandle, 0x302, IntPtr.Zero, IntPtr.Zero);
                }
                next = elapsed.ElapsedMilliseconds + 72 + (Char.IsUpper(c) ? 18 : Char.IsDigit(c) ? 35 : Char.IsPunctuation(c) ? 55 : 0) +
                (c == ' ' ? 90 : c == '\n' ? 240 : c == '.' || c == '?' ? 320 : 0);
            }
            catch (Exception e)
            {
                deliveryFailure = e;
                complete.Set();
            }
        }, null, 0, 10))
        {
            if (!complete.WaitOne(42000))
            {
                throw new Exception("Capture hard timeout");
            }
        }
        if (deliveryFailure != null)
        {
            throw deliveryFailure;
        }
        ForegroundGuard();
        Console.WriteLine("DELIVERED " + index + " characters; elapsed_ms=" + elapsed.ElapsedMilliseconds);
        var resultsHandle = WaitForOwnedWindow("Calibration results");
        var results = AutomationElement.FromHandle(resultsHandle);
        Assert(!IsWindowEnabled((IntPtr)main.Current.NativeWindowHandle), "results use the owner's modal path");
        Image(resultsHandle, "calibration-results-" + cycle + ".png");
        var summaryText = new StringBuilder(1024);
        SendMessage((IntPtr)Find(results, "Calibration summary").Current.NativeWindowHandle, 0xD, (IntPtr)summaryText.Capacity,
        summaryText);
        Assert(summaryText.ToString().Contains("Measured speed:") && summaryText.ToString().Contains("Accuracy:"),
        "results show measured speed and accuracy");
        Console.WriteLine("SUMMARY " + summaryText);
        Assert(!Find(main, "Calibrate typing").Current.IsEnabled, "second calibration disabled during results");
        Assert(!Find(main, "Type in 4s").Current.IsEnabled, "automatic typing disabled during results");
        return results;
    }
    [STAThread]
    private static int Main(string[] args)
    {
        _output = Path.GetFullPath(args[1]);
        Directory.CreateDirectory(_output);
        var data = Path.Combine(_output, "e2e-" + Guid.NewGuid().ToString("N").Substring(0, 8));
        Directory.CreateDirectory(data);
        var settingsPath = Path.Combine(data, "settings.ini");
        File.Copy(args[2], settingsPath);
        var before = File.ReadAllText(settingsPath);
        var beforeValues = Ini(settingsPath);
        if (!beforeValues.ContainsKey("paragraphPauseMilliseconds"))
        {
            beforeValues.Add("paragraphPauseMilliseconds", "0");
        }
        _foreground = GetForegroundWindow();
        var start = new ProcessStartInfo(Path.GetFullPath(args[0]))
        {
            UseShellExecute = false
        };
        start.EnvironmentVariables["AUTOWRITE_DATA_DIR"] = data;
        start.EnvironmentVariables["AUTOWRITE_DIAGNOSTIC_NOACTIVATE"] = "1";
        var hardTimeout = new System.Threading.Timer(delegate
        {
            try
            {
                if (_process != null && !_process.HasExited)
                {
                    _process.Kill();
                    _process.WaitForExit();
                }
            }
            catch (Exception)
            {
            }
            Console.WriteLine("FAIL E2E hard timeout; owned process terminated");
            Environment.Exit(124);
        }, null, 210000, Timeout.Infinite);
        try
        {
            _process = Process.Start(start);
            Assert(_process.WaitForInputIdle(8000), "built executable reached message loop");
            var mainHandle = WaitForOwnedWindow("AutoWrite");
            var main = AutomationElement.FromHandle(mainHandle);
            ForegroundGuard();
            foreach (var pair in Names)
            {
                var displayed = MainValue(main, pair.Key, pair.Value);
                Console.WriteLine("INITIAL " + pair.Key + "=" + displayed);
                Assert(Normalize(pair.Key, displayed) == beforeValues[pair.Key], "baseline main control " + pair.Key);
            }
            var results = PerformCapture(main, 0);
            var proposed = new Dictionary<string, string>();
            foreach (var pair in Names)
            {
                proposed[pair.Key] = Normalize(pair.Key, Read(Find(results, "Proposed " + pair.Value)));
                Console.WriteLine("PROPOSED " + pair.Key + "=" + proposed[pair.Key]);
            }
            Assert(File.ReadAllText(settingsPath) == before, "opening results leaves settings unchanged");
            Click(results, "Discard calibration");
            Assert(File.ReadAllText(settingsPath) == before, "Discard leaves settings bytes unchanged");
            foreach (var pair in Names)
            {
                var element = Find(main, pair.Value);
                var value = Read(element);
                Console.WriteLine("MAIN " + pair.Key + " type=" + element.Current.ControlType.ProgrammaticName + " class=" + element
                .Current.ClassName + " value=" + value + " expected=" + beforeValues[pair.Key]);
                Assert(Normalize(pair.Key, value) == beforeValues[pair.Key], "Discard leaves main control " + pair.Key);
            }
            results = PerformCapture(main, 1);
            foreach (var pair in Names)
            {
                proposed[pair.Key] = Normalize(pair.Key, Read(Find(results, "Proposed " + pair.Value)));
            }
            Click(results, "Apply calibration");
            var after = Ini(settingsPath);
            int changed = 0;
            foreach (var pair in Names)
            {
                Assert(after[pair.Key] == proposed[pair.Key], "saved proposal " + pair.Key);
                string displayed = MainValue(main, pair.Key, pair.Value);
                Assert(Normalize(pair.Key, displayed) == proposed[pair.Key], "main control proposal " + pair.Key);
                if (beforeValues[pair.Key] != after[pair.Key])
                {
                    changed++;
                }
            }
            Assert(changed >= 4, "at least four settings changed after real Apply");
            var captures = Directory.GetFiles(Path.Combine(data, "calibration"), "capture-*.tsv");
            Assert(captures.Length == 2, "two raw completed captures saved");
            string raw = File.ReadAllText(captures[0]);
            string[] header = raw.Split('\n')[0].Trim().Split(';');
            Assert(header[1] == "seconds=30" && header[2] == "completed=True" && header[3] == "ignored_keys=1",
                "raw capture: exactly one ignored press, the deliberate WM_PASTE; no character-key collisions");
            var afterApplyText = File.ReadAllText(settingsPath);
            Click(main, "Calibrate typing");
            var captureHandle = WaitForOwnedWindow("Typing calibration");
            var capture = AutomationElement.FromHandle(captureHandle);
            var cancel = capture.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.NameProperty, "Cancel"));
            SendMessage((IntPtr)cancel.Current.NativeWindowHandle, 0xF5, IntPtr.Zero, IntPtr.Zero);
            Assert(File.ReadAllText(settingsPath) == afterApplyText, "cancel completed without apply");
            ForegroundGuard();
            SendMessage(mainHandle, 0x10, IntPtr.Zero, IntPtr.Zero);
            Assert(_process.WaitForExit(5000), "owned app exited cleanly");
            foreach (var pair in proposed)
            {
                Assert(Ini(settingsPath)[pair.Key] == pair.Value, "close preserves proposal " + pair.Key);
            }
            Console.WriteLine("TOTAL assertions=" + _assertions + " failed=0 skipped=0; DATA=" + data);
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine("FAIL " + ex);
            return 1;
        }
        finally
        {
            hardTimeout.Dispose();
            if (_process != null && !_process.HasExited)
            {
                _process.CloseMainWindow();
                if (!_process.WaitForExit(3000))
                {
                    _process.Kill();
                }
                _process.WaitForExit();
            }
            if (_process != null)
            {
                Console.WriteLine("CLEANUP owned_pid=" + _process.Id + " exited=" + _process.HasExited);
                _process.Dispose();
            }
        }
    }
}
