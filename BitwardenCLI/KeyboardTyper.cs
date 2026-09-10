using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using BarRaider.SdTools;
using CliWrap;
using WindowsInput;

namespace BitwardenStreamdeckPlugin
{
    /// <summary>
    /// Types text into whatever window currently has focus.
    /// Each platform needs a different mechanism, so the implementation is chosen at runtime.
    /// </summary>
    internal sealed class KeyboardTyper : IKeyboardTyper
    {
        internal static IKeyboardTyper Shared { get; set; } = new KeyboardTyper();

        public async Task TypeText(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            if (OperatingSystem.IsWindowsVersionAtLeast(5))
            {
                TypeTextWindows(text);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                await RunTool(LinuxTypingTool(), new[] { "type", "--file", "-" }, text);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                await RunAppleScript($"tell application \"System Events\" to keystroke {EscapeAppleScript(text)}");
            }
            else
            {
                throw new PlatformNotSupportedException("Typing credentials is not supported on this platform");
            }
        }

        public async Task PressTab()
        {
            if (OperatingSystem.IsWindowsVersionAtLeast(5))
            {
                PressTabWindows();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                await RunTool(LinuxTypingTool(), new[] { "key", "Tab" });
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                await RunAppleScript("tell application \"System Events\" to key code 48");
            }
        }

        [SupportedOSPlatform("windows5.0")]
        private static void TypeTextWindows(string text)
        {
            new InputSimulator().Keyboard.TextEntry(text);
        }

        [SupportedOSPlatform("windows5.0")]
        private static void PressTabWindows()
        {
            new InputSimulator().Keyboard.KeyPress(VirtualKeyCode.TAB);
        }

        /// <summary>
        /// xdotool covers X11, ydotool covers Wayland. WAYLAND_DISPLAY is set under Wayland
        /// sessions, where XTEST (and therefore xdotool) is unavailable.
        /// </summary>
        internal static string LinuxTypingTool()
        {
            return string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY"))
                ? "xdotool"
                : "ydotool";
        }

        /// <summary>
        /// Quotes a value for embedding in an AppleScript string literal.
        /// </summary>
        internal static string EscapeAppleScript(string value)
        {
            return "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        }

        private static async Task RunAppleScript(string script)
        {
            await RunTool("osascript", new[] { "-" }, script);
        }

        /// <summary>
        /// Secrets are handed over on stdin rather than as arguments, so they never appear
        /// in the process list where any local user could read them.
        /// </summary>
        private static async Task RunTool(string tool, string[] arguments, string standardInput = null)
        {
            Command command = Cli.Wrap(tool).WithArguments(arguments).WithValidation(CommandResultValidation.None);

            if (standardInput != null)
            {
                command = command.WithStandardInputPipe(PipeSource.FromString(standardInput));
            }

            CommandResult result = await command.ExecuteAsync();

            if (result.ExitCode != 0)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"{tool} exited with code {result.ExitCode}");
                throw new InvalidOperationException($"'{tool}' failed. Is it installed and on PATH?");
            }
        }
    }
}
