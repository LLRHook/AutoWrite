using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
namespace AutoWrite.Tests
{
    internal sealed class KeyStroke
    {
        public long Time;
        public char Value;
        public KeyStroke(long time, char value)
        {
            Time = time;
            Value = value;
        }
    }
    internal sealed class VirtualRuntime : IEngineRuntime
    {
        public readonly List<KeyStroke> Keys = new List<KeyStroke>();
        public long Now;
        public long Limit = Int64.MaxValue;
        private readonly int _seed;
        public VirtualRuntime(int seed)
        {
            _seed = seed;
        }

        public Random CreateRandom()
        {
            return new Random(_seed);
        }

        public IntPtr ForegroundWindow()
        {
            return IntPtr.Zero;
        }

        public bool IsKeyDown(int key)
        {
            return false;
        }

        public void ReleaseModifiers()
        {
        }

        public void SendCharacter(char value)
        {
            Keys.Add(new KeyStroke(Now, value));
        }

        public void Sleep(int milliseconds, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            Now += Math.Max(0, milliseconds);
            if (Now > Limit)
            {
                throw new OperationCanceledException();
            }
        }

        public string FinalText()
        {
            var s = new StringBuilder();
            foreach (var key in Keys)
            {
                if (key.Value == '\b')
                {
                    if (s.Length > 0)
                    {
                        s.Length--;
                    }
                }
                else s.Append(key.Value);
            }
            return s.ToString();
        }

        public string Log()
        {
            var s = new StringBuilder();
            foreach (var key in Keys)
            {
                s.Append(key.Time).Append(':').Append((int)key.Value).Append(';');
            }
            return s.ToString();
        }
    }
    internal static partial class Runner
    {
        private static int _passed, _failed, _skipped;
        internal static string Output, SourceRoot;
        internal static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                throw new Exception(message);
            }
        }

        internal static void Test(string name, Action action)
        {
            try
            {
                action();
                _passed++;
                Console.WriteLine("PASS " + name);
            }
            catch (Exception ex)
            {
                _failed++;
                Console.WriteLine("FAIL " + name + ": " + ex);
            }
        }

        internal static TypingOptions Options(AppSettings settings)
        {
            var options = new TypingOptions();
            foreach (var p in typeof(AppSettings).GetProperties())
            {
                var target = typeof(TypingOptions).GetProperty(p.Name);
                if (target != null)
                {
                    target.SetValue(options, p.GetValue(settings, null), null);
                }
            }
            options.TelemetryPath = Path.Combine(DataDirectory.PathRoot, "engine.tsv");
            Directory.CreateDirectory(DataDirectory.PathRoot);
            return options;
        }

        internal static VirtualRuntime RunEngine(string text, AppSettings settings, int seed, long limit, bool telemetry = true)
        {
            var runtime = new VirtualRuntime(seed);
            runtime.Limit = limit;
            var options = Options(settings);
            if (!telemetry)
            {
                options.TelemetryPath = "-";
            }
            try
            {
                new TypingEngine(runtime).TypeText(text, options, null, CancellationToken.None);
            }
            catch (OperationCanceledException)
            {
                if (limit == Int64.MaxValue)
                {
                    throw;
                }
            }
            return runtime;
        }
        [STAThread]
        private static int Main(string[] args)
        {
            Output = args[0];
            SourceRoot = args[1];
            Test("fixed seed reproduces real engine output and exact final text", delegate
            {
                var text = "A report (Smith, 2021) tests 42.5%: useful?\nSecond paragraph.\nThird and final line.";
                var settings = AppSettings.Defaults();
                settings.TypoChancePercent = 8;
                var a = RunEngine(text, settings, 17, Int64.MaxValue);
                var b = RunEngine(text, settings, 17, Int64.MaxValue);
                Assert(a.Log() == b.Log(), "seed drift");
                Assert(a.FinalText() == text, "incorrect final text");
                Assert(a.Now > 0 && a.Keys.Count > text.Length, "engine did not correct mistakes");
                File.WriteAllText(Path.Combine(Output, "item1-seed17.txt"), a.Log());
            });
            Test("settings round trip and isolated telemetry", delegate
            {
                var settings = AppSettings.Defaults();
                settings.WordsPerMinute = 73;
                SettingsStore.Save(settings);
                var actual = SettingsStore.Load();
                foreach (var p in typeof(AppSettings).GetProperties())
                {
                    Assert(Object.Equals(p.GetValue(settings, null), p.GetValue(actual, null)), p.Name);
                }
                Assert(TelemetryLogger.CreatePath().StartsWith(DataDirectory.PathRoot), "telemetry escaped isolation");
                Assert(File.Exists(Path.Combine(DataDirectory.PathRoot, "settings.ini")), "settings escaped isolation");
            });
            Test("data override rejects relative and drive-relative paths", delegate
            {
                var previous = Environment.GetEnvironmentVariable("AUTOWRITE_DATA_DIR");
                try
                {
                    foreach (var invalid in new[]
                    {
                        "relative", "C:relative", "\\relative"
                    })
                    {
                        Environment.SetEnvironmentVariable("AUTOWRITE_DATA_DIR", invalid);
                        bool rejected = false;
                        try
                        {
                            var unused = DataDirectory.PathRoot;
                        }
                        catch (ArgumentException)
                        {
                            rejected = true;
                        }
                        Assert(rejected, "relative data path accepted");
                    }
                }
                finally
                {
                    Environment.SetEnvironmentVariable("AUTOWRITE_DATA_DIR", previous);
                }
            });
            MoreTests();
            Console.WriteLine("TOTAL passed=" + _passed + " failed=" + _failed + " skipped=" + _skipped);
            return _failed == 0 ? 0 : 1;
        }

        static partial void MoreTests();
    }
}
