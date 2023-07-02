using CliWrap;

namespace BitwardenStreamdeckPlugin
{
    internal static class BwCliWrapper
    {

        private static Command _cli;

        internal static Command GetCli()
        {
            return _cli ??= Cli.Wrap("bw");
        }

        internal static void SetCli(Command cli)
        {
            _cli = cli;
        }
    }
}
