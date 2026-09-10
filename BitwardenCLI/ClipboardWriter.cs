using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace BitwardenStreamdeckPlugin
{
    /// <summary>
    /// Copies text to the clipboard through whichever helper the platform provides.
    /// Like the keyboard layer, the value goes over stdin so it never appears in the
    /// process list.
    /// </summary>
    internal sealed class ClipboardWriter : IClipboardWriter
    {
        internal static IClipboardWriter Shared { get; set; } = new ClipboardWriter();

        public async Task SetText(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            if (OperatingSystem.IsWindows())
            {
                // Ships with Windows and handles symbols and non-ASCII correctly.
                await Shell.Run("clip.exe", Array.Empty<string>(), text);
                return;
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                await Shell.Run(LinuxClipboardTool(), LinuxClipboardArguments(), text);
                return;
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                await Shell.Run("pbcopy", Array.Empty<string>(), text);
                return;
            }

            throw new PlatformNotSupportedException("Copying to the clipboard is not supported on this platform");
        }

        /// <summary>
        /// wl-copy under Wayland, xclip under X11 - the same split the typing helpers use.
        /// </summary>
        internal static string LinuxClipboardTool()
        {
            return string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY"))
                ? "xclip"
                : "wl-copy";
        }

        internal static string[] LinuxClipboardArguments()
        {
            // xclip writes to the primary selection unless told otherwise; wl-copy already
            // targets the clipboard.
            return string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY"))
                ? new[] { "-selection", "clipboard" }
                : Array.Empty<string>();
        }
    }
}
