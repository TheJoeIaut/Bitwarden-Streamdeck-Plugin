using System.Threading.Tasks;

namespace BitwardenStreamdeckPlugin
{
    /// <summary>
    /// Runs Bitwarden CLI commands. Exists so the actions can be tested against a stub
    /// instead of a real 'bw' installation.
    /// </summary>
    internal interface IBwCli
    {
        /// <summary>
        /// Runs 'bw' with the given arguments and returns its standard output.
        /// Throws <see cref="BwCliException"/> if the CLI reports a non-zero exit code.
        /// </summary>
        Task<string> Run(params string[] arguments);

        /// <summary>
        /// Stores the session key handed out by 'bw unlock'. Later commands need it to
        /// read the vault, and it is passed through the BW_SESSION environment variable.
        /// </summary>
        void SetSessionKey(string sessionKey);
    }
}
