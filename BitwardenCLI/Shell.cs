using System;
using System.Text;
using System.Threading.Tasks;
using BarRaider.SdTools;
using CliWrap;

namespace BitwardenStreamdeckPlugin
{
    /// <summary>
    /// Runs the small platform helpers the plugin depends on for typing and for the
    /// clipboard.
    /// </summary>
    internal static class Shell
    {
        /// <summary>
        /// Runs a tool, optionally feeding it standard input.
        ///
        /// Secrets are always passed on stdin rather than as arguments, so they never appear
        /// in the process list where any local user could read them.
        /// </summary>
        internal static async Task Run(string tool, string[] arguments, string standardInput = null)
        {
            Command command = Cli.Wrap(tool).WithArguments(arguments).WithValidation(CommandResultValidation.None);

            if (standardInput != null)
            {
                // The encoding is stated rather than left to CliWrap, which otherwise uses
                // Console.InputEncoding - the ANSI code page on Windows, which mangles
                // anything outside it. Every helper here reads UTF-8: clip.exe, xdotool's
                // --file, pbcopy and osascript.
                command = command.WithStandardInputPipe(
                    PipeSource.FromString(standardInput, new UTF8Encoding(false)));
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
