using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace AutoWrite.Tests
{
    // Reference output of the Windows build, for ports to other platforms.
    // Build() is deterministic: the same sources always produce the same files.
    // reference.ps1 writes them to reference/data, and the test suite checks that
    // the committed files still match, so this engine cannot drift from them
    // unnoticed. reference/README.md describes every file.
    internal static class ReferenceData
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private const string Prose =
            "The committee met twice in the spring to review the draft. Most members agreed that the plan was sound, " +
            "but several asked for clearer costs and a longer trial. A small working group was formed; it will gather " +
            "figures from each site, compare them with last year, and report back in June. Until then the current " +
            "arrangement stays in place. Nobody expects a quick answer, and that is fine: a careful decision now will " +
            "save time later. Questions can be sent to the secretary, who keeps the minutes and the list of actions.";

        private static readonly string[] GoldenTexts =
        {
            "quiet ordinary words and a pause. Another sentence.\nEnd.",
            "A report [12]: 42.5% (p < 0.05)! R&D\r\nNext line.\r\nLast.",
            "aB9! [] {} @NASA and/or the study's 128 cases?\nYes: e.g., Table 2.1."
        };

        // Names are stable identifiers used in the case notes.
        private static readonly string[][] Texts =
        {
            new[] { "golden0", GoldenTexts[0] },
            new[] { "golden1", GoldenTexts[1] },
            new[] { "golden2", GoldenTexts[2] },
            new[] { "single", "a" },
            new[] { "empty", "" },
            new[] { "blank", "  \n\t  " },
            new[] { "symbols", "Hello, World! 123 (test) #1 @home ~50% <tag> {x} [y] a|b c\\d \"q\" 'r' `s` ^ & * _ + = ; : / ?" },
            new[] { "newlines", "one\r\ntwo\rthree\n\nfour\r\n\r\nfive" },
            new[] { "tabs", "col1\tcol2\t\tcol3 end." },
            new[] { "accents", "Caf\u00E9 na\u00EFve \u2014 \u201Cquoted\u201D \u2018single\u2019 \u20AC5, \u00BD cup; ni\u00F1o, " +
                "Stra\u00DFe, \u00DCber\u2026 10\u00A0km \u00D7 2 = 20\u00B0." },
            new[] { "emoji", "ok \uD83D\uDC4D done \uD83C\uDF89! caf\u00E9 \uD83D\uDE00\uD83D\uDE00 end" },
            new[] { "codes", "ID: AB-12345 / REF 2024-11-16 @ 09:30 UTC, ROOM B7 (LEVEL 3)" },
            new[] { "prose", Prose },
            new[] { "passage", CalibrationPassage.Text }
        };

        // Added after the first port was reviewed. Kept apart from Texts so that
        // the ids and inputs of the earlier cases do not change.
        private static readonly string[][] MoreTexts =
        {
            new[] { "scripts", "\u0395\u03BB\u03BB\u03B7\u03BD\u03B9\u03BA\u03AC \u03A3\u039F\u03A6\u039F\u03A3 \u03C3\u03BF\u03C6\u03CC\u03C2, \u041F\u0440\u0438\u0432\u0435\u0442 \u041C\u0418\u0420! \u0130stanbul \u0131l\u0131k i\u0130 I\u0131; \u01C4 \u01C5 \u01C6, \u1E9E \u00DF, \u212A \u212B \u2126. \u65E5\u672C\u8A9E\u3001\u4E2D\u6587\u3002 \uFF21\uFF22\uFF43\uFF11\uFF12\uFF01\uFF1F \u0663\u0664 \u05E9\u05DC\u05D5\u05DD \u0633\u0644\u0627\u0645." },
            new[] { "spaces", "a\u180Eb\u200Bc\u00A0d\u2028e\u2029f\u0085g\u3000h\uFEFFi\u2009j\u202Fk\u205Fl\u1680m\u001Cn\u001Fo\u000Bp\u000Cq\u200Dr\u2060s. Two\u00A0words,\u2009thin; end\u3000wide." },
            new[] { "clusters", "e\u0301 n\u0303o; \uD83D\uDC4D\uD83C\uDFFD ok \uD83D\uDC68\u200D\uD83D\uDC69\u200D\uD83D\uDC67 fam \uD83C\uDDEB\uD83C\uDDF7 fr \u2764\uFE0F 1\uFE0F\u20E3 \u1112\u1161\u11AB \u0928\u092E\u0938\u094D\u0924\u0947 \u0E2A\u0E27\u0E31\u0E2A\u0E14\u0E35. Cafe\u0301 at 9." }
        };

        internal static SortedDictionary<string, string> Build()
        {
            var files = new SortedDictionary<string, string>(StringComparer.Ordinal);
            var culture = Thread.CurrentThread.CurrentCulture;
            Directory.CreateDirectory(DataDirectory.PathRoot);
            string settingsPath = Path.Combine(DataDirectory.PathRoot, "settings.ini");
            byte[] settingsBefore = File.Exists(settingsPath) ? File.ReadAllBytes(settingsPath) : null;
            try
            {
                // The app formats a few numbers with the current culture. The reference uses '.'.
                Thread.CurrentThread.CurrentCulture = Inv;
                files["random.tsv"] = RandomFile();
                files["format.tsv"] = FormatFile();
                files["passage.txt"] = Esc(CalibrationPassage.Text) + "\n";
                files["settings-ranges.tsv"] = RangesFile();
                EngineFiles(files);
                TelemetryFiles(files);
                files["settings-load.tsv"] = SettingsLoadFile(settingsPath);
                files["settings-load-bytes.tsv"] = SettingsLoadBytesFile(settingsPath);
                files["settings-save.tsv"] = SettingsSaveFile(settingsPath);
                files["capture-sessions.txt"] = SessionsFile();
                files["alignment.tsv"] = AlignmentFile();
                AnalysisFiles(files);
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = culture;
                if (settingsBefore == null)
                {
                    File.Delete(settingsPath);
                }
                else File.WriteAllBytes(settingsPath, settingsBefore);
            }
            return files;
        }

        // Backslash, tab, CR and LF are escaped; every code unit outside printable
        // ASCII becomes \uXXXX. The files are therefore pure ASCII, and a reader
        // recovers the exact UTF-16 code units, including each half of a surrogate pair.
        internal static string Esc(string value)
        {
            var s = new StringBuilder();
            foreach (char c in value)
            {
                if (c == '\\')
                {
                    s.Append("\\\\");
                }
                else if (c == '\t')
                {
                    s.Append("\\t");
                }
                else if (c == '\n')
                {
                    s.Append("\\n");
                }
                else if (c == '\r')
                {
                    s.Append("\\r");
                }
                else if (c < ' ' || c > '~')
                {
                    s.Append("\\u").Append(((int)c).ToString("X4", Inv));
                }
                else s.Append(c);
            }
            return s.ToString();
        }

        private static string Bits(double value)
        {
            return BitConverter.DoubleToInt64Bits(value).ToString("X16", Inv);
        }

        private static string R(double value)
        {
            return value.ToString("R", Inv);
        }

        internal static string SettingsLine(AppSettings settings)
        {
            var s = new StringBuilder();
            foreach (var p in typeof(AppSettings).GetProperties())
            {
                if (s.Length > 0)
                {
                    s.Append(';');
                }
                s.Append(p.Name).Append('=').Append(Convert.ToString(p.GetValue(settings, null), Inv));
            }
            return s.ToString();
        }

        private static AppSettings Extreme(bool maximum)
        {
            var s = AppSettings.Defaults();
            foreach (var p in typeof(AppSettings).GetProperties())
            {
                if (p.PropertyType == typeof(bool))
                {
                    p.SetValue(s, maximum, null);
                }
                else p.SetValue(s, maximum ? SettingRanges.Maximum(p.Name) : SettingRanges.Minimum(p.Name), null);
            }
            return s;
        }

        private static AppSettings RandomSettings(Random random)
        {
            var s = AppSettings.Defaults();
            foreach (var p in typeof(AppSettings).GetProperties())
            {
                if (p.PropertyType == typeof(bool))
                {
                    p.SetValue(s, random.Next(4) != 0, null);
                }
                else p.SetValue(s, random.Next(SettingRanges.Minimum(p.Name), SettingRanges.Maximum(p.Name) + 1), null);
            }
            return s;
        }

        private static string RandomFile()
        {
            var s = new StringBuilder();
            s.Append("# System.Random of .NET Framework 4, each row from a new Random(seed).\n");
            s.Append("# seed\tcall\tvalues separated by spaces. NextDouble values are the 64 IEEE-754 bits in hex.\n");
            s.Append("# Mixed repeats ten times: NextDouble(), Next(8,24), Next(100), Next().\n");
            int[] seeds = { 0, 1, 17, 791, 70001, 123456789, Int32.MaxValue, -1, -17, Int32.MinValue };
            int[] maxima = { 1, 2, 7, 100, Int32.MaxValue };
            int[][] ranges = { new[] { 8, 24 }, new[] { 733, 2201 }, new[] { -5, 5 }, new[] { 1, 2 } };
            foreach (int seed in seeds)
            {
                var random = new Random(seed);
                var row = new StringBuilder();
                for (int i = 0; i < 40; i++)
                {
                    row.Append(i == 0 ? "" : " ").Append(random.Next().ToString(Inv));
                }
                s.Append(seed).Append("\tNext()\t").Append(row).Append('\n');

                random = new Random(seed);
                row = new StringBuilder();
                for (int i = 0; i < 40; i++)
                {
                    row.Append(i == 0 ? "" : " ").Append(Bits(random.NextDouble()));
                }
                s.Append(seed).Append("\tNextDouble()\t").Append(row).Append('\n');

                foreach (int maximum in maxima)
                {
                    random = new Random(seed);
                    row = new StringBuilder();
                    for (int i = 0; i < 40; i++)
                    {
                        row.Append(i == 0 ? "" : " ").Append(random.Next(maximum).ToString(Inv));
                    }
                    s.Append(seed).Append("\tNext(").Append(maximum).Append(")\t").Append(row).Append('\n');
                }
                foreach (int[] range in ranges)
                {
                    random = new Random(seed);
                    row = new StringBuilder();
                    for (int i = 0; i < 40; i++)
                    {
                        row.Append(i == 0 ? "" : " ").Append(random.Next(range[0], range[1]).ToString(Inv));
                    }
                    s.Append(seed).Append("\tNext(").Append(range[0]).Append(',').Append(range[1]).Append(")\t").Append(row).Append('\n');
                }
                random = new Random(seed);
                row = new StringBuilder();
                for (int i = 0; i < 10; i++)
                {
                    row.Append(i == 0 ? "" : " ").Append(Bits(random.NextDouble()));
                    row.Append(' ').Append(random.Next(8, 24).ToString(Inv));
                    row.Append(' ').Append(random.Next(100).ToString(Inv));
                    row.Append(' ').Append(random.Next().ToString(Inv));
                }
                s.Append(seed).Append("\tMixed\t").Append(row).Append('\n');
            }
            return s.ToString();
        }

        private static string FormatFile()
        {
            var s = new StringBuilder();
            s.Append("# How this build formats and rounds numbers.\n");
            s.Append("# bits\tR\tF1\tF2\tF3\tMath.Round\t(int) cast\n");
            double[] values =
            {
                0, 0.05, 0.15, 0.25, 0.35, 0.45, 0.5, 0.95, 1.005, 1.5, 2.0005, 2.5, 3.5, 12.345, 15.2, 0.615,
                61.25, 96.25, 97.25, 99.95, 99.949999999999989, 99.95000000000001, 100, 1234.5675, 59999.9995,
                -0.25, -0.5, -1.5, -2.5, 0.0000001, 123456789.125, 1.0 / 3, 2.0 / 3, 389.0 / 400 * 100, 77.0 / 80 * 100,
                4.35 * 100, 1.15, 2.675, 1e15 + 0.5, 219.5, 220.5, 44.5, 45.5
            };
            foreach (double value in values)
            {
                s.Append(Bits(value)).Append('\t').Append(R(value)).Append('\t').Append(value.ToString("F1", Inv)).Append('\t')
                    .Append(value.ToString("F2", Inv)).Append('\t').Append(value.ToString("F3", Inv)).Append('\t')
                    .Append(R(Math.Round(value))).Append('\t').Append(((long)value).ToString(Inv)).Append('\n');
            }
            return s.ToString();
        }

        private static string RangesFile()
        {
            var s = new StringBuilder();
            s.Append("# name\tlabel\tminimum\tmaximum\tdefault, in the order the settings are stored.\n");
            var defaults = AppSettings.Defaults();
            foreach (var p in typeof(AppSettings).GetProperties())
            {
                s.Append(p.Name).Append('\t').Append(SettingNames.Labels[p.Name]).Append('\t');
                if (p.PropertyType == typeof(bool))
                {
                    s.Append("False\tTrue\t");
                }
                else s.Append(SettingRanges.Minimum(p.Name)).Append('\t').Append(SettingRanges.Maximum(p.Name)).Append('\t');
                s.Append(Convert.ToString(p.GetValue(defaults, null), Inv)).Append('\n');
            }
            return s.ToString();
        }

        private static AppSettings With(Action<AppSettings> change)
        {
            var s = AppSettings.Defaults();
            change(s);
            return s;
        }

        private static List<KeyValuePair<string, AppSettings>> EngineProfiles()
        {
            var list = new List<KeyValuePair<string, AppSettings>>();
            list.Add(new KeyValuePair<string, AppSettings>("defaults", AppSettings.Defaults()));
            list.Add(new KeyValuePair<string, AppSettings>("minimums", Extreme(false)));
            list.Add(new KeyValuePair<string, AppSettings>("maximums", Extreme(true)));
            list.Add(new KeyValuePair<string, AppSettings>("mistakes-off", With(delegate(AppSettings s) { s.EnableTypos = false; })));
            list.Add(new KeyValuePair<string, AppSettings>("duplicates", With(delegate(AppSettings s)
            {
                s.TypoChancePercent = 6;
                s.DuplicateChancePercent = 100;
            })));
            list.Add(new KeyValuePair<string, AppSettings>("transposes", With(delegate(AppSettings s)
            {
                s.TypoChancePercent = 6;
                s.TransposeChancePercent = 100;
            })));
            list.Add(new KeyValuePair<string, AppSettings>("mixed-kinds", With(delegate(AppSettings s)
            {
                s.TypoChancePercent = 6;
                s.DuplicateChancePercent = 35;
                s.TransposeChancePercent = 45;
            })));
            list.Add(new KeyValuePair<string, AppSettings>("shares-over-100", With(delegate(AppSettings s)
            {
                s.TypoChancePercent = 6;
                s.DuplicateChancePercent = 80;
                s.TransposeChancePercent = 70;
            })));
            list.Add(new KeyValuePair<string, AppSettings>("detect-0", With(delegate(AppSettings s)
            {
                s.TypoChancePercent = 6;
                s.CorrectionDetectChars = 0;
            })));
            list.Add(new KeyValuePair<string, AppSettings>("detect-8-no-pauses", With(delegate(AppSettings s)
            {
                s.TypoChancePercent = 6;
                s.CorrectionDetectChars = 8;
                s.CorrectionNoticeMilliseconds = 0;
                s.CorrectionPauseMilliseconds = 0;
                s.CorrectionResumeMilliseconds = 0;
            })));
            list.Add(new KeyValuePair<string, AppSettings>("keep-target-off", With(delegate(AppSettings s) { s.KeepTargetWpm = false; })));
            list.Add(new KeyValuePair<string, AppSettings>("metronome", With(delegate(AppSettings s)
            {
                s.JitterPercent = 0;
                s.BurstPercent = 0;
                s.KeyPairRhythmPercent = 0;
                s.WordPauseMilliseconds = 0;
                s.SentencePauseMilliseconds = 0;
                s.ThinkingPauseChancePercent = 0;
            })));
            list.Add(new KeyValuePair<string, AppSettings>("paragraph-and-short-thinking", With(delegate(AppSettings s)
            {
                s.ParagraphPauseMilliseconds = 700;
                s.ThinkingPauseChancePercent = 25;
                s.ThinkingPauseMilliseconds = 2;
            })));
            list.Add(new KeyValuePair<string, AppSettings>("letters-only-mistakes", With(delegate(AppSettings s)
            {
                s.TypoChancePercent = 10;
                s.NumberTypoMultiplierPercent = 0;
                s.PunctuationTypoMultiplierPercent = 0;
                s.ShiftSymbolTypoMultiplierPercent = 0;
            })));
            list.Add(new KeyValuePair<string, AppSettings>("heavy-difficult-keys", With(delegate(AppSettings s)
            {
                s.TypoChancePercent = 10;
                s.NumberTypoMultiplierPercent = 500;
                s.PunctuationTypoMultiplierPercent = 500;
                s.ShiftSymbolTypoMultiplierPercent = 500;
                s.UppercaseDelayPenaltyPercent = 200;
                s.NumberDelayPenaltyPercent = 200;
                s.PunctuationDelayPenaltyPercent = 200;
                s.ShiftSymbolDelayPenaltyPercent = 300;
            })));
            list.Add(new KeyValuePair<string, AppSettings>("slowest", With(delegate(AppSettings s) { s.WordsPerMinute = 5; })));
            list.Add(new KeyValuePair<string, AppSettings>("fastest", With(delegate(AppSettings s) { s.WordsPerMinute = 180; })));
            list.Add(new KeyValuePair<string, AppSettings>("wild-rhythm", With(delegate(AppSettings s)
            {
                s.JitterPercent = 100;
                s.BurstPercent = 100;
            })));
            return list;
        }

        private static string TextNamed(string name)
        {
            foreach (var pair in Texts)
            {
                if (pair[0] == name)
                {
                    return pair[1];
                }
            }
            throw new ArgumentException(name);
        }

        private static void EngineFiles(SortedDictionary<string, string> files)
        {
            var index = new StringBuilder();
            var logs = new StringBuilder();
            index.Append("# One engine run per row, on a virtual clock that advances only by the engine's own pauses.\n");
            index.Append("# id\tseed\tfinal_ms\tkeys\tsha256 of the log\tnote\tsettings\ttext (escaped)\n");
            logs.Append("# id\tlog: one \"time_ms:code;\" entry per key sent. Code 8 is Backspace, 10 is Enter.\n");
            int golden = 0;
            for (int profile = 0; profile < 3; profile++)
            {
                var settings = profile == 0 ? AppSettings.Defaults() : Runner.Profile(profile == 1 ? 0 : 2);
                for (int text = 0; text < GoldenTexts.Length; text++)
                {
                    foreach (int seed in new[] { 0, 17, 791 })
                    {
                        Engine(index, logs, "g" + golden.ToString("00", Inv), "golden " + golden, settings, GoldenTexts[text], seed);
                        golden++;
                    }
                }
            }
            var profiles = EngineProfiles();
            string[] crossed = { "golden0", "golden2", "symbols", "newlines", "accents", "codes", "prose" };
            int number = 0;
            foreach (var profile in profiles)
            {
                foreach (string name in crossed)
                {
                    Engine(index, logs, "e" + number.ToString("000", Inv), profile.Key + " / " + name, profile.Value, TextNamed(name), 17);
                    number++;
                }
            }
            foreach (var pair in Texts)
            {
                if (pair[0] == "passage")
                {
                    continue;
                }
                foreach (int seed in new[] { 0, 791 })
                {
                    Engine(index, logs, "e" + number.ToString("000", Inv), "defaults / " + pair[0], AppSettings.Defaults(), pair[1], seed);
                    number++;
                }
            }
            foreach (string name in new[] { "defaults", "maximums", "mixed-kinds", "keep-target-off" })
            {
                foreach (var profile in profiles)
                {
                    if (profile.Key == name)
                    {
                        Engine(index, logs, "e" + number.ToString("000", Inv), name + " / passage", profile.Value,
                            CalibrationPassage.Text, 5);
                        number++;
                    }
                }
            }
            var random = new Random(20261116);
            for (int i = 0; i < 40; i++)
            {
                var settings = RandomSettings(random);
                var pair = Texts[random.Next(Texts.Length - 1)];
                int seed = random.Next(4) == 0 ? -random.Next(1, 100000) : random.Next();
                Engine(index, logs, "r" + i.ToString("00", Inv), "random settings / " + pair[0], settings, pair[1], seed);
            }
            int extra = 0;
            foreach (var pair in MoreTexts)
            {
                foreach (string name in new[] { "defaults", "mixed-kinds", "heavy-difficult-keys", "maximums" })
                {
                    foreach (var profile in profiles)
                    {
                        if (profile.Key == name)
                        {
                            Engine(index, logs, "x" + extra.ToString("00", Inv), name + " / " + pair[0], profile.Value, pair[1], 17);
                            extra++;
                        }
                    }
                }
            }
            files["engine.tsv"] = index.ToString();
            files["engine-logs.tsv"] = logs.ToString();
        }

        private static void Engine(StringBuilder index, StringBuilder logs, string id, string note, AppSettings settings, string text,
            int seed)
        {
            var runtime = Runner.RunEngine(text, settings, seed, Int64.MaxValue, false);
            string log = runtime.Log();
            string hash;
            using (var sha = SHA256.Create())
            {
                hash = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(log))).Replace("-", "");
            }
            index.Append(id).Append('\t').Append(seed).Append('\t').Append(runtime.Now).Append('\t').Append(runtime.Keys.Count)
                .Append('\t').Append(hash).Append('\t').Append(note).Append('\t').Append(SettingsLine(settings)).Append('\t')
                .Append(Esc(text)).Append('\n');
            logs.Append(id).Append('\t').Append(log).Append('\n');
        }

        // The telemetry file of a run, with the three wall-clock columns replaced:
        // utc_iso by T, elapsed_ms by E, and gap_ms by G wherever it is present.
        private static void TelemetryFiles(SortedDictionary<string, string> files)
        {
            var profiles = EngineProfiles();
            string[][] cases =
            {
                new[] { "defaults", "golden1", "17" }, new[] { "mixed-kinds", "golden2", "791" },
                new[] { "maximums", "symbols", "17" }, new[] { "defaults", "newlines", "0" },
                new[] { "defaults", "accents", "0" }, new[] { "detect-0", "prose", "17" }
            };
            int number = 0;
            foreach (var item in cases)
            {
                AppSettings settings = null;
                foreach (var profile in profiles)
                {
                    if (profile.Key == item[0])
                    {
                        settings = profile.Value;
                    }
                }
                string text = TextNamed(item[1]);
                int seed = Int32.Parse(item[2], Inv);
                var runtime = new VirtualRuntime(seed);
                var options = Runner.Options(settings);
                options.TelemetryPath = Path.Combine(DataDirectory.PathRoot, "reference-telemetry.tsv");
                new TypingEngine(runtime).TypeText(text, options, null, CancellationToken.None);
                string[] lines = File.ReadAllLines(options.TelemetryPath);
                File.Delete(options.TelemetryPath);
                var s = new StringBuilder();
                s.Append("# seed=").Append(seed).Append('\n');
                s.Append("# settings=").Append(SettingsLine(settings)).Append('\n');
                s.Append("# text=").Append(Esc(text)).Append('\n');
                for (int i = 0; i < lines.Length; i++)
                {
                    if (i > 0)
                    {
                        string[] columns = lines[i].Split('\t');
                        columns[1] = "T";
                        columns[2] = "E";
                        columns[3] = columns[3].Length == 0 ? "" : "G";
                        lines[i] = String.Join("\t", columns);
                    }
                    s.Append(lines[i]).Append('\n');
                }
                files["telemetry/t" + number.ToString("0", Inv) + ".tsv"] = s.ToString();
                number++;
            }
        }

        private static string SettingsLoadFile(string path)
        {
            // null means no file. Each string is the file's text; the file is its UTF-8 encoding,
            // so a leading \uFEFF is a byte-order mark.
            string[][] cases =
            {
                new[] { "missing", null },
                new[] { "empty", "" },
                new[] { "baseline", File.ReadAllText(Path.Combine(Runner.SourceRoot, "tests", "baseline-settings.ini")) },
                new[] { "comments-and-blanks", "# a comment\r\n\r\n   \r\nwordsPerMinute=61\r\n#wordsPerMinute=99\r\n" },
                new[] { "unix-newlines", "wordsPerMinute=62\njitterPercent=11\n" },
                new[] { "old-mac-newlines", "wordsPerMinute=63\rjitterPercent=12\r" },
                new[] { "no-final-newline", "wordsPerMinute=64" },
                new[] { "byte-order-mark", "\uFEFFwordsPerMinute=65\r\njitterPercent=13\r\n" },
                new[] { "key-case", "WORDSPERMINUTE=66\r\nJitterpercent=14\r\nkeeptargetwpm=false\r\n" },
                new[] { "spaces", "  wordsPerMinute  =  67  \r\n\tjitterPercent\t=\t15\t\r\n" },
                new[] { "no-break-space", "wordsPerMinute=\u00A068\u00A0\r\n" },
                new[] { "legacy-speed-15", "charactersPerSecond=15\r\n" },
                new[] { "legacy-speed-10", "charactersPerSecond=10\r\n" },
                new[] { "legacy-speed-1", "charactersPerSecond=1\r\n" },
                new[] { "legacy-speed-99", "charactersPerSecond=99\r\n" },
                new[] { "legacy-speed-then-wpm", "charactersPerSecond=10\r\nwordsPerMinute=70\r\n" },
                new[] { "legacy-variance", "varianceMilliseconds=40\r\n" },
                new[] { "too-high", "wordsPerMinute=9999\r\ntypoChancePercent=50\r\nbuttonDelaySeconds=11\r\n" },
                new[] { "too-low", "wordsPerMinute=-3\r\njitterPercent=-1\r\nthinkingPauseMilliseconds=-500\r\n" },
                new[] { "not-numbers", "wordsPerMinute=abc\r\njitterPercent=\r\nburstPercent=7.0\r\nwordPauseMilliseconds=0x10\r\n" +
                    "sentencePauseMilliseconds=1e3\r\nthinkingPauseMilliseconds=1,000\r\n" },
                new[] { "number-forms", "wordsPerMinute=+71\r\njitterPercent=007\r\nburstPercent=-0\r\nwordPauseMilliseconds= 12 \r\n" },
                new[] { "full-width-digits", "wordsPerMinute=\uFF17\uFF12\r\n" },
                new[] { "overflow", "wordsPerMinute=2147483648\r\njitterPercent=-2147483649\r\nburstPercent=2147483647\r\n" },
                new[] { "booleans", "keepTargetWpm=FALSE\r\nenableTypos= false \r\n" },
                new[] { "boolean-digits", "keepTargetWpm=0\r\nenableTypos=no\r\n" },
                new[] { "boolean-true", "keepTargetWpm=false\r\nkeepTargetWpm=TRUE\r\nenableTypos=False\r\n" },
                new[] { "duplicate-keys", "wordsPerMinute=40\r\nwordsPerMinute=73\r\n" },
                new[] { "no-separator", "wordsPerMinute\r\n=74\r\nwordsPerMinute=75=6\r\njitterPercent==16\r\n" },
                new[] { "unknown-keys", "favouriteColour=blue\r\nwordsPerMinute=76\r\n" },
                new[] { "multipliers", "numberTypoMultiplierPercent=250\r\npunctuationTypoMultiplierPercent=501\r\n" +
                    "shiftSymbolTypoMultiplierPercent=0\r\n" },
                new[] { "kelvin-sign-in-key", "\u212AeepTargetWpm=False\r\n" },
                new[] { "dotless-i-in-key", "wordsPerM\u0131nute=100\r\n" },
                new[] { "dotted-capital-i-in-key", "wordsPerM\u0130nute=101\r\n" },
                new[] { "long-s-in-key", "\u017FentencePauseMilliseconds=0\r\n" },
                new[] { "nul-after-boolean", "keepTargetWpm=false\u0000\r\n" },
                new[] { "nul-after-number", "wordsPerMinute=90\u0000\r\n" },
                new[] { "nul-before-number", "wordsPerMinute=\u000091\r\n" },
                new[] { "zero-width-space", "wordsPerMinute=\u200B100\r\n" },
                new[] { "mongolian-vowel-separator", "wordsPerMinute=\u180E102\u180E\r\n" },
                new[] { "unusual-spaces", "wordsPerMinute=\u2003103\u3000\r\n\u2028jitterPercent=21\r\n" },
                new[] { "line-separator", "wordsPerMinute=104\u2028jitterPercent=22\u0085burstPercent=23\r\n" },
                new[] { "form-feed-and-vertical-tab", "wordsPerMinute=105\u000CjitterPercent=24\u000BburstPercent=25\r\n" },
                new[] { "legacy-speed-overflow", "charactersPerSecond=200000000\r\n" },
                new[] { "legacy-speed-negative", "charactersPerSecond=-10\r\n" },
                new[] { "legacy-speed-zero", "wordsPerMinute=44\r\ncharactersPerSecond=0\r\n" },
                new[] { "hash-inside-line", "wordsPerMinute=106 # fast\r\n  # jitterPercent=1\r\nburstPercent=26#\r\n" },
                new[] { "boolean-with-inner-space", "keepTargetWpm=fal se\r\nenableTypos=\tFALSE\t\r\n" }
            };
            var s = new StringBuilder();
            s.Append("# id\tsettings file text (escaped; <missing> means no file)\tsettings after loading\n");
            foreach (var item in cases)
            {
                if (item[1] == null)
                {
                    File.Delete(path);
                }
                else File.WriteAllBytes(path, new UTF8Encoding(false).GetBytes(item[1]));
                s.Append(item[0]).Append('\t').Append(item[1] == null ? "<missing>" : Esc(item[1])).Append('\t')
                    .Append(SettingsLine(SettingsStore.Load())).Append('\n');
            }
            return s.ToString();
        }

        private static string SettingsLoadBytesFile(string path)
        {
            var cases = new List<KeyValuePair<string, byte[]>>();
            var utf16le = new List<byte>(new byte[] { 0xFF, 0xFE });
            utf16le.AddRange(Encoding.Unicode.GetBytes("wordsPerMinute=120\r\njitterPercent=10\r\n"));
            cases.Add(new KeyValuePair<string, byte[]>("utf-16-little-endian-with-mark", utf16le.ToArray()));
            var utf16be = new List<byte>(new byte[] { 0xFE, 0xFF });
            utf16be.AddRange(Encoding.BigEndianUnicode.GetBytes("wordsPerMinute=121\r\n"));
            cases.Add(new KeyValuePair<string, byte[]>("utf-16-big-endian-with-mark", utf16be.ToArray()));
            cases.Add(new KeyValuePair<string, byte[]>("utf-16-without-mark", Encoding.Unicode.GetBytes("wordsPerMinute=122\r\n")));
            cases.Add(new KeyValuePair<string, byte[]>("utf-8-mark-only", new byte[] { 0xEF, 0xBB, 0xBF }));
            var invalid = new List<byte>(Encoding.ASCII.GetBytes("wordsPerMinute=9"));
            invalid.Add(0xFF);
            invalid.AddRange(Encoding.ASCII.GetBytes("1\r\njitterPercent=17\r\n"));
            cases.Add(new KeyValuePair<string, byte[]>("invalid-utf-8-byte", invalid.ToArray()));
            var latin1 = new List<byte>(Encoding.ASCII.GetBytes("# caf"));
            latin1.Add(0xE9);
            latin1.AddRange(Encoding.ASCII.GetBytes("\r\nwordsPerMinute=123\r\n"));
            cases.Add(new KeyValuePair<string, byte[]>("latin-1-comment", latin1.ToArray()));
            var s = new StringBuilder();
            s.Append("# id\tsettings file bytes in hex\tsettings after loading\n");
            foreach (var item in cases)
            {
                File.WriteAllBytes(path, item.Value);
                s.Append(item.Key).Append('\t').Append(BitConverter.ToString(item.Value).Replace("-", "")).Append('\t')
                    .Append(SettingsLine(SettingsStore.Load())).Append('\n');
            }
            return s.ToString();
        }

        private static string SettingsSaveFile(string path)
        {
            var s = new StringBuilder();
            s.Append("# id\tsettings\tsaved file text (escaped). Windows ends each line with CR LF; shown here as \\n.\n");
            var cases = new List<KeyValuePair<string, AppSettings>>();
            cases.Add(new KeyValuePair<string, AppSettings>("defaults", AppSettings.Defaults()));
            cases.Add(new KeyValuePair<string, AppSettings>("minimums", Extreme(false)));
            cases.Add(new KeyValuePair<string, AppSettings>("maximums", Extreme(true)));
            cases.Add(new KeyValuePair<string, AppSettings>("profile", Runner.Profile(2)));
            foreach (var item in cases)
            {
                SettingsStore.Save(item.Value);
                s.Append(item.Key).Append('\t').Append(SettingsLine(item.Value)).Append('\t')
                    .Append(Esc(File.ReadAllText(path).Replace("\r\n", "\n"))).Append('\n');
            }
            return s.ToString();
        }

        // Script entries: "a|code|ms" calls Accept, "i|reason|ms" calls Ignore.
        private static void Session(StringBuilder s, string id, int seconds, AppSettings current, string[] script)
        {
            var session = new CaptureSession(seconds, current);
            var returns = new StringBuilder();
            s.Append("== ").Append(id).Append('\n');
            s.Append("in\tseconds\t").Append(seconds).Append('\n');
            s.Append("in\tsettings\t").Append(SettingsLine(current)).Append('\n');
            foreach (string entry in script)
            {
                string[] parts = entry.Split('|');
                double ms = Double.Parse(parts[2], Inv);
                if (parts[0] == "a")
                {
                    int code = Int32.Parse(parts[1], Inv);
                    s.Append("in\taccept\t").Append(code).Append('\t').Append(parts[2]).Append('\n');
                    returns.Append(returns.Length == 0 ? "" : " ").Append(session.Accept((char)code, ms));
                }
                else
                {
                    s.Append("in\tignore\t").Append(parts[1]).Append('\t').Append(parts[2]).Append('\n');
                    session.Ignore(parts[1], ms);
                }
            }
            s.Append("out\treturns\t").Append(returns).Append('\n');
            s.Append("out\ttyped\t").Append(Esc(session.TypedText)).Append('\n');
            s.Append("out\tignored\t").Append(session.IgnoredKeys).Append('\n');
            s.Append("out\tstarted\t").Append(session.Started).Append('\n');
            s.Append("out\tfinished\t").Append(session.Finished).Append('\n');
            foreach (var key in session.Keys)
            {
                s.Append("out\tkey\t").Append(R(key.Milliseconds)).Append('\t').Append((int)key.Character).Append('\n');
            }
            string path = session.Save(true);
            foreach (string line in File.ReadAllLines(path))
            {
                s.Append("out\tsaved\t").Append(Esc(line)).Append('\n');
            }
            File.Delete(path);
        }

        private static string SessionsFile()
        {
            var s = new StringBuilder();
            s.Append("# The recording rules of the calibration test. \"in\" rows are the calls, in order;\n");
            s.Append("# \"out\" rows are the state afterwards and the lines of the saved recording.\n");
            s.Append("# accept: character code, milliseconds since the first character. ignore: reason, milliseconds.\n");
            var defaults = AppSettings.Defaults();
            Session(s, "plain", 30, defaults, new[] { "a|83|0", "a|109|140.5", "a|105|262", "a|116|401.25", "a|104|533.125" });
            Session(s, "backspace", 30, defaults, new[]
            {
                "a|8|0", "a|83|10", "a|110|150", "a|8|420", "a|109|600", "a|8|700", "a|8|760", "a|8|820", "a|83|900"
            });
            Session(s, "enter-forms", 60, defaults, new[] { "a|97|0", "a|13|100", "a|98|200", "a|10|300", "a|99|400" });
            Session(s, "rejected-characters", 30, defaults, new[]
            {
                "a|9|0", "a|27|50", "a|233|100", "a|97|150", "a|127|200", "a|8364|250", "a|126|300", "a|32|350", "a|31|400",
                "a|0|450", "a|55357|500"
            });
            Session(s, "ignored-keys", 30, defaults, new[]
            {
                "a|97|0", "i|key Tab|80", "i|Ctrl+Backspace|120", "i|edit message 770|200", "a|98|260", "i|shortcut character 22|300"
            });
            Session(s, "time-limit", 30, defaults, new[]
            {
                "a|97|0", "a|98|29999.999", "a|99|30000", "a|100|30000.5", "a|101|100", "i|key Left|31000"
            });
            Session(s, "limit-before-start", 30, defaults, new[] { "a|233|45000", "a|9|46000", "a|97|47000", "a|98|47100" });
            Session(s, "late-first-key", 60, Runner.Profile(2), new[] { "a|97|59999.5", "a|98|60000", "a|99|10" });
            return s.ToString();
        }

        private static string Mutate(string text, int seed, int count)
        {
            var random = new Random(seed);
            var characters = text.ToCharArray();
            for (int i = 0; i < count; i++)
            {
                characters[random.Next(characters.Length)] = (char)('a' + random.Next(26));
            }
            return new string(characters);
        }

        private static string AlignmentFile()
        {
            string passage = CalibrationPassage.Text;
            int second = passage.IndexOf('\n') + 1;
            int third = passage.IndexOf('\n', second) + 1;
            var cases = new List<string>();
            cases.Add("");
            cases.Add(passage.Substring(0, 1));
            cases.Add(passage.Substring(0, 10));
            cases.Add(passage.Substring(0, second - 1));
            cases.Add(passage.Substring(0, second));
            cases.Add(passage.Substring(0, second + 5));
            cases.Add("smith et al. (2021) found");
            cases.Add("Smith et al. (2021) fuond a 42.5% rise");
            cases.Add("Smith et al (2021) found a 42.5% rise");
            cases.Add("Smith et all. (2021) found a 42.5%% rise");
            cases.Add("Smith et al. (2021) found a 42.5% rise [12]. Is it");
            cases.Add("Smith et al. (2021) found a 42.5% rise [12].\n\nIs it");
            cases.Add("Smmith et al. (2021) fuond  a 42,5% rise [12].\nIs it \"robust\"? yes: p<0.05.");
            cases.Add(passage.Substring(second, 60));
            cases.Add(passage.Substring(0, second) + passage.Substring(third, 80));
            cases.Add("zzzz");
            cases.Add("The quick brown fox jumps over the lazy dog. The quick brown fox jumps over the lazy dog.");
            cases.Add(passage.Substring(0, 200).ToUpperInvariant());
            cases.Add(Mutate(passage.Substring(0, 300), 3, 15));
            cases.Add(Mutate(passage.Substring(0, 300), 4, 90));
            cases.Add(passage.Substring(0, 700));
            cases.Add(passage);
            cases.Add(passage + "more");
            // Short inputs with several equally good matches pin down the tie rules.
            cases.Add("mtS ");
            cases.Add("mtmh");
            cases.Add("mSith te la.");
            var ties = new Random(8128);
            string alphabet = passage.Substring(0, 24) + "\n";
            for (int i = 0; i < 60; i++)
            {
                var text = new StringBuilder();
                int length = 1 + ties.Next(10);
                for (int j = 0; j < length; j++)
                {
                    text.Append(alphabet[ties.Next(alphabet.Length)]);
                }
                cases.Add(text.ToString());
            }
            var s = new StringBuilder();
            s.Append("# How typed text is matched to the passage: CalibrationAnalysis.AlignPrefix(typed, passage).\n");
            s.Append("# typed (escaped)\tend\tdistance\tpassage index of each typed character, -1 where unmatched\n");
            foreach (string typed in cases)
            {
                var alignment = CalibrationAnalysis.AlignPrefix(typed, passage);
                var map = new StringBuilder();
                foreach (int source in alignment.Source)
                {
                    map.Append(map.Length == 0 ? "" : " ").Append(source);
                }
                s.Append(Esc(typed)).Append('\t').Append(alignment.End).Append('\t').Append(alignment.Distance).Append('\t')
                    .Append(map).Append('\n');
            }
            return s.ToString();
        }

        private static List<CaptureKey> Typed(string text, double start, double gap)
        {
            var keys = new List<CaptureKey>();
            for (int i = 0; i < text.Length; i++)
            {
                keys.Add(new CaptureKey(start + i * gap, text[i]));
            }
            return keys;
        }

        private static List<CaptureKey> EngineLog(AppSettings settings, int seed, int seconds)
        {
            var runtime = Runner.RunEngine(CalibrationPassage.Text, settings, seed, seconds * 1000L, false);
            var keys = new List<CaptureKey>();
            foreach (var key in runtime.Keys)
            {
                keys.Add(new CaptureKey(key.Time, key.Value));
            }
            return keys;
        }

        private static void Shift(List<CaptureKey> keys, int from, double milliseconds)
        {
            for (int i = from; i < keys.Count; i++)
            {
                keys[i].Milliseconds += milliseconds;
            }
        }

        private static void AnalysisFiles(SortedDictionary<string, string> files)
        {
            var index = new StringBuilder();
            index.Append("# id\tseconds\tnote. Each id has <id>.capture.tsv (a recording as the app saves it) and\n");
            index.Append("# <id>.expected.tsv (what the analysis and the results window make of it).\n");
            string passage = CalibrationPassage.Text;
            var defaults = AppSettings.Defaults();
            int number = 0;
            // Independent typists: a timing model that shares nothing with the engine.
            foreach (int letter in new[] { 70, 95, 130, 170, 215, 260 })
            {
                Analysis(files, index, ref number, "typist " + letter + " ms, 60 s", TypistLog.Generate(letter, 60, 0, letter), 60, defaults);
                Analysis(files, index, ref number, "typist " + letter + " ms, 30 s", TypistLog.Generate(letter + 1, 30, 0, letter), 30,
                    defaults);
            }
            foreach (int mode in new[] { 1, 2, 3 })
            {
                Analysis(files, index, ref number, "typist 110 ms, mode " + mode, TypistLog.Generate(300 + mode, 60, mode, 110), 60, defaults);
                Analysis(files, index, ref number, "typist 150 ms, mode " + mode, TypistLog.Generate(400 + mode, 60, mode, 150), 60, defaults);
            }
            Analysis(files, index, ref number, "typist 400 ms: slower than the engine can go", TypistLog.Generate(77, 60, 0, 400), 60,
                defaults);
            Analysis(files, index, ref number, "typist 30 ms: faster than the engine can go", TypistLog.Generate(78, 30, 0, 30), 30,
                defaults);
            // Recordings made by the engine itself, which contain corrected mistakes.
            var profiles = EngineProfiles();
            foreach (string name in new[] { "defaults", "duplicates", "transposes", "mixed-kinds", "letters-only-mistakes",
                "heavy-difficult-keys", "detect-0", "keep-target-off", "metronome", "wild-rhythm" })
            {
                foreach (var profile in profiles)
                {
                    if (profile.Key == name)
                    {
                        Analysis(files, index, ref number, "engine recording: " + name, EngineLog(profile.Value, 41 + number, 60), 60,
                            defaults);
                    }
                }
            }
            for (int profile = 0; profile < 3; profile++)
            {
                Analysis(files, index, ref number, "engine recording: test profile " + profile + ", 60 s",
                    EngineLog(Runner.Profile(profile), 600 + profile, 60), 60, defaults);
                Analysis(files, index, ref number, "engine recording: test profile " + profile + ", 30 s",
                    EngineLog(Runner.Profile(profile), 700 + profile, 30), 30, defaults);
            }
            Analysis(files, index, ref number, "engine recording: many long thinking pauses", EngineLog(With(delegate(AppSettings s)
            {
                s.ThinkingPauseChancePercent = 25;
                s.ThinkingPauseMilliseconds = 3000;
            }), 91, 60), 60, defaults);
            Analysis(files, index, ref number, "engine recording: 30 WPM", EngineLog(With(delegate(AppSettings s)
            {
                s.WordsPerMinute = 30;
            }), 92, 60), 60, defaults);
            Analysis(files, index, ref number, "engine recording: 140 WPM", EngineLog(With(delegate(AppSettings s)
            {
                s.WordsPerMinute = 140;
            }), 93, 60), 60, defaults);
            // The same recordings judged against settings other than the defaults.
            Analysis(files, index, ref number, "typist 130 ms, current settings: test profile 1", TypistLog.Generate(130, 60, 0, 130), 60,
                Runner.Profile(1));
            Analysis(files, index, ref number, "typist 130 ms, current settings: no pauses or mistakes", TypistLog.Generate(130, 60, 0, 130),
                60, With(delegate(AppSettings s)
            {
                s.ThinkingPauseChancePercent = 0;
                s.TypoChancePercent = 0;
            }));
            Analysis(files, index, ref number, "typist 95 ms, current settings: keep target off", TypistLog.Generate(95, 60, 0, 95), 60,
                With(delegate(AppSettings s) { s.KeepTargetWpm = false; }));
            Analysis(files, index, ref number, "typist 170 ms, current settings: mistakes off", TypistLog.Generate(170, 60, 0, 170), 60,
                With(delegate(AppSettings s) { s.EnableTypos = false; }));
            Analysis(files, index, ref number, "typist 215 ms, current settings: maximums", TypistLog.Generate(215, 60, 0, 215), 60,
                Extreme(true));
            Analysis(files, index, ref number, "engine recording: defaults, current settings: minimums", EngineLog(defaults, 55, 60), 60,
                Extreme(false));
            // Unusual recordings.
            Analysis(files, index, ref number, "one key", Typed("S", 0, 0), 30, defaults);
            Analysis(files, index, ref number, "twelve correct keys", Typed(passage.Substring(0, 12), 0, 180), 30, defaults);
            Analysis(files, index, ref number, "different text", Typed(
                "The quick brown fox jumps over the lazy dog. The quick brown fox jumps over the lazy dog. Again and again.", 0, 150),
                30, defaults);
            int second = passage.IndexOf('\n') + 1;
            int third = passage.IndexOf('\n', second) + 1;
            Analysis(files, index, ref number, "starts at the second line", Typed(passage.Substring(second, 260), 0, 140), 60, defaults);
            Analysis(files, index, ref number, "second line skipped", Typed(passage.Substring(0, second) + passage.Substring(third, 250), 0,
                140), 60, defaults);
            Analysis(files, index, ref number, "all capitals", Typed(passage.Substring(0, 250).ToUpperInvariant(), 0, 150), 60, defaults);
            var trailing = Typed(passage.Substring(0, 80), 0, 150);
            for (int i = 0; i < 10; i++)
            {
                trailing.Add(new CaptureKey(80 * 150 + i * 110, '\b'));
            }
            Analysis(files, index, ref number, "ends with ten Backspaces", trailing, 30, defaults);
            var interrupted = TypistLog.Generate(500, 60, 0, 120);
            Shift(interrupted, 90, 12000);
            Analysis(files, index, ref number, "twelve-second interruption", interrupted, 60, defaults);
            var hesitant = TypistLog.Generate(501, 60, 0, 120);
            int pauses = 0;
            for (int i = 20; i < hesitant.Count && pauses < 5; i++)
            {
                if (hesitant[i - 1].Character == ' ')
                {
                    Shift(hesitant, i, 2500 + pauses * 275);
                    pauses++;
                    i += 25;
                }
            }
            Analysis(files, index, ref number, "five long pauses at word starts", hesitant, 60, defaults);
            var nearlyTen = TypistLog.Generate(502, 60, 0, 120);
            Shift(nearlyTen, 60, 9999);
            Analysis(files, index, ref number, "one pause just under ten seconds", nearlyTen, 60, defaults);
            Analysis(files, index, ref number, "typist 130 ms with scattered wrong letters", Typed(Mutate(passage.Substring(0, 330), 9, 20),
                0, 175), 60, defaults);
            files["analysis/index.tsv"] = index.ToString();
        }

        private static void Analysis(SortedDictionary<string, string> files, StringBuilder index, ref int number, string note,
            IList<CaptureKey> keys, int seconds, AppSettings current)
        {
            string id = "a" + number.ToString("00", Inv);
            number++;
            var session = new CaptureSession(seconds, current);
            foreach (var key in keys)
            {
                // A recording stores three decimals; analyse exactly what the file holds.
                session.Accept(key.Character, Double.Parse(key.Milliseconds.ToString("F3", Inv), Inv));
            }
            string path = session.Save(true);
            string capture = File.ReadAllText(path).Replace("\r\n", "\n");
            File.Delete(path);
            var result = CalibrationAnalysis.Analyze(session.Keys, session.Seconds, current);
            var s = new StringBuilder();
            s.Append("measuredWpm\t").Append(R(result.MeasuredWpm)).Append('\n');
            s.Append("predictedWpm\t").Append(R(result.PredictedWpm)).Append('\n');
            s.Append("accuracy\t").Append(R(result.Accuracy)).Append('\n');
            s.Append("minimumWpm\t").Append(R(result.MinimumWpm)).Append('\n');
            s.Append("maximumWpm\t").Append(R(result.MaximumWpm)).Append('\n');
            s.Append("baseDelay\t").Append(R(result.BaseDelay)).Append('\n');
            s.Append("ordinaryGap\t").Append(R(result.OrdinaryGapMilliseconds)).Append('\n');
            s.Append("predictedOrdinaryGap\t").Append(R(result.PredictedOrdinaryGapMilliseconds)).Append('\n');
            s.Append("ordinaryGapCount\t").Append(result.OrdinaryGapCount).Append('\n');
            s.Append("correctedMistakes\t").Append(result.CorrectedMistakes).Append('\n');
            s.Append("alignedCharacters\t").Append(result.AlignedCharacters).Append('\n');
            // The summary exactly as the results window builds it.
            string summary = "Measured speed: " + result.MeasuredWpm.ToString("F1") + " WPM active; AutoWrite expected: " +
                result.PredictedWpm.ToString("F1") + " WPM     Accuracy: " + result.Accuracy.ToString("F1") +
                "%     Corrected episodes: " + result.CorrectedMistakes + "\n" + result.ActivityDescription;
            s.Append("summary\t").Append(Esc(summary)).Append('\n');
            s.Append("warning\t").Append(Esc(result.Warning)).Append('\n');
            s.Append("activity\t").Append(Esc(result.ActivityDescription)).Append('\n');
            foreach (var estimate in result.Estimates)
            {
                s.Append("estimate\t").Append(estimate.Setting).Append('\t').Append(SettingNames.Labels[estimate.Setting]).Append('\t')
                    .Append(SettingNames.Format(estimate.Setting, estimate.Current)).Append('\t')
                    .Append(SettingNames.Format(estimate.Setting, estimate.Proposed)).Append('\t').Append(estimate.Measured).Append('\t')
                    .Append(estimate.ClosestAchievable).Append('\t').Append(Esc(estimate.Reason)).Append('\n');
            }
            s.Append("proposed\t").Append(SettingsLine(result.Proposed)).Append('\n');
            files["analysis/" + id + ".capture.tsv"] = capture;
            files["analysis/" + id + ".expected.tsv"] = s.ToString();
            index.Append(id).Append('\t').Append(seconds).Append('\t').Append(note).Append('\n');
        }
    }

    internal static partial class Runner
    {
        private static void ReferenceDataTests()
        {
            Test("committed reference data for other platforms matches this build", delegate
            {
                var expected = ReferenceData.Build();
                string root = Path.Combine(SourceRoot, "reference", "data");
                var problems = new List<string>();
                foreach (var pair in expected)
                {
                    string path = Path.Combine(root, pair.Key.Replace('/', Path.DirectorySeparatorChar));
                    if (!File.Exists(path))
                    {
                        problems.Add("missing " + pair.Key);
                    }
                    else if (File.ReadAllText(path, new UTF8Encoding(false)).Replace("\r\n", "\n") != pair.Value)
                    {
                        problems.Add("differs " + pair.Key);
                    }
                }
                if (Directory.Exists(root))
                {
                    foreach (string file in Directory.GetFiles(root, "*", SearchOption.AllDirectories))
                    {
                        string relative = file.Substring(root.Length + 1).Replace('\\', '/');
                        if (!expected.ContainsKey(relative))
                        {
                            problems.Add("stale " + relative);
                        }
                    }
                }
                Assert(problems.Count == 0, "behaviour changed; run reference.ps1 and carry the change to every port: " +
                    String.Join(", ", problems.ToArray()));
            });
        }
    }

    internal static class ReferenceWriter
    {
        private static int Main(string[] args)
        {
            string root = Path.GetFullPath(args[0]);
            Runner.SourceRoot = args[1];
            var files = ReferenceData.Build();
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
            foreach (var pair in files)
            {
                string path = Path.Combine(root, pair.Key.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, pair.Value, new UTF8Encoding(false));
            }
            Console.WriteLine("WROTE " + files.Count + " files to " + root);
            return 0;
        }
    }
}
