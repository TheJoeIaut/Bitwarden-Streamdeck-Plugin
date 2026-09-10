using System;
using System.IO;
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
    internal static class KeyboardTyper
    {
        internal static async Task TypeText(string text)
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
                await TypeTextLinux(text);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                await TypeTextMacOs(text);
            }
            else
            {
                throw new PlatformNotSupportedException("Typing credentials is not supported on this platform");
            }
        }

        internal static async Task PressTab()
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
        /// xdotool covers X11, ydotool covers Wayland. Both read the text from stdin so the
        /// secret never lands in the process command line, where any local user could read it.
        /// </summary>
        private static async Task TypeTextLinux(string text)
        {
            await RunTool(LinuxTypingTool(), new[] { "type", "--file", "-" }, text);
        }

        private static async Task TypeTextMacOs(string text)
        {
            // Passed over stdin rather than as an argument so the secret stays off the command line.
            await RunAppleScript($"tell application \"System Events\" to keystroke {EscapeAppleScript(text)}");
        }

        private static string LinuxTypingTool()
        {
            // WAYLAND_DISPLAY is set under Wayland sessions, where XTEST (xdotool) is unavailable.
            return string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY"))
                ? "xdotool"
                : "ydotool";
        }

        private static async Task RunAppleScript(string script)
        {
            await RunTool("osascript", new[] { "-" }, script);
        }

        private static string EscapeAppleScript(string value)
        {
            return "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        }

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
