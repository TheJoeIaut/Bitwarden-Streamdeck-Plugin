using System.Runtime.InteropServices;
using CliWrap;
using CliWrap.Buffered;
using Xunit;

namespace BitwardenStreamdeckPlugin.Tests.Integration;

/// <summary>
/// Exercises the real clipboard. Whether a generated password survives the trip through
/// the platform helper cannot be answered by a substitute, and the characters a generator
/// produces are exactly the ones a naive implementation mangles.
///
/// The clipboard belongs to whoever is at the machine, so its contents are put back
/// afterwards. Marked Integration and excluded from the default run for that reason.
/// </summary>
[Trait("Category", "Integration")]
public class ClipboardTests
{
    private static bool OnWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    private static async Task<string> ReadClipboard()
    {
        BufferedCommandResult result = await Cli.Wrap("powershell")
            .WithArguments(new[] { "-NoProfile", "-Command", "Get-Clipboard -Raw" })
            .WithValidation(CommandResultValidation.None)
            .ExecuteBufferedAsync();

        return result.StandardOutput.TrimEnd('\r', '\n');
    }

    private static async Task WriteClipboard(string text)
    {
        await new ClipboardWriter().SetText(text);
    }

    [SkippableTheory]
    [InlineData("gener4ted-p4ssword")]
    [InlineData("p@ssw0rd!#$%^&*()_+-=[]{}|;':\",./<>?")]
    [InlineData("correct-horse-battery-staple")]
    [InlineData("pässwörd-üñî")]
    public async Task A_generated_value_survives_the_clipboard_intact(string secret)
    {
        Skip.IfNot(OnWindows, "Windows only; the Linux and macOS helpers need a session to talk to");

        string original = await ReadClipboard();

        try
        {
            await WriteClipboard(secret);

            Assert.Equal(secret, await ReadClipboard());
        }
        finally
        {
            await RestoreClipboard(original);
        }
    }

    [SkippableFact]
    public async Task An_empty_value_leaves_the_clipboard_alone()
    {
        Skip.IfNot(OnWindows, "Windows only");

        string marker = "clipboard-should-not-change-" + Guid.NewGuid().ToString("N");
        string original = await ReadClipboard();

        try
        {
            await WriteClipboard(marker);
            await new ClipboardWriter().SetText("");

            Assert.Equal(marker, await ReadClipboard());
        }
        finally
        {
            await RestoreClipboard(original);
        }
    }

    /// <summary>
    /// Puts the clipboard back as it was found - including leaving it empty, which an
    /// earlier version of this got wrong and left a test value sitting there.
    /// </summary>
    private static async Task RestoreClipboard(string original)
    {
        if (!string.IsNullOrEmpty(original))
        {
            await WriteClipboard(original);
            return;
        }

        await Cli.Wrap("powershell")
            .WithArguments(new[] { "-NoProfile", "-Command", "Set-Clipboard -Value $null" })
            .WithValidation(CommandResultValidation.None)
            .ExecuteAsync();
    }
}
