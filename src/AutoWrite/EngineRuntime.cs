using System;
using System.IO;
using System.Threading;

namespace AutoWrite
{
    // Only environmental effects live here; timing arithmetic stays in TypingEngine.
    internal interface IEngineRuntime
    {
        Random CreateRandom();
        IntPtr ForegroundWindow();
        bool IsKeyDown(int key);
        void ReleaseModifiers();
        void SendCharacter(char value);
        void Sleep(int milliseconds, CancellationToken token);
    }

    internal sealed class WindowsEngineRuntime : IEngineRuntime
    {
        public Random CreateRandom() { return new Random(); }
        public IntPtr ForegroundWindow() { return NativeMethods.GetForegroundWindow(); }
        public bool IsKeyDown(int key) { return NativeMethods.IsKeyDown(key); }
        public void ReleaseModifiers() { NativeMethods.ReleaseModifierKeys(); }
        public void SendCharacter(char value)
        {
            if (value == '\b') NativeMethods.SendBackspace();
            else if (value == '\n') NativeMethods.SendEnter();
            else NativeMethods.SendUnicodeChar(value);
        }
        public void Sleep(int milliseconds, CancellationToken token)
        {
            if (milliseconds <= 0) { token.ThrowIfCancellationRequested(); return; }
            if (token.WaitHandle.WaitOne(milliseconds)) token.ThrowIfCancellationRequested();
        }
    }

    internal static class DataDirectory
    {
        public static string PathRoot
        {
            get
            {
                var path = Environment.GetEnvironmentVariable("AUTOWRITE_DATA_DIR");
                if (!String.IsNullOrEmpty(path))
                {
                    if (!Path.IsPathRooted(path) || Path.GetPathRoot(path).Length < 3) throw new ArgumentException("AUTOWRITE_DATA_DIR must be absolute.");
                    return Path.GetFullPath(path);
                }
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AutoWrite");
            }
        }
    }
}
