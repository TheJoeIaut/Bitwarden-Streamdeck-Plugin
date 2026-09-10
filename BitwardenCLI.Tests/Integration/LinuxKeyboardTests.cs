using Xunit;
using System.Runtime.InteropServices;
using CliWrap;
using CliWrap.Buffered;

namespace BitwardenStreamdeckPlugin.Tests.Integration;

/// <summary>
/// Exercises the real Linux typing path. Everything else about KeyboardTyper can be
/// checked with unit tests, but whether xdotool is actually invoked correctly - and
/// whether the characters land - can only be answered on a real X session.
///
/// Runs inside the dev container and on the Linux CI job, where Xvfb and xdotool are
/// installed. Skips everywhere else.
/// </summary>
[Trait("Category", "Integration")]
public class LinuxKeyboardTests
{
    private static bool OnLinux => RuntimeInformation.IsOSPlatform(OSPlatform.Linux);

    private static bool HasDisplay => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DISPLAY"));

    private static async Task<bool> ToolExists(string tool)
    {
        try
        {
            BufferedCommandResult result = await Cli.Wrap("which")
                .WithArguments(new[] { tool })
                .WithValidation(CommandResultValidation.None)
                .ExecuteBufferedAsync();

            return result.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<string?> Unavailable()
    {
        if (!OnLinux)
        {
            return "Linux only";
        }

        if (!HasDisplay)
        {
            return "No DISPLAY - start Xvfb first (the dev container does this for you)";
        }

        return await ToolExists("xdotool") ? null : "xdotool is not installed";
    }

    [SkippableFact]
    public async Task Typing_text_drives_xdotool_without_error()
    {
        string? unavailable = await Unavailable();
        Skip.If(unavailable != null, unavailable ?? string.Empty);

        var typer = new KeyboardTyper();

        // Proves the argument list and the stdin piping are right: xdotool rejects a bad
        // invocation with a non-zero exit code, which KeyboardTyper turns into an exception.
        await typer.TypeText("plain text");
        await typer.PressTab();
    }

    [SkippableFact]
    public async Task Typing_handles_characters_a_shell_would_mangle()
    {
        string? unavailable = await Unavailable();
        Skip.If(unavailable != null, unavailable ?? string.Empty);

        var typer = new KeyboardTyper();

        // The exact reason secrets go over stdin instead of the command line.
        await typer.TypeText("p@ss w0rd $(whoami) `id` \"quoted\" 'single' \\slash");
    }

    [SkippableFact]
    public async Task Typed_characters_actually_reach_the_focused_window()
    {
        string? unavailable = await Unavailable();
        Skip.If(unavailable != null, unavailable ?? string.Empty);
        Skip.IfNot(await ToolExists("xterm"), "xterm is not installed");

        string outputFile = Path.Combine(Path.GetTempPath(), $"typed-{Guid.NewGuid():N}.txt");
        const string secret = "correct horse battery staple";

        // A terminal running 'cat' gives us somewhere to type into and read back from.
        using var terminalLifetime = new CancellationTokenSource();
        CommandTask<CommandResult> terminal = Cli.Wrap("xterm")
            .WithArguments(new[] { "-e", $"cat > {outputFile}" })
            .WithValidation(CommandResultValidation.None)
            .ExecuteAsync(terminalLifetime.Token);

        try
        {
            await WaitForWindow("xterm");

            var typer = new KeyboardTyper();
            await typer.TypeText(secret);

            // Close cat's stdin so it flushes to disk.
            await Cli.Wrap("xdotool").WithArguments(new[] { "key", "Return" }).ExecuteAsync();
            await Cli.Wrap("xdotool").WithArguments(new[] { "key", "ctrl+d" }).ExecuteAsync();

            string typed = await ReadWhenNonEmpty(outputFile);

            Assert.Equal(secret, typed.Trim());
        }
        finally
        {
            await terminalLifetime.CancelAsync();
            try { await terminal; } catch { /* killed on purpose */ }
            if (File.Exists(outputFile)) File.Delete(outputFile);
        }
    }

    private static async Task WaitForWindow(string name)
    {
        for (int attempt = 0; attempt < 50; attempt++)
        {
            BufferedCommandResult result = await Cli.Wrap("xdotool")
                .WithArguments(new[] { "search", "--sync", "--onlyvisible", "--class", name })
                .WithValidation(CommandResultValidation.None)
                .ExecuteBufferedAsync();

            if (result.ExitCode == 0 && !string.IsNullOrWhiteSpace(result.StandardOutput))
            {
                // Give the terminal a moment to be ready for input.
                await Task.Delay(500);
                return;
            }

            await Task.Delay(100);
        }

        throw new TimeoutException($"No visible '{name}' window appeared");
    }

    private static async Task<string> ReadWhenNonEmpty(string path)
    {
        for (int attempt = 0; attempt < 50; attempt++)
        {
            if (File.Exists(path))
            {
                string content = await File.ReadAllTextAsync(path);
                if (!string.IsNullOrWhiteSpace(content))
                {
                    return content;
                }
            }

            await Task.Delay(100);
        }

        throw new TimeoutException($"Nothing was typed into {path}");
    }
}
