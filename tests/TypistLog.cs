using System;
using System.Collections.Generic;

namespace AutoWrite.Tests
{
    // An additive timing model: six independent exponential stages make a gamma
    // motor interval. Bursts change regime at word boundaries. No engine timing
    // functions, hand tables, class factors or analyser constants are used.
    internal static class TypistLog
    {
        internal static List<CaptureKey> Generate(int seed, int seconds, int mode, double letterMilliseconds)
        {
            var random = new Random(seed);
            var keys = new List<CaptureKey>();
            double time = 0, regime = 1;
            string text = CalibrationPassage.Text;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (i > 0)
                {
                    double gamma = 0;
                    for (int stage = 0; stage < 24; stage++)
                    {
                        gamma -= Math.Log(Math.Max(1e-12, random.NextDouble())) / 24;
                    }
                    double cost = Char.IsUpper(c) ? 55 : Char.IsDigit(c) ? 75 :
                        "<>%&@\"():?".IndexOf(c) >= 0 ? 125 : Char.IsPunctuation(c) ? 65 : 0;
                    time += letterMilliseconds * gamma * regime + cost;
                    char previous = text[i - 1];
                    if (Char.IsWhiteSpace(previous))
                    {
                        time += 90;
                        if (random.NextDouble() < .2)
                        {
                            regime = random.NextDouble() < .5 ? .92 : 1.08;
                        }
                    }
                    if (".!?".IndexOf(previous) >= 0)
                    {
                        time += 380;
                    }
                    if (previous == '\n')
                    {
                        time += 200;
                    }
                }
                if (mode == 1 && i == 85)
                {
                    time += 15200;
                }
                if (time >= seconds * 1000 || (mode == 2 && time >= 10000))
                {
                    break;
                }
                keys.Add(new CaptureKey(time, c));
                if (mode == 3 && i == 299)
                {
                    break;
                }
            }
            return keys;
        }
    }
}
