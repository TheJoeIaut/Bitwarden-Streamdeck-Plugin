using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CliWrap;
using CliWrap.Buffered;

namespace BitwardenStreamdeckPlugin
{
    /// <summary>
    /// Thrown when the Bitwarden CLI exits with a non-zero code.
    /// </summary>
    internal class BwCliException : Exception
    {
        internal BwCliException(string message, int exitCode) : base(message)
        {
            ExitCode = exitCode;
        }

        internal int ExitCode { get; }
    }

    /// <summary>
    /// Talks to the real 'bw' executable.
    /// </summary>
    internal sealed class BwCli : IBwCli
    {
        /// <summary>
        /// The instance the actions use. It is shared because the session key obtained by
        /// the Unlock action has to be visible to the Get action afterwards.
        /// </summary>
        internal static IBwCli Shared { get; set; } = new BwCli();

        private readonly string executable;
        private readonly IReadOnlyDictionary<string, string> environment;
        private string sessionKey;

        /// <summary>
        /// The optional environment is how tests point the CLI at a throwaway data
        /// directory (BITWARDENCLI_APPDATA_DIR) instead of the user's real profile.
        /// </summary>
        internal BwCli(string executable = "bw", IReadOnlyDictionary<string, string> environment = null)
        {
            this.executable = executable;
            this.environment = environment;
        }

        public void SetSessionKey(string sessionKey)
        {
            this.sessionKey = sessionKey;
        }

        public async Task<string> Run(params string[] arguments)
        {
            Command command = Cli.Wrap(executable)
                .WithArguments(arguments)
                .WithValidation(CommandResultValidation.None);

            var variables = new Dictionary<string, string>();

            if (environment != null)
            {
                foreach (KeyValuePair<string, string> entry in environment)
                {
                    variables[entry.Key] = entry.Value;
                }
            }

            if (!string.IsNullOrEmpty(sessionKey))
            {
                variables["BW_SESSION"] = sessionKey;
            }

            if (variables.Count > 0)
            {
                command = command.WithEnvironmentVariables(variables);
            }

            BufferedCommandResult result = await command.ExecuteBufferedAsync();

            if (result.ExitCode != 0)
            {
                // StandardError carries the reason ("not logged in", "invalid master password", ...).
                // The vault contents only ever appear on stdout, so this is safe to surface.
                throw new BwCliException(
                    $"'{executable} {string.Join(" ", arguments)}' failed: {result.StandardError.Trim()}",
                    result.ExitCode);
            }

            return result.StandardOutput;
        }
    }
}
